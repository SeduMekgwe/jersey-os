using JerseyOs.SharedKernel;

namespace JerseyOs.Domain;

public enum ProductStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}

public sealed record ProductCreated(Guid ProductId, Guid OrganizationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record ProductUpdated(Guid ProductId, Guid OrganizationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record ProductActivated(Guid ProductId, Guid OrganizationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record ProductArchived(Guid ProductId, Guid OrganizationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record InventoryAdjusted(
    Guid VariantId,
    Guid OrganizationId,
    int DeltaOnHand,
    int OnHand,
    int Reserved,
    string Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed class Team : AuditableEntity, IOrganizationScoped
{
    private Team() { }

    public Team(Guid organizationId, string name, string slug)
    {
        OrganizationId = organizationId;
        Rename(name, slug);
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public void Rename(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }
}

public sealed class Season : AuditableEntity, IOrganizationScoped
{
    private Season() { }

    public Season(Guid organizationId, string name, string slug)
    {
        OrganizationId = organizationId;
        Rename(name, slug);
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public void Rename(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }
}

public sealed class Category : AuditableEntity, IOrganizationScoped
{
    private Category() { }

    public Category(Guid organizationId, string name, string slug, Guid? parentId = null)
    {
        OrganizationId = organizationId;
        ParentId = parentId;
        Rename(name, slug);
    }

    public Guid OrganizationId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public void Rename(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }

    public void SetParent(Guid? parentId) => ParentId = parentId;
}

public sealed class Tag : AuditableEntity, IOrganizationScoped
{
    private Tag() { }

    public Tag(Guid organizationId, string name, string slug)
    {
        OrganizationId = organizationId;
        Rename(name, slug);
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public void Rename(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }
}

public sealed class Product : AuditableEntity, IOrganizationScoped
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];
    private readonly List<ProductCategory> _categories = [];
    private readonly List<ProductTag> _tags = [];

    private Product() { }

    public Product(
        Guid organizationId,
        string name,
        string slug,
        string? styleCode,
        Guid? teamId,
        Guid? seasonId,
        DateTimeOffset now)
    {
        OrganizationId = organizationId;
        TeamId = teamId;
        SeasonId = seasonId;
        Status = ProductStatus.Draft;
        ApplyIdentity(name, slug, styleCode);
        Raise(new ProductCreated(Id, OrganizationId, now));
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? StyleCode { get; private set; }
    public ProductStatus Status { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? SeasonId { get; private set; }
    public Team? Team { get; private set; }
    public Season? Season { get; private set; }
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();
    public IReadOnlyCollection<ProductCategory> Categories => _categories.AsReadOnly();
    public IReadOnlyCollection<ProductTag> Tags => _tags.AsReadOnly();

    public void UpdateDetails(
        string name,
        string slug,
        string? styleCode,
        Guid? teamId,
        Guid? seasonId,
        DateTimeOffset now)
    {
        EnsureNotArchived();
        ApplyIdentity(name, slug, styleCode);
        TeamId = teamId;
        SeasonId = seasonId;
        Raise(new ProductUpdated(Id, OrganizationId, now));
    }

    public void Activate(DateTimeOffset now)
    {
        EnsureNotArchived();
        if (_variants.Count == 0)
        {
            throw new InvalidOperationException("An active product requires at least one variant.");
        }

        if (TeamId is null || SeasonId is null)
        {
            throw new InvalidOperationException("An active product requires team and season.");
        }

        Status = ProductStatus.Active;
        Raise(new ProductActivated(Id, OrganizationId, now));
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
        {
            return;
        }

        Status = ProductStatus.Archived;
        Raise(new ProductArchived(Id, OrganizationId, now));
    }

    public ProductVariant UpsertVariant(Guid? variantId, string sku, string size, int sortOrder, DateTimeOffset now)
    {
        EnsureNotArchived();
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(size);

        var normalizedSku = sku.Trim().ToUpperInvariant();
        var normalizedSize = size.Trim();
        ProductVariant variant;
        if (variantId is { } id)
        {
            variant = _variants.SingleOrDefault(x => x.Id == id)
                ?? throw new InvalidOperationException("Variant was not found on this product.");
            variant.Update(normalizedSku, normalizedSize, sortOrder);
        }
        else
        {
            variant = new ProductVariant(OrganizationId, Id, normalizedSku, normalizedSize, sortOrder);
            _variants.Add(variant);
        }

        Raise(new ProductUpdated(Id, OrganizationId, now));
        return variant;
    }

    public void RemoveVariant(Guid variantId, DateTimeOffset now)
    {
        EnsureNotArchived();
        var variant = _variants.SingleOrDefault(x => x.Id == variantId)
            ?? throw new InvalidOperationException("Variant was not found on this product.");
        if (Status == ProductStatus.Active && _variants.Count <= 1)
        {
            throw new InvalidOperationException("An active product must retain at least one variant.");
        }

        _variants.Remove(variant);
        Raise(new ProductUpdated(Id, OrganizationId, now));
    }

    public ProductImage AttachImage(string objectKey, string contentType, string? altText, int sortOrder, DateTimeOffset now)
    {
        EnsureNotArchived();
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        var image = new ProductImage(OrganizationId, Id, objectKey.Trim(), contentType.Trim(), altText?.Trim(), sortOrder);
        _images.Add(image);
        Raise(new ProductUpdated(Id, OrganizationId, now));
        return image;
    }

    public void ReorderImages(IReadOnlyDictionary<Guid, int> sortOrders, DateTimeOffset now)
    {
        EnsureNotArchived();
        foreach (var image in _images)
        {
            if (sortOrders.TryGetValue(image.Id, out var order))
            {
                image.SetSortOrder(order);
            }
        }

        Raise(new ProductUpdated(Id, OrganizationId, now));
    }

    public ProductImage RemoveImage(Guid imageId, DateTimeOffset now)
    {
        EnsureNotArchived();
        var image = _images.SingleOrDefault(x => x.Id == imageId)
            ?? throw new InvalidOperationException("Image was not found on this product.");
        _images.Remove(image);
        Raise(new ProductUpdated(Id, OrganizationId, now));
        return image;
    }

    public void SetCategories(IEnumerable<Guid> categoryIds, DateTimeOffset now)
    {
        EnsureNotArchived();
        _categories.Clear();
        foreach (var categoryId in categoryIds.Distinct())
        {
            _categories.Add(new ProductCategory(OrganizationId, Id, categoryId));
        }

        Raise(new ProductUpdated(Id, OrganizationId, now));
    }

    public void SetTags(IEnumerable<Guid> tagIds, DateTimeOffset now)
    {
        EnsureNotArchived();
        _tags.Clear();
        foreach (var tagId in tagIds.Distinct())
        {
            _tags.Add(new ProductTag(OrganizationId, Id, tagId));
        }

        Raise(new ProductUpdated(Id, OrganizationId, now));
    }

    private void ApplyIdentity(string name, string slug, string? styleCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        StyleCode = string.IsNullOrWhiteSpace(styleCode) ? null : styleCode.Trim();
    }

    private void EnsureNotArchived()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new InvalidOperationException("Archived products cannot be modified.");
        }
    }
}

public sealed class ProductVariant : AuditableEntity, IOrganizationScoped
{
    private ProductVariant() { }

    public ProductVariant(Guid organizationId, Guid productId, string sku, string size, int sortOrder)
    {
        OrganizationId = organizationId;
        ProductId = productId;
        Sku = sku;
        Size = size;
        SortOrder = sortOrder;
        Inventory = new InventoryLevel(organizationId, Id);
    }

    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Size { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public Product Product { get; private set; } = null!;
    public InventoryLevel Inventory { get; private set; } = null!;

    public void Update(string sku, string size, int sortOrder)
    {
        Sku = sku;
        Size = size;
        SortOrder = sortOrder;
    }
}

public sealed class InventoryLevel : AuditableEntity, IOrganizationScoped
{
    private InventoryLevel() { }

    public InventoryLevel(Guid organizationId, Guid variantId)
    {
        OrganizationId = organizationId;
        VariantId = variantId;
        Id = variantId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid VariantId { get; private set; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public ProductVariant Variant { get; private set; } = null!;

    public void Adjust(int deltaOnHand, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var next = OnHand + deltaOnHand;
        if (next < 0)
        {
            throw new InvalidOperationException("Inventory on-hand cannot be negative.");
        }

        if (Reserved > next)
        {
            throw new InvalidOperationException("Inventory on-hand cannot fall below reserved quantity.");
        }

        OnHand = next;
        Raise(new InventoryAdjusted(VariantId, OrganizationId, deltaOnHand, OnHand, Reserved, reason.Trim(), now));
    }
}

public sealed class ProductImage : AuditableEntity, IOrganizationScoped
{
    private ProductImage() { }

    public ProductImage(
        Guid organizationId,
        Guid productId,
        string objectKey,
        string contentType,
        string? altText,
        int sortOrder)
    {
        OrganizationId = organizationId;
        ProductId = productId;
        ObjectKey = objectKey;
        ContentType = contentType;
        AltText = altText;
        SortOrder = sortOrder;
    }

    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ObjectKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string? AltText { get; private set; }
    public int SortOrder { get; private set; }
    public Product Product { get; private set; } = null!;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}

public sealed class ProductCategory : AuditableEntity, IOrganizationScoped
{
    private ProductCategory() { }

    public ProductCategory(Guid organizationId, Guid productId, Guid categoryId)
    {
        OrganizationId = organizationId;
        ProductId = productId;
        CategoryId = categoryId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Product Product { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
}

public sealed class ProductTag : AuditableEntity, IOrganizationScoped
{
    private ProductTag() { }

    public ProductTag(Guid organizationId, Guid productId, Guid tagId)
    {
        OrganizationId = organizationId;
        ProductId = productId;
        TagId = tagId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid TagId { get; private set; }
    public Product Product { get; private set; } = null!;
    public Tag Tag { get; private set; } = null!;
}
