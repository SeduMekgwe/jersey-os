using System.Text.Json;

namespace JerseyOs.Contracts;

public sealed record IntegrationEventEnvelope(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    Guid OrganizationId,
    string CorrelationId,
    string? CausationId,
    DateTimeOffset OccurredAtUtc,
    JsonElement Payload);
