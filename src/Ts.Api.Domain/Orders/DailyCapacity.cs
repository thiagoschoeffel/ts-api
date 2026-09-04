using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class DailyCapacity : ITenantOwned
{
    private DailyCapacity() { }

    private DailyCapacity(Guid organizationId, DateOnly operationalDate, int totalUnits)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (totalUnits <= 0)
        {
            throw new DomainException("A capacidade total deve ser positiva.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OperationalDate = operationalDate;
        TotalUnits = totalUnits;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateOnly OperationalDate { get; private set; }
    public int TotalUnits { get; private set; }
    public int ReservedUnits { get; private set; }
    public int AvailableUnits => TotalUnits - ReservedUnits;

    public static DailyCapacity Create(Guid organizationId, DateOnly operationalDate, int totalUnits) =>
        new(organizationId, operationalDate, totalUnits);

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A quantidade reservada deve ser positiva.");
        }

        if (quantity > AvailableUnits)
        {
            throw new DomainException("A capacidade diária disponível é insuficiente para confirmar o pedido.");
        }

        ReservedUnits += quantity;
    }
}
