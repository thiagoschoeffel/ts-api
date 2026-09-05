using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;

namespace Ts.Api.Application.Commerce;

public sealed record AddressInput(string Label, string Street, string? Number, string? Complement,
    string? Neighborhood, string? City, string? State, string? PostalCode, string? ReferencePoint);
public sealed record CustomerInput(string Name, string Phone, bool IsActive, string? Notes,
    string? PreferredDeliveryDriverId, string? PreferredPaymentCondition, string? PreferredPaymentMethod,
    IReadOnlyCollection<AddressInput> Addresses, IReadOnlyCollection<string> Preferences,
    IReadOnlyCollection<string> DietaryRestrictions, long? ExpectedVersion);
public sealed record PlanInput(string Name, string? Description, string BenefitDescription,
    IReadOnlyCollection<Guid> CompatibleOfferIds, int DefaultCredits, decimal DefaultPrice,
    int? ValidityDays, bool IsActive, long? ExpectedVersion);
public sealed record AcquisitionInput(Guid CustomerId, Guid PlanId, int Credits, decimal PaidAmount,
    DateOnly PurchasedOn, DateOnly? ExpiresOn);
public sealed record CreditAdjustmentInput(Guid AcquisitionId, Guid? MovementId, int Quantity, string Reason);
public sealed record FinancialAdjustmentInput(Guid CustomerId, decimal Amount, string Reason);
public sealed record PaymentAllocationInput(Guid ChargeId, decimal Amount);
public sealed record PaymentInput(Guid CustomerId, decimal Amount, DateOnly ReceivedOn, PaymentMethod Method,
    string? Reference, IReadOnlyCollection<PaymentAllocationInput> Allocations);

public sealed record AddressResult(Guid Id, string Label, string Street, string? Number, string? Complement,
    string? Neighborhood, string? City, string? State, string? PostalCode, string? ReferencePoint);
public sealed record CustomerResult(Guid Id, string Name, string Phone, bool IsActive, string? Notes,
    string? PreferredDeliveryDriverId, string? PreferredPaymentCondition, string? PreferredPaymentMethod,
    long Version, IReadOnlyCollection<AddressResult> Addresses, IReadOnlyCollection<string> Preferences,
    IReadOnlyCollection<string> DietaryRestrictions);
public sealed record PlanResult(Guid Id, string Name, string? Description, string BenefitDescription,
    IReadOnlyCollection<Guid> CompatibleOfferIds, IReadOnlyCollection<string> CompatibleOfferNames,
    int DefaultCredits, decimal DefaultPrice, int? ValidityDays, bool IsActive, long Version);
public sealed record AcquisitionResult(Guid Id, Guid CustomerId, string CustomerNameSnapshot, Guid? PlanId,
    string PlanNameSnapshot, string BenefitDescriptionSnapshot, IReadOnlyCollection<Guid> CompatibleOfferIds,
    Guid EligibleOfferId, int Quantity, int Balance, decimal PaidAmount,
    decimal BenefitAmountPerCredit, DateOnly PurchasedOn, DateOnly? ExpiresOn, DateTimeOffset CreatedAt);
public sealed record PlanMovementResult(Guid Id, Guid AcquisitionId, PlanCreditMovementType Type,
    int Quantity, int SignedQuantity, Guid? OrderId, Guid? OrderItemId, Guid? ActorId, DateTimeOffset OccurredAt);
public sealed record ChargeResult(Guid Id, Guid CustomerId, string CustomerNameSnapshot, Guid OrderId,
    decimal Amount, decimal AllocatedAmount, decimal Balance, DateOnly DueOn, DateTimeOffset CreatedAt, OrderChargeStatus Status);
public sealed record PaymentResult(Guid Id, Guid CustomerId, string CustomerNameSnapshot, decimal Amount,
    decimal AllocatedAmount, decimal FinancialCreditGenerated, DateOnly ReceivedOn, PaymentMethod Method,
    string? Reference, DateTimeOffset CreatedAt);
public sealed record AllocationResult(Guid Id, Guid PaymentId, Guid ChargeId, decimal Amount);
public sealed record FinancialMovementResult(Guid Id, Guid CustomerId, FinancialCreditMovementType Type,
    decimal Amount, decimal SignedAmount, string Reason, Guid? OrderId, Guid? PaymentId, Guid ActorId, DateTimeOffset OccurredAt);
