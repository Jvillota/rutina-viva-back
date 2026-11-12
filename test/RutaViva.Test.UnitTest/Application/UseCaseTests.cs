using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RutaViva.Application.Common;
using RutaViva.Application.Routines;
using RutaViva.Application.ServiceOrders;
using RutaViva.Application.Telemetry;
using RutaViva.Domain.Common;
using RutaViva.Domain.ServiceOrders;
using RutaViva.Domain.Vehicles;
using RutaViva.Test.UnitTest.Domain;

namespace RutaViva.Test.UnitTest.Application;

public class UseCaseTests
{
    // 2026-03-01 17:00 UTC = 12:00 in Bogota (UTC-5).
    private static readonly DateTimeOffset StartUtc = new(2026, 3, 1, 17, 0, 0, TimeSpan.Zero);

    private readonly AppTestBed _app = new(StartUtc);

    [Fact]
    public async Task RegisterReading_WhenRoutineBecomesOverdue_OpensOneServiceOrder()
    {
        var vehicle = SeedVehicleWithOilChange(odometerKm: 59_500);

        var result = await ReadingUseCase().ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_000));

        result.Value.Should().Be(new OdometerReadingResult(OdometerReadingOutcome.Applied, 60_000, 1));
        _app.Orders.Items.Should().ContainSingle().Which.Status.Should().Be(ServiceOrderStatus.Created);
        _app.Readings.Items.Should().ContainSingle().Which.Outcome.Should().Be(OdometerReadingOutcome.Applied);
    }

    [Fact]
    public async Task RegisterReading_WhenIgnored_OpensNothingButLogsTheReading()
    {
        var vehicle = SeedVehicleWithOilChange(odometerKm: 59_500);

        var result = await ReadingUseCase().ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 59_000));

        result.Value.Status.Should().Be(OdometerReadingOutcome.Ignored);
        result.Value.CurrentOdometerKm.Should().Be(59_500);
        _app.Orders.Items.Should().BeEmpty();
        _app.Readings.Items.Should().ContainSingle().Which.Outcome.Should().Be(OdometerReadingOutcome.Ignored);
    }

    [Fact]
    public async Task RegisterReading_WhenOrderIsAlreadyOpen_DoesNotOpenAnother()
    {
        var vehicle = SeedVehicleWithOilChange(odometerKm: 59_500);
        var useCase = ReadingUseCase();
        await useCase.ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_000));

        await useCase.ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_200));

        _app.Orders.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterReading_AfterOrderWasCancelled_OpensANewOrder()
    {
        // Decision B: the routine is still overdue and has no open order.
        var vehicle = SeedVehicleWithOilChange(odometerKm: 59_500);
        var useCase = ReadingUseCase();
        await useCase.ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_000));
        _app.Orders.Items[0].Cancel(_app.Clock.Now);

        var result = await useCase.ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_200));

        result.Value.ServiceOrdersOpened.Should().Be(1);
        _app.Orders.Items.Should().HaveCount(2);
        _app.Orders.Items.Count(o => o.IsOpen).Should().Be(1);
    }

    [Fact]
    public async Task RegisterReading_UnknownVehicle_FailsWithNotFound()
    {
        var result = await ReadingUseCase().ExecuteAsync(new RegisterOdometerReadingRequest(Guid.NewGuid(), 1));

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task RegisterReading_WhenConcurrencyConflictHappens_RetriesAndSucceeds()
    {
        var vehicle = SeedVehicleWithOilChange(odometerKm: 50_000);
        _app.UnitOfWork.ConflictsToThrow = 1;

        var result = await ReadingUseCase().ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 51_000));

        result.IsSuccess.Should().BeTrue();
        _app.UnitOfWork.Resets.Should().Be(1);
        _app.UnitOfWork.Saves.Should().Be(1);
    }

    [Fact]
    public async Task RetryExecutor_WhenEveryAttemptConflicts_ReturnsConflict()
    {
        _app.UnitOfWork.ConflictsToThrow = ConcurrencyRetryExecutor.MaxAttempts;

        var result = await _app.Retry.ExecuteAsync<int>(
            async ct =>
            {
                await _app.UnitOfWork.SaveChangesAsync(ct);
                return 1;
            },
            CancellationToken.None);

        result.Error.Should().Be(ConcurrencyRetryExecutor.RetriesExhausted);
        _app.UnitOfWork.Resets.Should().Be(ConcurrencyRetryExecutor.MaxAttempts);
    }

    [Fact]
    public async Task DailyCheck_WhenTimeIntervalExpires_OpensOrderAndIsIdempotent()
    {
        SeedVehicleWithOilChange(odometerKm: 52_000);
        var useCase = DailyCheckUseCase();
        _app.Time.SetUtcNow(new DateTimeOffset(2026, 7, 1, 17, 0, 0, TimeSpan.Zero));

        var first = await useCase.ExecuteAsync(new RunDailyCheckRequest());
        var second = await useCase.ExecuteAsync(new RunDailyCheckRequest());

        first.Value.Should().Be(new DailyCheckSummary(new DateOnly(2026, 7, 1), 1, 1, 1));
        second.Value.Should().Be(new DailyCheckSummary(new DateOnly(2026, 7, 1), 1, 0, 0));
        _app.Orders.Items.Should().ContainSingle();
        _app.Runs.Items.Should().ContainSingle().Which.BusinessDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task DailyCheck_UsesBusinessTimeZone()
    {
        SeedVehicleWithOilChange(odometerKm: 52_000);

        // 2026-07-01 03:00 UTC is still 2026-06-30 in Bogota: one day left, due soon but not overdue.
        _app.Time.SetUtcNow(new DateTimeOffset(2026, 7, 1, 3, 0, 0, TimeSpan.Zero));
        var result = await DailyCheckUseCase().ExecuteAsync(new RunDailyCheckRequest());

        result.Value.BusinessDate.Should().Be(new DateOnly(2026, 6, 30));
        result.Value.ServiceOrdersOpened.Should().Be(0);
    }

    [Fact]
    public async Task CompleteService_RestartsRoutineFromWorkshopValues_WithoutTouchingOdometer()
    {
        var vehicle = SeedVehicleWithOilChange(odometerKm: 59_500);
        await ReadingUseCase().ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_300));
        var order = _app.Orders.Items.Single();
        order.SubmitQuote(Money.Create(900_000).Value, _app.Policy, _app.Clock.Now);
        order.Start(_app.Clock.Now);

        var result = await CompleteUseCase().ExecuteAsync(new CompleteServiceRequest(order.Id, 60_450, _app.Clock.Today));

        result.Value.Status.Should().Be(ServiceOrderStatus.Completed);
        var routine = vehicle.Routines.Single();
        routine.Status.Should().Be(RoutineStatus.UpToDate);
        routine.CycleStartKm.Should().Be(60_450);
        vehicle.OdometerKm.Should().Be(60_300);
    }

    [Fact]
    public async Task SubmitQuote_WithInvalidAmount_FailsWithValidation()
    {
        var vehicle = SeedVehicleWithOilChange(odometerKm: 59_500);
        await ReadingUseCase().ExecuteAsync(new RegisterOdometerReadingRequest(vehicle.Id, 60_000));
        var useCase = new SubmitQuoteUseCase(_app.Orders, _app.Retry, _app.UnitOfWork, _app.Clock, _app.Policy);

        var result = await useCase.ExecuteAsync(new SubmitQuoteRequest(_app.Orders.Items[0].Id, 0));

        result.Error.Should().Be(ServiceOrderErrors.QuoteMustBePositive);
    }

    [Fact]
    public async Task SubmitQuote_UnknownOrder_FailsWithNotFound()
    {
        var useCase = new SubmitQuoteUseCase(_app.Orders, _app.Retry, _app.UnitOfWork, _app.Clock, _app.Policy);

        var result = await useCase.ExecuteAsync(new SubmitQuoteRequest(Guid.NewGuid(), 10));

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    private Vehicle SeedVehicleWithOilChange(int odometerKm)
    {
        var (vehicle, _) = DomainFixtures.VehicleWithOilChange(odometerKm);
        _app.Vehicles.Add(vehicle);
        return vehicle;
    }

    private RegisterOdometerReadingUseCase ReadingUseCase() =>
        new(_app.Vehicles, _app.Readings, _app.Opener, _app.Retry, _app.UnitOfWork, _app.Clock);

    private RunDailyCheckUseCase DailyCheckUseCase() =>
        new(_app.Vehicles, _app.Opener, _app.Retry, _app.UnitOfWork, _app.Runs, _app.Clock, NullLogger<RunDailyCheckUseCase>.Instance);

    private CompleteServiceUseCase CompleteUseCase() =>
        new(_app.Orders, _app.Vehicles, _app.Retry, _app.UnitOfWork, _app.Clock);
}
