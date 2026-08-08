using System.Text;
using Asp.Versioning;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JerseyOs.Api;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.PublishingRead)]
[Route("api/v{version:apiVersion}/publishing")]
public sealed class PublishingController(ISender sender) : ControllerBase
{
    [HttpGet("channels")]
    [ProducesResponseType<IReadOnlyCollection<SalesChannelResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<SalesChannelResponse>> ListChannels(CancellationToken cancellationToken) =>
        sender.Send(new ListSalesChannelsQuery(), cancellationToken);

    [HttpPut("channels/{channelId:guid}")]
    [Authorize(Policy = Permissions.PublishingManage)]
    [ProducesResponseType<SalesChannelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalesChannelResponse>> SetChannelEnabled(
        Guid channelId, SetChannelEnabledRequest request, CancellationToken cancellationToken)
    {
        var channel = await sender.Send(new SetChannelEnabledCommand(channelId, request.Enabled), cancellationToken);
        return channel is null ? NotFound() : Ok(channel);
    }

    [HttpGet("runs")]
    [ProducesResponseType<IReadOnlyCollection<PublishRunResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<PublishRunResponse>> ListRuns(
        [FromQuery] Guid? productId, CancellationToken cancellationToken) =>
        sender.Send(new ListPublishRunsQuery(productId), cancellationToken);

    [HttpGet("products/{productId:guid}/runs")]
    [ProducesResponseType<IReadOnlyCollection<PublishRunResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<PublishRunResponse>> ListProductRuns(
        Guid productId, CancellationToken cancellationToken) =>
        sender.Send(new GetProductPublishRunsQuery(productId), cancellationToken);

    [HttpPost("products/{productId:guid}/republish")]
    [Authorize(Policy = Permissions.PublishingManage)]
    [ProducesResponseType<PublishRunResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishRunResponse>> Republish(
        Guid productId, CancellationToken cancellationToken)
    {
        var run = await sender.Send(new RepublishProductCommand(productId), cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("webhook-deliveries")]
    [ProducesResponseType<IReadOnlyCollection<WebhookDeliveryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<WebhookDeliveryResponse>> ListWebhookDeliveries(
        CancellationToken cancellationToken) =>
        sender.Send(new ListWebhookDeliveriesQuery(), cancellationToken);
}

[ApiController]
[ApiVersion(1.0)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/publishing/webhooks")]
public sealed class ShopifyWebhookController(
    ISender sender,
    IShopifyWebhookHmac hmac,
    IOptions<DatabaseOptions> databaseOptions) : ControllerBase
{
    [HttpPost("shopify")]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveShopify(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var hmacHeader = Request.Headers["X-Shopify-Hmac-Sha256"].ToString();
        if (!hmac.IsValid(body, hmacHeader))
        {
            return Unauthorized();
        }

        var topic = Request.Headers["X-Shopify-Topic"].ToString();
        var webhookId = Request.Headers["X-Shopify-Webhook-Id"].ToString();
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(webhookId))
        {
            return BadRequest();
        }

        var orgId = databaseOptions.Value.DefaultOrganizationId;
        if (orgId == Guid.Empty)
        {
            return BadRequest();
        }

        await sender.Send(new IngestShopifyWebhookCommand(orgId, webhookId, topic, body), cancellationToken);
        return Ok();
    }
}
