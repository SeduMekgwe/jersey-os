using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public static class AiContentKinds
{
    public const string Title = "title";
    public const string Description = "description";
    public const string SeoTitle = "seo-title";
    public const string SeoDescription = "seo-description";
    public const string AltText = "alt-text";

    public static readonly string[] All =
    [
        Title,
        Description,
        SeoTitle,
        SeoDescription,
        AltText
    ];

    public static bool IsKnown(string kind) =>
        All.Contains(kind, StringComparer.OrdinalIgnoreCase);
}

public static class AiOutputLimits
{
    public static int MaxLength(string kind) =>
        kind.Trim().ToLowerInvariant() switch
        {
            AiContentKinds.Title => 200,
            AiContentKinds.Description => 4000,
            AiContentKinds.SeoTitle => 200,
            AiContentKinds.SeoDescription => 320,
            AiContentKinds.AltText => 300,
            _ => 4000
        };

    public static string Bound(string kind, string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        var text = output.Trim();
        var max = MaxLength(kind);
        return text.Length <= max ? text : text[..max].TrimEnd();
    }
}

public static class AiDefaultPromptTemplates
{
    public const string SharedUserPrompt =
        "Product: {{name}}. Team: {{team}}. Season: {{season}}. Style: {{styleCode}}. SKU: {{sku}}. Existing description: {{description}}.";

    public const string AltUserPrompt =
        "Product: {{name}}. Existing alt text: {{altText}}. Content type: {{contentType}}.";

    public static readonly (string Kind, string Name, string SystemPrompt, string UserPromptTemplate)[] All =
    [
        (AiContentKinds.Title, "Default title",
            "You write concise storefront product titles for football jerseys. Return only the title.",
            SharedUserPrompt),
        (AiContentKinds.Description, "Default description",
            "You write a 2-sentence product description for football jerseys. Return only the description.",
            SharedUserPrompt),
        (AiContentKinds.SeoTitle, "Default SEO title",
            "You write an SEO title under 60 characters for a jersey product. Return only the title.",
            SharedUserPrompt),
        (AiContentKinds.SeoDescription, "Default SEO description",
            "You write a meta description under 155 characters for a jersey product. Return only the description.",
            SharedUserPrompt),
        (AiContentKinds.AltText, "Default alt text",
            "You write accessible alt text for a product photo. Return only the alt text.",
            AltUserPrompt)
    ];

    public static (string SystemPrompt, string UserPromptTemplate) ForKind(string kind)
    {
        foreach (var match in All)
        {
            if (string.Equals(match.Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                return (match.SystemPrompt, match.UserPromptTemplate);
            }
        }

        return ("You write catalog copy. Return only the requested text.", SharedUserPrompt);
    }
}

public static class AiTargetTypes
{
    public const string Product = "product";
    public const string ImportItem = "import-item";
    public const string ProductImage = "product-image";

    public static bool IsKnown(string targetType) =>
        string.Equals(targetType, Product, StringComparison.OrdinalIgnoreCase)
        || string.Equals(targetType, ImportItem, StringComparison.OrdinalIgnoreCase)
        || string.Equals(targetType, ProductImage, StringComparison.OrdinalIgnoreCase);
}

public enum AiGenerationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Approved = 3,
    Rejected = 4,
    Applied = 5
}

public sealed class AiPromptTemplate : AuditableEntity, IOrganizationScoped
{
    private AiPromptTemplate() { }

    public AiPromptTemplate(
        Guid organizationId,
        string name,
        string kind,
        string systemPrompt,
        string userPromptTemplate)
    {
        OrganizationId = organizationId;
        Rename(name);
        SetKind(kind);
        SetPrompts(systemPrompt, userPromptTemplate);
        IsEnabled = true;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public string SystemPrompt { get; private set; } = string.Empty;
    public string UserPromptTemplate { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        if (Name.Length > 200)
        {
            throw new InvalidOperationException("Prompt template name cannot exceed 200 characters.");
        }
    }

    public void SetKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var normalized = kind.Trim().ToLowerInvariant();
        if (!AiContentKinds.IsKnown(normalized))
        {
            throw new InvalidOperationException($"Unknown AI content kind '{kind}'.");
        }

        Kind = normalized;
    }

