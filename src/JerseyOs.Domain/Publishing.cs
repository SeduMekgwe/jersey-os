using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public enum PublishRunStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

public static class PublishEntityTypes
{
    public const string Product = "product";
    public const string Variant = "variant";
}

public static class SalesChannelCodes
{
    public const string Shopify = "shopify";
}

public sealed class SalesChannel : AuditableEntity, IOrganizationScoped
{
    private SalesChannel() { }

    public SalesChannel(Guid organizationId, string code, string displayName, bool enabled = true)
    {
        OrganizationId = organizationId;
        Code = code.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Enabled = enabled;
    }

    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }

    public void SetEnabled(bool enabled) => Enabled = enabled;
}

public sealed class ExternalIdMap : AuditableEntity, IOrganizationScoped
{
    private ExternalIdMap() { }

    public ExternalIdMap(
        Guid organizationId,
        Guid channelId,
        string entityType,
        Guid localId,
        string externalId)
    {
        OrganizationId = organizationId;
        ChannelId = channelId;
        EntityType = entityType.Trim().ToLowerInvariant();
        LocalId = localId;
        ExternalId = externalId.Trim();
    }

    public Guid OrganizationId { get; private set; }
    public Guid ChannelId { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid LocalId { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public SalesChannel Channel { get; private set; } = null!;

    public void UpdateExternalId(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ExternalId = externalId.Trim();
    }
}

public sealed class PublishRun : AuditableEntity, IOrganizationScoped
{
    private PublishRun() { }

    public PublishRun(Guid organizationId, Guid channelId, Guid productId)
    {
        OrganizationId = organizationId;
        ChannelId = channelId;
        ProductId = productId;
        Status = PublishRunStatus.Pending;
    }

    public Guid OrganizationId { get; private set; }
    public Guid ChannelId { get; private set; }
    public Guid ProductId { get; private set; }
    public PublishRunStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public SalesChannel Channel { get; private set; } = null!;

    public void MarkSucceeded(DateTimeOffset now)
    {
        Status = PublishRunStatus.Succeeded;
        Error = null;
        CompletedAtUtc = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Status = PublishRunStatus.Failed;
        Error = error.Trim();
        CompletedAtUtc = now;
    }

    public void MarkPending()
    {
        Status = PublishRunStatus.Pending;
        Error = null;
        CompletedAtUtc = null;
    }
}
