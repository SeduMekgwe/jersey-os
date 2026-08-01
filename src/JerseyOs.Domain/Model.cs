using JerseyOs.SharedKernel;
using Microsoft.AspNetCore.Identity;

namespace JerseyOs.Domain;

public sealed record OrganizationCreated(Guid OrganizationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed class Organization : SoftDeletableEntity
{
    private Organization() { }
    public Organization(string name, string slug, DateTimeOffset now, Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        if (id is { } organizationId) Id = organizationId;
        Raise(new OrganizationCreated(Id, now));
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public ICollection<OrganizationMembership> Memberships { get; } = [];
}

public sealed class ApplicationUser : IdentityUser<Guid>, IAuditableEntity
{
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset ModifiedAtUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<OrganizationMembership> Memberships { get; } = [];
}

public sealed class ApplicationRole : IdentityRole<Guid>, IAuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset ModifiedAtUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<RolePermissionGrant> PermissionGrants { get; } = [];
}

public sealed class OrganizationMembership : SoftDeletableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Organization Organization { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<MembershipRole> Roles { get; } = [];
}

public sealed class PermissionDefinition : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class RolePermissionGrant : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
    public PermissionDefinition Permission { get; set; } = null!;
}

public sealed class MembershipRole : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid RoleId { get; set; }
    public OrganizationMembership Membership { get; set; } = null!;
    public ApplicationRole Role { get; set; } = null!;
}

public sealed class RefreshTokenSession : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public bool IsUsable(DateTimeOffset now) => UsedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > now;
    public void Rotate(Guid replacementId, DateTimeOffset now) { UsedAtUtc = now; ReplacedBySessionId = replacementId; }
    public void Revoke(DateTimeOffset now, string reason) { RevokedAtUtc ??= now; RevocationReason ??= reason; }
}

public sealed class AuditLog : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class OutboxMessage : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

public sealed class FeatureFlag : SoftDeletableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
