namespace RutaViva.Domain.ServiceOrders;

/// <summary>
/// Rule 4: quotes above the threshold need the client's approval; quotes at or below it are approved automatically
/// (decision G: exactly 1,500,000 COP is automatic). The threshold comes from configuration.
/// </summary>
public sealed class QuoteApprovalPolicy
{
    public const decimal DefaultThresholdCop = 1_500_000m;

    public QuoteApprovalPolicy(decimal thresholdCop = DefaultThresholdCop)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdCop);
        ThresholdCop = thresholdCop;
    }

    public decimal ThresholdCop { get; }

    public bool RequiresClientApproval(Money quote) => quote.Amount > ThresholdCop;
}
