using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class PricingCalculatorTests
{
    [Fact]
    public void MarkupPercentComputesSellFromCost()
    {
        var orgId = Guid.NewGuid();
        var rule = new PricingRule(orgId, "Default markup", PricingRuleKind.MarkupPercent, 40m, 10);
        var resolved = PricingCalculator.ResolveForCatalog(100m, null, null, [rule]);

        Assert.Equal(140.00m, resolved.PriceAmount);
        Assert.Equal(rule.Id, resolved.SellRuleId);
        Assert.Null(resolved.CompareAtAmount);
    }

    [Fact]
    public void MarginPercentComputesSellFromCost()
    {
        var orgId = Guid.NewGuid();
        var rule = new PricingRule(orgId, "Target margin", PricingRuleKind.MarginPercent, 20m, 10);
        var resolved = PricingCalculator.ResolveForCatalog(80m, null, null, [rule]);

        Assert.Equal(100.00m, resolved.PriceAmount);
    }

    [Fact]
    public void CompareAtPercentUsesResolvedSell()
    {
        var orgId = Guid.NewGuid();
        var rules = new[]
        {
            new PricingRule(orgId, "Markup", PricingRuleKind.MarkupPercent, 50m, 10),
            new PricingRule(orgId, "Compare", PricingRuleKind.CompareAtPercent, 25m, 5)
        };
        var resolved = PricingCalculator.ResolveForCatalog(100m, null, null, rules);

        Assert.Equal(150.00m, resolved.PriceAmount);
        Assert.Equal(187.50m, resolved.CompareAtAmount);
    }

    [Fact]
    public void ExplicitPriceWinsOverRule()
    {
        var orgId = Guid.NewGuid();
        var rule = new PricingRule(orgId, "Markup", PricingRuleKind.MarkupPercent, 40m, 10);
        var resolved = PricingCalculator.ResolveForCatalog(100m, 199m, null, [rule]);

        Assert.Equal(199m, resolved.PriceAmount);
        Assert.Null(resolved.SellRuleId);
    }

    [Fact]
    public void ChannelOverrideBeatsOrgRuleOnPublish()
    {
        var orgId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var rules = new[]
        {
            new PricingRule(orgId, "Org markup", PricingRuleKind.MarkupPercent, 40m, 10),
            new PricingRule(orgId, "Shopify markup", PricingRuleKind.MarkupPercent, 60m, 10, channelId)
        };

        var resolved = PricingCalculator.ResolveForPublish(100m, 140m, null, rules, channelId);

        Assert.Equal(160.00m, resolved.PriceAmount);
        Assert.Equal(rules[1].Id, resolved.SellRuleId);
    }

    [Fact]
    public void MarginPercentRejectsRateAtOrAbove100()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PricingRule(Guid.NewGuid(), "Bad", PricingRuleKind.MarginPercent, 100m, 1));
    }
}
