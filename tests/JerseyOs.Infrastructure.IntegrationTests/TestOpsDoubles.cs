using JerseyOs.Application;

namespace JerseyOs.Infrastructure.IntegrationTests;

internal sealed class NoOpAuditRecorder : IAuditRecorder
{
    public void Record(Guid organizationId, string action, string entityType, string entityId, object? data = null)
    {
    }
}

internal sealed class NoOpNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(Guid organizationId, string kind, object payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
