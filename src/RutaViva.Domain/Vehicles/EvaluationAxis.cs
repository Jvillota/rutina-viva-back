namespace RutaViva.Domain.Vehicles;

/// <summary>Which interval caused a status change: kilometers (odometer readings) or time (daily check).</summary>
public enum EvaluationAxis
{
    Kilometers = 0,
    Time = 1,
}
