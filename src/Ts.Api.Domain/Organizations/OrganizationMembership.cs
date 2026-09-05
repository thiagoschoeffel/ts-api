using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Organizations;

public sealed class OrganizationMembership : ITenantOwned
{
    private OrganizationMembership() { }

    private OrganizationMembership(Guid organizationId, Guid userId, OrganizationRole role)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        IsActive = true;
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public static OrganizationMembership Create(Guid organizationId, Guid userId, OrganizationRole role)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainException("Organização e usuário são obrigatórios na associação.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new DomainException("O papel do usuário na organização é inválido.");
        }

        return new OrganizationMembership(organizationId, userId, role);
    }
}

public enum OrganizationRole
{
    Owner = 1,
    Administrator = 2,
    Operator = 3,
    DeliveryDriver = 4,
}
