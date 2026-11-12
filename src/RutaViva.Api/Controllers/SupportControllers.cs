using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RutaViva.Api.Contracts;
using RutaViva.Api.ErrorHandling;
using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Application.Notifications;
using RutaViva.Application.Routines;
using RutaViva.Application.SystemClock;

namespace RutaViva.Api.Controllers;

/// <summary>Fleet manager inbox: published events turned into readable notices (real delivery is out of scope).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController : ControllerBase
{
    /// <summary>Lists notifications, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationEntry>>> List(
        [FromServices] IUseCase<ListNotificationsRequest, IReadOnlyList<NotificationEntry>> useCase,
        CancellationToken cancellationToken,
        [FromQuery] int take = 50) =>
        (await useCase.ExecuteAsync(new ListNotificationsRequest(take), cancellationToken)).ToActionResult(this);

    /// <summary>Marks a notification as read.</summary>
    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        [FromServices] IUseCase<MarkNotificationReadRequest, bool> useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new MarkNotificationReadRequest(notificationId), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}

/// <summary>Demo tools: business clock and on-demand daily check. Respond 404 when Demo:Enabled is false.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/system")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class SystemController : ControllerBase
{
    /// <summary>Current business date and demo offset.</summary>
    [HttpGet("clock")]
    [ProducesResponseType<ClockView>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClockView>> GetClock(
        [FromServices] IUseCase<GetClockRequest, ClockView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new GetClockRequest(), cancellationToken)).ToActionResult(this);

    /// <summary>Moves the business clock forward N days (never back), for the API and the Worker.</summary>
    [HttpPost("clock/advance")]
    [ProducesResponseType<ClockView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClockView>> Advance(
        [FromBody] AdvanceClockBody body,
        [FromServices] IUseCase<AdvanceClockRequest, ClockView> useCase,
        CancellationToken cancellationToken) =>
        (await useCase.ExecuteAsync(new AdvanceClockRequest(body.Days!.Value), cancellationToken)).ToActionResult(this);

    /// <summary>Runs the daily check now with the current business date (idempotent).</summary>
    [HttpPost("daily-check")]
    [ProducesResponseType<DailyCheckSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyCheckSummary>> RunDailyCheck(
        [FromServices] IDemoClock demoClock,
        [FromServices] IUseCase<RunDailyCheckRequest, DailyCheckSummary> useCase,
        CancellationToken cancellationToken)
    {
        if (!demoClock.IsEnabled)
        {
            return this.ToProblem(ApplicationErrors.DemoDisabled);
        }

        return (await useCase.ExecuteAsync(new RunDailyCheckRequest(), cancellationToken)).ToActionResult(this);
    }
}
