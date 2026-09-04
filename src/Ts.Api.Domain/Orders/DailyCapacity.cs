using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class DailyCapacity : ITenantOwned
{
    private DailyCapacity() { }

    private DailyCapacity(
        Guid organizationId,
        DateOnly operationalDate,
        int totalUnits,
        string idempotencyKey)
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
        LastConfigurationIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateOnly OperationalDate { get; private set; }
    public int TotalUnits { get; private set; }
    public int ReservedUnits { get; private set; }
    public long Version { get; private set; }
    public string LastConfigurationIdempotencyKey { get; private set; } = string.Empty;
    public int AvailableUnits => TotalUnits - ReservedUnits;

    public static DailyCapacity Create(
        Guid organizationId,
        DateOnly operationalDate,
        int totalUnits,
        string? idempotencyKey = null) =>
        new(
            organizationId,
            operationalDate,
            totalUnits,
            idempotencyKey is null ? $"internal-{Guid.NewGuid():N}" : idempotencyKey);

    public void Configure(int totalUnits, string idempotencyKey)
    {
        if (totalUnits <= 0)
        {
            throw new DomainException("A capacidade total deve ser positiva.");
        }

        if (totalUnits < ReservedUnits)
        {
            throw new DomainException("A capacidade total não pode ser menor que a capacidade já reservada.");
        }

        TotalUnits = totalUnits;
        LastConfigurationIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        Version++;
    }

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
        Version++;
    }

    private static string NormalizeIdempotencyKey(string idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        return normalized;
    }
}
