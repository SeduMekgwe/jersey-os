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
        Assert.Throws<InvalidOperationException>(() => product.Activate(now));

        product.UpsertVariant(product.Variants.Single().Id, "AWAY-M", "M", 0, now, 899.00m);
        product.Activate(now);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal(899.00m, product.Variants.Single().PriceAmount);
        Assert.Single(product.DomainEvents.OfType<ProductActivated>());
    }

    [Fact]
    public void PriceCannotBeNegative()
    {
        var product = new Product(Guid.NewGuid(), "Kit", "kit", null, null, null, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() =>
            product.UpsertVariant(null, "KIT-M", "M", 0, DateTimeOffset.UtcNow, -1m));
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

    [Fact]
    public void ReserveReleaseAndCommitInventory()
    {
        var now = DateTimeOffset.UtcNow;
        var inventory = new InventoryLevel(Guid.NewGuid(), Guid.NewGuid());
        inventory.Adjust(10, "receive", now);

        Assert.Throws<InvalidOperationException>(() => inventory.Reserve(11, "shopify-reserve", now));
        inventory.Reserve(4, "shopify-reserve", now);
        Assert.Equal(4, inventory.Reserved);
        Assert.Equal(10, inventory.OnHand);

        inventory.Release(1, "shopify-release", now);
        Assert.Equal(3, inventory.Reserved);

        inventory.Commit(2, "shopify-commit", now);
        Assert.Equal(8, inventory.OnHand);
        Assert.Equal(1, inventory.Reserved);

        Assert.Throws<InvalidOperationException>(() => inventory.Commit(5, "shopify-commit", now));
        Assert.Throws<InvalidOperationException>(() => inventory.Release(5, "shopify-release", now));
    }
}
