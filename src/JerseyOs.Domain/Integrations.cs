using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public sealed class ApiKeyCredential : AuditableEntity, IOrganizationScoped
{
    public const string PlaintextPrefix = "jos_";
    public const int NameMaxLength = 100;
    public const int DisplayPrefixMaxLength = 16;
    public const int ScopesMaxLength = 1000;

    private ApiKeyCredential()
    {
    }

    public ApiKeyCredential(
        Guid organizationId,
        string name,
        string prefix,
        byte[] secretHash,
        IReadOnlyCollection<string> scopes,
        Guid? createdByUserId)
    {
        OrganizationId = organizationId;
        Name = RequireName(name);
        Prefix = RequirePrefix(prefix);
        if (secretHash is not { Length: 32 })
        {
            throw new InvalidOperationException("API key hash must be SHA-256 (32 bytes).");
        }

        SecretHash = secretHash;
        Scopes = NormalizeScopes(scopes);
        CreatedByUserId = createdByUserId;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Prefix { get; private set; } = string.Empty;
    public byte[] SecretHash { get; private set; } = [];
    public string Scopes { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public IReadOnlyList<string> ScopeKeys =>
        Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsUsable => RevokedAtUtc is null;

    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;

    public void TouchLastUsed(DateTimeOffset now)
    {
        if (LastUsedAtUtc is { } previous && now - previous < TimeSpan.FromMinutes(5))
        {
            return;
        }

        LastUsedAtUtc = now;
    }

    public static string NormalizeScopes(IReadOnlyCollection<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0)
        {
            throw new InvalidOperationException("API key requires at least one scope.");
        }

        var normalized = scopes
            .Select(RequireScope)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var joined = string.Join(',', normalized);
        if (joined.Length > ScopesMaxLength)
        {
            throw new InvalidOperationException($"API key scopes cannot exceed {ScopesMaxLength} characters.");
        }

        return joined;
    }

    private static string RequireName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            throw new InvalidOperationException($"API key name cannot exceed {NameMaxLength} characters.");
        }

        return trimmed;
    }

    private static string RequirePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var trimmed = prefix.Trim();
        if (!trimmed.StartsWith(PlaintextPrefix, StringComparison.Ordinal)
            || trimmed.Length is < 8 or > DisplayPrefixMaxLength)
        {
            throw new InvalidOperationException("API key prefix is invalid.");
        }

        return trimmed;
    }

    private static string RequireScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var trimmed = scope.Trim().ToLowerInvariant();
        if (trimmed.Length > 150 || trimmed.Contains(',', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid API key scope '{scope}'.");
        }

        foreach (var ch in trimmed)
        {
            if (ch is not ('.' or '_' or (>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                throw new InvalidOperationException($"Invalid API key scope '{scope}'.");
            }
        }

        return trimmed;
    }
}
