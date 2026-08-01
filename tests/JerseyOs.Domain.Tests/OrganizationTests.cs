using System.Globalization;
using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class OrganizationTests
{
    [Fact]
    public void CreatingOrganizationRaisesDomainEventAndNormalizesSlug()
    {
        var now = DateTimeOffset.Parse("2026-07-27T12:00:00Z", CultureInfo.InvariantCulture);
        var organization = new Organization("Jersey No.10 Collective", " Jersey-No10 ", now);

        Assert.Equal("jersey-no10", organization.Slug);
        Assert.Contains(organization.DomainEvents, e => e is OrganizationCreated created && created.OrganizationId == organization.Id);
    }
}

public sealed class RefreshTokenSessionTests
{
    [Fact]
    public void ReplayDetectionPathRevokesUsableSession()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new RefreshTokenSession
        {
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            TokenHash = new byte[32],
            ExpiresAtUtc = now.AddDays(1)
        };

        Assert.True(session.IsUsable(now));
        session.Rotate(Guid.NewGuid(), now);
        Assert.False(session.IsUsable(now));
        session.Revoke(now, "refresh-token-replay");
        Assert.Equal("refresh-token-replay", session.RevocationReason);
    }
}
