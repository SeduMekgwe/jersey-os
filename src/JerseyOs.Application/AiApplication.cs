using System.Text.Json;
using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public sealed record AiGenerateRequest(
    string Kind,
    string SystemPrompt,
    string UserPrompt);

public sealed record AiGenerateResult(
    string OutputText,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd);

public interface IAiContentGenerator
{
    Task<AiGenerateResult> GenerateAsync(AiGenerateRequest request, CancellationToken cancellationToken);
}

public interface IAiJobScheduler
{
    void EnqueueGenerate(Guid generationId);
}

public static class AiMapping
{
    public static AiPromptTemplateResponse ToTemplate(AiPromptTemplate template) =>
        new(
            template.Id,
            template.Name,
            template.Kind,
            template.SystemPrompt,
            template.UserPromptTemplate,
            template.IsEnabled);

    public static AiGenerationResponse ToGeneration(AiGeneration generation) =>
        new(
            generation.Id,
            generation.Kind,
            generation.TargetType,
            generation.TargetId,
            generation.Status.ToString(),
            generation.OutputText,
            generation.Model,
            generation.PromptTokens,
            generation.CompletionTokens,
            generation.EstimatedCostUsd,
            generation.Error,
            generation.CorrelationId,
            generation.CreatedAtUtc,
            generation.CompletedAtUtc,
            generation.AppliedAtUtc);
}

public sealed record ListAiPromptTemplatesQuery : IRequest<IReadOnlyCollection<AiPromptTemplateResponse>>;
public sealed record ListAiGenerationsQuery(string? TargetType, Guid? TargetId)
    : IRequest<IReadOnlyCollection<AiGenerationResponse>>;
public sealed record EnqueueAiGenerationCommand(
    string Kind,
    string TargetType,
    Guid TargetId,
    Guid? PromptTemplateId) : IRequest<AiGenerationResponse>;
public sealed record ApproveAiGenerationCommand(Guid Id) : IRequest<AiGenerationResponse?>;
public sealed record RejectAiGenerationCommand(Guid Id, string? Note) : IRequest<AiGenerationResponse?>;

public sealed class EnqueueAiGenerationValidator : AbstractValidator<EnqueueAiGenerationCommand>
{
    public EnqueueAiGenerationValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().Must(AiContentKinds.IsKnown);
        RuleFor(x => x.TargetType).NotEmpty().Must(AiTargetTypes.IsKnown);
        RuleFor(x => x.TargetId).NotEmpty();
    }
}

public sealed class ListAiPromptTemplatesHandler(IApplicationDbContext db)
    : IRequestHandler<ListAiPromptTemplatesQuery, IReadOnlyCollection<AiPromptTemplateResponse>>
{
    public async Task<IReadOnlyCollection<AiPromptTemplateResponse>> Handle(
        ListAiPromptTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await db.AiPromptTemplates.AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return templates.Select(AiMapping.ToTemplate).ToArray();
    }
}

public sealed class ListAiGenerationsHandler(IApplicationDbContext db)
    : IRequestHandler<ListAiGenerationsQuery, IReadOnlyCollection<AiGenerationResponse>>
{
    public async Task<IReadOnlyCollection<AiGenerationResponse>> Handle(
        ListAiGenerationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AiGenerations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.TargetType))
        {
            var type = request.TargetType.Trim().ToLowerInvariant();
            query = query.Where(x => x.TargetType == type);
        }

        if (request.TargetId is { } targetId)
        {
            query = query.Where(x => x.TargetId == targetId);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(AiMapping.ToGeneration).ToArray();
    }
}

