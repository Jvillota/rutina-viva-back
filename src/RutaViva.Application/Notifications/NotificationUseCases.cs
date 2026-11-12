using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Domain.Common;

namespace RutaViva.Application.Notifications;

public sealed record ListNotificationsRequest(int Take = 50);

public sealed class ListNotificationsUseCase(INotificationInbox inbox) : IUseCase<ListNotificationsRequest, IReadOnlyList<NotificationEntry>>
{
    public async Task<Result<IReadOnlyList<NotificationEntry>>> ExecuteAsync(ListNotificationsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Result.Success(await inbox.ListAsync(Math.Clamp(request.Take, 1, 200), cancellationToken).ConfigureAwait(false));
    }
}

public sealed record MarkNotificationReadRequest(Guid NotificationId);

public sealed class MarkNotificationReadUseCase(INotificationInbox inbox, IBusinessClock clock) : IUseCase<MarkNotificationReadRequest, bool>
{
    public async Task<Result<bool>> ExecuteAsync(MarkNotificationReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await inbox.MarkAsReadAsync(request.NotificationId, clock.Now, cancellationToken).ConfigureAwait(false);
        return found ? true : ApplicationErrors.NotificationNotFound(request.NotificationId);
    }
}
