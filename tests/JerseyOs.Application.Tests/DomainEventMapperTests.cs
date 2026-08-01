using JerseyOs.Application;
using JerseyOs.Domain;

namespace JerseyOs.Application.Tests;

public sealed class DomainEventMapperTests
{
    [Fact]
    public void MapsOrganizationCreatedToNotification()
    {
        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var created = new OrganizationCreated(orgId, DateTimeOffset.UtcNow);

        var notifications = DomainEventMapper.ToNotifications([created], orgId, "corr-1").ToArray();

        var notification = Assert.Single(notifications);
        var typed = Assert.IsType<OrganizationCreatedNotification>(notification);
        Assert.Equal(orgId, typed.OrganizationId);
        Assert.Equal("corr-1", typed.CorrelationId);
        Assert.Same(created, typed.DomainEvent);
    }
}
