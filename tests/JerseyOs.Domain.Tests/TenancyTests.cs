using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class OrganizationInvitationTests
{
    [Fact]
    public void AcceptMarksInvitationAndRejectsReuse()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new OrganizationInvitation(
            Guid.NewGuid(),
            "member@example.invalid",
            "member",
            new byte[32],
            now.AddDays(1));

        Assert.True(invitation.IsUsable(now));
        Assert.Equal(OrganizationRoles.Member, invitation.Role);
        invitation.Accept(now);
        Assert.False(invitation.IsUsable(now));
        Assert.Equal(now, invitation.AcceptedAtUtc);
        Assert.Throws<InvalidOperationException>(() => invitation.Accept(now.AddMinutes(1)));
    }

    [Fact]
    public void RevokeIsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new OrganizationInvitation(
            Guid.NewGuid(),
            "member@example.invalid",
            OrganizationRoles.Admin,
            new byte[32],
            now.AddDays(1));
        invitation.Revoke(now);
        invitation.Revoke(now.AddMinutes(1));
        Assert.Equal(now, invitation.RevokedAtUtc);
        Assert.False(invitation.IsUsable(now));
    }
}

public sealed class OrganizationQuotaTests
{
    [Fact]
    public void RejectsNonPositiveLimits()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new OrganizationQuota(Guid.NewGuid(), 0, 10, 10, 10));
    }

    [Fact]
    public void SetLimitsUpdatesValues()
    {
        var quota = new OrganizationQuota(Guid.NewGuid(), 10, 5, 3, 8);
        quota.SetLimits(20, 6, 4, 9);
        Assert.Equal(20, quota.MaxProducts);
        Assert.Equal(6, quota.MaxMembers);
        Assert.Equal(4, quota.MaxImportBatchesPerDay);
        Assert.Equal(9, quota.MaxAiGenerationsPerDay);
    }
}
