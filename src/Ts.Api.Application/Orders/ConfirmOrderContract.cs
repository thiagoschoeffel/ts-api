using Ts.Api.Domain.Orders;

namespace Ts.Api.Application.Orders;

public sealed record ConfirmOrderCommand(
    Guid OrderId,
    Guid ActorId,
    string IdempotencyKey,
    long ExpectedVersion,
    IReadOnlyCollection<PlanCreditRequest>? PlanCredits = null,
    decimal DiscountAmount = 0,
    string? DiscountReason = null,
    decimal DeliveryFee = 0,
    decimal FinancialCreditAmount = 0);

public sealed record PlanCreditRequest(Guid OrderItemId, int Quantity);

public sealed record ConfirmOrderResult(
    Guid OrderId,
    OrderStatus Status,
    long Version,
    IReadOnlyCollection<FrozenAllocation> FrozenAllocations,
    IReadOnlyCollection<ComponentSnapshot> Components,
    IReadOnlyCollection<PlanCreditAllocationResult> PlanCredits,
    ConfirmationFinancialSummary Financial);

public sealed record FrozenAllocation(
    Guid OrderItemId,
    Guid FrozenConfigurationId,
    Guid FrozenLotId,
    int Quantity);

public sealed record ComponentSnapshot(
    Guid OrderItemId, Guid CompositionId, int CompositionVersion, string Name,
    decimal QuantityPerUnit, decimal TotalQuantity, string MeasurementUnit,
    IReadOnlyCollection<string> DietaryMarkers);

public sealed record PlanCreditAllocationResult(
    Guid OrderItemId, Guid AcquisitionId, string PlanName, int Quantity, decimal CoveredAmount);

public sealed record ConfirmationFinancialSummary(
    decimal Subtotal, decimal PlanCreditCoveredAmount, decimal DiscountAmount,
    string? DiscountReason, decimal DeliveryFee, decimal FinancialCreditApplied, decimal AmountDue);
