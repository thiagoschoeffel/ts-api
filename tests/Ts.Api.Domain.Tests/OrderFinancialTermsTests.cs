using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Orders;

namespace Ts.Api.Domain.Tests;

public sealed class OrderFinancialTermsTests
{
    [Fact]
    public void DraftTerms_AreConsolidatedIntoConfirmationAndChargeDueDate()
    {
        var operationalDate = new DateOnly(2026, 9, 9);
        var dueDate = new DateOnly(2026, 9, 20);
        var order = Order.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), operationalDate,
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 20m)],
            financialTerms: new OrderFinancialTermsDefinition(
                "deferred", "pix", dueDate, 5m, 2m, "Fidelidade"));

        order.Confirm(Guid.NewGuid(), DateTimeOffset.UtcNow, "confirm-financial",
            discountAmount: order.DraftDiscountAmount,
            discountReason: order.DraftDiscountReason,
            deliveryFee: order.DraftDeliveryFee);

        var audit = Assert.Single(order.ConfirmationAudits);
        var charge = Assert.Single(order.Charges);
        Assert.Equal(23m, audit.AmountDue);
        Assert.Equal(23m, charge.Amount);
        Assert.Equal(dueDate, charge.DueOn);
    }

    [Fact]
    public void DraftTerms_RejectDiscountAboveOrderTotalWithFee()
    {
        var exception = Assert.Throws<DomainException>(() => Order.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 9),
            [new OrderItemDefinition(Guid.NewGuid(), OfferFulfillmentMode.DailyProduction, 1, 20m)],
            financialTerms: new OrderFinancialTermsDefinition(
                "cash", "pix", null, 0m, 21m, "Inválido")));

        Assert.Equal("As condições financeiras do pedido são inválidas.", exception.Message);
    }
}
