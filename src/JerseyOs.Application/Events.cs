using System.Text.Json;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using JerseyOs.SharedKernel;
using MediatR;

namespace JerseyOs.Application;

public interface IIntegrationEventPublisher
{
    void Enqueue(IntegrationEventEnvelope envelope);
}

public sealed record OrganizationCreatedNotification(
    OrganizationCreated DomainEvent,
    Guid OrganizationId,
    string CorrelationId) : INotification;

public sealed record ProductCreatedNotification(
    ProductCreated DomainEvent,
    Guid OrganizationId,
    string CorrelationId) : INotification;

public sealed record ProductUpdatedNotification(
    ProductUpdated DomainEvent,
    Guid OrganizationId,
    string CorrelationId) : INotification;

public sealed record ProductArchivedNotification(
    ProductArchived DomainEvent,
    Guid OrganizationId,
    string CorrelationId) : INotification;

public sealed record InventoryAdjustedNotification(
    InventoryAdjusted DomainEvent,
    Guid OrganizationId,
    string CorrelationId) : INotification;

public sealed class OrganizationCreatedHandler(IIntegrationEventPublisher publisher, TimeProvider time)
    : INotificationHandler<OrganizationCreatedNotification>
{
    public const int SchemaVersion = 1;

    public Task Handle(OrganizationCreatedNotification notification, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            organizationId = notification.DomainEvent.OrganizationId
        });
        publisher.Enqueue(new IntegrationEventEnvelope(
            EventId: Guid.NewGuid(),
            EventType: "jerseyos.organization.created",
            SchemaVersion: SchemaVersion,
            OrganizationId: notification.OrganizationId,
            CorrelationId: notification.CorrelationId,
            CausationId: notification.DomainEvent.OrganizationId.ToString("N"),
            OccurredAtUtc: notification.DomainEvent.OccurredAtUtc == default
                ? time.GetUtcNow()
                : notification.DomainEvent.OccurredAtUtc,
            Payload: payload));
        return Task.CompletedTask;
    }
}

public sealed class ProductCreatedHandler(IIntegrationEventPublisher publisher, TimeProvider time)
    : INotificationHandler<ProductCreatedNotification>
{
    public const int SchemaVersion = 1;

    public Task Handle(ProductCreatedNotification notification, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            productId = notification.DomainEvent.ProductId,
            organizationId = notification.DomainEvent.OrganizationId
        });
        publisher.Enqueue(new IntegrationEventEnvelope(
            EventId: Guid.NewGuid(),
            EventType: "jerseyos.product.created",
            SchemaVersion: SchemaVersion,
            OrganizationId: notification.OrganizationId,
            CorrelationId: notification.CorrelationId,
            CausationId: notification.DomainEvent.ProductId.ToString("N"),
            OccurredAtUtc: notification.DomainEvent.OccurredAtUtc == default
                ? time.GetUtcNow()
                : notification.DomainEvent.OccurredAtUtc,
            Payload: payload));
        return Task.CompletedTask;
    }
}

public sealed class ProductUpdatedHandler(IIntegrationEventPublisher publisher, TimeProvider time)
    : INotificationHandler<ProductUpdatedNotification>
{
    public const int SchemaVersion = 1;

    public Task Handle(ProductUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            productId = notification.DomainEvent.ProductId,
            organizationId = notification.DomainEvent.OrganizationId
        });
        publisher.Enqueue(new IntegrationEventEnvelope(
            EventId: Guid.NewGuid(),
            EventType: "jerseyos.product.updated",
            SchemaVersion: SchemaVersion,
            OrganizationId: notification.OrganizationId,
            CorrelationId: notification.CorrelationId,
            CausationId: notification.DomainEvent.ProductId.ToString("N"),
            OccurredAtUtc: notification.DomainEvent.OccurredAtUtc == default
                ? time.GetUtcNow()
                : notification.DomainEvent.OccurredAtUtc,
            Payload: payload));
        return Task.CompletedTask;
    }
}

public sealed class ProductArchivedHandler(IIntegrationEventPublisher publisher, TimeProvider time)
    : INotificationHandler<ProductArchivedNotification>
{
    public const int SchemaVersion = 1;

    public Task Handle(ProductArchivedNotification notification, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            productId = notification.DomainEvent.ProductId,
            organizationId = notification.DomainEvent.OrganizationId
        });
        publisher.Enqueue(new IntegrationEventEnvelope(
            EventId: Guid.NewGuid(),
            EventType: "jerseyos.product.archived",
            SchemaVersion: SchemaVersion,
            OrganizationId: notification.OrganizationId,
            CorrelationId: notification.CorrelationId,
            CausationId: notification.DomainEvent.ProductId.ToString("N"),
            OccurredAtUtc: notification.DomainEvent.OccurredAtUtc == default
                ? time.GetUtcNow()
                : notification.DomainEvent.OccurredAtUtc,
            Payload: payload));
        return Task.CompletedTask;
    }
}

public sealed class InventoryAdjustedHandler(IIntegrationEventPublisher publisher, TimeProvider time)
    : INotificationHandler<InventoryAdjustedNotification>
{
    public const int SchemaVersion = 1;

    public Task Handle(InventoryAdjustedNotification notification, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            variantId = notification.DomainEvent.VariantId,
            organizationId = notification.DomainEvent.OrganizationId,
            deltaOnHand = notification.DomainEvent.DeltaOnHand,
            onHand = notification.DomainEvent.OnHand,
            reserved = notification.DomainEvent.Reserved,
            reason = notification.DomainEvent.Reason
        });
        publisher.Enqueue(new IntegrationEventEnvelope(
            EventId: Guid.NewGuid(),
            EventType: "jerseyos.inventory.adjusted",
            SchemaVersion: SchemaVersion,
            OrganizationId: notification.OrganizationId,
            CorrelationId: notification.CorrelationId,
            CausationId: notification.DomainEvent.VariantId.ToString("N"),
            OccurredAtUtc: notification.DomainEvent.OccurredAtUtc == default
                ? time.GetUtcNow()
                : notification.DomainEvent.OccurredAtUtc,
            Payload: payload));
        return Task.CompletedTask;
    }
}

public static class DomainEventMapper
{
    public static IEnumerable<INotification> ToNotifications(
        IEnumerable<IDomainEvent> domainEvents, Guid organizationId, string correlationId)
    {
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case OrganizationCreated created:
                    yield return new OrganizationCreatedNotification(created, organizationId, correlationId);
                    break;
                case ProductCreated created:
                    yield return new ProductCreatedNotification(created, organizationId, correlationId);
                    break;
                case ProductUpdated updated:
                    yield return new ProductUpdatedNotification(updated, organizationId, correlationId);
                    break;
                case ProductArchived archived:
                    yield return new ProductArchivedNotification(archived, organizationId, correlationId);
                    break;
                case InventoryAdjusted adjusted:
                    yield return new InventoryAdjustedNotification(adjusted, organizationId, correlationId);
                    break;
            }
        }
    }
}