public sealed class EnqueueAiGenerationHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IAiJobScheduler jobs,
    IQuotaGuard quotas) : IRequestHandler<EnqueueAiGenerationCommand, AiGenerationResponse>
{
    public async Task<AiGenerationResponse> Handle(
        EnqueueAiGenerationCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        await quotas.EnsureAiCapacityAsync(orgId, cancellationToken).ConfigureAwait(false);
        var kind = request.Kind.Trim().ToLowerInvariant();
        var targetType = request.TargetType.Trim().ToLowerInvariant();
        if (string.Equals(kind, AiContentKinds.AltText, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetType, AiTargetTypes.ProductImage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Alt text generation requires a product-image target.");
        }

        Guid? templateId = request.PromptTemplateId;
        if (templateId is null)
        {
            templateId = await db.AiPromptTemplates
                .Where(x => x.Kind == kind && x.IsEnabled)
                .OrderBy(x => x.Name)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var input = await AiInputFactory.BuildAsync(db, kind, targetType, request.TargetId, cancellationToken)
            .ConfigureAwait(false);
        var generation = new AiGeneration(
            orgId,
            kind,
            targetType,
            request.TargetId,
            input,
            templateId,
            current.CorrelationId);
        db.Add(generation);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobs.EnqueueGenerate(generation.Id);
        return AiMapping.ToGeneration(generation);
    }
}

public sealed class ApproveAiGenerationHandler(
    IApplicationDbContext db,
    TimeProvider time,
    IAuditRecorder audit) : IRequestHandler<ApproveAiGenerationCommand, AiGenerationResponse?>
{
    public async Task<AiGenerationResponse?> Handle(
        ApproveAiGenerationCommand request, CancellationToken cancellationToken)
    {
        var generation = await db.AiGenerations
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (generation is null)
        {
            return null;
        }

        generation.Approve();
        await AiContentApplier.ApplyAsync(db, generation, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        generation.MarkApplied(time.GetUtcNow());
        audit.Record(
            generation.OrganizationId,
            AuditActions.AiGenerationApproved,
            nameof(AiGeneration),
            generation.Id.ToString("N"),
            new { generation.Kind, generation.TargetType, generation.TargetId });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AiMapping.ToGeneration(generation);
    }
}

public sealed class RejectAiGenerationHandler(IApplicationDbContext db)
    : IRequestHandler<RejectAiGenerationCommand, AiGenerationResponse?>
{
    public async Task<AiGenerationResponse?> Handle(
        RejectAiGenerationCommand request, CancellationToken cancellationToken)
    {
        var generation = await db.AiGenerations
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (generation is null)
        {
            return null;
        }

        generation.Reject(request.Note);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AiMapping.ToGeneration(generation);
    }
}

public static class AiInputFactory
{
    public static async Task<string> BuildAsync(
        IApplicationDbContext db,
        string kind,
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(targetType, AiTargetTypes.Product, StringComparison.OrdinalIgnoreCase))
        {
            var product = await db.Products.AsNoTracking()
                .Include(x => x.Team)
                .Include(x => x.Season)
                .SingleOrDefaultAsync(x => x.Id == targetId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Product was not found.");
            return JsonSerializer.Serialize(new
            {
                kind,
                name = product.Name,
                slug = product.Slug,
                styleCode = product.StyleCode,
                team = product.Team?.Name,
                season = product.Season?.Name,
                description = product.Description,
                seoTitle = product.SeoTitle,
                seoDescription = product.SeoDescription
            });
        }

        if (string.Equals(targetType, AiTargetTypes.ImportItem, StringComparison.OrdinalIgnoreCase))
        {
            var item = await db.ImportItems.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == targetId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Import item was not found.");
            return JsonSerializer.Serialize(new
            {
                kind,
                name = item.Name,
                slug = item.Slug,
                sku = item.Sku,
                size = item.Size,
                styleCode = item.StyleCode,
                team = item.TeamName,
                season = item.SeasonName
            });
        }

        var image = await db.Products.AsNoTracking()
            .SelectMany(p => p.Images.Select(i => new { Product = p, Image = i }))
            .SingleOrDefaultAsync(x => x.Image.Id == targetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product image was not found.");
        return JsonSerializer.Serialize(new
        {
            kind,
            name = image.Product.Name,
            altText = image.Image.AltText,
            contentType = image.Image.ContentType
        });
    }
}

public static class AiContentApplier
{
    public static async Task ApplyAsync(
        IApplicationDbContext db,
        AiGeneration generation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var output = generation.OutputText
            ?? throw new InvalidOperationException("Generation has no output to apply.");

        if (string.Equals(generation.TargetType, AiTargetTypes.ImportItem, StringComparison.OrdinalIgnoreCase))
        {
            var item = await db.ImportItems
                .SingleAsync(x => x.Id == generation.TargetId, cancellationToken)
                .ConfigureAwait(false);
            if (item.Status == ImportItemStatus.Applied && item.AppliedProductId is { } productId)
            {
                await ApplyToProductAsync(db, productId, generation.Kind, output, now, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            item.ApplyGeneratedCopy(generation.Kind, output);
            return;
        }

        if (string.Equals(generation.TargetType, AiTargetTypes.Product, StringComparison.OrdinalIgnoreCase))
        {
            await ApplyToProductAsync(db, generation.TargetId, generation.Kind, output, now, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var product = await db.Products
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Images.Any(i => i.Id == generation.TargetId), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product image was not found.");
        if (!string.Equals(generation.Kind, AiContentKinds.AltText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Product images only accept alt-text generations.");
        }

        product.SetImageAltText(generation.TargetId, output, now);
    }

    private static async Task ApplyToProductAsync(
        IApplicationDbContext db,
        Guid productId,
        string kind,
        string output,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var product = await db.Products
            .Include(x => x.Images)
            .SingleAsync(x => x.Id == productId, cancellationToken)
            .ConfigureAwait(false);
        if (string.Equals(kind, AiContentKinds.Title, StringComparison.OrdinalIgnoreCase))
        {
            product.UpdateDetails(output, product.Slug, product.StyleCode, product.TeamId, product.SeasonId, now);
            return;
        }

        if (string.Equals(kind, AiContentKinds.Description, StringComparison.OrdinalIgnoreCase))
        {
            product.SetDescription(output, now);
            return;
        }

        if (string.Equals(kind, AiContentKinds.SeoTitle, StringComparison.OrdinalIgnoreCase))
        {
            product.SetSeo(output, product.SeoDescription, product.SeoHandle, now);
            return;
        }

        if (string.Equals(kind, AiContentKinds.SeoDescription, StringComparison.OrdinalIgnoreCase))
        {
            product.SetSeo(product.SeoTitle, output, product.SeoHandle, now);
            return;
        }

        throw new InvalidOperationException($"Cannot apply AI kind '{kind}' to a product.");
    }
}
