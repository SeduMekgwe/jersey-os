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

    [Fact]
    public void MapsProductCreatedToNotification()
    {
        var orgId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var created = new ProductCreated(productId, orgId, DateTimeOffset.UtcNow);

        var notification = Assert.IsType<ProductCreatedNotification>(
            Assert.Single(DomainEventMapper.ToNotifications([created], orgId, "corr-2")));
        Assert.Equal(productId, notification.DomainEvent.ProductId);
    }

    [Fact]
    public void MapsProductActivatedToNotification()
    {
        var orgId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var activated = new ProductActivated(productId, orgId, DateTimeOffset.UtcNow);

        var notification = Assert.IsType<ProductActivatedNotification>(
            Assert.Single(DomainEventMapper.ToNotifications([activated], orgId, "corr-3")));
        Assert.Equal(productId, notification.DomainEvent.ProductId);
    }
}
