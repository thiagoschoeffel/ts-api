using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public static class SaasEntitlements
{
    public const string BusinessAccess = "business.access";
    public const string Attendance = "attendance";
    public const string Catalog = "catalog";
    public const string Commerce = "commerce";
    public const string Logistics = "logistics";
    public const string Operations = "operations";
}

public sealed class SaasPlanVersion
{
    private SaasPlanVersion() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool IsAvailable { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SaasPlanVersion Create(string code, string name, int version,
        bool isAvailable, DateTimeOffset now, Guid? id = null)
    {
        var normalizedCode = Required(code, 60, "O código do plano SaaS é obrigatório.").ToLowerInvariant();
        var normalizedName = Required(name, 120, "O nome do plano SaaS é obrigatório.");
        if (version < 1) throw new DomainException("A versão do plano SaaS deve ser positiva.");
        return new SaasPlanVersion { Id = id ?? Guid.NewGuid(), Code = normalizedCode,
            Name = normalizedName, Version = version, IsAvailable = isAvailable, CreatedAt = now };
    }

    private static string Required(string? value, int maximumLength, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength) throw new DomainException(message);
        return normalized;
    }
}

public sealed class SaasPlanEntitlement
{
    private SaasPlanEntitlement() { }
    public Guid PlanVersionId { get; private set; }
    public string Code { get; private set; } = string.Empty;

    public static SaasPlanEntitlement Create(Guid planVersionId, string code)
    {
        if (planVersionId == Guid.Empty) throw new DomainException("A versão do plano SaaS é obrigatória.");
        var normalized = code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 100) throw new DomainException("A habilitação do plano SaaS é inválida.");
        return new SaasPlanEntitlement { PlanVersionId = planVersionId, Code = normalized };
    }
}

public sealed class OrganizationSaasSubscription
{
    private OrganizationSaasSubscription() { }
    public Guid OrganizationId { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid AssignedBy { get; private set; }
    public long Version { get; private set; }

    public static OrganizationSaasSubscription Create(Guid organizationId, Guid planVersionId,
        Guid assignedBy, DateTimeOffset now) => new()
        {
            OrganizationId = Required(organizationId), PlanVersionId = Required(planVersionId),
            AssignedBy = Required(assignedBy), AssignedAt = now, Version = 1,
        };

    public void Assign(Guid planVersionId, Guid assignedBy, DateTimeOffset now, long expectedVersion)
    {
        if (Version != expectedVersion) throw new DomainException("A assinatura SaaS foi alterada.");
        if (PlanVersionId == planVersionId) return;
        PlanVersionId = Required(planVersionId); AssignedBy = Required(assignedBy);
        AssignedAt = now; Version++;
    }

    private static Guid Required(Guid value)
    {
        if (value == Guid.Empty) throw new DomainException("A referência da assinatura SaaS é obrigatória.");
        return value;
    }
}