public sealed record CommerceResult(IReadOnlyCollection<CustomerResult> Customers,
    IReadOnlyCollection<PlanResult> Plans, IReadOnlyCollection<AcquisitionResult> Acquisitions,
    IReadOnlyCollection<PlanMovementResult> PlanCreditMovements, IReadOnlyCollection<ChargeResult> Charges,
    IReadOnlyCollection<PaymentResult> Payments, IReadOnlyCollection<AllocationResult> PaymentAllocations,
    IReadOnlyCollection<FinancialMovementResult> FinancialCreditMovements);

public interface ICommerceStore
{
    Task<IReadOnlyList<Customer>> CustomersAsync(CancellationToken token);
    Task<Customer?> CustomerAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<CustomerAddress>> AddressesAsync(CancellationToken token);
    Task<IReadOnlyList<CustomerPreference>> PreferencesAsync(CancellationToken token);
    Task<IReadOnlyList<CustomerDietaryRestriction>> RestrictionsAsync(CancellationToken token);
    Task<IReadOnlyList<CommercialPlan>> PlansAsync(CancellationToken token);
    Task<CommercialPlan?> PlanAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<CommercialPlanOffer>> PlanOffersAsync(CancellationToken token);
    Task<IReadOnlyList<CatalogOffer>> OffersAsync(CancellationToken token);
    Task<IReadOnlyList<PlanAcquisition>> AcquisitionsAsync(CancellationToken token);
    Task<PlanAcquisition?> AcquisitionAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<OrderCharge>> ChargesAsync(CancellationToken token);
    Task<IReadOnlyList<Order>> OrdersAsync(CancellationToken token);
    Task<IReadOnlyList<Payment>> PaymentsAsync(CancellationToken token);
    Task<IReadOnlyList<PaymentAllocation>> AllocationsAsync(CancellationToken token);
    Task<IReadOnlyList<FinancialCreditMovement>> FinancialMovementsAsync(CancellationToken token);
    Task<bool> IsActiveDeliveryDriverAsync(Guid id, CancellationToken token);
    void Add(object entity); void RemoveRange(IEnumerable<object> entities);
    Task SaveAsync(CancellationToken token);
    Task<Payment> RegisterPaymentAsync(PaymentInput input, Guid actorId, string idempotencyKey, CancellationToken token);
}

