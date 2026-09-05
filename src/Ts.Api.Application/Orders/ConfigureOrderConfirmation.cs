using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.Plans;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Orders;

public interface IOrderConfirmationSetupStore
{
    Task<ProducibleItem?> FindProducibleItemAsync(Guid id, CancellationToken cancellationToken);
    Task<CatalogOffer?> FindOfferAsync(Guid id, CancellationToken cancellationToken);
    Task<int> GetNextCompositionVersionAsync(Guid producibleItemId, CancellationToken cancellationToken);
    Task<bool> RestrictionExistsAsync(Guid customerId, string marker, CancellationToken cancellationToken);
    void Add(ProducibleComposition composition);
    void Add(CustomerDietaryRestriction restriction);
    void Add(PlanAcquisition acquisition);
    void Add(FinancialCreditMovement movement);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class PublishCompositionHandler(
    IOrderConfirmationSetupStore store,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
{
    public async Task<CompositionResult> HandleAsync(PublishCompositionCommand command, CancellationToken cancellationToken)
    {
        _ = await store.FindProducibleItemAsync(command.ProducibleItemId, cancellationToken)
            ?? throw new ResourceNotFoundException("Item produzível não encontrado.");
        foreach (var referenceId in command.Components.Where(item => item.ReferencedProducibleItemId.HasValue)
                     .Select(item => item.ReferencedProducibleItemId!.Value).Distinct())
        {
            if (referenceId == command.ProducibleItemId)
                throw new DomainException("Um item produzível não pode referenciar a si próprio na composição.");
            _ = await store.FindProducibleItemAsync(referenceId, cancellationToken)
                ?? throw new DomainException("A composição referencia um item produzível inexistente ou inativo.");
        }
        var version = await store.GetNextCompositionVersionAsync(command.ProducibleItemId, cancellationToken);
        var composition = ProducibleComposition.Publish(
            organizationContext.OrganizationId, command.ProducibleItemId, version,
            timeProvider.GetUtcNow(), command.Components);
        store.Add(composition);
        await store.SaveChangesAsync(cancellationToken);
        return new CompositionResult(composition.Id, composition.ProducibleItemId, composition.Version);
    }
}

public sealed class AddCustomerRestrictionHandler(
    IOrderConfirmationSetupStore store,
    IOrganizationContext organizationContext)
{
    public async Task<CustomerRestrictionResult> HandleAsync(AddCustomerRestrictionCommand command, CancellationToken cancellationToken)
    {
        var restriction = CustomerDietaryRestriction.Create(
            organizationContext.OrganizationId, command.CustomerId, command.Marker);
        if (await store.RestrictionExistsAsync(command.CustomerId, restriction.Marker, cancellationToken))
            throw new ConflictException("A restrição já está cadastrada para o cliente.");
        store.Add(restriction);
        await store.SaveChangesAsync(cancellationToken);
        return new CustomerRestrictionResult(restriction.Id, restriction.CustomerId, restriction.Marker);
    }
}

public sealed class CreatePlanAcquisitionHandler(
    IOrderConfirmationSetupStore store,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
{
    public async Task<PlanAcquisitionResult> HandleAsync(CreatePlanAcquisitionCommand command, CancellationToken cancellationToken)
    {
        _ = await store.FindOfferAsync(command.EligibleOfferId, cancellationToken)
            ?? throw new ResourceNotFoundException("Oferta elegível não encontrada.");
        var acquisition = PlanAcquisition.Create(
            organizationContext.OrganizationId, command.CustomerId, command.EligibleOfferId,
            command.PlanName, command.Credits, command.BenefitAmountPerCredit,
            command.AcquiredOn, timeProvider.GetUtcNow());
        store.Add(acquisition);
        await store.SaveChangesAsync(cancellationToken);
        return new PlanAcquisitionResult(acquisition.Id, acquisition.CustomerId, acquisition.EligibleOfferId,
            acquisition.PlanName, acquisition.Balance, acquisition.BenefitAmountPerCredit, acquisition.AcquiredOn);
    }
}

public sealed class GrantFinancialCreditHandler(
    IOrderConfirmationSetupStore store,
    IOrganizationContext organizationContext,
    TimeProvider timeProvider)
{
    public async Task<FinancialCreditResult> HandleAsync(GrantFinancialCreditCommand command, CancellationToken cancellationToken)
    {
        var movement = FinancialCreditMovement.Grant(
            organizationContext.OrganizationId, command.CustomerId, command.Amount,
            command.Reason, command.ActorId, timeProvider.GetUtcNow());
        store.Add(movement);
        await store.SaveChangesAsync(cancellationToken);
        return new FinancialCreditResult(movement.Id, movement.CustomerId, movement.Amount, movement.Reason);
    }
}

public sealed record PublishCompositionCommand(Guid ProducibleItemId, IReadOnlyCollection<ProducibleComponentDefinition> Components);
public sealed record CompositionResult(Guid Id, Guid ProducibleItemId, int Version);
public sealed record AddCustomerRestrictionCommand(Guid CustomerId, string Marker);
public sealed record CustomerRestrictionResult(Guid Id, Guid CustomerId, string Marker);
public sealed record CreatePlanAcquisitionCommand(Guid CustomerId, Guid EligibleOfferId, string PlanName, int Credits, decimal BenefitAmountPerCredit, DateOnly AcquiredOn);
public sealed record PlanAcquisitionResult(Guid Id, Guid CustomerId, Guid EligibleOfferId, string PlanName, int Balance, decimal BenefitAmountPerCredit, DateOnly AcquiredOn);
public sealed record GrantFinancialCreditCommand(Guid CustomerId, decimal Amount, string Reason, Guid ActorId);
public sealed record FinancialCreditResult(Guid Id, Guid CustomerId, decimal Amount, string Reason);
