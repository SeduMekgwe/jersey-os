using JerseyOs.Infrastructure;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class ShopifyWebhookPayloadTests
{
    [Fact]
    public void ParseOrderLines_UsesFulfillableQuantityWhenRequested()
    {
        var payload = """
            {
              "line_items": [
                { "variant_id": 55, "quantity": 5, "fulfillable_quantity": 2 },
                { "variant_id": 56, "quantity": 1, "fulfillable_quantity": 0 }
              ]
            }
            """;

        var ordered = ShopifyOrderPayload.ParseOrderLines(payload, ShopifyLineQuantityMode.Ordered);
        Assert.Equal(2, ordered.Count);
        Assert.Equal(5, ordered[0].Quantity);

        var fulfillable = ShopifyOrderPayload.ParseOrderLines(
            payload, ShopifyLineQuantityMode.FulfillableOrOrdered);
        Assert.Single(fulfillable);
        Assert.Equal("gid://shopify/ProductVariant/55", fulfillable[0].VariantExternalId);
        Assert.Equal(2, fulfillable[0].Quantity);
    }

    [Fact]
    public void ParseOrderLines_PrefersGraphqlVariantId()
    {
        var payload = """
            {
              "line_items": [
                {
                  "variant_id": 1,
                  "admin_graphql_api_id": "gid://shopify/ProductVariant/999",
                  "quantity": 3
                }
              ]
            }
            """;

        var lines = ShopifyOrderPayload.ParseOrderLines(payload, ShopifyLineQuantityMode.Ordered);
        Assert.Equal("gid://shopify/ProductVariant/999", lines[0].VariantExternalId);
        Assert.Equal(3, lines[0].Quantity);
    }
}
