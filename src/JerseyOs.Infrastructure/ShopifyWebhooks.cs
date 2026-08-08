using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class ShopifyWebhookHmac(IOptions<ShopifyOptions> options) : IShopifyWebhookHmac
{
    public bool IsValid(string rawBody, string? hmacHeader)
    {
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(hmacHeader))
        {
            return false;
        }

        try
        {
            var key = Encoding.UTF8.GetBytes(secret);
            var body = Encoding.UTF8.GetBytes(rawBody);
            var hash = HMACSHA256.HashData(key, body);
            var provided = Convert.FromBase64String(hmacHeader.Trim());
            return CryptographicOperations.FixedTimeEquals(hash, provided);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class ProcessShopifyWebhookJob(
    JerseyOsDbContext db,
    TimeProvider time)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task ExecuteAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        var delivery = await db.WebhookDeliveriesSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == deliveryId, cancellationToken)
            .ConfigureAwait(false);
        if (delivery is null || delivery.Status != WebhookDeliveryStatus.Pending)
        {
            return;
        }

        var now = time.GetUtcNow();
        try
        {
            var channel = await db.SalesChannelsSet.IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => x.OrganizationId == delivery.OrganizationId
                         && x.Code == SalesChannelCodes.Shopify
                         && x.Enabled,
                    cancellationToken)
                .ConfigureAwait(false);
            if (channel is null)
            {
                delivery.MarkIgnored("Shopify sales channel is missing or disabled.", now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var action = delivery.Topic switch
            {
                "orders/create" => InventoryWebhookAction.Reserve,
                "orders/cancelled" => InventoryWebhookAction.Release,
                "orders/fulfilled" => InventoryWebhookAction.Commit,
                _ => InventoryWebhookAction.Unsupported
            };
            if (action == InventoryWebhookAction.Unsupported)
            {
                delivery.MarkIgnored($"Unsupported topic '{delivery.Topic}'.", now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var lines = ShopifyOrderPayload.ParseLineItems(delivery.PayloadJson);
            if (lines.Count == 0)
            {
                delivery.MarkIgnored("Order payload contained no line items.", now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var errors = new List<string>();
            var applied = 0;
            foreach (var line in lines)
            {
                var map = await db.ExternalIdMapsSet.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(
                        x => x.OrganizationId == delivery.OrganizationId
                             && x.ChannelId == channel.Id
                             && x.EntityType == PublishEntityTypes.Variant
                             && x.ExternalId == line.VariantExternalId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (map is null)
                {
                    errors.Add($"Unmapped variant {line.VariantExternalId}.");
                    continue;
                }

                var inventory = await db.InventoryLevelsSet.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(
                        x => x.OrganizationId == delivery.OrganizationId && x.VariantId == map.LocalId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (inventory is null)
                {
                    errors.Add($"Inventory missing for variant {map.LocalId:N}.");
                    continue;
                }

                try
                {
                    switch (action)
                    {
                        case InventoryWebhookAction.Reserve:
                            inventory.Reserve(line.Quantity, "shopify-reserve", now);
                            break;
                        case InventoryWebhookAction.Release:
                            inventory.Release(line.Quantity, "shopify-release", now);
                            break;
                        case InventoryWebhookAction.Commit:
                            inventory.Commit(line.Quantity, "shopify-commit", now);
                            break;
                    }

                    applied++;
                }
                catch (Exception exception)
                {
                    errors.Add($"{line.VariantExternalId}: {exception.Message}");
                }
            }

            if (applied == 0 && errors.Count > 0)
            {
                delivery.MarkFailed(string.Join(" ", errors), now);
            }
            else if (errors.Count > 0)
            {
                delivery.MarkFailed($"Applied {applied} line(s). {string.Join(" ", errors)}", now);
            }
            else
            {
                delivery.MarkSucceeded(now);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            delivery.MarkFailed(exception.Message, now);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}

internal enum InventoryWebhookAction
{
    Unsupported = 0,
    Reserve = 1,
    Release = 2,
    Commit = 3
}

internal static class ShopifyOrderPayload
{
    public sealed record LineItem(string VariantExternalId, int Quantity);

    public static IReadOnlyList<LineItem> ParseLineItems(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("line_items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var lines = new List<LineItem>();
        foreach (var item in items.EnumerateArray())
        {
            var quantity = 0;
            if (item.TryGetProperty("quantity", out var qtyEl))
            {
                quantity = qtyEl.ValueKind switch
                {
                    JsonValueKind.Number => qtyEl.GetInt32(),
                    JsonValueKind.String => int.TryParse(qtyEl.GetString(), out var q) ? q : 0,
                    _ => 0
                };
            }

            if (quantity <= 0)
            {
                continue;
            }

            string? externalId = null;
            if (item.TryGetProperty("admin_graphql_api_id", out var gql) &&
                gql.ValueKind == JsonValueKind.String &&
                gql.GetString() is { Length: > 0 } gqlId &&
                gqlId.Contains("ProductVariant", StringComparison.Ordinal))
            {
                externalId = gqlId;
            }
            else if (item.TryGetProperty("variant_id", out var variantIdEl))
            {
                var numeric = variantIdEl.ValueKind switch
                {
                    JsonValueKind.Number => variantIdEl.GetInt64().ToString(CultureInfo.InvariantCulture),
                    JsonValueKind.String => variantIdEl.GetString(),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(numeric))
                {
                    externalId = $"gid://shopify/ProductVariant/{numeric}";
                }
            }

            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            lines.Add(new LineItem(externalId, quantity));
        }

        return lines;
    }
}
