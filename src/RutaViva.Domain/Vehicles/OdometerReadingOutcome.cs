namespace RutaViva.Domain.Vehicles;

/// <summary>Result of an odometer reading. Repeated or out-of-order readings are ignored without error (rule 6).</summary>
public enum OdometerReadingOutcome
{
    Applied = 0,
    Ignored = 1,
}
