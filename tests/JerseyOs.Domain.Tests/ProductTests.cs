using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class ProductTests
{
    [Fact]
    public void CreateRaisesProductCreatedAndNormalizesSlug()
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var product = new Product(orgId, "Home Kit", "Home-Kit", "HK-25", null, null, now);

        Assert.Equal("home-kit", product.Slug);
        Assert.Equal(ProductStatus.Draft, product.Status);
        var created = Assert.Single(product.DomainEvents.OfType<ProductCreated>());
        Assert.Equal(product.Id, created.ProductId);
    }

    [Fact]
    public void ActivateRequiresVariantTeamAndSeason()
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var product = new Product(orgId, "Away Kit", "away-kit", null, null, null, now);
        Assert.Throws<InvalidOperationException>(() => product.Activate(now));

        product.UpsertVariant(null, "AWAY-M", "M", 0, now);
        Assert.Throws<InvalidOperationException>(() => product.Activate(now));

        product.UpdateDetails("Away Kit", "away-kit", null, Guid.NewGuid(), Guid.NewGuid(), now);
        product.Activate(now);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void InventoryCannotGoNegative()
    {
        var inventory = new InventoryLevel(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => inventory.Adjust(-1, "shrink", DateTimeOffset.UtcNow));
        inventory.Adjust(5, "receive", DateTimeOffset.UtcNow);
        Assert.Equal(5, inventory.OnHand);
        Assert.Contains(inventory.DomainEvents, e => e is InventoryAdjusted);
    }
}
