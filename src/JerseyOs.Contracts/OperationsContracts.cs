namespace JerseyOs.Contracts;

public sealed record AuditEventResponse(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    string? DataJson,
    string CorrelationId,
    string Actor,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationTemplateResponse(
    Guid Id,
    string Name,
    string Kind,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsEnabled);

public sealed record NotificationDeliveryResponse(
    Guid Id,
    string Channel,
    string Destination,
    string Status,
    int Attempt,
    string? Error,
    DateTimeOffset? CompletedAtUtc);

public sealed record NotificationMessageResponse(
    Guid Id,
    string Kind,
    string Subject,
    string Body,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyCollection<NotificationDeliveryResponse> Deliveries);
