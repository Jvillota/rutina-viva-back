using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Domain.Common;
using RutaViva.Domain.ServiceOrders;

namespace RutaViva.Application.ServiceOrders;

public sealed record ListServiceOrdersRequest(ServiceOrderStatus? Status, Guid? VehicleId);

public sealed class ListServiceOrdersUseCase(IServiceOrderRepository serviceOrders)
    : IUseCase<ListServiceOrdersRequest, IReadOnlyList<ServiceOrderView>>
{
    public async Task<Result<IReadOnlyList<ServiceOrderView>>> ExecuteAsync(ListServiceOrdersRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var orders = await serviceOrders.ListAsync(request.Status, request.VehicleId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ServiceOrderView>>(
            orders.OrderByDescending(o => o.UpdatedAt).Select(o => o.ToView()).ToList());
    }
}

public sealed record GetServiceOrderRequest(Guid ServiceOrderId);

public sealed class GetServiceOrderUseCase(IServiceOrderRepository serviceOrders)
    : IUseCase<GetServiceOrderRequest, ServiceOrderView>
{
    public async Task<Result<ServiceOrderView>> ExecuteAsync(GetServiceOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await serviceOrders.GetByIdAsync(request.ServiceOrderId, cancellationToken).ConfigureAwait(false);
        return order is null ? ServiceOrderErrors.NotFound(request.ServiceOrderId) : order.ToView();
    }
}
