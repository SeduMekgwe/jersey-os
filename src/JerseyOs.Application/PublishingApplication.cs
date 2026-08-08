using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace JerseyOs.Application;

public sealed record PublishVariantInput(
    Guid VariantId,
    string Sku,
    string Size,
    int AvailableQuantity,
    decimal? PriceAmount,
    string CurrencyCode,
    string? ExternalVariantId);

public sealed record PublishProductInput(
    Guid ProductId,
    Guid OrganizationId,
    string Title,
    string? StyleCode,
    string? TeamName,
    string? SeasonName,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<PublishVariantInput> Variants,
    string? ExternalProductId);

public sealed record PublishProductResult(
    string ExternalProductId,
    IReadOnlyDictionary<Guid, string> VariantExternalIds);

public interface ISalesChannelPublisher
{
    Task<PublishProductResult> UpsertProductAsync(PublishProductInput input, CancellationToken cancellationToken);
    Task UnpublishProductAsync(string externalProductId, CancellationToken cancellationToken);
    Task SetInventoryAsync(string externalVariantId, int availableQuantity, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the outbound publisher for a sales channel code (e.g. shopify, woocommerce).
/// Returns null when the channel has no configured adapter/credentials.
/// </summary>
public interface ISalesChannelPublisherResolver
{
    ISalesChannelPublisher? Resolve(string channelCode);
}

public interface IPublishingJobScheduler
{
    void EnqueuePublishProduct(Guid organizationId, Guid productId, bool unpublish);
    void EnqueueSyncInventory(Guid organizationId, Guid variantId);
    void EnqueueProcessWebhook(Guid deliveryId);
}

public interface IShopifyWebhookHmac
{
    bool IsValid(string rawBody, string? hmacHeader);
}

public sealed record ListSalesChannelsQuery : IRequest<IReadOnlyCollection<SalesChannelResponse>>;
public sealed record ListPublishRunsQuery(Guid? ProductId = null) : IRequest<IReadOnlyCollection<PublishRunResponse>>;
public sealed record SetChannelEnabledCommand(Guid ChannelId, bool Enabled) : IRequest<SalesChannelResponse?>;
public sealed record RepublishProductCommand(Guid ProductId) : IRequest<PublishRunResponse?>;
public sealed record GetProductPublishRunsQuery(Guid ProductId) : IRequest<IReadOnlyCollection<PublishRunResponse>>;
public sealed record ListWebhookDeliveriesQuery : IRequest<IReadOnlyCollection<WebhookDeliveryResponse>>;
public sealed record IngestShopifyWebhookCommand(
    Guid OrganizationId,
    string WebhookId,
    string Topic,
    string PayloadJson) : IRequest<Guid?>;

public sealed class SetChannelEnabledValidator : AbstractValidator<SetChannelEnabledCommand>
{
    public SetChannelEnabledValidator() => RuleFor(x => x.ChannelId).NotEmpty();
}

public sealed class RepublishProductValidator : AbstractValidator<RepublishProductCommand>
{
    public RepublishProductValidator() => RuleFor(x => x.ProductId).NotEmpty();
}

public sealed class ListSalesChannelsHandler(IApplicationDbContext db)
    : IRequestHandler<ListSalesChannelsQuery, IReadOnlyCollection<SalesChannelResponse>>
{
    public async Task<IReadOnlyCollection<SalesChannelResponse>> Handle(
        ListSalesChannelsQuery request, CancellationToken cancellationToken)
    {
        var channels = await db.SalesChannels.AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return channels.Select(PublishingMapping.ToResponse).ToArray();
    }
}

public sealed class ListPublishRunsHandler(IApplicationDbContext db)
    : IRequestHandler<ListPublishRunsQuery, IReadOnlyCollection<PublishRunResponse>>
{
    public async Task<IReadOnlyCollection<PublishRunResponse>> Handle(
        ListPublishRunsQuery request, CancellationToken cancellationToken)
    {
        var query = db.PublishRuns.AsNoTracking().Include(x => x.Channel).AsQueryable();
        if (request.ProductId is { } productId)
        {
            query = query.Where(x => x.ProductId == productId);
        }

        var runs = await query
            .OrderByDescending(x => x.ModifiedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return runs.Select(PublishingMapping.ToResponse).ToArray();
    }
}

public sealed class GetProductPublishRunsHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductPublishRunsQuery, IReadOnlyCollection<PublishRunResponse>>
{
    public Task<IReadOnlyCollection<PublishRunResponse>> Handle(
        GetProductPublishRunsQuery request, CancellationToken cancellationToken) =>
        new ListPublishRunsHandler(db).Handle(new ListPublishRunsQuery(request.ProductId), cancellationToken);
}

public sealed class SetChannelEnabledHandler(IApplicationDbContext db)
    : IRequestHandler<SetChannelEnabledCommand, SalesChannelResponse?>
{
    public async Task<SalesChannelResponse?> Handle(
        SetChannelEnabledCommand request, CancellationToken cancellationToken)
    {
        var channel = await db.SalesChannels
            .SingleOrDefaultAsync(x => x.Id == request.ChannelId, cancellationToken)
            .ConfigureAwait(false);
        if (channel is null)
        {
            return null;
        }

        channel.SetEnabled(request.Enabled);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PublishingMapping.ToResponse(channel);
    }
}

public sealed class RepublishProductHandler(
    IApplicationDbContext db,
    ICurrentRequest current,
    IPublishingJobScheduler jobs) : IRequestHandler<RepublishProductCommand, PublishRunResponse?>
{
    public async Task<PublishRunResponse?> Handle(
        RepublishProductCommand request, CancellationToken cancellationToken)
    {
        var orgId = current.OrganizationId
            ?? throw new InvalidOperationException("Organization context is required.");
        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            .ConfigureAwait(false);
        if (product is null || product.Status != ProductStatus.Active)
        {
            return null;
        }

        var channels = await db.SalesChannels
            .Where(x => x.Enabled)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (channels.Count == 0)
        {
            return null;
        }

        PublishRun? primaryRun = null;
        foreach (var channel in channels)
        {
            var run = await db.PublishRuns
                .Include(x => x.Channel)
                .SingleOrDefaultAsync(
                    x => x.ChannelId == channel.Id && x.ProductId == product.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (run is null)
            {
                run = new PublishRun(orgId, channel.Id, product.Id);
                db.Add(run);
            }
            else
            {
                run.MarkPending();
            }

            primaryRun ??= run;
            if (channel.Code == SalesChannelCodes.Shopify)
            {
                primaryRun = run;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobs.EnqueuePublishProduct(orgId, product.Id, unpublish: false);

        primaryRun = await db.PublishRuns.AsNoTracking()
            .Include(x => x.Channel)
            .SingleAsync(x => x.Id == primaryRun!.Id, cancellationToken)
            .ConfigureAwait(false);
        return PublishingMapping.ToResponse(primaryRun);
    }
}

public sealed class ListWebhookDeliveriesHandler(IApplicationDbContext db)
    : IRequestHandler<ListWebhookDeliveriesQuery, IReadOnlyCollection<WebhookDeliveryResponse>>
{
    public async Task<IReadOnlyCollection<WebhookDeliveryResponse>> Handle(
        ListWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var deliveries = await db.WebhookDeliveries.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return deliveries.Select(PublishingMapping.ToResponse).ToArray();
    }
}

public sealed class IngestShopifyWebhookHandler(
    IApplicationDbContext db,
    IPublishingJobScheduler jobs) : IRequestHandler<IngestShopifyWebhookCommand, Guid?>
{
    public async Task<Guid?> Handle(IngestShopifyWebhookCommand request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.WebhookId) ||
            string.IsNullOrWhiteSpace(request.Topic))
        {
            return null;
        }

        var existing = await db.WebhookDeliveries
            .AsNoTracking()
            .AnyAsync(
                x => x.OrganizationId == request.OrganizationId && x.WebhookId == request.WebhookId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing)
        {
            return Guid.Empty;
        }

        var delivery = new WebhookDelivery(
            request.OrganizationId,
            request.WebhookId,
            request.Topic,
            request.PayloadJson);
        db.Add(delivery);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobs.EnqueueProcessWebhook(delivery.Id);
        return delivery.Id;
    }
}

public static class PublishingMapping
{
    public static SalesChannelResponse ToResponse(SalesChannel channel) =>
        new(channel.Id, channel.Code, channel.DisplayName, channel.Enabled);

    public static PublishRunResponse ToResponse(PublishRun run) =>
        new(
            run.Id,
            run.ChannelId,
            run.Channel.Code,
            run.ProductId,
            run.Status.ToString(),
            run.Error,
            run.CompletedAtUtc,
            run.ModifiedAtUtc);

    public static WebhookDeliveryResponse ToResponse(WebhookDelivery delivery) =>
        new(
            delivery.Id,
            delivery.WebhookId,
            delivery.Topic,
            delivery.Status.ToString(),
            delivery.Error,
            delivery.ProcessedAtUtc,
            delivery.CreatedAtUtc);
}

