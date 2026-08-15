using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public static class CollectionMembership
{
    public static async Task<IReadOnlyCollection<Collection>> ForProductAsync(
        IApplicationDbContext db, Product product, CancellationToken cancellationToken)
    {
        var collections = await db.Collections
            .Include(x => x.Products)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return collections.Where(x => x.Includes(product)).ToArray();
    }

    public static async Task<IReadOnlyCollection<Guid>> IdsForProductAsync(
        IApplicationDbContext db, Product product, CancellationToken cancellationToken)
    {
        var collections = await ForProductAsync(db, product, cancellationToken).ConfigureAwait(false);
        return collections.Select(x => x.Id).ToArray();
    }

    public static async Task<int> CountMembersAsync(
        IApplicationDbContext db, Collection collection, CancellationToken cancellationToken)
    {
        if (collection.MembershipKind == CollectionMembershipKind.Manual)
        {
            return collection.Products.Count;
        }

        var products = await db.Products
            .Include(x => x.Categories)
            .Include(x => x.Tags)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return products.Count(collection.Includes);
    }

    public static async Task<IReadOnlyCollection<Guid>> MemberIdsAsync(
        IApplicationDbContext db, Collection collection, CancellationToken cancellationToken)
    {
        if (collection.MembershipKind == CollectionMembershipKind.Manual)
        {
            return collection.Products.Select(x => x.ProductId).ToArray();
        }

        var products = await db.Products
            .Include(x => x.Categories)
            .Include(x => x.Tags)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return products.Where(collection.Includes).Select(x => x.Id).ToArray();
    }
}

public static class CollectionMapping
{
    public static CollectionResponse ToResponse(Collection collection, int memberCount, IReadOnlyCollection<Guid> productIds) =>
        new(
            collection.Id,
            collection.Name,
            collection.Slug,
            collection.Description,
            collection.MembershipKind.ToString(),
            collection.TeamId,
            collection.SeasonId,
            collection.CategoryId,
            collection.TagId,
            productIds,
            memberCount);
}

public sealed record ListCollectionsQuery : IRequest<IReadOnlyCollection<CollectionResponse>>;
public sealed record GetCollectionQuery(Guid Id) : IRequest<CollectionResponse?>;
public sealed record CreateCollectionCommand(
    string Name,
    string Slug,
    string MembershipKind,
    string? Description,
    Guid? TeamId,
    Guid? SeasonId,
    Guid? CategoryId,
    Guid? TagId,
    IReadOnlyCollection<Guid>? ProductIds) : IRequest<CollectionResponse>;
public sealed record UpdateCollectionCommand(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? TeamId,
    Guid? SeasonId,
    Guid? CategoryId,
    Guid? TagId,
    IReadOnlyCollection<Guid>? ProductIds) : IRequest<CollectionResponse?>;
public sealed record DeleteCollectionCommand(Guid Id) : IRequest<bool>;

public sealed class CreateCollectionValidator : AbstractValidator<CreateCollectionCommand>
{
    public CreateCollectionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.MembershipKind).NotEmpty().Must(BeKnownKind);
        RuleFor(x => x.Description).MaximumLength(2000);
    }

    private static bool BeKnownKind(string kind) =>
        Enum.TryParse<CollectionMembershipKind>(kind, ignoreCase: true, out _);
}

