namespace JerseyOs.Contracts;

public sealed record AiPromptTemplateResponse(
    Guid Id,
    string Name,
    string Kind,
    string SystemPrompt,
    string UserPromptTemplate,
    bool IsEnabled);

public sealed record AiGenerationResponse(
    Guid Id,
    string Kind,
    string TargetType,
    Guid TargetId,
    string Status,
    string? OutputText,
    string? Model,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    string? Error,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? AppliedAtUtc);

public sealed record CreateAiGenerationRequest(
    string Kind,
    string TargetType,
    Guid TargetId,
    Guid? PromptTemplateId = null);

public sealed record RejectAiGenerationRequest(string? Note);
