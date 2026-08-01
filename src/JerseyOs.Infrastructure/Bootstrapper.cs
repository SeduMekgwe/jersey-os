using JerseyOs.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JerseyOs.Infrastructure;

public static class Bootstrapper
{
    public static async Task RunAsync(
        IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Bootstrap:Enabled")) return;
        var organizationId = configuration.GetValue<Guid>("Database:DefaultOrganizationId");
        var name = configuration["Bootstrap:OrganizationName"];
        var slug = configuration["Bootstrap:OrganizationSlug"];
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Enabled bootstrap requires organization id/name and admin credentials.");
        slug = string.IsNullOrWhiteSpace(slug)
            ? new string(name.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or ' ')
                .Select(ch => ch == ' ' ? '-' : ch).ToArray()).Trim('-')
            : slug;

        var db = services.GetRequiredService<JerseyOsDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var now = TimeProvider.System.GetUtcNow();
        var organization = await db.OrganizationsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            organization = new Organization(name, slug, now, organizationId);
            db.OrganizationsSet.Add(organization);
        }
        var permissions = new List<PermissionDefinition>();
        foreach (var key in Permissions.All)
        {
            var permission = await db.PermissionsSet.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (permission is null)
            {
                permission = new PermissionDefinition { Key = key, Description = key };
                db.PermissionsSet.Add(permission);
            }
            permissions.Add(permission);
        }
        await db.SaveChangesAsync(cancellationToken);

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
        var membership = await db.OrganizationMemberships.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == user.Id, cancellationToken);
        if (membership is null)
        {
            membership = new OrganizationMembership { OrganizationId = organizationId, UserId = user.Id };
            db.OrganizationMemberships.Add(membership);
        }
        var role = await db.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.NormalizedName == "ADMIN", cancellationToken);
        if (role is null)
        {
            role = new ApplicationRole { OrganizationId = organizationId, Name = "Admin", NormalizedName = "ADMIN" };
            db.Roles.Add(role);
        }
        await db.SaveChangesAsync(cancellationToken);

        if (!await db.MembershipRoles.IgnoreQueryFilters()
            .AnyAsync(x => x.MembershipId == membership.Id && x.RoleId == role.Id, cancellationToken))
            db.MembershipRoles.Add(new MembershipRole
            {
                OrganizationId = organizationId,
                MembershipId = membership.Id,
                RoleId = role.Id
            });
        foreach (var permission in permissions)
        {
            if (!await db.RolePermissions.IgnoreQueryFilters()
                .AnyAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id, cancellationToken))
                db.RolePermissions.Add(new RolePermissionGrant
                {
                    OrganizationId = organizationId,
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });
        }

        if (!await db.SalesChannelsSet.IgnoreQueryFilters()
                .AnyAsync(
                    x => x.OrganizationId == organizationId && x.Code == SalesChannelCodes.Shopify,
                    cancellationToken))
        {
            db.SalesChannelsSet.Add(new SalesChannel(organizationId, SalesChannelCodes.Shopify, "Shopify", enabled: true));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
