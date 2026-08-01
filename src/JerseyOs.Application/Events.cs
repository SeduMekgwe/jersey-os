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
            }
        }
    }
}
