namespace JerseyOs.SharedKernel;

public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; set; }
    string CreatedBy { get; set; }
    DateTimeOffset ModifiedAtUtc { get; set; }
    string ModifiedBy { get; set; }
    byte[] RowVersion { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}

public interface IOrganizationScoped
{
    Guid OrganizationId { get; }
}

public abstract class AuditableEntity : Entity, IAuditableEntity
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset ModifiedAtUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}

public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
