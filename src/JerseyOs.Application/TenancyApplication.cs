using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public interface IQuotaGuard
{
    Task EnsureProductCapacityAsync(Guid organizationId, CancellationToken cancellationToken);
    Task EnsureMemberCapacityAsync(Guid organizationId, CancellationToken cancellationToken);
    Task EnsureImportCapacityAsync(Guid organizationId, CancellationToken cancellationToken);
    Task EnsureAiCapacityAsync(Guid organizationId, CancellationToken cancellationToken);
}

public interface IOrganizationProvisioner
{
    Task<OrganizationSummaryResponse> ProvisionAsync(
        string name,
        string slug,
        string adminEmail,
        string adminPassword,
        string? defaultCurrency,
        bool grantPlatformAdmin,
        CancellationToken cancellationToken);
}

public interface IOrganizationIntegrationSettings
{
    ShopifyPublishSettings ResolveShopify(Guid organizationId);
    WooPublishSettings ResolveWoo(Guid organizationId);
    Guid? FindOrganizationByShopDomain(string? shopDomain);
}

public sealed record ShopifyPublishSettings(
    string ShopDomain,
    string AccessToken,
    string ApiVersion,
    string WebhookSecret);

public sealed record WooPublishSettings(
    string StoreBaseUrl,
    string ConsumerKey,
    string ConsumerSecret,
    string ApiVersion);

public sealed record ListOrganizationsQuery : IRequest<IReadOnlyCollection<OrganizationSummaryResponse>>;
public sealed record GetOrganizationQuotaQuery(Guid OrganizationId) : IRequest<OrganizationQuotaResponse?>;
public sealed record UpdateOrganizationQuotaCommand(
    Guid OrganizationId,
    int MaxProducts,
    int MaxMembers,
    int MaxImportBatchesPerDay,
    int MaxAiGenerationsPerDay) : IRequest<OrganizationQuotaResponse?>;
public sealed record ProvisionOrganizationCommand(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    string? DefaultCurrency) : IRequest<OrganizationSummaryResponse>;

public sealed record ListOrganizationInvitationsQuery : IRequest<IReadOnlyCollection<OrganizationInvitationResponse>>;
public sealed record CreateOrganizationInvitationCommand(string Email, string Role)
    : IRequest<CreatedOrganizationInvitationResponse>;
public sealed record RevokeOrganizationInvitationCommand(Guid InvitationId)
    : IRequest<OrganizationInvitationResponse?>;
public sealed record AcceptOrganizationInvitationCommand(string Token, string? Password)
    : IRequest<IssuedAuth?>;

public sealed record GetOrganizationIntegrationSettingsQuery
    : IRequest<OrganizationIntegrationSettingsResponse>;
public sealed record UpdateOrganizationIntegrationSettingsCommand(
    string? ShopifyShopDomain,
    string? ShopifyAccessToken,
    string? ShopifyWebhookSecret,
    string? ShopifyApiVersion,
    string? WooStoreBaseUrl,
    string? WooConsumerKey,
    string? WooConsumerSecret,
    string? WooApiVersion) : IRequest<OrganizationIntegrationSettingsResponse>;

public sealed class ProvisionOrganizationValidator : AbstractValidator<ProvisionOrganizationCommand>
{
    public ProvisionOrganizationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(12).MaximumLength(256);
    }
}

public sealed class UpdateOrganizationQuotaValidator : AbstractValidator<UpdateOrganizationQuotaCommand>
{
    public UpdateOrganizationQuotaValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MaxProducts).GreaterThan(0);
        RuleFor(x => x.MaxMembers).GreaterThan(0);
        RuleFor(x => x.MaxImportBatchesPerDay).GreaterThan(0);
        RuleFor(x => x.MaxAiGenerationsPerDay).GreaterThan(0);
    }
}

public sealed class CreateOrganizationInvitationValidator : AbstractValidator<CreateOrganizationInvitationCommand>
{
    public CreateOrganizationInvitationValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).NotEmpty().Must(OrganizationRoles.IsKnown);
    }
}

public sealed class AcceptOrganizationInvitationValidator : AbstractValidator<AcceptOrganizationInvitationCommand>
{
    public AcceptOrganizationInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).MaximumLength(256);
    }
}

