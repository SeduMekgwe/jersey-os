using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public static class OrganizationRoles
{
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static bool IsKnown(string role) =>
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Member, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Admin;
        }

        if (string.Equals(role, Member, StringComparison.OrdinalIgnoreCase))
        {
            return Member;
        }

        throw new InvalidOperationException($"Unknown organization role '{role}'.");
    }
}

public static class OrganizationSettingKeys
{
    public const string ShopifyShopDomain = "shopify.shopDomain";
    public const string ShopifyAccessToken = "shopify.accessToken";
    public const string ShopifyWebhookSecret = "shopify.webhookSecret";
    public const string ShopifyApiVersion = "shopify.apiVersion";
    public const string WooStoreBaseUrl = "woocommerce.storeBaseUrl";
    public const string WooConsumerKey = "woocommerce.consumerKey";
    public const string WooConsumerSecret = "woocommerce.consumerSecret";
    public const string WooApiVersion = "woocommerce.apiVersion";

    public static readonly string[] Secrets =
    [
        ShopifyAccessToken,
        ShopifyWebhookSecret,
        WooConsumerKey,
        WooConsumerSecret
    ];

    public static bool IsSecret(string key) =>
        Secrets.Contains(key, StringComparer.OrdinalIgnoreCase);
}

public sealed class OrganizationQuota : AuditableEntity, IOrganizationScoped
{
    private OrganizationQuota()
    {
    }

    public OrganizationQuota(
        Guid organizationId,
        int maxProducts,
        int maxMembers,
        int maxImportBatchesPerDay,
        int maxAiGenerationsPerDay)
    {
        OrganizationId = organizationId;
        SetLimits(maxProducts, maxMembers, maxImportBatchesPerDay, maxAiGenerationsPerDay);
    }

    public Guid OrganizationId { get; private set; }
    public int MaxProducts { get; private set; }
    public int MaxMembers { get; private set; }
    public int MaxImportBatchesPerDay { get; private set; }
    public int MaxAiGenerationsPerDay { get; private set; }

    public void SetLimits(int maxProducts, int maxMembers, int maxImportBatchesPerDay, int maxAiGenerationsPerDay)
    {
        MaxProducts = RequirePositive(maxProducts, nameof(maxProducts));
        MaxMembers = RequirePositive(maxMembers, nameof(maxMembers));
        MaxImportBatchesPerDay = RequirePositive(maxImportBatchesPerDay, nameof(maxImportBatchesPerDay));
        MaxAiGenerationsPerDay = RequirePositive(maxAiGenerationsPerDay, nameof(maxAiGenerationsPerDay));
    }

    private static int RequirePositive(int value, string name)
    {
        if (value < 1)
        {
            throw new InvalidOperationException($"{name} must be at least 1.");
        }

        return value;
    }
}

public sealed class OrganizationSetting : AuditableEntity, IOrganizationScoped
{
    private OrganizationSetting()
    {
    }

    public OrganizationSetting(Guid organizationId, string key, string? value)
    {
        OrganizationId = organizationId;
        Key = RequireKey(key);
        SetValue(value);
    }

    public Guid OrganizationId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string? Value { get; private set; }

    public bool IsSecret => OrganizationSettingKeys.IsSecret(Key);

    public void SetValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Value = null;
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 4000)
        {
            throw new InvalidOperationException("Setting value cannot exceed 4000 characters.");
        }

        Value = trimmed;
    }

    private static string RequireKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var trimmed = key.Trim();
        if (trimmed.Length > 128)
        {
            throw new InvalidOperationException("Setting key cannot exceed 128 characters.");
        }

        return trimmed;
    }
}

public sealed class OrganizationInvitation : AuditableEntity, IOrganizationScoped
{
    public const string PlaintextPrefix = "inv_";

    private OrganizationInvitation()
    {
    }

    public OrganizationInvitation(
        Guid organizationId,
        string email,
        string role,
        byte[] tokenHash,
        DateTimeOffset expiresAtUtc)
    {
        OrganizationId = organizationId;
        Email = RequireEmail(email);
        Role = OrganizationRoles.Normalize(role);
        if (tokenHash is not { Length: 32 })
        {
            throw new InvalidOperationException("Invitation token hash must be SHA-256 (32 bytes).");
        }

        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsUsable(DateTimeOffset now) =>
        AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > now;

    public void Accept(DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            throw new InvalidOperationException("Invitation is not usable.");
        }

        AcceptedAtUtc = now;
    }

    public void Revoke(DateTimeOffset now) => RevokedAtUtc ??= now;

    private static string RequireEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var trimmed = email.Trim().ToLowerInvariant();
        if (trimmed.Length > 256 || !trimmed.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invitation email is invalid.");
        }

        return trimmed;
    }
}
