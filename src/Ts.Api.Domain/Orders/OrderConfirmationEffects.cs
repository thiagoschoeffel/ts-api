using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Orders;

public sealed class OrderItemComponent : ITenantOwned
{
    private OrderItemComponent() { }
    internal OrderItemComponent(
        Guid organizationId, Guid orderId, Guid orderItemId, Guid compositionId,
        int compositionVersion, string name, decimal quantityPerUnit,
        decimal totalQuantity, string measurementUnit, string dietaryMarkers)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        CompositionId = compositionId;
        CompositionVersion = compositionVersion;
        Name = name;
        QuantityPerUnit = quantityPerUnit;
        TotalQuantity = totalQuantity;
        MeasurementUnit = measurementUnit;
        DietaryMarkers = dietaryMarkers;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid CompositionId { get; private set; }
    public int CompositionVersion { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal QuantityPerUnit { get; private set; }
    public decimal TotalQuantity { get; private set; }
    public string MeasurementUnit { get; private set; } = string.Empty;
    public string DietaryMarkers { get; private set; } = string.Empty;
}

public sealed class OrderPlanCreditAllocation : ITenantOwned
{
    private OrderPlanCreditAllocation() { }
    internal OrderPlanCreditAllocation(
        Guid organizationId, Guid orderId, Guid orderItemId, Guid acquisitionId,
        string planName, int quantity, decimal coveredAmount)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        AcquisitionId = acquisitionId;
        PlanName = planName;
        Quantity = quantity;
        CoveredAmount = coveredAmount;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid AcquisitionId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal CoveredAmount { get; private set; }
}

public sealed class OrderConfirmationAudit : ITenantOwned
{
    private OrderConfirmationAudit() { }
    internal OrderConfirmationAudit(
        Guid organizationId, Guid orderId, Guid actorId, DateTimeOffset occurredAt,
        string idempotencyKey, decimal subtotal, decimal planCreditCoveredAmount,
        decimal discountAmount, string? discountReason, decimal deliveryFee,
        decimal financialCreditApplied, decimal amountDue)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderId = orderId;
        ActorId = actorId;
        OccurredAt = occurredAt;
        IdempotencyKey = idempotencyKey;
        Subtotal = subtotal;
        PlanCreditCoveredAmount = planCreditCoveredAmount;
        DiscountAmount = discountAmount;
        DiscountReason = discountReason;
        DeliveryFee = deliveryFee;
        FinancialCreditApplied = financialCreditApplied;
        AmountDue = amountDue;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public decimal Subtotal { get; private set; }
    public decimal PlanCreditCoveredAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? DiscountReason { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public decimal FinancialCreditApplied { get; private set; }
    public decimal AmountDue { get; private set; }
}
