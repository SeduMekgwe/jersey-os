using System.Diagnostics.CodeAnalysis;
using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public enum CollectionMembershipKind
{
    Manual = 0,
    Taxonomy = 1
}

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Collection is the catalog merchandising aggregate.")]
public sealed class Collection : AuditableEntity, IOrganizationScoped
{
    private readonly List<CollectionProduct> _products = [];

    private Collection() { }

    public Collection(
        Guid organizationId,
        string name,
        string slug,
        CollectionMembershipKind membershipKind,
        string? description = null)
    {
        OrganizationId = organizationId;
        MembershipKind = membershipKind;
        Rename(name, slug);
        SetDescription(description);
        if (membershipKind == CollectionMembershipKind.Taxonomy)
        {
            // Taxonomy collections require at least one filter before they match products.
        }
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public CollectionMembershipKind MembershipKind { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? SeasonId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? TagId { get; private set; }
    public IReadOnlyCollection<CollectionProduct> Products => _products.AsReadOnly();

    public void Rename(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        if (Name.Length > 200)
        {
            throw new InvalidOperationException("Collection name cannot exceed 200 characters.");
        }

        if (Slug.Length > 100)
        {
            throw new InvalidOperationException("Collection slug cannot exceed 100 characters.");
        }
    }

    public void SetDescription(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void SetManualMembership(IEnumerable<Guid> productIds)
    {
        EnsureManual();
        _products.Clear();
        foreach (var productId in productIds.Distinct())
        {
            _products.Add(new CollectionProduct(OrganizationId, Id, productId));
        }
    }

    public void SetTaxonomyRule(Guid? teamId, Guid? seasonId, Guid? categoryId, Guid? tagId)
    {
        if (MembershipKind != CollectionMembershipKind.Taxonomy)
        {
            throw new InvalidOperationException("Taxonomy rules can only be set on taxonomy collections.");
        }

        if (teamId is null && seasonId is null && categoryId is null && tagId is null)
        {
            throw new InvalidOperationException("A taxonomy collection requires at least one team, season, category, or tag filter.");
        }

        TeamId = teamId;
        SeasonId = seasonId;
        CategoryId = categoryId;
        TagId = tagId;
        _products.Clear();
    }

    public bool Includes(Product product)
    {
        if (product.OrganizationId != OrganizationId)
        {
            return false;
        }

        if (MembershipKind == CollectionMembershipKind.Manual)
        {
            return _products.Any(x => x.ProductId == product.Id);
        }

        return MatchesTaxonomy(product);
    }

    public bool MatchesTaxonomy(Product product)
    {
        if (MembershipKind != CollectionMembershipKind.Taxonomy)
        {
            return false;
        }

        if (TeamId is null && SeasonId is null && CategoryId is null && TagId is null)
        {
            return false;
        }

        if (TeamId is { } teamId && product.TeamId != teamId)
        {
            return false;
        }

        if (SeasonId is { } seasonId && product.SeasonId != seasonId)
        {
            return false;
        }

        if (CategoryId is { } categoryId && product.Categories.All(x => x.CategoryId != categoryId))
        {
            return false;
        }

        if (TagId is { } tagId && product.Tags.All(x => x.TagId != tagId))
        {
            return false;
        }

        return true;
    }

    private void EnsureManual()
    {
        if (MembershipKind != CollectionMembershipKind.Manual)
        {
            throw new InvalidOperationException("Manual members can only be set on manual collections.");
        }
    }
}

public sealed class CollectionProduct : AuditableEntity, IOrganizationScoped
{
    private CollectionProduct() { }

    public CollectionProduct(Guid organizationId, Guid collectionId, Guid productId)
    {
        OrganizationId = organizationId;
        CollectionId = collectionId;
        ProductId = productId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid CollectionId { get; private set; }
    public Guid ProductId { get; private set; }
    public Collection Collection { get; private set; } = null!;
}
