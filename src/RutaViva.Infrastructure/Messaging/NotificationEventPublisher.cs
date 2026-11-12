using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RutaViva.Application.Abstractions.Messaging;
using RutaViva.Infrastructure.Persistence;

namespace RutaViva.Infrastructure.Messaging;

/// <summary>
/// Adapter of <see cref="IEventPublisher"/>: real delivery (email, push) is out of scope, so events are logged and
/// the ones relevant to the fleet manager land in the notifications inbox. Idempotent by outbox message id.
/// </summary>
internal sealed partial class NotificationEventPublisher(RutaVivaDbContext db, ILogger<NotificationEventPublisher> logger) : IEventPublisher
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-CO");

    public async Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogPublished(logger, message.Type, message.MessageId);

        using var json = JsonDocument.Parse(message.Payload);
        var notification = ToNotification(message.Type, json.RootElement);
        if (notification is null)
        {
            return;
        }

        var (title, text, vehicleId, orderId) = notification.Value;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO notifications (id, outbox_message_id, type, title, message, vehicle_id, service_order_id, created_at)
            VALUES ({Guid.CreateVersion7()}, {message.MessageId}, {message.Type}, {title}, {text}, {vehicleId}, {orderId}, {message.OccurredAt})
            ON CONFLICT (outbox_message_id) DO NOTHING
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static (string Title, string Message, Guid? VehicleId, Guid? OrderId)? ToNotification(string type, JsonElement e)
    {
        string S(string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : string.Empty;
        Guid? G(string name) => e.TryGetProperty(name, out var p) && p.TryGetGuid(out var g) ? g : null;
        string Due() =>
            string.Join(" o ", new[]
            {
                e.TryGetProperty("dueKm", out var km) && km.ValueKind == JsonValueKind.Number ? $"km {km.GetInt32().ToString("N0", Spanish)}" : null,
                S("dueDate") is { Length: > 0 } date ? $"el {date}" : null,
            }.Where(x => x is not null));
        string Cop(string name) =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal().ToString("C0", Spanish) : "?";

        var plate = S("plate");
        var routine = S("routineName");
        return type switch
        {
            "RoutineDueSoon" => ($"Mantenimiento por vencer: {plate}", $"La rutina «{routine}» del vehículo {plate} vence {Due()}.", G("vehicleId"), null),
            "RoutineOverdue" => ($"Mantenimiento vencido: {plate}", $"La rutina «{routine}» del vehículo {plate} está vencida. Se generó una orden de servicio.", G("vehicleId"), null),
            "ServiceOrderCreated" => ($"Orden de servicio creada: {plate}", $"Se creó la orden para «{routine}» y se envió al taller asignado.", G("vehicleId"), G("serviceOrderId")),
            "QuoteApprovalRequired" => ($"Cotización por aprobar: {plate}", $"El taller cotizó «{routine}» en {Cop("amountCop")}, por encima de {Cop("thresholdCop")}. Requiere su aprobación.", G("vehicleId"), G("serviceOrderId")),
            "ServiceOrderCompleted" => ($"Servicio completado: {plate}", $"El taller completó «{routine}». La rutina se reinició.", G("vehicleId"), G("serviceOrderId")),
            "ServiceOrderCancelled" => ($"Orden cancelada: {plate}", $"La orden de «{routine}» fue cancelada.", G("vehicleId"), G("serviceOrderId")),
            _ => null,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Event published: {EventType} ({MessageId})")]
    private static partial void LogPublished(ILogger logger, string eventType, Guid messageId);
}
