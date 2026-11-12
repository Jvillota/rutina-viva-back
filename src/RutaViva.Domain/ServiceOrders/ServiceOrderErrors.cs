using RutaViva.Domain.Common;

namespace RutaViva.Domain.ServiceOrders;

public static class ServiceOrderErrors
{
    public static readonly Error QuoteMustBePositive =
        Error.Validation("service_order.quote_not_positive", "The quote must be greater than zero.");

    public static readonly Error NegativeServiceKm =
        Error.Validation("service_order.negative_service_km", "The service kilometers cannot be negative.");

    public static readonly Error ServiceDateInFuture =
        Error.Validation("service_order.service_date_in_future", "The service date cannot be later than today.");

    public static readonly Error ServiceDateBeforeOrder =
        Error.Validation("service_order.service_date_before_order", "The service date cannot be earlier than the order creation.");

    public static Error NotFound(Guid orderId) =>
        Error.NotFound("service_order.not_found", $"Service order '{orderId}' was not found.");

    public static Error InvalidTransition(ServiceOrderStatus current, string action) =>
        Error.Conflict("service_order.invalid_transition", $"Cannot {action} a service order in status '{current}'.");
}
