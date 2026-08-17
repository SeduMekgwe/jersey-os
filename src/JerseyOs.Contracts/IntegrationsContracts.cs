namespace JerseyOs.Contracts;

public sealed record CreateApiKeyRequest(string Name, IReadOnlyCollection<string> Scopes);

public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record CreatedApiKeyResponse(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyCollection<string> Scopes,
    string Plaintext,
    DateTimeOffset CreatedAtUtc);

public sealed record OpsStatusEventDto(
    Guid OrganizationId,
    string Kind,
    string EntityId,
    string Status,
    DateTimeOffset OccurredAtUtc);
