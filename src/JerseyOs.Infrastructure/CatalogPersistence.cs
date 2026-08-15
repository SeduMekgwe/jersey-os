using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class ObjectStorageOptions
{
    public const string Section = "ObjectStorage";
    public string Provider { get; set; } = "Local";
    public string LocalRootPath { get; set; } = "App_Data/object-storage";
    public string PublicBasePath { get; set; } = "/media";
    /// <summary>
    /// Optional absolute public base (e.g. https://api.example.com/media). When set, Local GetUrl returns absolute URLs.
    /// Also used as fallback public base for Azure Blob when AzureBlob:PublicBaseUrl is empty.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
    public AzureBlobObjectStorageOptions AzureBlob { get; set; } = new();
}

public sealed class LocalObjectStorage(IOptions<ObjectStorageOptions> options, IHostEnvironment environment) : IObjectStorage
{
    private string RootPath => Path.GetFullPath(
        Path.IsPathRooted(options.Value.LocalRootPath)
            ? options.Value.LocalRootPath
            : Path.Combine(environment.ContentRootPath, options.Value.LocalRootPath));

    public async Task<string> PutAsync(
        string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        return key;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetUrl(string key)
    {
        var normalized = key.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl))
        {
            return $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{normalized}";
        }

        var basePath = options.Value.PublicBasePath.TrimEnd('/');
        return $"{basePath}/{normalized}";
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Object was not found in local storage.", path);
        }

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    private string ResolvePath(string key)
    {
        var relative = key.Replace('\\', '/').TrimStart('/');
        if (relative.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid object storage key.");
        }

        return Path.Combine(RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}

public static class CatalogModelBuilder
{
    public static void ConfigureCatalog(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<Team>(b =>
        {
            b.ToTable("catalog_teams");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<Season>(b =>
        {
            b.ToTable("catalog_seasons");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<Category>(b =>
        {
            b.ToTable("catalog_categories");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<Tag>(b =>
        {
            b.ToTable("catalog_tags");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<Product>(b =>
        {
            b.ToTable("catalog_products");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.Property(x => x.StyleCode).HasMaxLength(64);
            b.Property(x => x.SeoTitle).HasMaxLength(200);
            b.Property(x => x.SeoDescription).HasMaxLength(320);
            b.Property(x => x.SeoHandle).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Variants).WithOne(x => x.Product).HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Variants).HasField("_variants").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(x => x.Images).WithOne(x => x.Product).HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Images).HasField("_images").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(x => x.Categories).WithOne(x => x.Product).HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Categories).HasField("_categories").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(x => x.Tags).WithOne(x => x.Product).HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Tags).HasField("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ProductVariant>(b =>
        {
            b.ToTable("catalog_product_variants");
            b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            b.Property(x => x.Size).HasMaxLength(32).IsRequired();
            b.Property(x => x.PriceAmount).HasPrecision(18, 2);
            b.Property(x => x.CostAmount).HasPrecision(18, 2);
            b.Property(x => x.CompareAtAmount).HasPrecision(18, 2);
            b.HasIndex(x => new { x.OrganizationId, x.Sku }).IsUnique();
            b.HasOne(x => x.Inventory).WithOne(x => x.Variant)
                .HasForeignKey<InventoryLevel>(x => x.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<InventoryLevel>(b =>
        {
            b.ToTable("catalog_inventory_levels");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasIndex(x => x.VariantId).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ProductImage>(b =>
        {
            b.ToTable("catalog_product_images");
            b.Property(x => x.ObjectKey).HasMaxLength(500).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            b.Property(x => x.AltText).HasMaxLength(300);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ProductCategory>(b =>
        {
            b.ToTable("catalog_product_categories");
            b.HasIndex(x => new { x.ProductId, x.CategoryId }).IsUnique();
            b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<ProductTag>(b =>
        {
            b.ToTable("catalog_product_tags");
            b.HasIndex(x => new { x.ProductId, x.TagId }).IsUnique();
            b.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<PricingRule>(b =>
        {
            b.ToTable("catalog_pricing_rules");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.PercentRate).HasPrecision(18, 4);
            b.HasIndex(x => new { x.OrganizationId, x.Name });
            b.HasIndex(x => new { x.OrganizationId, x.SalesChannelId, x.Kind, x.Priority });
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<Collection>(b =>
        {
            b.ToTable("catalog_collections");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.MembershipKind).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(x => new { x.OrganizationId, x.Slug }).IsUnique();
            b.HasMany(x => x.Products).WithOne(x => x.Collection).HasForeignKey(x => x.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Products).HasField("_products").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<CollectionProduct>(b =>
        {
            b.ToTable("catalog_collection_products");
            b.HasIndex(x => new { x.CollectionId, x.ProductId }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }

    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.Section));
        var provider = configuration.GetSection(ObjectStorageOptions.Section)["Provider"] ?? "Local";

        if (ObjectStorageRegistration.IsAzureBlobProvider(provider))
        {
            var bound = configuration.GetSection(ObjectStorageOptions.Section).Get<ObjectStorageOptions>()
                ?? new ObjectStorageOptions();
            ObjectStorageRegistration.ValidateAzureBlobOptions(bound);

            services.AddSingleton<IAzureBlobGateway>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
                ObjectStorageRegistration.ValidateAzureBlobOptions(opts);
                var serviceClient = new Azure.Storage.Blobs.BlobServiceClient(opts.AzureBlob.ConnectionString);
                var container = serviceClient.GetBlobContainerClient(opts.AzureBlob.ContainerName);
                container.CreateIfNotExists();
                return new AzureBlobContainerGateway(container);
            });
            services.AddSingleton<IObjectStorage, AzureBlobObjectStorage>();
        }
        else
        {
            services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        }

        return services;
    }
}
