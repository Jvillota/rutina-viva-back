using RutaViva.Domain.Common;

namespace RutaViva.Domain.Vehicles;

/// <summary>
/// How often a routine is due: every N kilometers, every N months, or both (rule 1).
/// With both, the routine is due with whichever happens first.
/// </summary>
public sealed class MaintenanceInterval
{
    /// <summary>Share of the interval that, when remaining, moves the routine to «due soon» (rule 2).</summary>
    public const decimal DueSoonThreshold = 0.10m;

    private MaintenanceInterval()
    {
    }

    private MaintenanceInterval(int? kilometers, int? months)
    {
        Kilometers = kilometers;
        Months = months;
    }

    public int? Kilometers { get; private set; }

    public int? Months { get; private set; }

    public static Result<MaintenanceInterval> Create(int? kilometers, int? months)
    {
        if (kilometers is null && months is null)
        {
            return VehicleErrors.IntervalRequired;
        }

        if (kilometers is <= 0 || months is <= 0)
        {
            return VehicleErrors.IntervalMustBePositive;
        }

        return new MaintenanceInterval(kilometers, months);
    }

    public int? DueKilometers(int cycleStartKm) => cycleStartKm + Kilometers;

    /// <summary>Calendar months: Jan 31 + 1 month = Feb 28/29, as <see cref="DateOnly.AddMonths"/> resolves it.</summary>
    public DateOnly? DueDate(DateOnly cycleStartDate) => Months is int months ? cycleStartDate.AddMonths(months) : null;

    /// <summary>10 % of the kilometer interval.</summary>
    public decimal? KilometersThreshold() => Kilometers * DueSoonThreshold;

    /// <summary>10 % of the real calendar days between the cycle start and the due date (decision C).</summary>
    public decimal? DaysThreshold(DateOnly cycleStartDate) =>
        DueDate(cycleStartDate) is DateOnly due ? (due.DayNumber - cycleStartDate.DayNumber) * DueSoonThreshold : null;
}
