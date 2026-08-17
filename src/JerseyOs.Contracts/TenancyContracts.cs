namespace JerseyOs.Contracts;

public sealed record OrganizationSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string DefaultCurrency,
    int ProductCount,
    int MemberCount,
    DateTimeOffset CreatedAtUtc);

public sealed record OrganizationQuotaResponse(
    Guid OrganizationId,
    int MaxProducts,
    int MaxMembers,
    int MaxImportBatchesPerDay,
    int MaxAiGenerationsPerDay,
    int ProductCount,
    int MemberCount,
    int ImportBatchesToday,
    int AiGenerationsToday);

public sealed record UpdateOrganizationQuotaRequest(
    int MaxProducts,
    int MaxMembers,
    int MaxImportBatchesPerDay,
    int MaxAiGenerationsPerDay);

public sealed record ProvisionOrganizationRequest(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    string? DefaultCurrency);

public sealed record OrganizationMembershipResponse(
    Guid OrganizationId,
    string Name,
    string Slug,
    string Role);

public sealed record SwitchOrganizationRequest(Guid OrganizationId);

public sealed record OrganizationInvitationResponse(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record CreatedOrganizationInvitationResponse(
    Guid Id,
    string Email,
    string Role,
    string Plaintext,
    DateTimeOffset ExpiresAtUtc);

public sealed record CreateOrganizationInvitationRequest(string Email, string Role);

public sealed record AcceptOrganizationInvitationRequest(string Token, string? Password);

public sealed record OrganizationIntegrationSettingsResponse(
    string? ShopifyShopDomain,
    bool ShopifyAccessTokenConfigured,
    bool ShopifyWebhookSecretConfigured,
    string? ShopifyApiVersion,
    string? WooStoreBaseUrl,
    bool WooConsumerKeyConfigured,
    bool WooConsumerSecretConfigured,
    string? WooApiVersion);

public sealed record UpdateOrganizationIntegrationSettingsRequest(
    string? ShopifyShopDomain,
    string? ShopifyAccessToken,
    string? ShopifyWebhookSecret,
    string? ShopifyApiVersion,
    string? WooStoreBaseUrl,
    string? WooConsumerKey,
    string? WooConsumerSecret,
    string? WooApiVersion);
