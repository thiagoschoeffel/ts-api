using Ts.Api.Domain.Common;
using Ts.Api.Domain.Production;

namespace Ts.Api.Domain.Customers;

public sealed class CustomerDietaryRestriction : ITenantOwned
{
    private CustomerDietaryRestriction() { }

    private CustomerDietaryRestriction(Guid organizationId, Guid customerId, string marker)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CustomerId = customerId;
        Marker = marker;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Marker { get; private set; } = string.Empty;

    public static CustomerDietaryRestriction Create(Guid organizationId, Guid customerId, string marker)
    {
        var normalized = ProducibleComponent.NormalizeMarker(marker);
        if (organizationId == Guid.Empty || customerId == Guid.Empty || normalized.Length is 0 or > 80)
        {
            throw new DomainException("Organização, cliente e marcador de restrição válido são obrigatórios.");
        }

        return new CustomerDietaryRestriction(organizationId, customerId, normalized);
    }
}