public sealed class ListOrganizationsHandler(IApplicationDbContext db)
    : IRequestHandler<ListOrganizationsQuery, IReadOnlyCollection<OrganizationSummaryResponse>>
{
    public async Task<IReadOnlyCollection<OrganizationSummaryResponse>> Handle(
        ListOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var orgs = await db.Organizations.AsNoTracking().IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var ids = orgs.Select(x => x.Id).ToArray();
        var productCounts = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(x => ids.Contains(x.OrganizationId))
            .GroupBy(x => x.OrganizationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var memberCounts = await db.OrganizationMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => ids.Contains(m.OrganizationId) && !m.IsDeleted)
            .GroupBy(m => m.OrganizationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var products = productCounts.ToDictionary(x => x.Key, x => x.Count);
        var members = memberCounts.ToDictionary(x => x.Key, x => x.Count);
        return orgs.Select(o => new OrganizationSummaryResponse(
                o.Id,
                o.Name,
                o.Slug,
                o.DefaultCurrency,
                products.GetValueOrDefault(o.Id),
                members.GetValueOrDefault(o.Id),
                o.CreatedAtUtc))
            .ToArray();
    }
}

public sealed class ProvisionOrganizationHandler(IOrganizationProvisioner provisioner)
    : IRequestHandler<ProvisionOrganizationCommand, OrganizationSummaryResponse>
{
    public Task<OrganizationSummaryResponse> Handle(
        ProvisionOrganizationCommand request, CancellationToken cancellationToken) =>
        provisioner.ProvisionAsync(
            request.Name,
            request.Slug,
            request.AdminEmail,
            request.AdminPassword,
            request.DefaultCurrency,
            grantPlatformAdmin: false,
            cancellationToken);
}

public sealed class GetOrganizationQuotaHandler(IApplicationDbContext db)
    : IRequestHandler<GetOrganizationQuotaQuery, OrganizationQuotaResponse?>
{
    public async Task<OrganizationQuotaResponse?> Handle(
        GetOrganizationQuotaQuery request, CancellationToken cancellationToken)
    {
        var quota = await db.OrganizationQuotas.AsNoTracking().IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (quota is null)
        {
            return null;
        }

        return await TenancyMapping.ToQuotaAsync(db, quota, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateOrganizationQuotaHandler(IApplicationDbContext db, IAuditRecorder audit)
    : IRequestHandler<UpdateOrganizationQuotaCommand, OrganizationQuotaResponse?>
{
    public async Task<OrganizationQuotaResponse?> Handle(
        UpdateOrganizationQuotaCommand request, CancellationToken cancellationToken)
    {
        var quota = await db.OrganizationQuotas.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (quota is null)
        {
            return null;
        }

        quota.SetLimits(
            request.MaxProducts,
            request.MaxMembers,
            request.MaxImportBatchesPerDay,
            request.MaxAiGenerationsPerDay);
        audit.Record(
            request.OrganizationId,
            AuditActions.OrganizationQuotaUpdated,
            nameof(OrganizationQuota),
            quota.Id.ToString("N"),
            new
            {
                request.MaxProducts,
                request.MaxMembers,
                request.MaxImportBatchesPerDay,
                request.MaxAiGenerationsPerDay
            });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await TenancyMapping.ToQuotaAsync(db, quota, cancellationToken).ConfigureAwait(false);
    }
}

public static class TenancyMapping
{
    public static async Task<OrganizationQuotaResponse> ToQuotaAsync(
        IApplicationDbContext db, OrganizationQuota quota, CancellationToken cancellationToken)
    {
        var dayStart = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var dayStartOffset = new DateTimeOffset(dayStart, TimeSpan.Zero);
        var productCount = await db.Products.IgnoreQueryFilters()
            .CountAsync(x => x.OrganizationId == quota.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        var memberCount = await db.OrganizationMemberships.IgnoreQueryFilters()
            .CountAsync(
                m => m.OrganizationId == quota.OrganizationId && !m.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        var importToday = await db.ImportBatches.IgnoreQueryFilters()
            .CountAsync(
                x => x.OrganizationId == quota.OrganizationId && x.CreatedAtUtc >= dayStartOffset,
                cancellationToken)
            .ConfigureAwait(false);
        var aiToday = await db.AiGenerations.IgnoreQueryFilters()
            .CountAsync(
                x => x.OrganizationId == quota.OrganizationId && x.CreatedAtUtc >= dayStartOffset,
                cancellationToken)
            .ConfigureAwait(false);
        return new OrganizationQuotaResponse(
            quota.OrganizationId,
            quota.MaxProducts,
            quota.MaxMembers,
            quota.MaxImportBatchesPerDay,
            quota.MaxAiGenerationsPerDay,
            productCount,
            memberCount,
            importToday,
            aiToday);
    }
}
