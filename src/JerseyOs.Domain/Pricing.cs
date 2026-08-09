using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public enum PricingRuleKind
{
    MarkupPercent = 0,
    MarginPercent = 1,
    CompareAtPercent = 2
}

public sealed class PricingRule : AuditableEntity, IOrganizationScoped
{
    private PricingRule() { }

    public PricingRule(
        Guid organizationId,
        string name,
        PricingRuleKind kind,
        decimal percentRate,
        int priority,
        Guid? salesChannelId = null,
        bool isEnabled = true)
    {
        OrganizationId = organizationId;
        Rename(name);
        SetKind(kind);
        SetPercentRate(percentRate);
        SetPriority(priority);
        SalesChannelId = salesChannelId;
        IsEnabled = isEnabled;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PricingRuleKind Kind { get; private set; }
    public decimal PercentRate { get; private set; }
    public Guid? SalesChannelId { get; private set; }
    public int Priority { get; private set; }
    public bool IsEnabled { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        if (Name.Length > 200)
        {
            throw new InvalidOperationException("Pricing rule name cannot exceed 200 characters.");
        }
    }

    public void SetKind(PricingRuleKind kind) => Kind = kind;

    public void SetPercentRate(decimal percentRate)
    {
        if (percentRate < 0)
        {
            throw new InvalidOperationException("Percent rate cannot be negative.");
        }

        if (Kind == PricingRuleKind.MarginPercent && percentRate >= 100)
        {
            throw new InvalidOperationException("Margin percent must be less than 100.");
        }

        PercentRate = percentRate;
    }

    public void SetPriority(int priority) => Priority = priority;

    public void SetSalesChannelId(Guid? salesChannelId) => SalesChannelId = salesChannelId;

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void Update(
        string name,
        PricingRuleKind kind,
        decimal percentRate,
        int priority,
        Guid? salesChannelId,
        bool isEnabled)
    {
        Rename(name);
        SetKind(kind);
        SetPercentRate(percentRate);
        SetPriority(priority);
        SetSalesChannelId(salesChannelId);
        SetEnabled(isEnabled);
    }
}

public static class PricingCalculator
{
    public sealed record ResolvedPrices(
        decimal? PriceAmount,
        decimal? CompareAtAmount,
        Guid? SellRuleId,
        Guid? CompareAtRuleId);

    public static ResolvedPrices ResolveForCatalog(
        decimal? costAmount,
        decimal? explicitPriceAmount,
        decimal? explicitCompareAtAmount,
        IEnumerable<PricingRule> rules)
    {
        var orgRules = rules
            .Where(r => r.IsEnabled && r.SalesChannelId is null)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToArray();

        return Resolve(costAmount, explicitPriceAmount, explicitCompareAtAmount, orgRules, channelScoped: false);
    }

    public static ResolvedPrices ResolveForPublish(
        decimal? costAmount,
        decimal? storedPriceAmount,
        decimal? storedCompareAtAmount,
        IEnumerable<PricingRule> rules,
        Guid salesChannelId)
    {
        var enabled = rules.Where(r => r.IsEnabled).ToArray();
        var sellRule = PickSellRule(enabled, salesChannelId);
        var compareRule = PickCompareAtRule(enabled, salesChannelId);

        decimal? price = storedPriceAmount;
        Guid? sellRuleId = null;
        if (costAmount is > 0 && sellRule is not null)
        {
            price = ComputeSell(costAmount.Value, sellRule);
            sellRuleId = sellRule.Id;
        }

        decimal? compareAt = storedCompareAtAmount;
        Guid? compareRuleId = null;
        if (price is > 0 && compareRule is not null)
        {
            compareAt = RoundMoney(price.Value * (1m + compareRule.PercentRate / 100m));
            compareRuleId = compareRule.Id;
        }

        return new ResolvedPrices(price, compareAt, sellRuleId, compareRuleId);
    }

    private static ResolvedPrices Resolve(
        decimal? costAmount,
        decimal? explicitPriceAmount,
        decimal? explicitCompareAtAmount,
        IReadOnlyList<PricingRule> orderedRules,
        bool channelScoped)
    {
        _ = channelScoped;
        var sellRule = orderedRules.FirstOrDefault(r =>
            r.Kind is PricingRuleKind.MarkupPercent or PricingRuleKind.MarginPercent);
        var compareRule = orderedRules.FirstOrDefault(r => r.Kind == PricingRuleKind.CompareAtPercent);

        decimal? price = explicitPriceAmount;
        Guid? sellRuleId = null;
        if (price is null && costAmount is > 0 && sellRule is not null)
        {
            price = ComputeSell(costAmount.Value, sellRule);
            sellRuleId = sellRule.Id;
        }

        decimal? compareAt = explicitCompareAtAmount;
        Guid? compareRuleId = null;
        if (compareAt is null && price is > 0 && compareRule is not null)
        {
            compareAt = RoundMoney(price.Value * (1m + compareRule.PercentRate / 100m));
            compareRuleId = compareRule.Id;
        }

        return new ResolvedPrices(price, compareAt, sellRuleId, compareRuleId);
    }

    private static PricingRule? PickSellRule(IReadOnlyList<PricingRule> rules, Guid salesChannelId) =>
        Pick(rules, salesChannelId, r => r.Kind is PricingRuleKind.MarkupPercent or PricingRuleKind.MarginPercent);

    private static PricingRule? PickCompareAtRule(IReadOnlyList<PricingRule> rules, Guid salesChannelId) =>
        Pick(rules, salesChannelId, r => r.Kind == PricingRuleKind.CompareAtPercent);

    private static PricingRule? Pick(
        IReadOnlyList<PricingRule> rules,
        Guid salesChannelId,
        Func<PricingRule, bool> kindMatch)
    {
        var ordered = rules
            .Where(kindMatch)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Name)
            .ToArray();
        return ordered.FirstOrDefault(r => r.SalesChannelId == salesChannelId)
               ?? ordered.FirstOrDefault(r => r.SalesChannelId is null);
    }

    public static decimal ComputeSell(decimal costAmount, PricingRule rule)
    {
        if (costAmount <= 0)
        {
            throw new InvalidOperationException("Cost must be greater than zero to compute a sell price.");
        }

        return rule.Kind switch
        {
            PricingRuleKind.MarkupPercent => RoundMoney(costAmount * (1m + rule.PercentRate / 100m)),
            PricingRuleKind.MarginPercent => RoundMoney(costAmount / (1m - rule.PercentRate / 100m)),
            _ => throw new InvalidOperationException($"Rule kind {rule.Kind} cannot compute a sell price.")
        };
    }

    public static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