public sealed class CommerceService(ICommerceStore store, IOrganizationContext organization,
    ICurrentUserContext currentUser, TimeProvider timeProvider)
{
    public async Task<CommerceResult> GetAsync(CancellationToken token)
    {
        var customers = await store.CustomersAsync(token); var addresses = await store.AddressesAsync(token);
        var preferences = await store.PreferencesAsync(token); var restrictions = await store.RestrictionsAsync(token);
        var plans = await store.PlansAsync(token); var planOffers = await store.PlanOffersAsync(token);
        var offers = await store.OffersAsync(token); var acquisitions = await store.AcquisitionsAsync(token);
        var charges = await store.ChargesAsync(token); var orders = await store.OrdersAsync(token); var payments = await store.PaymentsAsync(token);
        var allocations = await store.AllocationsAsync(token); var financial = await store.FinancialMovementsAsync(token);
        var customerNames = customers.ToDictionary(x => x.Id, x => x.Name);
        var allocationByCharge = allocations.GroupBy(x => x.ChargeId).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var allocationByPayment = allocations.GroupBy(x => x.PaymentId).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var ordersById = orders.ToDictionary(x => x.Id);
        return new(
            customers.Select(x => new CustomerResult(x.Id, x.Name, x.Phone, x.IsActive, x.Notes,
                x.PreferredDeliveryDriverId, x.PreferredPaymentCondition, x.PreferredPaymentMethod, x.Version,
                addresses.Where(a => a.CustomerId == x.Id).Select(a => new AddressResult(a.Id, a.Label, a.Street, a.Number, a.Complement, a.Neighborhood, a.City, a.State, a.PostalCode, a.ReferencePoint)).ToArray(),
                preferences.Where(p => p.CustomerId == x.Id).Select(p => p.Description).ToArray(),
                restrictions.Where(r => r.CustomerId == x.Id).Select(r => r.Marker).ToArray())).ToArray(),
            plans.Select(x => { var ids = planOffers.Where(p => p.PlanId == x.Id).Select(p => p.OfferId).ToArray(); return new PlanResult(x.Id, x.Name, x.Description, x.BenefitDescription, ids, offers.Where(o => ids.Contains(o.Id)).Select(o => o.Name).ToArray(), x.DefaultCredits, x.DefaultPrice, x.ValidityDays, x.IsActive, x.Version); }).ToArray(),
            acquisitions.Select(x => new AcquisitionResult(x.Id, x.CustomerId, string.IsNullOrEmpty(x.CustomerNameSnapshot) ? customerNames.GetValueOrDefault(x.CustomerId, "Cliente") : x.CustomerNameSnapshot,
                x.PlanId, x.PlanName, x.BenefitDescriptionSnapshot,
                x.CompatibleOfferIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray(),
                x.EligibleOfferId, x.Movements.Where(m => m.Type == PlanCreditMovementType.Acquired).Sum(m => m.Quantity), x.Balance, x.PaidAmount, x.BenefitAmountPerCredit, x.AcquiredOn, x.ExpiresOn, x.CreatedAt)).ToArray(),
            acquisitions.SelectMany(x => x.Movements).Select(x => new PlanMovementResult(x.Id, x.AcquisitionId, x.Type, x.Quantity, x.SignedQuantity, x.OrderId, x.OrderItemId, x.ActorId, x.OccurredAt)).ToArray(),
            charges.Select(x => { var allocated = allocationByCharge.GetValueOrDefault(x.Id); var order = ordersById[x.OrderId]; return new ChargeResult(x.Id, order.CustomerId,
                order.CustomerNameSnapshot, x.OrderId, x.Amount, allocated, Math.Max(0, x.Amount - allocated), x.DueOn, x.CreatedAt, x.Status); }).ToArray(),
            payments.Select(x => { var allocated = allocationByPayment.GetValueOrDefault(x.Id); return new PaymentResult(x.Id, x.CustomerId, x.CustomerNameSnapshot, x.Amount, allocated, x.Amount - allocated, x.ReceivedOn, x.Method, x.Reference, x.CreatedAt); }).ToArray(),
            allocations.Select(x => new AllocationResult(x.Id, x.PaymentId, x.ChargeId, x.Amount)).ToArray(),
            financial.Select(x => new FinancialMovementResult(x.Id, x.CustomerId, x.Type, x.Amount, x.SignedAmount, x.Reason, x.OrderId, x.PaymentId, x.ActorId, x.OccurredAt)).ToArray());
    }

    public async Task<Guid> SaveCustomerAsync(Guid? id, CustomerInput input, CancellationToken token)
    {
        var customer = id is null ? Customer.Create(organization.OrganizationId, input.Name, input.Phone)
            : await store.CustomerAsync(id.Value, token) ?? throw new ResourceNotFoundException("Cliente não encontrado.");
        if (!string.IsNullOrWhiteSpace(input.PreferredDeliveryDriverId)
            && input.PreferredDeliveryDriverId != customer.PreferredDeliveryDriverId
            && (!Guid.TryParse(input.PreferredDeliveryDriverId, out var preferredDriverId)
                || !await store.IsActiveDeliveryDriverAsync(preferredDriverId, token)))
            throw new DomainException("O entregador preferencial deve estar ativo e pertencer à organização.");
        if (id is not null && customer.Version != input.ExpectedVersion) throw new ConflictException("O cliente foi alterado. Recarregue os dados antes de editar.");
        if (id is null) store.Add(customer); else customer.Update(input.Name, input.Phone, input.IsActive, input.Notes,
            input.PreferredDeliveryDriverId, input.PreferredPaymentCondition, input.PreferredPaymentMethod,
            input.ExpectedVersion ?? throw new DomainException("A versão esperada é obrigatória."));
        if (id is null && (!input.IsActive || input.Notes is not null || input.PreferredDeliveryDriverId is not null || input.PreferredPaymentCondition is not null || input.PreferredPaymentMethod is not null))
            customer.Update(input.Name, input.Phone, input.IsActive, input.Notes, input.PreferredDeliveryDriverId, input.PreferredPaymentCondition, input.PreferredPaymentMethod, 1);
        var addresses = (await store.AddressesAsync(token)).Where(x => x.CustomerId == customer.Id).Cast<object>();
        var preferences = (await store.PreferencesAsync(token)).Where(x => x.CustomerId == customer.Id).Cast<object>();
        var restrictions = (await store.RestrictionsAsync(token)).Where(x => x.CustomerId == customer.Id).Cast<object>();
        store.RemoveRange(addresses.Concat(preferences).Concat(restrictions));
        foreach (var x in input.Addresses) store.Add(CustomerAddress.Create(organization.OrganizationId, customer.Id, x.Label, x.Street, x.Number, x.Complement, x.Neighborhood, x.City, x.State, x.PostalCode, x.ReferencePoint));
        foreach (var x in input.Preferences) store.Add(CustomerPreference.Create(organization.OrganizationId, customer.Id, x));
        foreach (var x in input.DietaryRestrictions) store.Add(CustomerDietaryRestriction.Create(organization.OrganizationId, customer.Id, x));
        await store.SaveAsync(token); return customer.Id;
    }

    public async Task<Guid> SavePlanAsync(Guid? id, PlanInput input, CancellationToken token)
    {
        if (input.CompatibleOfferIds.Count == 0) throw new DomainException("Selecione ao menos uma oferta compatível.");
        var validOffers = (await store.OffersAsync(token)).Select(x => x.Id).ToHashSet();
        if (input.CompatibleOfferIds.Any(x => !validOffers.Contains(x))) throw new DomainException("O plano referencia uma oferta inválida.");
        var plan = id is null ? CommercialPlan.Create(organization.OrganizationId, input.Name, input.Description,
            input.BenefitDescription, input.DefaultCredits, input.DefaultPrice, input.ValidityDays)
            : await store.PlanAsync(id.Value, token) ?? throw new ResourceNotFoundException("Plano não encontrado.");
        if (id is not null && plan.Version != input.ExpectedVersion) throw new ConflictException("O plano foi alterado. Recarregue os dados antes de editar.");
        if (id is null) store.Add(plan); else plan.Update(input.Name, input.Description, input.BenefitDescription,
            input.DefaultCredits, input.DefaultPrice, input.ValidityDays, input.IsActive,
            input.ExpectedVersion ?? throw new DomainException("A versão esperada é obrigatória."));
        if (id is null && !input.IsActive) plan.Update(input.Name, input.Description, input.BenefitDescription, input.DefaultCredits, input.DefaultPrice, input.ValidityDays, false, 1);
        store.RemoveRange((await store.PlanOffersAsync(token)).Where(x => x.PlanId == plan.Id).Cast<object>());
        foreach (var offerId in input.CompatibleOfferIds.Distinct()) store.Add(CommercialPlanOffer.Create(organization.OrganizationId, plan.Id, offerId));
        await store.SaveAsync(token); return plan.Id;
    }

    public async Task<Guid> AcquireAsync(AcquisitionInput input, CancellationToken token)
    {
        var customer = await store.CustomerAsync(input.CustomerId, token) ?? throw new ResourceNotFoundException("Cliente não encontrado.");
        var plan = await store.PlanAsync(input.PlanId, token) ?? throw new ResourceNotFoundException("Plano não encontrado.");
        if (!customer.IsActive || !plan.IsActive) throw new DomainException("Cliente e plano precisam estar ativos.");
        var offerIds = (await store.PlanOffersAsync(token)).Where(x => x.PlanId == plan.Id).Select(x => x.OfferId).ToArray();
        if (offerIds.Length == 0) throw new DomainException("O plano não possui oferta compatível.");
        var acquisition = PlanAcquisition.CreateFromPlan(organization.OrganizationId, customer.Id, customer.Name,
            plan.Id, offerIds, plan.Name, plan.BenefitDescription, input.Credits, input.PaidAmount,
            input.Credits == 0 ? 0 : input.PaidAmount / input.Credits, input.PurchasedOn, input.ExpiresOn, timeProvider.GetUtcNow());
        store.Add(acquisition); await store.SaveAsync(token); return acquisition.Id;
    }

    public async Task AdjustCreditAsync(CreditAdjustmentInput input, CancellationToken token)
    {
        var acquisition = await store.AcquisitionAsync(input.AcquisitionId, token) ?? throw new ResourceNotFoundException("Aquisição não encontrada.");
        if (string.IsNullOrWhiteSpace(input.Reason)) throw new DomainException("Informe o motivo do ajuste.");
        if (input.MovementId is Guid movementId)
        {
            var source = acquisition.Movements.SingleOrDefault(x => x.Id == movementId && x.Type == PlanCreditMovementType.Consumed)
                ?? throw new ResourceNotFoundException("Consumo não encontrado.");
            acquisition.Reverse(source.OrderId!.Value, source.OrderItemId!.Value, Math.Abs(input.Quantity), currentUser.UserId, timeProvider.GetUtcNow());
        }
        else acquisition.Adjust(input.Quantity, currentUser.UserId, timeProvider.GetUtcNow());
        await store.SaveAsync(token);
    }
    public async Task AdjustFinancialAsync(FinancialAdjustmentInput input, CancellationToken token)
    {
        _ = await store.CustomerAsync(input.CustomerId, token) ?? throw new ResourceNotFoundException("Cliente não encontrado.");
        store.Add(FinancialCreditMovement.ManualAdjustment(organization.OrganizationId, input.CustomerId,
            input.Amount, input.Reason, currentUser.UserId, timeProvider.GetUtcNow())); await store.SaveAsync(token);
    }
    public Task<Payment> PayAsync(PaymentInput input, string key, CancellationToken token) =>
        store.RegisterPaymentAsync(input, currentUser.UserId, key, token);
}
