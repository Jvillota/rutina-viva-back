using System.ComponentModel.DataAnnotations;

namespace RutaViva.Api.Contracts;

// HTTP request bodies. Only shape validation lives here; business rules are validated by the domain.

/// <summary>New workshop.</summary>
/// <param name="Name">Workshop name, e.g. "Taller Autoservicio Norte".</param>
public sealed record RegisterWorkshopBody([Required] string? Name);

/// <summary>New vehicle with its assigned workshop.</summary>
/// <param name="Plate">Plate, up to 10 characters, e.g. "ABC123".</param>
/// <param name="WorkshopId">Existing workshop assigned to the vehicle.</param>
/// <param name="InitialOdometerKm">Current odometer in kilometers.</param>
public sealed record RegisterVehicleBody(
    [Required] string? Plate,
    [Required] Guid? WorkshopId,
    [Required, Range(0, int.MaxValue)] int? InitialOdometerKm);

/// <summary>New maintenance routine. At least one interval is required; with both, it is due with whichever happens first.</summary>
/// <param name="Name">Routine name, e.g. "Cambio de aceite".</param>
/// <param name="IntervalKm">Every N kilometers, e.g. 10000.</param>
/// <param name="IntervalMonths">Every N months, e.g. 6.</param>
/// <param name="LastServiceKm">Odometer of the last service. Defaults to the current odometer.</param>
/// <param name="LastServiceDate">Date of the last service. Defaults to today.</param>
public sealed record AddRoutineBody(
    [Required] string? Name,
    int? IntervalKm,
    int? IntervalMonths,
    int? LastServiceKm,
    DateOnly? LastServiceDate);

/// <summary>Odometer reading sent by the telemetry platform.</summary>
/// <param name="Km">Odometer in kilometers.</param>
public sealed record OdometerReadingBody([Required] int? Km);

/// <summary>Workshop quote.</summary>
/// <param name="AmountCop">Amount in Colombian pesos, e.g. 850000.</param>
public sealed record SubmitQuoteBody([Required] decimal? AmountCop);

/// <summary>Real service values reported by the workshop when completing the order.</summary>
/// <param name="ServiceKm">Odometer at the service.</param>
/// <param name="ServiceDate">Date of the service (not in the future).</param>
public sealed record CompleteServiceBody([Required] int? ServiceKm, [Required] DateOnly? ServiceDate);

/// <summary>Demo only: days to move the business clock forward.</summary>
/// <param name="Days">Days to advance, greater than zero.</param>
public sealed record AdvanceClockBody([Required] int? Days);
