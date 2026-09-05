using System.Data;
using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Application.Commerce;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class CommerceStore(AppDbContext database, IOrganizationContext organization, TimeProvider timeProvider) : ICommerceStore
{
    public async Task<IReadOnlyList<Customer>> CustomersAsync(CancellationToken token) => await database.Customers.OrderBy(x => x.Name).ToArrayAsync(token);
    public Task<Customer?> CustomerAsync(Guid id, CancellationToken token) => database.Customers.SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<CustomerAddress>> AddressesAsync(CancellationToken token) => await database.CustomerAddresses.ToArrayAsync(token);
    public async Task<IReadOnlyList<CustomerPreference>> PreferencesAsync(CancellationToken token) => await database.CustomerPreferences.ToArrayAsync(token);
    public async Task<IReadOnlyList<CustomerDietaryRestriction>> RestrictionsAsync(CancellationToken token) => await database.CustomerDietaryRestrictions.ToArrayAsync(token);
    public async Task<IReadOnlyList<CommercialPlan>> PlansAsync(CancellationToken token) => await database.CommercialPlans.OrderBy(x => x.Name).ToArrayAsync(token);
    public Task<CommercialPlan?> PlanAsync(Guid id, CancellationToken token) => database.CommercialPlans.SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<CommercialPlanOffer>> PlanOffersAsync(CancellationToken token) => await database.CommercialPlanOffers.ToArrayAsync(token);
    public async Task<IReadOnlyList<CatalogOffer>> OffersAsync(CancellationToken token) => await database.CatalogOffers.ToArrayAsync(token);
    public async Task<IReadOnlyList<PlanAcquisition>> AcquisitionsAsync(CancellationToken token) => await database.PlanAcquisitions.Include("_movements").OrderByDescending(x => x.AcquiredOn).ToArrayAsync(token);
    public Task<PlanAcquisition?> AcquisitionAsync(Guid id, CancellationToken token) => database.PlanAcquisitions.Include("_movements").SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<OrderCharge>> ChargesAsync(CancellationToken token) => await database.OrderCharges.OrderByDescending(x => x.CreatedAt).ToArrayAsync(token);
    public async Task<IReadOnlyList<Order>> OrdersAsync(CancellationToken token) => await database.Orders.ToArrayAsync(token);
    public async Task<IReadOnlyList<Payment>> PaymentsAsync(CancellationToken token) => await database.Payments.OrderByDescending(x => x.CreatedAt).ToArrayAsync(token);
    public async Task<IReadOnlyList<PaymentAllocation>> AllocationsAsync(CancellationToken token) => await database.PaymentAllocations.ToArrayAsync(token);
    public async Task<IReadOnlyList<FinancialCreditMovement>> FinancialMovementsAsync(CancellationToken token) => await database.FinancialCreditMovements.OrderByDescending(x => x.OccurredAt).ToArrayAsync(token);
    public void Add(object entity) => database.Add(entity);
    public void RemoveRange(IEnumerable<object> entities) => database.RemoveRange(entities);
    public Task SaveAsync(CancellationToken token) => database.SaveChangesAsync(token);

    public async Task<Payment> RegisterPaymentAsync(PaymentInput input, Guid actorId, string idempotencyKey, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var repeated = await database.Payments.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, token);
        if (repeated is not null)
        {
            var persisted = await database.PaymentAllocations.Where(x => x.PaymentId == repeated.Id).ToArrayAsync(token);
            var sameAllocations = persisted.Length == input.Allocations.Count
                && input.Allocations.All(x => persisted.Any(p => p.ChargeId == x.ChargeId && p.Amount == decimal.Round(x.Amount, 2)));
            if (repeated.CustomerId != input.CustomerId || repeated.Amount != decimal.Round(input.Amount, 2)
                || repeated.ReceivedOn != input.ReceivedOn || repeated.Method != input.Method
                || repeated.Reference != (string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim()) || !sameAllocations)
                throw new ConflictException("A chave de idempotência já foi usada com outro pagamento.");
            return repeated;
        }
        var customer = await database.Customers.SingleOrDefaultAsync(x => x.Id == input.CustomerId, token)
            ?? throw new ResourceNotFoundException("Cliente não encontrado.");
        var allocations = input.Allocations.Where(x => x.Amount > 0).ToArray();
        if (allocations.Length == 0 || allocations.GroupBy(x => x.ChargeId).Any(x => x.Count() > 1))
            throw new DomainException("Informe alocações válidas e sem duplicidade.");
        var chargeIds = allocations.Select(x => x.ChargeId).ToArray();
        var charges = await database.OrderCharges.Where(x => chargeIds.Contains(x.Id)).ToArrayAsync(token);
        var orders = await database.Orders.Where(x => charges.Select(c => c.OrderId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, token);
        var previous = await database.PaymentAllocations.Where(x => chargeIds.Contains(x.ChargeId)).GroupBy(x => x.ChargeId)
            .Select(x => new { ChargeId = x.Key, Amount = x.Sum(a => a.Amount) }).ToDictionaryAsync(x => x.ChargeId, x => x.Amount, token);
        foreach (var allocation in allocations)
        {
            var charge = charges.SingleOrDefault(x => x.Id == allocation.ChargeId) ?? throw new DomainException("Cobrança não encontrada.");
            if (orders[charge.OrderId].CustomerId != customer.Id || charge.Status != OrderChargeStatus.Pending || allocation.Amount > charge.Amount - previous.GetValueOrDefault(charge.Id))
                throw new DomainException("A cobrança não pertence ao cliente ou não possui saldo suficiente.");
        }
        var total = allocations.Sum(x => x.Amount);
        if (input.Amount <= 0 || total > input.Amount) throw new DomainException("O total alocado não pode superar o pagamento.");
        var payment = Payment.Create(organization.OrganizationId, customer.Id, customer.Name, input.Amount,
            input.ReceivedOn, input.Method, input.Reference, actorId, timeProvider.GetUtcNow(), idempotencyKey);
        database.Payments.Add(payment);
        foreach (var allocation in allocations) database.PaymentAllocations.Add(PaymentAllocation.Create(organization.OrganizationId, payment.Id, allocation.ChargeId, allocation.Amount));
        var surplus = input.Amount - total;
        if (surplus > 0) database.FinancialCreditMovements.Add(FinancialCreditMovement.GrantFromPayment(
            organization.OrganizationId, customer.Id, payment.Id, surplus, actorId, timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(token); await transaction.CommitAsync(token); return payment;
    }
}
