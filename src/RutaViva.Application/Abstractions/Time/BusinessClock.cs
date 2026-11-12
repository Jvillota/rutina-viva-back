using Microsoft.Extensions.Options;

namespace RutaViva.Application.Abstractions.Time;

public sealed class BusinessOptions
{
    public const string SectionName = "Business";

    /// <summary>Offset of the business time zone. Colombia is UTC-5 all year (no daylight saving).</summary>
    public int UtcOffsetHours { get; set; } = -5;

    /// <summary>Quotes above this amount need the client's approval (rule 4).</summary>
    public decimal QuoteApprovalThresholdCop { get; set; } = 1_500_000m;
}

/// <summary>Business «now» and «today». Everything time-dependent goes through here, never through DateTime.Now.</summary>
public interface IBusinessClock
{
    DateTimeOffset Now { get; }

    DateOnly Today { get; }
}

public sealed class BusinessClock(TimeProvider timeProvider, IOptions<BusinessOptions> options) : IBusinessClock
{
    private readonly TimeSpan _offset = TimeSpan.FromHours(options.Value.UtcOffsetHours);

    public DateTimeOffset Now => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(_offset).DateTime);
}

/// <summary>Demo-only control of the business clock, shared by every process through the database.</summary>
public interface IDemoClock
{
    bool IsEnabled { get; }

    Task<TimeSpan> GetOffsetAsync(CancellationToken cancellationToken);

    Task<TimeSpan> AdvanceAsync(TimeSpan by, CancellationToken cancellationToken);
}
