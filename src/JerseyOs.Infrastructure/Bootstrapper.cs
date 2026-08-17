using JerseyOs.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        var tenancy = services.GetRequiredService<IOptions<TenancyOptions>>().Value;
        var now = TimeProvider.System.GetUtcNow();
        var organization = await db.OrganizationsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            organization = new Organization(name, slug, now, organizationId, "ZAR");
            db.OrganizationsSet.Add(organization);
        }
        else if (string.IsNullOrWhiteSpace(organization.DefaultCurrency))
        {
            organization.SetDefaultCurrency("ZAR");
        }

        await db.SaveChangesAsync(cancellationToken);
        await OrganizationSeeder.SeedOrganizationAsync(
                db, organizationId, grantPlatformAdmin: true, tenancy, cancellationToken)
            .ConfigureAwait(false);

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
            await db.SaveChangesAsync(cancellationToken);
        }

        var role = await db.Roles.IgnoreQueryFilters()
            .SingleAsync(x => x.OrganizationId == organizationId && x.NormalizedName == "ADMIN", cancellationToken);
        if (!await db.MembershipRoles.IgnoreQueryFilters()
                .AnyAsync(x => x.MembershipId == membership.Id && x.RoleId == role.Id, cancellationToken))
        {
            db.MembershipRoles.Add(new MembershipRole
            {
                OrganizationId = organizationId,
                MembershipId = membership.Id,
                RoleId = role.Id
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
