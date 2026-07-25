using JerseyOs.Application;

namespace JerseyOs.Worker;

public sealed class WorkerCurrentRequest : ICurrentRequest
{
    public Guid? UserId => null;
    public Guid? OrganizationId => null;
    public string Actor => "worker";
    public string CorrelationId => Guid.NewGuid().ToString("N");
}
