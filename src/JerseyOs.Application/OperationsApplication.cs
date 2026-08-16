using System.Text.Json;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public interface IAuditRecorder
{
    void Record(Guid organizationId, string action, string entityType, string entityId, object? data = null);
}

public interface INotificationPublisher
{
    Task PublishAsync(Guid organizationId, string kind, object payload, CancellationToken cancellationToken);
}

public sealed class AuditRecorder(IApplicationDbContext db, ICurrentRequest current) : IAuditRecorder
{
    public void Record(Guid organizationId, string action, string entityType, string entityId, object? data = null)
    {
        var json = data is null ? null : JsonSerializer.Serialize(data);
        db.Add(new AuditLog(organizationId, action, entityType, entityId, json, current.CorrelationId));
    }
}

public sealed record ListAuditEventsQuery(string? Action, string? EntityType)
    : IRequest<IReadOnlyCollection<AuditEventResponse>>;

public sealed class ListAuditEventsHandler(IApplicationDbContext db)
    : IRequestHandler<ListAuditEventsQuery, IReadOnlyCollection<AuditEventResponse>>
{
    public async Task<IReadOnlyCollection<AuditEventResponse>> Handle(
        ListAuditEventsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();
            query = query.Where(x => x.EntityType == entityType);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(x => new AuditEventResponse(
                x.Id,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.DataJson,
                x.CorrelationId,
                x.CreatedBy,
                x.CreatedAtUtc))
            .ToArray();
    }
}

public sealed record ListNotificationTemplatesQuery : IRequest<IReadOnlyCollection<NotificationTemplateResponse>>;
public sealed record ListNotificationMessagesQuery
    : IRequest<IReadOnlyCollection<NotificationMessageResponse>>;

public static class NotificationMapping
{
    public static NotificationTemplateResponse ToTemplate(NotificationTemplate template) =>
        new(template.Id, template.Name, template.Kind, template.SubjectTemplate, template.BodyTemplate, template.IsEnabled);

    public static NotificationMessageResponse ToMessage(
        NotificationMessage message, IReadOnlyCollection<NotificationDelivery> deliveries) =>
        new(
            message.Id,
            message.Kind,
            message.Subject,
            message.Body,
            message.Status.ToString(),
            message.CorrelationId,
            message.CreatedAtUtc,
            message.CompletedAtUtc,
            deliveries
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new NotificationDeliveryResponse(
                    x.Id,
                    x.Channel,
                    x.Destination,
                    x.Status.ToString(),
                    x.Attempt,
                    x.Error,
                    x.CompletedAtUtc))
                .ToArray());
}

public sealed class ListNotificationTemplatesHandler(IApplicationDbContext db)
    : IRequestHandler<ListNotificationTemplatesQuery, IReadOnlyCollection<NotificationTemplateResponse>>
{
    public async Task<IReadOnlyCollection<NotificationTemplateResponse>> Handle(
        ListNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await db.NotificationTemplates.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return templates.Select(NotificationMapping.ToTemplate).ToArray();
    }
}

public sealed class ListNotificationMessagesHandler(IApplicationDbContext db)
    : IRequestHandler<ListNotificationMessagesQuery, IReadOnlyCollection<NotificationMessageResponse>>
{
    public async Task<IReadOnlyCollection<NotificationMessageResponse>> Handle(
        ListNotificationMessagesQuery request, CancellationToken cancellationToken)
    {
        var messages = await db.NotificationMessages.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var ids = messages.Select(x => x.Id).ToArray();
        var deliveries = await db.NotificationDeliveries.AsNoTracking()
            .Where(x => ids.Contains(x.MessageId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byMessage = deliveries.ToLookup(x => x.MessageId);
        return messages.Select(x => NotificationMapping.ToMessage(x, byMessage[x.Id].ToArray())).ToArray();
    }
}
