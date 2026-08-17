using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class AiOptions
{
    public const string Section = "Ai";
    public string Provider { get; set; } = "Fixture";
    public AiOpenAiOptions OpenAI { get; set; } = new();
}

public sealed class AiOpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public decimal InputUsdPerMillionTokens { get; set; } = 0.15m;
    public decimal OutputUsdPerMillionTokens { get; set; } = 0.60m;
}

public static class AiModelBuilderExtensions
{
    public static void ConfigureAi(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<AiPromptTemplate>(b =>
        {
            b.ToTable("ai_prompt_templates");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            b.Property(x => x.SystemPrompt).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.UserPromptTemplate).HasColumnType("nvarchar(max)").IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Kind, x.Name }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<AiGeneration>(b =>
        {
            b.ToTable("ai_generations");
            b.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            b.Property(x => x.TargetType).HasMaxLength(32).IsRequired();
            b.Property(x => x.InputJson).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.OutputText).HasColumnType("nvarchar(max)");
            b.Property(x => x.Model).HasMaxLength(100);
            b.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
            b.Property(x => x.Error).HasMaxLength(1000);
            b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(x => new { x.OrganizationId, x.TargetType, x.TargetId, x.CreatedAtUtc });
            b.HasOne<AiPromptTemplate>().WithMany().HasForeignKey(x => x.PromptTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public sealed class HangfireAiJobScheduler(IBackgroundJobClient jobs) : IAiJobScheduler
{
    public void EnqueueGenerate(Guid generationId) =>
        jobs.Enqueue<GenerateAiContentJob>(job => job.ExecuteAsync(generationId, CancellationToken.None));
}

public sealed class FixtureAiContentGenerator : IAiContentGenerator
{
    public Task<AiGenerateResult> GenerateAsync(AiGenerateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var name = ExtractName(request.UserPrompt);
        var kind = request.Kind.Trim().ToLowerInvariant();
        var output = kind switch
        {
            AiContentKinds.Title => $"Fixture title for {name}",
            AiContentKinds.Description => $"Fixture description for {name}. Ready for operator review.",
            AiContentKinds.SeoTitle => $"Buy {name} | Jersey OS",
            AiContentKinds.SeoDescription => $"Shop {name}. Fixture SEO copy for local and CI.",
            AiContentKinds.AltText => $"Product photo of {name}",
            _ => $"Fixture copy for {name}"
        };
        return Task.FromResult(new AiGenerateResult(output, "fixture", 12, 16, 0m));
    }

    private static string ExtractName(string userPrompt)
    {
        const string prefix = "Product: ";
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return "product";
        }

        var start = userPrompt.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "product";
        }

        var rest = userPrompt[(start + prefix.Length)..];
        var end = rest.IndexOf('.', StringComparison.Ordinal);
        var name = (end < 0 ? rest : rest[..end]).Trim();
        return string.IsNullOrWhiteSpace(name) ? "product" : name;
    }
}

public sealed class OpenAiContentGenerator(IHttpClientFactory httpFactory, IOptions<AiOptions> options)
    : IAiContentGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiGenerateResult> GenerateAsync(AiGenerateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var openAi = options.Value.OpenAI;
        if (string.IsNullOrWhiteSpace(openAi.ApiKey))
        {
            throw new InvalidOperationException("Ai:OpenAI:ApiKey is required when Ai:Provider=OpenAI.");
        }

        var client = httpFactory.CreateClient("openai");
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        message.Content = JsonContent.Create(
            new
            {
                model = string.IsNullOrWhiteSpace(openAi.Model) ? "gpt-4o-mini" : openAi.Model.Trim(),
                messages = new[]
                {
                    new { role = "system", content = request.SystemPrompt },
                    new { role = "user", content = request.UserPrompt }
                }
            });

        using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI request failed ({(int)response.StatusCode}).");
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        var text = payload?.Choices is { Count: > 0 } choices ? choices[0].Message?.Content : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("OpenAI returned empty content.");
        }

        var promptTokens = payload?.Usage?.PromptTokens ?? 0;
        var completionTokens = payload?.Usage?.CompletionTokens ?? 0;
        var cost = (promptTokens / 1_000_000m * openAi.InputUsdPerMillionTokens)
            + (completionTokens / 1_000_000m * openAi.OutputUsdPerMillionTokens);
        return new AiGenerateResult(
            text.Trim(),
            payload?.Model ?? openAi.Model,
            promptTokens,
            completionTokens,
            decimal.Round(cost, 6, MidpointRounding.AwayFromZero));
    }

