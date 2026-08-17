using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public static class AuditActions
{
    public const string AuthorizationDenied = "authorization.denied";
    public const string InventoryAdjust = "inventory.adjust";
    public const string ImportItemApproved = "import.item.approved";
    public const string ImportItemRejected = "import.item.rejected";
    public const string PublishingRunFailed = "publishing.run.failed";
    public const string ScrapeRunFailed = "scrape.run.failed";
    public const string AiGenerationApproved = "ai.generation.approved";
    public const string PricingRuleCreated = "pricing.rule.created";
    public const string PricingRuleDeleted = "pricing.rule.deleted";
    public const string CollectionCreated = "collection.created";
    public const string CollectionDeleted = "collection.deleted";
    public const string SupplierFeedUpdated = "supplier.feed.updated";
    public const string ApiKeyCreated = "integrations.apikey.created";
    public const string ApiKeyRevoked = "integrations.apikey.revoked";
    public const string OrganizationProvisioned = "org.provisioned";
    public const string OrganizationQuotaUpdated = "org.quota.updated";
    public const string InvitationCreated = "org.invitation.created";
    public const string InvitationAccepted = "org.invitation.accepted";
    public const string InvitationRevoked = "org.invitation.revoked";
    public const string OrganizationSettingsUpdated = "org.settings.updated";
    public const string OrganizationSwitched = "auth.organization.switched";
}

public static class NotificationKinds
{
    public const string ImportReady = "import.ready";
    public const string PublishFailed = "publish.failed";
    public const string ScrapeFailed = "scrape.failed";

    public static readonly string[] All = [ImportReady, PublishFailed, ScrapeFailed];

    public static bool IsKnown(string kind) =>
        All.Contains(kind, StringComparer.OrdinalIgnoreCase);
}

public enum NotificationMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public sealed class NotificationTemplate : AuditableEntity, IOrganizationScoped
{
    private NotificationTemplate() { }

    public NotificationTemplate(
        Guid organizationId,
        string kind,
        string name,
        string subjectTemplate,
        string bodyTemplate)
    {
        OrganizationId = organizationId;
        Name = RequireName(name);
        SetKind(kind);
        SetBody(subjectTemplate, bodyTemplate);
        IsEnabled = true;
    }

    public Guid OrganizationId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string SubjectTemplate { get; private set; } = string.Empty;
    public string BodyTemplate { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public void SetKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var normalized = kind.Trim().ToLowerInvariant();
        if (!NotificationKinds.IsKnown(normalized))
        {
            throw new InvalidOperationException($"Unknown notification kind '{kind}'.");
        }

        Kind = normalized;
    }

    public void SetBody(string subjectTemplate, string bodyTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyTemplate);
        SubjectTemplate = subjectTemplate.Trim();
        BodyTemplate = bodyTemplate.Trim();
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public string RenderSubject(IReadOnlyDictionary<string, string?> values) =>
        Render(SubjectTemplate, values);

    public string RenderBody(IReadOnlyDictionary<string, string?> values) =>
        Render(BodyTemplate, values);

    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(values);
        var rendered = template;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace("{{" + key + "}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }

    public static readonly (string Kind, string Name, string Subject, string Body)[] Defaults =
    [
        (NotificationKinds.ImportReady, "Import ready",
            "Import {{fileName}} is ready for review",
            "Batch {{batchId}} from {{supplierName}} is ready ({{itemCount}} items)."),
        (NotificationKinds.PublishFailed, "Publish failed",
            "Publish failed for {{productName}} on {{channelCode}}",
            "Publish run {{runId}} failed: {{error}}"),
        (NotificationKinds.ScrapeFailed, "Scrape failed",
            "Supplier scrape failed for {{supplierCode}}",
            "Scrape run {{runId}} failed: {{error}}")
    ];

    public static (string Kind, string Name, string Subject, string Body) DefaultFor(string kind)
    {
        foreach (var item in Defaults)
        {
            if (string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return Defaults[0];
    }

    private static string RequireName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            throw new InvalidOperationException("Notification template name cannot exceed 200 characters.");
        }

        return trimmed;
    }
}

public sealed class NotificationMessage : AuditableEntity, IOrganizationScoped
{
    private NotificationMessage() { }

    public NotificationMessage(
        Guid organizationId,
        string kind,
        string subject,
        string body,
        string payloadJson,
        Guid? templateId,
        string correlationId)
    {
        OrganizationId = organizationId;
        SetKind(kind);
        Subject = RequireText(subject, 200, "Subject");
        Body = RequireText(body, 4000, "Body");
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
        TemplateId = templateId;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();
        Status = NotificationMessageStatus.Pending;
    }

    public Guid OrganizationId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public Guid? TemplateId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public string CorrelationId { get; private set; } = string.Empty;
    public NotificationMessageStatus Status { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void MarkSent(DateTimeOffset now)
    {
        if (Status is not NotificationMessageStatus.Pending)
        {
            throw new InvalidOperationException("Only pending messages can be marked sent.");
        }

        Status = NotificationMessageStatus.Sent;
        CompletedAtUtc = now;
    }

    public void MarkFailed(DateTimeOffset now)
    {
        if (Status is not NotificationMessageStatus.Pending)
        {
            throw new InvalidOperationException("Only pending messages can be marked failed.");
        }

        Status = NotificationMessageStatus.Failed;
        CompletedAtUtc = now;
    }

    private void SetKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var normalized = kind.Trim().ToLowerInvariant();
        if (!NotificationKinds.IsKnown(normalized))
        {
            throw new InvalidOperationException($"Unknown notification kind '{kind}'.");
        }

        Kind = normalized;
    }

    private static string RequireText(string value, int maxLength, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd();
    }
}

public sealed class NotificationDelivery : AuditableEntity, IOrganizationScoped
{
    private NotificationDelivery() { }

    public NotificationDelivery(
        Guid organizationId,
        Guid messageId,
        string channel,
        string destination)
    {
        OrganizationId = organizationId;
        MessageId = messageId;
        Channel = RequireToken(channel, 32, "Channel");
        Destination = RequireToken(destination, 500, "Destination");
        Status = NotificationDeliveryStatus.Pending;
        Attempt = 1;
    }

    public Guid OrganizationId { get; private set; }
    public Guid MessageId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public int Attempt { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationDeliveryStatus.Sent;
        Error = null;
        CompletedAtUtc = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Status = NotificationDeliveryStatus.Failed;
        Error = string.IsNullOrWhiteSpace(error) ? "Delivery failed." : error.Trim();
        if (Error.Length > 1000)
        {
            Error = Error[..1000];
        }

        CompletedAtUtc = now;
    }

    private static string RequireToken(string value, int maxLength, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
