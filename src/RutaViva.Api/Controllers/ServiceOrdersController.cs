using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RutaViva.Api.Contracts;
using RutaViva.Api.ErrorHandling;
using RutaViva.Application.Abstractions;
using RutaViva.Application.ServiceOrders;
using RutaViva.Domain.ServiceOrders;

namespace RutaViva.Api.Controllers;

/// <summary>
/// Service orders: created → quoted → approved → in progress → completed, or cancelled before execution.
/// Workshop actions: quote, start, complete. Client actions: approve, reject, cancel.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/service-orders")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ServiceOrdersController : ControllerBase
{
    /// <summary>Lists orders, optionally filtered by status and vehicle.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceOrderView>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ServiceOrderView>>> List(
        [FromServices] IUseCase<ListServiceOrdersRequest, IReadOnlyList<ServiceOrderView>> useCase,
        CancellationToken cancellationToken,
        [FromQuery] ServiceOrderStatus? status = null,
        [FromQuery] Guid? vehicleId = null) =>
        (await useCase.ExecuteAsync(new ListServiceOrdersRequest(status, vehicleId), cancellationToken)).ToActionResult(this);

    /// <summary>Order detail with its full transition history.</summary>
    [HttpGet("{serviceOrderId:guid}")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceOrderView>> Get(
        Guid serviceOrderId,
        [FromServices] IUseCase<GetServiceOrderRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new GetServiceOrderRequest(serviceOrderId), cancellationToken)).ToActionResult(this);

    /// <summary>Workshop: quotes a created order. Up to 1,500,000 COP it is approved automatically.</summary>
    /// <response code="409">The order is not in status Created.</response>
    [HttpPost("{serviceOrderId:guid}/quote")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderView>> Quote(
        Guid serviceOrderId,
        [FromBody] SubmitQuoteBody body,
        [FromServices] IUseCase<SubmitQuoteRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new SubmitQuoteRequest(serviceOrderId, body.AmountCop!.Value), cancellationToken)).ToActionResult(this);

    /// <summary>Client: approves a quote above the threshold.</summary>
    [HttpPost("{serviceOrderId:guid}/approve")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderView>> Approve(
        Guid serviceOrderId,
        [FromServices] IUseCase<ApproveQuoteRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new ApproveQuoteRequest(serviceOrderId), cancellationToken)).ToActionResult(this);

    /// <summary>Client: rejects the quote, which cancels the order.</summary>
    [HttpPost("{serviceOrderId:guid}/reject")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderView>> Reject(
        Guid serviceOrderId,
        [FromServices] IUseCase<RejectQuoteRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new RejectQuoteRequest(serviceOrderId), cancellationToken)).ToActionResult(this);

    /// <summary>Client: cancels the order (only before it is in progress).</summary>
    [HttpPost("{serviceOrderId:guid}/cancel")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderView>> Cancel(
        Guid serviceOrderId,
        [FromServices] IUseCase<CancelServiceOrderRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new CancelServiceOrderRequest(serviceOrderId), cancellationToken)).ToActionResult(this);

    /// <summary>Workshop: starts an approved order.</summary>
    [HttpPost("{serviceOrderId:guid}/start")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderView>> Start(
        Guid serviceOrderId,
        [FromServices] IUseCase<StartServiceRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new StartServiceRequest(serviceOrderId), cancellationToken)).ToActionResult(this);

    /// <summary>Workshop: completes the order with the real service km and date; the routine restarts from them.</summary>
    [HttpPost("{serviceOrderId:guid}/complete")]
    [ProducesResponseType<ServiceOrderView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceOrderView>> Complete(
        Guid serviceOrderId,
        [FromBody] CompleteServiceBody body,
        [FromServices] IUseCase<CompleteServiceRequest, ServiceOrderView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new CompleteServiceRequest(serviceOrderId, body.ServiceKm!.Value, body.ServiceDate!.Value), cancellationToken))
            .ToActionResult(this);
}
