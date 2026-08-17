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
    public bool IsValid(string rawBody, string? hmacHeader, string? webhookSecret = null)
    {
        var secret = string.IsNullOrWhiteSpace(webhookSecret) ? options.Value.WebhookSecret : webhookSecret;
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

            var topic = delivery.Topic.Trim().ToLowerInvariant();
            if (topic is "orders/partially_fulfilled")
            {
                // Inventory commits are driven by fulfillments/create (supports partial quantities).
                delivery.MarkIgnored("Partial fulfillments are applied via fulfillments/create.", now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var movements = topic switch
            {
                "orders/create" => ShopifyOrderPayload.ParseOrderLines(
                        delivery.PayloadJson, ShopifyLineQuantityMode.Ordered)
                    .Select(l => new InventoryMovement(l.VariantExternalId, l.Quantity, InventoryWebhookAction.Reserve))
                    .ToList(),
                "orders/cancelled" => ShopifyOrderPayload.ParseOrderLines(
                        delivery.PayloadJson, ShopifyLineQuantityMode.FulfillableOrOrdered)
                    .Select(l => new InventoryMovement(l.VariantExternalId, l.Quantity, InventoryWebhookAction.Release))
                    .ToList(),
                "orders/fulfilled" => ShopifyOrderPayload.ParseOrderLines(
                        delivery.PayloadJson, ShopifyLineQuantityMode.Ordered)
                    .Select(l => new InventoryMovement(l.VariantExternalId, l.Quantity, InventoryWebhookAction.Commit))
                    .ToList(),
                "fulfillments/create" => ShopifyOrderPayload.ParseOrderLines(
                        delivery.PayloadJson, ShopifyLineQuantityMode.Ordered)
                    .Select(l => new InventoryMovement(l.VariantExternalId, l.Quantity, InventoryWebhookAction.Commit))
                    .ToList(),
                "refunds/create" => ShopifyOrderPayload.ParseRefundMovements(delivery.PayloadJson).ToList(),
                _ => null
            };

            if (movements is null)
            {
                delivery.MarkIgnored($"Unsupported topic '{delivery.Topic}'.", now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (movements.Count == 0)
            {
                delivery.MarkIgnored("Webhook payload contained no inventory movements.", now);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var errors = new List<string>();
            var applied = 0;
            foreach (var movement in movements)
            {
                var map = await db.ExternalIdMapsSet.IgnoreQueryFilters()
                    .SingleOrDefaultAsync(
                        x => x.OrganizationId == delivery.OrganizationId
                             && x.ChannelId == channel.Id
                             && x.EntityType == PublishEntityTypes.Variant
                             && x.ExternalId == movement.VariantExternalId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (map is null)
                {
                    errors.Add($"Unmapped variant {movement.VariantExternalId}.");
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
                    var units = ApplyMovement(inventory, movement, now);
                    if (units > 0)
                    {
                        applied++;
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"{movement.VariantExternalId}: {exception.Message}");
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

    private static int ApplyMovement(InventoryLevel inventory, InventoryMovement movement, DateTimeOffset now) =>
        movement.Action switch
        {
            InventoryWebhookAction.Reserve =>
                ApplyReserve(inventory, movement.Quantity, now),
            InventoryWebhookAction.Release =>
                inventory.ReleaseUpTo(movement.Quantity, "shopify-release", now),
            InventoryWebhookAction.Commit =>
                inventory.CommitUpTo(movement.Quantity, "shopify-commit", now),
            InventoryWebhookAction.Restock =>
                ApplyRestock(inventory, movement.Quantity, now),
            _ => 0
        };

    private static int ApplyReserve(InventoryLevel inventory, int quantity, DateTimeOffset now)
    {
        inventory.Reserve(quantity, "shopify-reserve", now);
        return quantity;
    }

    private static int ApplyRestock(InventoryLevel inventory, int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        inventory.Adjust(quantity, "shopify-restock", now);
        return quantity;
    }
}

internal enum InventoryWebhookAction
{
    Unsupported = 0,
    Reserve = 1,
    Release = 2,
    Commit = 3,
    Restock = 4
}

internal sealed record InventoryMovement(
    string VariantExternalId,
    int Quantity,
    InventoryWebhookAction Action);

public enum ShopifyLineQuantityMode
{
    Ordered = 0,
    FulfillableOrOrdered = 1
}

public static class ShopifyOrderPayload
{
    public sealed record LineItem(string VariantExternalId, int Quantity);

    public static IReadOnlyList<LineItem> ParseOrderLines(string payloadJson, ShopifyLineQuantityMode mode)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("line_items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return ParseLineItemArray(items, mode);
    }

    internal static IReadOnlyList<InventoryMovement> ParseRefundMovements(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("refund_line_items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var movements = new List<InventoryMovement>();
        foreach (var item in items.EnumerateArray())
        {
            var quantity = ReadInt(item, "quantity");
            if (quantity <= 0)
            {
                continue;
            }

            var restockType = item.TryGetProperty("restock_type", out var restockEl)
                              && restockEl.ValueKind == JsonValueKind.String
                ? restockEl.GetString()?.Trim().ToLowerInvariant()
                : null;

            var action = restockType switch
            {
                "cancel" => InventoryWebhookAction.Release,
                "return" => InventoryWebhookAction.Restock,
                "no_restock" => InventoryWebhookAction.Unsupported,
                _ => InventoryWebhookAction.Unsupported
            };
            if (action == InventoryWebhookAction.Unsupported)
            {
                continue;
            }

            if (!item.TryGetProperty("line_item", out var lineItem) || lineItem.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var externalId = ReadVariantExternalId(lineItem);
            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            movements.Add(new InventoryMovement(externalId, quantity, action));
        }

        return movements;
    }

    private static List<LineItem> ParseLineItemArray(JsonElement items, ShopifyLineQuantityMode mode)
    {
        var lines = new List<LineItem>();
        foreach (var item in items.EnumerateArray())
        {
            var quantity = mode == ShopifyLineQuantityMode.FulfillableOrOrdered
                ? ReadFulfillableOrOrderedQuantity(item)
                : ReadInt(item, "quantity");
            if (quantity <= 0)
            {
                continue;
            }

            var externalId = ReadVariantExternalId(item);
            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            lines.Add(new LineItem(externalId, quantity));
        }

        return lines;
    }

    private static int ReadFulfillableOrOrderedQuantity(JsonElement item)
    {
        if (item.TryGetProperty("fulfillable_quantity", out _))
        {
            return ReadInt(item, "fulfillable_quantity");
        }

        return ReadInt(item, "quantity");
    }

    private static int ReadInt(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var qtyEl))
        {
            return 0;
        }

        return qtyEl.ValueKind switch
        {
            JsonValueKind.Number => qtyEl.TryGetInt32(out var n) ? n : 0,
            JsonValueKind.String => int.TryParse(qtyEl.GetString(), out var q) ? q : 0,
            _ => 0
        };
    }

    private static string? ReadVariantExternalId(JsonElement item)
    {
        if (item.TryGetProperty("admin_graphql_api_id", out var gql) &&
            gql.ValueKind == JsonValueKind.String &&
            gql.GetString() is { Length: > 0 } gqlId &&
            gqlId.Contains("ProductVariant", StringComparison.Ordinal))
        {
            return gqlId;
        }

        if (item.TryGetProperty("variant_id", out var variantIdEl))
        {
            var numeric = variantIdEl.ValueKind switch
            {
                JsonValueKind.Number => variantIdEl.GetInt64().ToString(CultureInfo.InvariantCulture),
                JsonValueKind.String => variantIdEl.GetString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(numeric))
            {
                return $"gid://shopify/ProductVariant/{numeric}";
            }
        }

        return null;
    }
}
