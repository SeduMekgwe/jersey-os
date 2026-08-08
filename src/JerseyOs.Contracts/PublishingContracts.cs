namespace JerseyOs.Contracts;

public sealed record SalesChannelResponse(Guid Id, string Code, string DisplayName, bool Enabled);
public sealed record PublishRunResponse(
    Guid Id,
    Guid ChannelId,
    string ChannelCode,
    Guid ProductId,
    string Status,
    string? Error,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ModifiedAtUtc);
public sealed record SetChannelEnabledRequest(bool Enabled);
public sealed record WebhookDeliveryResponse(
    Guid Id,
    string WebhookId,
    string Topic,
    string Status,
    string? Error,
    DateTimeOffset? ProcessedAtUtc,
    DateTimeOffset CreatedAtUtc);
