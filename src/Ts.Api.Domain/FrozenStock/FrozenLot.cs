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
        string idempotencyKey,
        string producibleNameSnapshot,
        string presentationSnapshot)
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
        ProducibleNameSnapshot = producibleNameSnapshot;
        PresentationSnapshot = presentationSnapshot;
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
    public string ProducibleNameSnapshot { get; private set; } = string.Empty;
    public string PresentationSnapshot { get; private set; } = string.Empty;
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
        string idempotencyKey,
        string producibleNameSnapshot = "Item produzível",
        string presentationSnapshot = "Apresentação")
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

        if (string.IsNullOrWhiteSpace(producibleNameSnapshot)
            || string.IsNullOrWhiteSpace(presentationSnapshot))
        {
            throw new DomainException("O nome e a apresentação vigentes são obrigatórios no snapshot do lote.");
        }

        var lot = new FrozenLot(
            Guid.NewGuid(),
            organizationId,
            frozenConfigurationId,
            manufacturedOn,
            producedQuantity,
            recordedBy,
            recordedAt,
            idempotencyKey.Trim(),
            producibleNameSnapshot.Trim(),
            presentationSnapshot.Trim());

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

    public void ReverseOrderExit(
        Guid orderId,
        Guid orderItemId,
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason)
    {
        if (orderId == Guid.Empty || orderItemId == Guid.Empty || actorId == Guid.Empty || quantity <= 0)
        {
            throw new DomainException("Pedido, item, quantidade e responsável são obrigatórios no estorno de estoque.");
        }

        var origin = $"Order:{orderId:N}:{orderItemId:N}";
        var exited = _movements
            .Where(item => item.Type == StockMovementType.OrderExit && item.Origin == origin)
            .Sum(item => item.Quantity);
        var reversed = _movements
            .Where(item => item.Type == StockMovementType.OrderReversal && item.Origin == origin)
            .Sum(item => item.Quantity);
        if (quantity > exited - reversed)
        {
            throw new DomainException("O estorno excede a saída deste item do pedido no lote.");
        }

        _movements.Add(FrozenStockMovement.CreateOrderReversal(
            OrganizationId, Id, orderId, orderItemId, quantity, actorId, occurredAt, reason));
    }

    public FrozenStockMovement AdjustStock(
        int signedQuantity,
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason,
        string idempotencyKey)
    {
        ValidateManualMovement(actorId, reason, idempotencyKey);
        if (signedQuantity == 0 || Balance + signedQuantity < 0)
        {
            throw new DomainException("O ajuste deve ser diferente de zero e não pode deixar o saldo negativo.");
        }

        var movement = FrozenStockMovement.CreateManualAdjustment(
            OrganizationId, Id, signedQuantity, actorId, occurredAt, reason, idempotencyKey.Trim());
        _movements.Add(movement);
        return movement;
    }

    public FrozenStockMovement DisposeExpiredStock(
        int quantity,
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason,
        string idempotencyKey)
    {
        ValidateManualMovement(actorId, reason, idempotencyKey);
        if (quantity <= 0 || quantity > Balance)
        {
            throw new DomainException("O descarte deve ser positivo e não pode superar o saldo físico.");
        }

        var movement = FrozenStockMovement.CreateExpirationDisposal(
            OrganizationId, Id, quantity, actorId, occurredAt, reason, idempotencyKey.Trim());
        _movements.Add(movement);
        return movement;
    }

    private static void ValidateManualMovement(Guid actorId, string reason, string idempotencyKey)
    {
        if (actorId == Guid.Empty)
        {
            throw new DomainException("O responsável pela movimentação é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw new DomainException("O motivo da movimentação deve possuir entre 1 e 500 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
        {
            throw new DomainException("A chave de idempotência deve possuir entre 1 e 200 caracteres.");
        }
    }
}
