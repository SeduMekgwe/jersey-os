using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public static class PricingMapping
{
    public static PricingRuleResponse ToResponse(PricingRule rule) =>
        new(
            rule.Id,
            rule.Name,
            rule.Kind.ToString(),
            rule.PercentRate,
            rule.SalesChannelId,
            rule.Priority,
            rule.IsEnabled);

    public static PricePreviewResponse ToPreview(PricingCalculator.ResolvedPrices resolved) =>
        new(resolved.PriceAmount, resolved.CompareAtAmount, resolved.SellRuleId, resolved.CompareAtRuleId);
}

public sealed record ListPricingRulesQuery : IRequest<IReadOnlyCollection<PricingRuleResponse>>;

public sealed record CreatePricingRuleCommand(
    string Name,
    string Kind,
    decimal PercentRate,
    int Priority,
    Guid? SalesChannelId,
    bool IsEnabled) : IRequest<PricingRuleResponse>;

public sealed record UpdatePricingRuleCommand(
    Guid Id,
    string Name,
    string Kind,
    decimal PercentRate,
    int Priority,
    Guid? SalesChannelId,
    bool IsEnabled) : IRequest<PricingRuleResponse?>;

public sealed record DeletePricingRuleCommand(Guid Id) : IRequest<bool>;

public sealed record PreviewPricingCommand(
    decimal? CostAmount,
    decimal? ExplicitPriceAmount,
    decimal? ExplicitCompareAtAmount,
    Guid? SalesChannelId) : IRequest<PricePreviewResponse>;

public sealed class CreatePricingRuleValidator : AbstractValidator<CreatePricingRuleCommand>
{
    public CreatePricingRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).NotEmpty().Must(BeKnownKind);
        RuleFor(x => x.PercentRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PercentRate).LessThan(100)
            .When(x => string.Equals(x.Kind, nameof(PricingRuleKind.MarginPercent), StringComparison.OrdinalIgnoreCase));
    }

    private static bool BeKnownKind(string kind) =>
        Enum.TryParse<PricingRuleKind>(kind, ignoreCase: true, out _);
}

public sealed class UpdatePricingRuleValidator : AbstractValidator<UpdatePricingRuleCommand>
{
    public UpdatePricingRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Kind).NotEmpty().Must(BeKnownKind);
        RuleFor(x => x.PercentRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PercentRate).LessThan(100)
            .When(x => string.Equals(x.Kind, nameof(PricingRuleKind.MarginPercent), StringComparison.OrdinalIgnoreCase));
    }

    private static bool BeKnownKind(string kind) =>
        Enum.TryParse<PricingRuleKind>(kind, ignoreCase: true, out _);
}

public sealed class PreviewPricingValidator : AbstractValidator<PreviewPricingCommand>
{
    public PreviewPricingValidator()
    {
        RuleFor(x => x.CostAmount).GreaterThanOrEqualTo(0).When(x => x.CostAmount is not null);
        RuleFor(x => x.ExplicitPriceAmount).GreaterThanOrEqualTo(0).When(x => x.ExplicitPriceAmount is not null);
        RuleFor(x => x.ExplicitCompareAtAmount).GreaterThanOrEqualTo(0)
            .When(x => x.ExplicitCompareAtAmount is not null);
    }
}

public sealed class ListPricingRulesHandler(IApplicationDbContext db)
    : IRequestHandler<ListPricingRulesQuery, IReadOnlyCollection<PricingRuleResponse>>
{
    public async Task<IReadOnlyCollection<PricingRuleResponse>> Handle(
        ListPricingRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await db.PricingRules.AsNoTracking()
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rules.Select(PricingMapping.ToResponse).ToArray();
    }
}

public sealed class CreatePricingRuleHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IAuditRecorder audit) : IRequestHandler<CreatePricingRuleCommand, PricingRuleResponse>
{
    public async Task<PricingRuleResponse> Handle(CreatePricingRuleCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        var kind = Enum.Parse<PricingRuleKind>(request.Kind, ignoreCase: true);
        await EnsureChannelAsync(db, request.SalesChannelId, cancellationToken).ConfigureAwait(false);

        var rule = new PricingRule(
            orgId,
            request.Name,
            kind,
            request.PercentRate,
            request.Priority,
            request.SalesChannelId,
            request.IsEnabled);
        db.Add(rule);
        audit.Record(orgId, AuditActions.PricingRuleCreated, nameof(PricingRule), rule.Id.ToString("N"), new { rule.Name, kind = request.Kind });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PricingMapping.ToResponse(rule);
    }

    private static async Task EnsureChannelAsync(
        IApplicationDbContext db, Guid? salesChannelId, CancellationToken cancellationToken)
    {
        if (salesChannelId is null)
        {
            return;
        }

        var exists = await db.SalesChannels.AnyAsync(x => x.Id == salesChannelId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            throw new InvalidOperationException("Sales channel was not found.");
        }
    }
}

public sealed class UpdatePricingRuleHandler(IApplicationDbContext db)
    : IRequestHandler<UpdatePricingRuleCommand, PricingRuleResponse?>
{
    public async Task<PricingRuleResponse?> Handle(
        UpdatePricingRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
        {
            return null;
        }

        if (request.SalesChannelId is { } channelId)
        {
            var exists = await db.SalesChannels.AnyAsync(x => x.Id == channelId, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
            {
                throw new InvalidOperationException("Sales channel was not found.");
            }
        }

        var kind = Enum.Parse<PricingRuleKind>(request.Kind, ignoreCase: true);
        rule.Update(
            request.Name,
            kind,
            request.PercentRate,
            request.Priority,
            request.SalesChannelId,
            request.IsEnabled);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PricingMapping.ToResponse(rule);
    }
}

public sealed class DeletePricingRuleHandler(IApplicationDbContext db, IAuditRecorder audit)
    : IRequestHandler<DeletePricingRuleCommand, bool>
{
    public async Task<bool> Handle(DeletePricingRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
        {
            return false;
        }

        audit.Record(rule.OrganizationId, AuditActions.PricingRuleDeleted, nameof(PricingRule), rule.Id.ToString("N"), new { rule.Name });
        db.Remove(rule);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed class PreviewPricingHandler(IApplicationDbContext db)
    : IRequestHandler<PreviewPricingCommand, PricePreviewResponse>
{
    public async Task<PricePreviewResponse> Handle(
        PreviewPricingCommand request, CancellationToken cancellationToken)
    {
        var rules = await db.PricingRules.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var resolved = request.SalesChannelId is { } channelId
            ? PricingCalculator.ResolveForPublish(
                request.CostAmount,
                request.ExplicitPriceAmount,
                request.ExplicitCompareAtAmount,
                rules,
                channelId)
            : PricingCalculator.ResolveForCatalog(
                request.CostAmount,
                request.ExplicitPriceAmount,
                request.ExplicitCompareAtAmount,
                rules);
        return PricingMapping.ToPreview(resolved);
    }
}