    private sealed record OpenAiChatResponse(
        string? Model,
        OpenAiUsage? Usage,
        IReadOnlyList<OpenAiChoice>? Choices);

    private sealed record OpenAiUsage(int PromptTokens, int CompletionTokens);

    private sealed record OpenAiChoice(OpenAiMessage? Message);

    private sealed record OpenAiMessage(string? Content);
}

public sealed class GenerateAiContentJob(
    JerseyOsDbContext db,
    IAiContentGenerator generator,
    IOpsStatusPublisher ops,
    TimeProvider time)
{
    [Queue("ai")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var generation = await db.AiGenerationsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == generationId, cancellationToken)
            .ConfigureAwait(false);
        if (generation is null || generation.Status != AiGenerationStatus.Pending)
        {
            return;
        }

        try
        {
            string systemPrompt;
            string userTemplate;
            if (generation.PromptTemplateId is { } templateId)
            {
                var template = await db.AiPromptTemplatesSet.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(x => x.Id == templateId, cancellationToken)
                    .ConfigureAwait(false);
                if (template is null)
                {
                    (systemPrompt, userTemplate) = AiDefaultPromptTemplates.ForKind(generation.Kind);
                }
                else
                {
                    systemPrompt = template.SystemPrompt;
                    userTemplate = template.UserPromptTemplate;
                }
            }
            else
            {
                (systemPrompt, userTemplate) = AiDefaultPromptTemplates.ForKind(generation.Kind);
            }

            var values = ParseValues(generation.InputJson);
            var rendered = AiPromptTemplate.Render(userTemplate, values);
            var result = await generator.GenerateAsync(
                    new AiGenerateRequest(generation.Kind, systemPrompt, rendered),
                    cancellationToken)
                .ConfigureAwait(false);
            generation.MarkSucceeded(
                AiOutputLimits.Bound(generation.Kind, result.OutputText),
                result.Model,
                result.PromptTokens,
                result.CompletionTokens,
                result.EstimatedCostUsd,
                time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await ops.PublishAsync(
                    new OpsStatusEvent(
                        generation.OrganizationId,
                        OpsStatusKinds.AiGeneration,
                        generation.Id.ToString("N"),
                        generation.Status.ToString(),
                        DateTimeOffset.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            generation.MarkFailed(exception.Message, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await ops.PublishAsync(
                    new OpsStatusEvent(
                        generation.OrganizationId,
                        OpsStatusKinds.AiGeneration,
                        generation.Id.ToString("N"),
                        generation.Status.ToString(),
                        DateTimeOffset.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static Dictionary<string, string?> ParseValues(string json)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Null => null,
                _ => property.Value.ToString()
            };
        }

        return values;
    }
}

public static class AiInfrastructureExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.Section));
        services.AddHttpClient("openai", (sp, client) =>
        {
            var openAi = sp.GetRequiredService<IOptions<AiOptions>>().Value.OpenAI;
            var baseUrl = string.IsNullOrWhiteSpace(openAi.BaseUrl)
                ? "https://api.openai.com/v1/"
                : openAi.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddSingleton<IAiContentGenerator>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<AiOptions>>().Value.Provider;
            return string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<OpenAiContentGenerator>(sp)
                : new FixtureAiContentGenerator();
        });
        services.AddScoped<IAiJobScheduler, HangfireAiJobScheduler>();
        services.AddScoped<GenerateAiContentJob>();
        return services;
    }
}