    public void SetPrompts(string systemPrompt, string userPromptTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userPromptTemplate);
        SystemPrompt = systemPrompt.Trim();
        UserPromptTemplate = userPromptTemplate.Trim();
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public string RenderUserPrompt(IReadOnlyDictionary<string, string?> values) =>
        Render(UserPromptTemplate, values);

    public static string Render(string userPromptTemplate, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userPromptTemplate);
        ArgumentNullException.ThrowIfNull(values);
        var rendered = userPromptTemplate;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace("{{" + key + "}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }
}

public sealed class AiGeneration : AuditableEntity, IOrganizationScoped
{
    private AiGeneration() { }

    public AiGeneration(
        Guid organizationId,
        string kind,
        string targetType,
        Guid targetId,
        string inputJson,
        Guid? promptTemplateId,
        string correlationId)
    {
        OrganizationId = organizationId;
        SetKind(kind);
        SetTarget(targetType, targetId);
        InputJson = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson.Trim();
        PromptTemplateId = promptTemplateId;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim();
        Status = AiGenerationStatus.Pending;
    }

    public Guid OrganizationId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public Guid? PromptTemplateId { get; private set; }
    public string InputJson { get; private set; } = "{}";
    public string? OutputText { get; private set; }
    public string? Model { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public string? Error { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public AiGenerationStatus Status { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? AppliedAtUtc { get; private set; }

    public void MarkSucceeded(
        string outputText,
        string model,
        int promptTokens,
        int completionTokens,
        decimal estimatedCostUsd,
        DateTimeOffset now)
    {
        if (Status is not AiGenerationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending generations can succeed.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outputText);
        OutputText = outputText.Trim();
        Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
        PromptTokens = Math.Max(0, promptTokens);
        CompletionTokens = Math.Max(0, completionTokens);
        EstimatedCostUsd = estimatedCostUsd < 0 ? 0 : estimatedCostUsd;
        Error = null;
        Status = AiGenerationStatus.Succeeded;
        CompletedAtUtc = now;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (Status is not AiGenerationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending generations can fail.");
        }

        Error = string.IsNullOrWhiteSpace(error) ? "Generation failed." : error.Trim();
        Status = AiGenerationStatus.Failed;
        CompletedAtUtc = now;
    }

    public void Approve()
    {
        if (Status is not AiGenerationStatus.Succeeded)
        {
            throw new InvalidOperationException("Only successful generations can be approved.");
        }

        Status = AiGenerationStatus.Approved;
    }

    public void Reject(string? note = null)
    {
        if (Status is not AiGenerationStatus.Succeeded and not AiGenerationStatus.Approved)
        {
            throw new InvalidOperationException("Only successful or approved generations can be rejected.");
        }

        Status = AiGenerationStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(note))
        {
            Error = note.Trim();
        }
    }

    public void MarkApplied(DateTimeOffset now)
    {
        if (Status is not AiGenerationStatus.Approved)
        {
            throw new InvalidOperationException("Generation must be approved before apply.");
        }

        if (string.IsNullOrWhiteSpace(OutputText))
        {
            throw new InvalidOperationException("Generation has no output to apply.");
        }

        Status = AiGenerationStatus.Applied;
        AppliedAtUtc = now;
    }

    private void SetKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var normalized = kind.Trim().ToLowerInvariant();
        if (!AiContentKinds.IsKnown(normalized))
        {
            throw new InvalidOperationException($"Unknown AI content kind '{kind}'.");
        }

        Kind = normalized;
    }

    private void SetTarget(string targetType, Guid targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        if (targetId == Guid.Empty)
        {
            throw new InvalidOperationException("Target id is required.");
        }

        var normalized = targetType.Trim().ToLowerInvariant();
        if (!AiTargetTypes.IsKnown(normalized))
        {
            throw new InvalidOperationException($"Unknown AI target type '{targetType}'.");
        }

        TargetType = normalized;
        TargetId = targetId;
    }
}
