using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.FrozenStock;

public sealed class FrozenLot : ITenantOwned
{
    private readonly List<FrozenStockMovement> _movements = [];

    private FrozenLot() { }

    private FrozenLot(
        Guid id,
        Guid organizationId,
        Guid frozenConfigurationId,
        DateOnly manufacturedOn,
        int producedQuantity,
        Guid recordedBy,
        DateTimeOffset recordedAt,
        string idempotencyKey)
    {
        Id = id;
        OrganizationId = organizationId;
        FrozenConfigurationId = frozenConfigurationId;
        ManufacturedOn = manufacturedOn;
        ExpiresOn = FrozenShelfLifePolicy.CalculateExpiration(manufacturedOn);
        ProducedQuantity = producedQuantity;
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
        IdempotencyKey = idempotencyKey;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid FrozenConfigurationId { get; private set; }
    public DateOnly ManufacturedOn { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public int ProducedQuantity { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public IReadOnlyCollection<FrozenStockMovement> Movements => _movements.AsReadOnly();
    public int Balance => _movements.Sum(movement => movement.SignedQuantity);
    public bool IsSellableOn(DateOnly date) => Balance > 0 && ExpiresOn >= date;

    public static FrozenLot RegisterProduction(
        Guid organizationId,
        Guid frozenConfigurationId,
        DateOnly manufacturedOn,
        int producedQuantity,
        Guid recordedBy,
        DateTimeOffset recordedAt,
        string idempotencyKey)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A organização é obrigatória.");
        }

        if (frozenConfigurationId == Guid.Empty)
        {
            throw new DomainException("A configuração de congelado é obrigatória.");
        }

        if (producedQuantity <= 0)
        {
            throw new DomainException("A quantidade produzida deve ser positiva.");
        }

        if (recordedBy == Guid.Empty)
        {
            throw new DomainException("O responsável pelo registro é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }

        var lot = new FrozenLot(
            Guid.NewGuid(),
            organizationId,
            frozenConfigurationId,
            manufacturedOn,
            producedQuantity,
            recordedBy,
            recordedAt,
            idempotencyKey.Trim());

        lot._movements.Add(FrozenStockMovement.CreateProductionEntry(
            organizationId,
            lot.Id,
            producedQuantity,
            recordedBy,
            recordedAt));

        return lot;
    }

    public bool MatchesRegistration(
        Guid frozenConfigurationId,
        DateOnly manufacturedOn,
        int producedQuantity,
        Guid recordedBy) =>
        FrozenConfigurationId == frozenConfigurationId
        && ManufacturedOn == manufacturedOn
        && ProducedQuantity == producedQuantity
        && RecordedBy == recordedBy;

    public void RemoveForOrder(
        Guid orderId,
        Guid orderItemId,
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (orderId == Guid.Empty || orderItemId == Guid.Empty)
        {
            throw new DomainException("O pedido e o item do pedido são obrigatórios na saída de estoque.");
        }

        if (actorId == Guid.Empty)
        {
            throw new DomainException("O responsável pela saída de estoque é obrigatório.");
        }

        if (quantity <= 0 || quantity > Balance)
        {
            throw new DomainException("O lote não possui saldo suficiente para a saída solicitada.");
        }

        _movements.Add(FrozenStockMovement.CreateOrderExit(
            OrganizationId,
            Id,
            orderId,
            orderItemId,
            quantity,
            actorId,
            occurredAt));
    }
}
