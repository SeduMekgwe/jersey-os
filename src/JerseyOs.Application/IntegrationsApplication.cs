using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public static class OpsStatusKinds
{
    public const string ImportBatch = "import.batch";
    public const string ImportScrape = "import.scrape";
    public const string PublishingRun = "publishing.run";
    public const string AiGeneration = "ai.generation";
}

public sealed record OpsStatusEvent(
    Guid OrganizationId,
    string Kind,
    string EntityId,
    string Status,
    DateTimeOffset OccurredAtUtc);

public interface IOpsStatusPublisher
{
    Task PublishAsync(OpsStatusEvent status, CancellationToken cancellationToken);
}

public static class ApiKeySecrets
{
    public static (string Plaintext, string Prefix, byte[] Hash) Generate()
    {
        var prefixToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var prefix = ApiKeyCredential.PlaintextPrefix + prefixToken;
        var plaintext = prefix + "_" + secret;
        return (plaintext, prefix, Hash(plaintext));
    }

    public static byte[] Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return SHA256.HashData(Encoding.UTF8.GetBytes(plaintext.Trim()));
    }
}

public sealed record ListApiKeysQuery : IRequest<IReadOnlyCollection<ApiKeyResponse>>;
public sealed record CreateApiKeyCommand(string Name, IReadOnlyCollection<string> Scopes)
    : IRequest<CreatedApiKeyResponse>;
public sealed record RevokeApiKeyCommand(Guid ApiKeyId) : IRequest<ApiKeyResponse?>;

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ApiKeyCredential.NameMaxLength);
        RuleFor(x => x.Scopes).NotEmpty().Must(s => s.Count <= 32);
        RuleForEach(x => x.Scopes).NotEmpty().MaximumLength(150);
    }
}

public static class ApiKeyMapping
{
    public static ApiKeyResponse ToResponse(ApiKeyCredential key) =>
        new(
            key.Id,
            key.Name,
            key.Prefix,
            key.ScopeKeys,
            key.CreatedAtUtc,
            key.LastUsedAtUtc,
            key.RevokedAtUtc);
}

public sealed class ListApiKeysHandler(IApplicationDbContext db)
    : IRequestHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeyResponse>>
{
    public async Task<IReadOnlyCollection<ApiKeyResponse>> Handle(
        ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var keys = await db.ApiKeys.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return keys.Select(ApiKeyMapping.ToResponse).ToArray();
    }
}

public sealed class CreateApiKeyHandler(IApplicationDbContext db, ICurrentRequest current, IAuditRecorder audit)
    : IRequestHandler<CreateApiKeyCommand, CreatedApiKeyResponse>
{
    public async Task<CreatedApiKeyResponse> Handle(
        CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        if (current.OrganizationId is not { } organizationId)
        {
            throw new InvalidOperationException("Organization context is required.");
        }

        var (plaintext, prefix, hash) = ApiKeySecrets.Generate();
        var key = new ApiKeyCredential(
            organizationId,
            request.Name,
            prefix,
            hash,
            request.Scopes,
            current.UserId);
        db.Add(key);
        audit.Record(
            organizationId,
            AuditActions.ApiKeyCreated,
            nameof(ApiKeyCredential),
            key.Id.ToString("N"),
            new { key.Name, key.Prefix, scopes = key.ScopeKeys });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreatedApiKeyResponse(
            key.Id,
            key.Name,
            key.Prefix,
            key.ScopeKeys,
            plaintext,
            key.CreatedAtUtc);
    }
}

public sealed class RevokeApiKeyHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IAuditRecorder audit,
    TimeProvider time)
    : IRequestHandler<RevokeApiKeyCommand, ApiKeyResponse?>
{
    public async Task<ApiKeyResponse?> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.SingleOrDefaultAsync(x => x.Id == request.ApiKeyId, cancellationToken)
            .ConfigureAwait(false);
        if (key is null)
        {
            return null;
        }

        key.Revoke(time.GetUtcNow());
        if (current.OrganizationId is { } organizationId)
        {
            audit.Record(
                organizationId,
                AuditActions.ApiKeyRevoked,
                nameof(ApiKeyCredential),
                key.Id.ToString("N"),
                new { key.Name, key.Prefix });
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApiKeyMapping.ToResponse(key);
    }
}
