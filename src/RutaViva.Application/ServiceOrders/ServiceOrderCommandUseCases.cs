using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.Common;
using RutaViva.Domain.ServiceOrders;

namespace RutaViva.Application.ServiceOrders;

/// <summary>
/// Shared shape of the order commands: load the order, apply one domain transition, save it.
/// Every step runs inside the concurrency retry so two simultaneous actions on the same order cannot both win.
/// </summary>
public abstract class ServiceOrderCommandUseCase<TRequest>(
    IServiceOrderRepository serviceOrders,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : IUseCase<TRequest, ServiceOrderView>
    where TRequest : IServiceOrderCommand
{
    protected IBusinessClock Clock => clock;

    public Task<Result<ServiceOrderView>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return retry.ExecuteAsync(ct => RunAsync(request, ct), cancellationToken);
    }

    protected abstract Task<Result> ApplyAsync(ServiceOrder order, TRequest request, CancellationToken cancellationToken);

    private async Task<Result<ServiceOrderView>> RunAsync(TRequest request, CancellationToken cancellationToken)
    {
        var order = await serviceOrders.GetByIdAsync(request.ServiceOrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return ServiceOrderErrors.NotFound(request.ServiceOrderId);
        }

        var result = await ApplyAsync(order, request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return order.ToView();
    }
}

public interface IServiceOrderCommand
{
    Guid ServiceOrderId { get; }
}

public sealed record SubmitQuoteRequest(Guid ServiceOrderId, decimal AmountCop) : IServiceOrderCommand;

public sealed record ApproveQuoteRequest(Guid ServiceOrderId) : IServiceOrderCommand;

public sealed record RejectQuoteRequest(Guid ServiceOrderId) : IServiceOrderCommand;

public sealed record CancelServiceOrderRequest(Guid ServiceOrderId) : IServiceOrderCommand;

public sealed record StartServiceRequest(Guid ServiceOrderId) : IServiceOrderCommand;

public sealed record CompleteServiceRequest(Guid ServiceOrderId, int ServiceKm, DateOnly ServiceDate) : IServiceOrderCommand;

/// <summary>Workshop quotes the order; approval is automatic up to the configured threshold (rule 4).</summary>
public sealed class SubmitQuoteUseCase(
    IServiceOrderRepository serviceOrders,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock,
    QuoteApprovalPolicy policy) : ServiceOrderCommandUseCase<SubmitQuoteRequest>(serviceOrders, retry, unitOfWork, clock)
{
    protected override Task<Result> ApplyAsync(ServiceOrder order, SubmitQuoteRequest request, CancellationToken cancellationToken)
    {
        var amount = Money.Create(request.AmountCop);
        return Task.FromResult(amount.IsFailure ? Result.Failure(amount.Error) : order.SubmitQuote(amount.Value, policy, Clock.Now));
    }
}

public sealed class ApproveQuoteUseCase(
    IServiceOrderRepository serviceOrders,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : ServiceOrderCommandUseCase<ApproveQuoteRequest>(serviceOrders, retry, unitOfWork, clock)
{
    protected override Task<Result> ApplyAsync(ServiceOrder order, ApproveQuoteRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(order.Approve(Clock.Now));
}

public sealed class RejectQuoteUseCase(
    IServiceOrderRepository serviceOrders,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : ServiceOrderCommandUseCase<RejectQuoteRequest>(serviceOrders, retry, unitOfWork, clock)
{
    protected override Task<Result> ApplyAsync(ServiceOrder order, RejectQuoteRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(order.Reject(Clock.Now));
}

public sealed class CancelServiceOrderUseCase(
    IServiceOrderRepository serviceOrders,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : ServiceOrderCommandUseCase<CancelServiceOrderRequest>(serviceOrders, retry, unitOfWork, clock)
{
    protected override Task<Result> ApplyAsync(ServiceOrder order, CancelServiceOrderRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(order.Cancel(Clock.Now));
}

public sealed class StartServiceUseCase(
    IServiceOrderRepository serviceOrders,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : ServiceOrderCommandUseCase<StartServiceRequest>(serviceOrders, retry, unitOfWork, clock)
{
    protected override Task<Result> ApplyAsync(ServiceOrder order, StartServiceRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(order.Start(Clock.Now));
}

/// <summary>
/// Rule 5: completes the order and restarts the routine from the real service values, both in the same
/// transaction (two aggregates, same bounded context and database: a documented pragmatic choice).
/// </summary>
public sealed class CompleteServiceUseCase(
    IServiceOrderRepository serviceOrders,
    IVehicleRepository vehicles,
    ConcurrencyRetryExecutor retry,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : ServiceOrderCommandUseCase<CompleteServiceRequest>(serviceOrders, retry, unitOfWork, clock)
{
    protected override async Task<Result> ApplyAsync(ServiceOrder order, CompleteServiceRequest request, CancellationToken cancellationToken)
    {
        var (today, now) = (Clock.Today, Clock.Now);
        var completed = order.Complete(request.ServiceKm, request.ServiceDate, today, now);
        if (completed.IsFailure)
        {
            return completed;
        }

        var vehicle = await vehicles.GetByIdAsync(order.VehicleId, cancellationToken).ConfigureAwait(false);
        if (vehicle is null)
        {
            return ApplicationErrors.VehicleNotFound(order.VehicleId);
        }

        return vehicle.RestartRoutine(order.RoutineId, request.ServiceKm, request.ServiceDate, today, now);
    }
}
