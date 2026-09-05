using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class AuditEvent : ITenantOwned
{
    private AuditEvent() { }

    private AuditEvent(Guid organizationId, Guid actorId, string action, string resourceType,
        Guid resourceId, DateTimeOffset occurredAt, string correlationId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;

    public static AuditEvent Create(Guid organizationId, Guid actorId, string action,
        string resourceType, Guid resourceId, DateTimeOffset occurredAt, string correlationId)
    {
        if (organizationId == Guid.Empty || actorId == Guid.Empty || resourceId == Guid.Empty
            || string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(resourceType)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainException("Os dados da auditoria são obrigatórios.");
        }

        return new AuditEvent(organizationId, actorId, action.Trim(), resourceType.Trim(), resourceId,
            occurredAt, correlationId.Trim());
    }
}