public sealed class UpdateCollectionValidator : AbstractValidator<UpdateCollectionCommand>
{
    public UpdateCollectionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class ListCollectionsHandler(IApplicationDbContext db)
    : IRequestHandler<ListCollectionsQuery, IReadOnlyCollection<CollectionResponse>>
{
    public async Task<IReadOnlyCollection<CollectionResponse>> Handle(
        ListCollectionsQuery request, CancellationToken cancellationToken)
    {
        var collections = await db.Collections.Include(x => x.Products)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var results = new List<CollectionResponse>(collections.Count);
        foreach (var collection in collections)
        {
            var memberIds = await CollectionMembership.MemberIdsAsync(db, collection, cancellationToken)
                .ConfigureAwait(false);
            results.Add(CollectionMapping.ToResponse(collection, memberIds.Count, memberIds));
        }

        return results;
    }
}

public sealed class GetCollectionHandler(IApplicationDbContext db)
    : IRequestHandler<GetCollectionQuery, CollectionResponse?>
{
    public async Task<CollectionResponse?> Handle(GetCollectionQuery request, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.Include(x => x.Products)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (collection is null)
        {
            return null;
        }

        var memberIds = await CollectionMembership.MemberIdsAsync(db, collection, cancellationToken)
            .ConfigureAwait(false);
        return CollectionMapping.ToResponse(collection, memberIds.Count, memberIds);
    }
}

public sealed class CreateCollectionHandler(
    IApplicationDbContext db,
    ICurrentRequest current) : IRequestHandler<CreateCollectionCommand, CollectionResponse>
{
    public async Task<CollectionResponse> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        await EnsureSlugAvailable(db, request.Slug, null, cancellationToken).ConfigureAwait(false);
        var kind = Enum.Parse<CollectionMembershipKind>(request.MembershipKind, ignoreCase: true);
        var collection = new Collection(orgId, request.Name, request.Slug, kind, request.Description);
        if (kind == CollectionMembershipKind.Taxonomy)
        {
            await CatalogHandlerSupport.EnsureTaxonomyExists(
                    db,
                    request.TeamId,
                    request.SeasonId,
                    request.CategoryId is { } cid ? [cid] : null,
                    request.TagId is { } tid ? [tid] : null,
                    cancellationToken)
                .ConfigureAwait(false);
            collection.SetTaxonomyRule(request.TeamId, request.SeasonId, request.CategoryId, request.TagId);
        }
        else if (request.ProductIds is { Count: > 0 })
        {
            await EnsureProductsExist(db, request.ProductIds, cancellationToken).ConfigureAwait(false);
            collection.SetManualMembership(request.ProductIds);
        }

        db.Add(collection);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var memberIds = await CollectionMembership.MemberIdsAsync(db, collection, cancellationToken)
            .ConfigureAwait(false);
        return CollectionMapping.ToResponse(collection, memberIds.Count, memberIds);
    }

    internal static async Task EnsureSlugAvailable(
        IApplicationDbContext db, string slug, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var exists = await db.Collections
            .AnyAsync(x => x.Slug == normalized && (excludingId == null || x.Id != excludingId), cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Collection slug '{normalized}' is already in use.");
        }
    }

    internal static async Task EnsureProductsExist(
        IApplicationDbContext db, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var count = await db.Products.CountAsync(x => productIds.Contains(x.Id), cancellationToken)
            .ConfigureAwait(false);
        if (count != productIds.Distinct().Count())
        {
            throw new InvalidOperationException("One or more products were not found.");
        }
    }
}

public sealed class UpdateCollectionHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateCollectionCommand, CollectionResponse?>
{
    public async Task<CollectionResponse?> Handle(UpdateCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.Include(x => x.Products)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (collection is null)
        {
            return null;
        }

        await CreateCollectionHandler.EnsureSlugAvailable(db, request.Slug, collection.Id, cancellationToken)
            .ConfigureAwait(false);
        collection.Rename(request.Name, request.Slug);
        collection.SetDescription(request.Description);
        if (collection.MembershipKind == CollectionMembershipKind.Taxonomy)
        {
            await CatalogHandlerSupport.EnsureTaxonomyExists(
                    db,
                    request.TeamId,
                    request.SeasonId,
                    request.CategoryId is { } cid ? [cid] : null,
                    request.TagId is { } tid ? [tid] : null,
                    cancellationToken)
                .ConfigureAwait(false);
            collection.SetTaxonomyRule(request.TeamId, request.SeasonId, request.CategoryId, request.TagId);
        }
        else if (request.ProductIds is not null)
        {
            await CreateCollectionHandler.EnsureProductsExist(db, request.ProductIds, cancellationToken)
                .ConfigureAwait(false);
            collection.SetManualMembership(request.ProductIds);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var memberIds = await CollectionMembership.MemberIdsAsync(db, collection, cancellationToken)
            .ConfigureAwait(false);
        return CollectionMapping.ToResponse(collection, memberIds.Count, memberIds);
    }
}

public sealed class DeleteCollectionHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteCollectionCommand, bool>
{
    public async Task<bool> Handle(DeleteCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await db.Collections.Include(x => x.Products)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (collection is null)
        {
            return false;
        }

        db.Remove(collection);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
