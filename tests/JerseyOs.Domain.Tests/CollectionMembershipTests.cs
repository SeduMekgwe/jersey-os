using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class CollectionMembershipTests
{
    [Fact]
    public void ManualCollectionIncludesAssignedProductsOnly()
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var included = new Product(orgId, "Home", "home", null, null, null, now);
        var excluded = new Product(orgId, "Away", "away", null, null, null, now);
        var collection = new Collection(orgId, "Featured", "featured", CollectionMembershipKind.Manual);
        collection.SetManualMembership([included.Id]);

        Assert.True(collection.Includes(included));
        Assert.False(collection.Includes(excluded));
    }

    [Fact]
    public void TaxonomyCollectionMatchesTeamAndCategory()
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var teamId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var product = new Product(orgId, "Home", "home", null, teamId, Guid.NewGuid(), now);
        product.SetCategories([categoryId], now);
        var other = new Product(orgId, "Away", "away", null, Guid.NewGuid(), Guid.NewGuid(), now);

        var collection = new Collection(orgId, "Arsenal kits", "arsenal-kits", CollectionMembershipKind.Taxonomy);
        collection.SetTaxonomyRule(teamId, null, categoryId, null);

        Assert.True(collection.Includes(product));
        Assert.False(collection.Includes(other));
    }

    [Fact]
    public void TaxonomyCollectionRequiresAFilter()
    {
        var collection = new Collection(
            Guid.NewGuid(), "Empty", "empty", CollectionMembershipKind.Taxonomy);
        Assert.Throws<InvalidOperationException>(() => collection.SetTaxonomyRule(null, null, null, null));
    }

    [Fact]
    public void ProductSeoHandleIsNormalized()
    {
        var now = DateTimeOffset.UtcNow;
        var product = new Product(Guid.NewGuid(), "Home Kit", "home-kit", null, null, null, now);
        product.SetSeo("Home Kit | Shop", "Official home kit.", "Home-Kit-2026", now);

        Assert.Equal("Home Kit | Shop", product.SeoTitle);
        Assert.Equal("Official home kit.", product.SeoDescription);
        Assert.Equal("home-kit-2026", product.SeoHandle);
    }
}
