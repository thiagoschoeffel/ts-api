using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class PlatformOperatorGrant
{
    private PlatformOperatorGrant() { }

    private PlatformOperatorGrant(Guid userId, PlatformOperatorProfile profile,
        string operationalActor, string reason, DateTimeOffset grantedAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Profile = profile;
        OperationalActor = operationalActor;
        Reason = reason;
        GrantedAt = grantedAt;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public PlatformOperatorProfile Profile { get; private set; }
    public string OperationalActor { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public long Version { get; private set; }

    public bool IsActiveAt(DateTimeOffset instant) => RevokedAt is null
        && (!ExpiresAt.HasValue || ExpiresAt > instant);

    public static PlatformOperatorGrant Create(Guid userId, PlatformOperatorProfile profile,
        string operationalActor, string reason, DateTimeOffset grantedAt)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(profile)
            || string.IsNullOrWhiteSpace(operationalActor) || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Usuário, perfil, ator operacional e justificativa são obrigatórios.");
        }

        return new PlatformOperatorGrant(userId, profile, operationalActor.Trim(), reason.Trim(), grantedAt);
    }
}

public enum PlatformOperatorProfile
{
    PlatformAdministrator = 1,
    PlatformOnboardingOperator = 2,
    PlatformSupportReader = 3,
}

public sealed class PlatformAuditEvent
{
    private PlatformAuditEvent() { }

    private PlatformAuditEvent(Guid? actorUserId, string actorKind, string action,
        string targetType, Guid targetId, string result, string reason,
        DateTimeOffset occurredAt, string correlationId)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        ActorKind = actorKind;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Result = result;
        Reason = reason;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorKind { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;

    public static PlatformAuditEvent Create(Guid? actorUserId, string actorKind, string action,
        string targetType, Guid targetId, string result, string reason,
        DateTimeOffset occurredAt, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(actorKind) || string.IsNullOrWhiteSpace(action)
            || string.IsNullOrWhiteSpace(targetType) || targetId == Guid.Empty
            || string.IsNullOrWhiteSpace(result) || string.IsNullOrWhiteSpace(reason)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainException("Os dados da auditoria global são obrigatórios.");
        }

        return new PlatformAuditEvent(actorUserId, actorKind.Trim(), action.Trim(), targetType.Trim(),
            targetId, result.Trim(), reason.Trim(), occurredAt, correlationId.Trim());
    }
}
