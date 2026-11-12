using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RutaViva.Api.Contracts;
using RutaViva.Api.ErrorHandling;
using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Fleet;
using RutaViva.Application.Telemetry;

namespace RutaViva.Api.Controllers;

/// <summary>Workshops already assigned to vehicles (choosing or scheduling them is out of scope).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/workshops")]
public sealed class WorkshopsController : ControllerBase
{
    /// <summary>Lists the workshops.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkshopView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkshopView>>> List(
        [FromServices] IUseCase<ListWorkshopsRequest, IReadOnlyList<WorkshopView>> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new ListWorkshopsRequest(), cancellationToken)).ToActionResult(this);

    /// <summary>Registers a workshop.</summary>
    [HttpPost]
    [ProducesResponseType<WorkshopView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkshopView>> Register(
        [FromBody] RegisterWorkshopBody body,
        [FromServices] IUseCase<RegisterWorkshopRequest, WorkshopView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new RegisterWorkshopRequest(body.Name!), cancellationToken)).ToActionResult(this);
}

/// <summary>Fleet vehicles, their routines and their odometer readings.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    /// <summary>Lists the vehicles with a summary of their routines.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehicleSummaryView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleSummaryView>>> List(
        [FromServices] IUseCase<ListVehiclesRequest, IReadOnlyList<VehicleSummaryView>> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new ListVehiclesRequest(), cancellationToken)).ToActionResult(this);

    /// <summary>Vehicle detail: odometer, workshop and every routine with its status, due values and open order.</summary>
    [HttpGet("{vehicleId:guid}")]
    [ProducesResponseType<VehicleDetailView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDetailView>> Get(
        Guid vehicleId,
        [FromServices] IUseCase<GetVehicleDetailRequest, VehicleDetailView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new GetVehicleDetailRequest(vehicleId), cancellationToken)).ToActionResult(this);

    /// <summary>Registers a vehicle with its assigned workshop and initial odometer.</summary>
    /// <response code="400">Invalid data or unknown workshop.</response>
    /// <response code="409">The plate already exists.</response>
    [HttpPost]
    [ProducesResponseType<VehicleDetailView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDetailView>> Register(
        [FromBody] RegisterVehicleBody body,
        [FromServices] IUseCase<RegisterVehicleRequest, VehicleDetailView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new RegisterVehicleRequest(body.Plate!, body.WorkshopId!.Value, body.InitialOdometerKm!.Value), cancellationToken))
            .ToActionResult(this);

    /// <summary>Adds a maintenance routine (every N km, every N months, or both).</summary>
    [HttpPost("{vehicleId:guid}/routines")]
    [ProducesResponseType<RoutineView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoutineView>> AddRoutine(
        Guid vehicleId,
        [FromBody] AddRoutineBody body,
        [FromServices] IUseCase<AddRoutineRequest, RoutineView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(
            new AddRoutineRequest(vehicleId, body.Name!, body.IntervalKm, body.IntervalMonths, body.LastServiceKm, body.LastServiceDate),
            cancellationToken)).ToActionResult(this);

    /// <summary>
    /// Receives an odometer reading from telemetry (or the simulator). Readings lower than or equal to the registered
    /// odometer are ignored without error (repeated or out of order): the response says Applied or Ignored.
    /// </summary>
    /// <response code="200">Reading processed: <c>status</c> is Applied or Ignored.</response>
    /// <response code="404">Unknown vehicle.</response>
    /// <response code="409">Too many concurrent changes; safe to resend.</response>
    [HttpPost("{vehicleId:guid}/odometer-readings")]
    [ProducesResponseType<OdometerReadingResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OdometerReadingResult>> RegisterReading(
        Guid vehicleId,
        [FromBody] OdometerReadingBody body,
        [FromServices] IUseCase<RegisterOdometerReadingRequest, OdometerReadingResult> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new RegisterOdometerReadingRequest(vehicleId, body.Km!.Value), cancellationToken)).ToActionResult(this);

    /// <summary>Readings received for the vehicle (applied and ignored), newest first.</summary>
    [HttpGet("{vehicleId:guid}/odometer-readings")]
    [ProducesResponseType<IReadOnlyList<OdometerReadingEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OdometerReadingEntry>>> ListReadings(
        Guid vehicleId,
        [FromServices] IUseCase<ListOdometerReadingsRequest, IReadOnlyList<OdometerReadingEntry>> useCase,
        CancellationToken cancellationToken,
        [FromQuery] int take = 50) =>
        (await useCase.ExecuteAsync(new ListOdometerReadingsRequest(vehicleId, take), cancellationToken)).ToActionResult(this);
}
