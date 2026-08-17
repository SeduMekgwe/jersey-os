using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class TenancyOptions
{
    public const string Section = "Tenancy";
    public int DefaultMaxProducts { get; set; } = 5000;
    public int DefaultMaxMembers { get; set; } = 25;
    public int DefaultMaxImportBatchesPerDay { get; set; } = 50;
    public int DefaultMaxAiGenerationsPerDay { get; set; } = 200;
}

public static class TenancyModelBuilder
{
    public static void ConfigureTenancy(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<OrganizationQuota>(b =>
        {
            b.ToTable("organization_quotas");
            b.HasIndex(x => x.OrganizationId).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<OrganizationSetting>(b =>
        {
            b.ToTable("organization_settings");
            b.Property(x => x.Key).HasMaxLength(128).IsRequired();
            b.Property(x => x.Value).HasMaxLength(4000);
            b.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<OrganizationInvitation>(b =>
        {
            b.ToTable("organization_invitations");
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.Role).HasMaxLength(32).IsRequired();
            b.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new { x.OrganizationId, x.Email });
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public static class OrganizationSeeder
{
    public static async Task EnsurePermissionsAsync(JerseyOsDbContext db, CancellationToken cancellationToken)
    {
        foreach (var key in Permissions.All)
        {
            if (!await db.PermissionsSet.AnyAsync(x => x.Key == key, cancellationToken).ConfigureAwait(false))
            {
                db.PermissionsSet.Add(new PermissionDefinition { Key = key, Description = key });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task SeedOrganizationAsync(
        JerseyOsDbContext db,
        Guid organizationId,
        bool grantPlatformAdmin,
        TenancyOptions tenancy,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionsAsync(db, cancellationToken).ConfigureAwait(false);
        var permissions = await db.PermissionsSet.ToListAsync(cancellationToken).ConfigureAwait(false);

        var adminRole = await EnsureRoleAsync(db, organizationId, OrganizationRoles.Admin, "ADMIN", cancellationToken)
            .ConfigureAwait(false);
        var memberRole = await EnsureRoleAsync(db, organizationId, OrganizationRoles.Member, "MEMBER", cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var adminKeys = grantPlatformAdmin ? Permissions.All : Permissions.TenantAdminKeys;
        await GrantAsync(db, organizationId, adminRole.Id, permissions.Where(p => adminKeys.Contains(p.Key)), cancellationToken)
            .ConfigureAwait(false);
        await GrantAsync(
                db,
                organizationId,
                memberRole.Id,
                permissions.Where(p => Permissions.MemberKeys.Contains(p.Key)),
                cancellationToken)
            .ConfigureAwait(false);

        if (!await db.SalesChannelsSet.IgnoreQueryFilters()
                .AnyAsync(
                    x => x.OrganizationId == organizationId && x.Code == SalesChannelCodes.Shopify,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            db.SalesChannelsSet.Add(new SalesChannel(organizationId, SalesChannelCodes.Shopify, "Shopify", enabled: true));
        }

        if (!await db.SalesChannelsSet.IgnoreQueryFilters()
                .AnyAsync(
                    x => x.OrganizationId == organizationId && x.Code == SalesChannelCodes.WooCommerce,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            db.SalesChannelsSet.Add(
                new SalesChannel(organizationId, SalesChannelCodes.WooCommerce, "WooCommerce", enabled: false));
        }

        foreach (var spec in AiDefaultPromptTemplates.All)
        {
            if (!await db.AiPromptTemplatesSet.IgnoreQueryFilters()
                    .AnyAsync(
                        x => x.OrganizationId == organizationId && x.Kind == spec.Kind,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                db.AiPromptTemplatesSet.Add(
                    new AiPromptTemplate(
                        organizationId,
                        spec.Name,
                        spec.Kind,
                        spec.SystemPrompt,
                        spec.UserPromptTemplate));
            }
        }

        foreach (var spec in NotificationTemplate.Defaults)
        {
            if (!await db.NotificationTemplatesSet.IgnoreQueryFilters()
                    .AnyAsync(
                        x => x.OrganizationId == organizationId && x.Kind == spec.Kind,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                db.NotificationTemplatesSet.Add(
                    new NotificationTemplate(
                        organizationId,
                        spec.Kind,
                        spec.Name,
                        spec.Subject,
                        spec.Body));
            }
        }

        if (!await db.OrganizationQuotasSet.IgnoreQueryFilters()
                .AnyAsync(x => x.OrganizationId == organizationId, cancellationToken)
                .ConfigureAwait(false))
        {
            db.OrganizationQuotasSet.Add(
                new OrganizationQuota(
                    organizationId,
                    tenancy.DefaultMaxProducts,
                    tenancy.DefaultMaxMembers,
                    tenancy.DefaultMaxImportBatchesPerDay,
                    tenancy.DefaultMaxAiGenerationsPerDay));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ApplicationRole> EnsureRoleAsync(
        JerseyOsDbContext db,
        Guid organizationId,
        string name,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var role = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.NormalizedName == normalizedName,
                cancellationToken)
            .ConfigureAwait(false);
        if (role is not null)
        {
            return role;
        }

        role = new ApplicationRole
        {
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName
        };
        db.Roles.Add(role);
        return role;
    }

    private static async Task GrantAsync(
        JerseyOsDbContext db,
        Guid organizationId,
        Guid roleId,
        IEnumerable<PermissionDefinition> permissions,
        CancellationToken cancellationToken)
    {
        foreach (var permission in permissions)
        {
            if (!await db.RolePermissions.IgnoreQueryFilters()
                    .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permission.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                db.RolePermissions.Add(
                    new RolePermissionGrant
                    {
                        OrganizationId = organizationId,
                        RoleId = roleId,
                        PermissionId = permission.Id
                    });
            }
        }
    }
}

public sealed class QuotaGuard(JerseyOsDbContext db, IOptions<TenancyOptions> options) : IQuotaGuard
{
    public Task EnsureProductCapacityAsync(Guid organizationId, CancellationToken cancellationToken) =>
        EnsureAsync(
            organizationId,
            q => q.MaxProducts,
            () => db.ProductsSet.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == organizationId, cancellationToken),
            "Product quota exceeded.",
            cancellationToken);

    public Task EnsureMemberCapacityAsync(Guid organizationId, CancellationToken cancellationToken) =>
        EnsureAsync(
            organizationId,
            q => q.MaxMembers,
            () => db.OrganizationMemberships.IgnoreQueryFilters()
                .CountAsync(x => x.OrganizationId == organizationId && !x.IsDeleted, cancellationToken),
            "Member quota exceeded.",
            cancellationToken);

    public Task EnsureImportCapacityAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var dayStart = StartOfUtcDay();
        return EnsureAsync(
            organizationId,
            q => q.MaxImportBatchesPerDay,
            () => db.ImportBatchesSet.IgnoreQueryFilters()
                .CountAsync(
                    x => x.OrganizationId == organizationId && x.CreatedAtUtc >= dayStart,
                    cancellationToken),
            "Daily import quota exceeded.",
            cancellationToken);
    }

    public Task EnsureAiCapacityAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var dayStart = StartOfUtcDay();
        return EnsureAsync(
            organizationId,
            q => q.MaxAiGenerationsPerDay,
            () => db.AiGenerationsSet.IgnoreQueryFilters()
                .CountAsync(
                    x => x.OrganizationId == organizationId && x.CreatedAtUtc >= dayStart,
                    cancellationToken),
            "Daily AI generation quota exceeded.",
            cancellationToken);
    }

    private async Task EnsureAsync(
        Guid organizationId,
        Func<OrganizationQuota, int> limit,
        Func<Task<int>> countAsync,
        string message,
        CancellationToken cancellationToken)
    {
        var quota = await db.OrganizationQuotasSet.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);
        var max = quota is null
            ? limit(
                new OrganizationQuota(
                    organizationId,
                    options.Value.DefaultMaxProducts,
                    options.Value.DefaultMaxMembers,
                    options.Value.DefaultMaxImportBatchesPerDay,
                    options.Value.DefaultMaxAiGenerationsPerDay))
            : limit(quota);
        var used = await countAsync().ConfigureAwait(false);
        if (used >= max)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static DateTimeOffset StartOfUtcDay()
    {
        var dayStart = DateTimeOffset.UtcNow.UtcDateTime.Date;
        return new DateTimeOffset(dayStart, TimeSpan.Zero);
    }
}

public sealed class OrganizationProvisioner(
    JerseyOsDbContext db,
    UserManager<ApplicationUser> users,
    IAuditRecorder audit,
    IOptions<TenancyOptions> tenancy,
    TimeProvider time) : IOrganizationProvisioner
{
    public async Task<OrganizationSummaryResponse> ProvisionAsync(
        string name,
        string slug,
        string adminEmail,
        string adminPassword,
        string? defaultCurrency,
        bool grantPlatformAdmin,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (await db.OrganizationsSet.IgnoreQueryFilters()
                .AnyAsync(x => x.Slug == normalizedSlug && !x.IsDeleted, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Organization slug '{normalizedSlug}' is already in use.");
        }

        var now = time.GetUtcNow();
        var organization = new Organization(name, slug, now, id: null, defaultCurrency ?? "ZAR");
        db.OrganizationsSet.Add(organization);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await OrganizationSeeder.SeedOrganizationAsync(
                db, organization.Id, grantPlatformAdmin, tenancy.Value, cancellationToken)
            .ConfigureAwait(false);

        var user = await users.FindByEmailAsync(adminEmail).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var created = await users.CreateAsync(user, adminPassword).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
            }
        }

        var membership = new OrganizationMembership { OrganizationId = organization.Id, UserId = user.Id };
        db.OrganizationMemberships.Add(membership);
        var adminRole = await db.Roles.IgnoreQueryFilters()
            .SingleAsync(
                x => x.OrganizationId == organization.Id && x.NormalizedName == "ADMIN",
                cancellationToken)
            .ConfigureAwait(false);
        db.MembershipRoles.Add(
            new MembershipRole
            {
                OrganizationId = organization.Id,
                MembershipId = membership.Id,
                RoleId = adminRole.Id
            });
        audit.Record(
            organization.Id,
            AuditActions.OrganizationProvisioned,
            nameof(Organization),
            organization.Id.ToString("N"),
            new { organization.Name, organization.Slug, adminEmail });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new OrganizationSummaryResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.DefaultCurrency,
            0,
            1,
            organization.CreatedAtUtc);
    }
}

public sealed class OrganizationIntegrationSettingsStore(
    JerseyOsDbContext db,
    IOptions<ShopifyOptions> shopifyOptions,
    IOptions<WooCommerceOptions> wooCommerceOptions,
    IOptions<DatabaseOptions> databaseOptions) : IOrganizationIntegrationSettings
{
    public ShopifyPublishSettings ResolveShopify(Guid organizationId)
    {
        var overlay = Overlay(organizationId);
        var global = shopifyOptions.Value;
        return new ShopifyPublishSettings(
            Value(overlay, OrganizationSettingKeys.ShopifyShopDomain, global.ShopDomain),
            Value(overlay, OrganizationSettingKeys.ShopifyAccessToken, global.AccessToken),
            Value(overlay, OrganizationSettingKeys.ShopifyApiVersion, global.ApiVersion),
            Value(overlay, OrganizationSettingKeys.ShopifyWebhookSecret, global.WebhookSecret));
    }

    public WooPublishSettings ResolveWoo(Guid organizationId)
    {
        var overlay = Overlay(organizationId);
        var global = wooCommerceOptions.Value;
        return new WooPublishSettings(
            Value(overlay, OrganizationSettingKeys.WooStoreBaseUrl, global.StoreBaseUrl),
            Value(overlay, OrganizationSettingKeys.WooConsumerKey, global.ConsumerKey),
            Value(overlay, OrganizationSettingKeys.WooConsumerSecret, global.ConsumerSecret),
            Value(overlay, OrganizationSettingKeys.WooApiVersion, global.ApiVersion));
    }

    public Guid? FindOrganizationByShopDomain(string? shopDomain)
    {
        if (string.IsNullOrWhiteSpace(shopDomain))
        {
            return null;
        }

        var normalized = NormalizeShop(shopDomain);
        var match = db.OrganizationSettingsSet.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Key == OrganizationSettingKeys.ShopifyShopDomain && x.Value != null)
            .AsEnumerable()
            .FirstOrDefault(x => NormalizeShop(x.Value) == normalized);
        if (match is not null)
        {
            return match.OrganizationId;
        }

        if (NormalizeShop(shopifyOptions.Value.ShopDomain) == normalized
            && databaseOptions.Value.DefaultOrganizationId != Guid.Empty)
        {
            return databaseOptions.Value.DefaultOrganizationId;
        }

        return null;
    }

    private Dictionary<string, string?> Overlay(Guid organizationId) =>
        db.OrganizationSettingsSet.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static string Value(Dictionary<string, string?> overlay, string key, string fallback) =>
        overlay.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback ?? string.Empty;

    private static string NormalizeShop(string? value) =>
        (value ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
}

public sealed class InvitationAcceptor(
    JerseyOsDbContext db,
    UserManager<ApplicationUser> users,
    IdentityService identity,
    IQuotaGuard quotas,
    IAuditRecorder audit,
    TimeProvider time) : IInvitationAcceptor
{
    public async Task<IssuedAuth?> AcceptAsync(string token, string? password, CancellationToken cancellationToken)
    {
        var hash = InvitationSecrets.Hash(token);
        var invitation = await db.OrganizationInvitationsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);
        var now = time.GetUtcNow();
        if (invitation is null || !invitation.IsUsable(now))
        {
            return null;
        }

        await quotas.EnsureMemberCapacityAsync(invitation.OrganizationId, cancellationToken).ConfigureAwait(false);
        var user = await users.FindByEmailAsync(invitation.Email).ConfigureAwait(false);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            {
                throw new InvalidOperationException("A password of at least 12 characters is required for new accounts.");
            }

            user = new ApplicationUser
            {
                UserName = invitation.Email,
                Email = invitation.Email,
                EmailConfirmed = true
            };
            var created = await users.CreateAsync(user, password).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
            }
        }

        var existing = await db.OrganizationMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == invitation.OrganizationId && x.UserId == user.Id && !x.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            existing = new OrganizationMembership
            {
                OrganizationId = invitation.OrganizationId,
                UserId = user.Id
            };
            db.OrganizationMemberships.Add(existing);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var normalizedRole = invitation.Role == OrganizationRoles.Admin ? "ADMIN" : "MEMBER";
        var role = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == invitation.OrganizationId && x.NormalizedName == normalizedRole,
                cancellationToken)
            .ConfigureAwait(false);
        if (role is not null
            && !await db.MembershipRoles.IgnoreQueryFilters()
                .AnyAsync(x => x.MembershipId == existing.Id && x.RoleId == role.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            db.MembershipRoles.Add(
                new MembershipRole
                {
                    OrganizationId = invitation.OrganizationId,
                    MembershipId = existing.Id,
                    RoleId = role.Id
                });
        }

        invitation.Accept(now);
        audit.Record(
            invitation.OrganizationId,
            AuditActions.InvitationAccepted,
            nameof(OrganizationInvitation),
            invitation.Id.ToString("N"),
            new { invitation.Email, invitation.Role });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await identity.IssueForUserAsync(user.Id, invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
    }
}
