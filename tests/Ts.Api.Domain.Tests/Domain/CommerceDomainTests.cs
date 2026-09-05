using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.Plans;

namespace Ts.Api.Domain.Tests.Domain;

public sealed class CommerceDomainTests
{
    [Fact]
    public void Customer_update_requires_the_current_version()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Maria Silva", "(11) 99999-0000");
        customer.Update("Maria da Silva", "11999990000", true, null, null, "À vista", "Pix", 1);

        Assert.Equal(2, customer.Version);
        Assert.ThrowsAny<Exception>(() => customer.Update("Outro nome", "11999990000", true, null, null, null, null, 1));
    }

    [Fact]
    public void Plan_balance_is_derived_from_movements_and_adjustments_cannot_make_it_negative()
    {
        var firstOffer = Guid.NewGuid();
        var secondOffer = Guid.NewGuid();
        var acquisition = PlanAcquisition.CreateFromPlan(Guid.NewGuid(), Guid.NewGuid(), "Maria",
            Guid.NewGuid(), [firstOffer, secondOffer], "Plano almoço", "Um almoço", 5, 100m, 20m,
            new DateOnly(2026, 9, 5), null, DateTimeOffset.UtcNow);

        acquisition.Adjust(-2, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(3, acquisition.Balance);
        Assert.True(acquisition.IsEligibleFor(firstOffer));
        Assert.True(acquisition.IsEligibleFor(secondOffer));
        Assert.ThrowsAny<Exception>(() => acquisition.Adjust(-4, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Payment_surplus_is_a_positive_financial_credit_with_payment_origin()
    {
        var paymentId = Guid.NewGuid();
        var movement = FinancialCreditMovement.GrantFromPayment(Guid.NewGuid(), Guid.NewGuid(),
            paymentId, 15.50m, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(15.50m, movement.SignedAmount);
        Assert.Equal(paymentId, movement.PaymentId);
        Assert.Null(movement.OrderId);
    }
}
