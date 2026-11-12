using RutaViva.Domain.Common;

namespace RutaViva.Domain.ServiceOrders;

/// <summary>A positive amount in Colombian pesos (COP), the only currency of the quotes.</summary>
public sealed record Money
{
    public const string Currency = "COP";

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Result<Money> Create(decimal amount) =>
        amount > 0 ? new Money(decimal.Round(amount, 2)) : ServiceOrderErrors.QuoteMustBePositive;

    public override string ToString() => $"{Amount:N0} {Currency}";
}
