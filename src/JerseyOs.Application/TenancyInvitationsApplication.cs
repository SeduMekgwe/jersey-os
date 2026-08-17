using System.Security.Cryptography;
using System.Text;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public static class InvitationSecrets
{
    public static (string Plaintext, byte[] Hash) Generate()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var plaintext = OrganizationInvitation.PlaintextPrefix + token;
        return (plaintext, Hash(plaintext));
    }

    public static byte[] Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return SHA256.HashData(Encoding.UTF8.GetBytes(plaintext.Trim()));
    }
}

public interface IInvitationAcceptor
{
    Task<IssuedAuth?> AcceptAsync(string token, string? password, CancellationToken cancellationToken);
}

public sealed class ListOrganizationInvitationsHandler(IApplicationDbContext db)
    : IRequestHandler<ListOrganizationInvitationsQuery, IReadOnlyCollection<OrganizationInvitationResponse>>
{
    public async Task<IReadOnlyCollection<OrganizationInvitationResponse>> Handle(
        ListOrganizationInvitationsQuery request, CancellationToken cancellationToken)
    {
        var items = await db.OrganizationInvitations.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(ToResponse).ToArray();
    }

    internal static OrganizationInvitationResponse ToResponse(OrganizationInvitation invitation) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAtUtc,
            invitation.AcceptedAtUtc,
            invitation.RevokedAtUtc);
}

public sealed class CreateOrganizationInvitationHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IQuotaGuard quotas,
    IAuditRecorder audit,
    TimeProvider time)
    : IRequestHandler<CreateOrganizationInvitationCommand, CreatedOrganizationInvitationResponse>
{
    public async Task<CreatedOrganizationInvitationResponse> Handle(
        CreateOrganizationInvitationCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        await quotas.EnsureMemberCapacityAsync(orgId, cancellationToken).ConfigureAwait(false);
        var (plaintext, hash) = InvitationSecrets.Generate();
        var invitation = new OrganizationInvitation(
            orgId,
            request.Email,
            request.Role,
            hash,
            time.GetUtcNow().AddDays(7));
        db.Add(invitation);
        audit.Record(
            orgId,
            AuditActions.InvitationCreated,
            nameof(OrganizationInvitation),
            invitation.Id.ToString("N"),
            new { invitation.Email, invitation.Role });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreatedOrganizationInvitationResponse(
            invitation.Id,
            invitation.Email,
            invitation.Role,
            plaintext,
            invitation.ExpiresAtUtc);
    }
}

public sealed class RevokeOrganizationInvitationHandler(IApplicationDbContext db, IAuditRecorder audit, TimeProvider time)
    : IRequestHandler<RevokeOrganizationInvitationCommand, OrganizationInvitationResponse?>
{
    public async Task<OrganizationInvitationResponse?> Handle(
        RevokeOrganizationInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await db.OrganizationInvitations
            .SingleOrDefaultAsync(x => x.Id == request.InvitationId, cancellationToken)
            .ConfigureAwait(false);
        if (invitation is null)
        {
            return null;
        }

        invitation.Revoke(time.GetUtcNow());
        audit.Record(
            invitation.OrganizationId,
            AuditActions.InvitationRevoked,
            nameof(OrganizationInvitation),
            invitation.Id.ToString("N"),
            new { invitation.Email });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ListOrganizationInvitationsHandler.ToResponse(invitation);
    }
}

public sealed class AcceptOrganizationInvitationHandler(IInvitationAcceptor acceptor)
    : IRequestHandler<AcceptOrganizationInvitationCommand, IssuedAuth?>
{
    public Task<IssuedAuth?> Handle(
        AcceptOrganizationInvitationCommand request, CancellationToken cancellationToken) =>
        acceptor.AcceptAsync(request.Token, request.Password, cancellationToken);
}

public sealed class GetOrganizationIntegrationSettingsHandler(IApplicationDbContext db, ICurrentRequest current)
    : IRequestHandler<GetOrganizationIntegrationSettingsQuery, OrganizationIntegrationSettingsResponse>
{
    public async Task<OrganizationIntegrationSettingsResponse> Handle(
        GetOrganizationIntegrationSettingsQuery request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        var settings = await db.OrganizationSettings.AsNoTracking()
            .Where(x => x.OrganizationId == orgId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return OrganizationSettingsMapping.ToResponse(settings);
    }
}

public sealed class UpdateOrganizationIntegrationSettingsHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IAuditRecorder audit)
    : IRequestHandler<UpdateOrganizationIntegrationSettingsCommand, OrganizationIntegrationSettingsResponse>
{
    public async Task<OrganizationIntegrationSettingsResponse> Handle(
        UpdateOrganizationIntegrationSettingsCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        await Upsert(db, orgId, OrganizationSettingKeys.ShopifyShopDomain, request.ShopifyShopDomain, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.ShopifyAccessToken, request.ShopifyAccessToken, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.ShopifyWebhookSecret, request.ShopifyWebhookSecret, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.ShopifyApiVersion, request.ShopifyApiVersion, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.WooStoreBaseUrl, request.WooStoreBaseUrl, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.WooConsumerKey, request.WooConsumerKey, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.WooConsumerSecret, request.WooConsumerSecret, cancellationToken)
            .ConfigureAwait(false);
        await Upsert(db, orgId, OrganizationSettingKeys.WooApiVersion, request.WooApiVersion, cancellationToken)
            .ConfigureAwait(false);
        audit.Record(
            orgId,
            AuditActions.OrganizationSettingsUpdated,
            nameof(OrganizationSetting),
            orgId.ToString("N"),
            new { keys = OrganizationSettingsMapping.SettingAuditKeys });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var settings = await db.OrganizationSettings.AsNoTracking()
            .Where(x => x.OrganizationId == orgId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return OrganizationSettingsMapping.ToResponse(settings);
    }

    private static async Task Upsert(
        IApplicationDbContext db,
        Guid organizationId,
        string key,
        string? value,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return;
        }

        var existing = await db.OrganizationSettings
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            db.Add(new OrganizationSetting(organizationId, key, value));
            return;
        }

        existing.SetValue(value);
    }
}

public static class OrganizationSettingsMapping
{
    internal static readonly string[] SettingAuditKeys = ["shopify", "woocommerce"];
    public static OrganizationIntegrationSettingsResponse ToResponse(
        IReadOnlyCollection<OrganizationSetting> settings)
    {
        string? Value(string key) =>
            settings.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        bool Configured(string key) => !string.IsNullOrWhiteSpace(Value(key));

        return new OrganizationIntegrationSettingsResponse(
            Value(OrganizationSettingKeys.ShopifyShopDomain),
            Configured(OrganizationSettingKeys.ShopifyAccessToken),
            Configured(OrganizationSettingKeys.ShopifyWebhookSecret),
            Value(OrganizationSettingKeys.ShopifyApiVersion),
            Value(OrganizationSettingKeys.WooStoreBaseUrl),
            Configured(OrganizationSettingKeys.WooConsumerKey),
            Configured(OrganizationSettingKeys.WooConsumerSecret),
            Value(OrganizationSettingKeys.WooApiVersion));
    }
}
