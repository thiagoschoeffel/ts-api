using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Application.Organizations;

public sealed record SaasPlanVersionResult(Guid Id, string Code, string Name, int Version,
    IReadOnlyCollection<string> Entitlements);
public sealed record OrganizationSaasSubscriptionResult(Guid PlanVersionId, string PlanCode,
    string PlanName, int PlanVersion, IReadOnlyCollection<string> Entitlements, long Version);
public sealed record OrganizationLifecycleResult(Guid Id, OrganizationLifecycleStatus Status,
    long Version, OrganizationSaasSubscriptionResult? Subscription);

public interface IPlatformLifecycleStore
{
    Task<IReadOnlyCollection<(SaasPlanVersion Plan, IReadOnlyCollection<string> Entitlements)>>
        ListAvailablePlansAsync(CancellationToken token);
    Task<SaasPlanVersion?> FindPlanAsync(Guid id, CancellationToken token);
    Task<IReadOnlyCollection<string>> GetEntitlementsAsync(Guid planVersionId, CancellationToken token);
    Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token);
    Task<OrganizationSaasSubscription?> FindSubscriptionAsync(Guid organizationId, CancellationToken token);
    Task<bool> HasActiveOwnerAsync(Guid organizationId, CancellationToken token);
    Task<bool> IsOnboardingReadyAsync(Guid organizationId, CancellationToken token);
    Task MarkOnboardingActiveAsync(Guid organizationId, DateTimeOffset now, CancellationToken token);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token);
    void Add(object entity);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class PlatformLifecycleService(IPlatformLifecycleStore store, IPlatformActorContext actor,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyCollection<SaasPlanVersionResult>> ListPlansAsync(CancellationToken token) =>
        (await store.ListAvailablePlansAsync(token)).Select(item => new SaasPlanVersionResult(
            item.Plan.Id, item.Plan.Code, item.Plan.Name, item.Plan.Version,
            item.Entitlements.Order(StringComparer.Ordinal).ToArray())).ToArray();

    public async Task<OrganizationSaasSubscriptionResult?> GetSubscriptionAsync(Guid organizationId,
        CancellationToken token)
    {
        _ = await RequiredOrganization(organizationId, token);
        var subscription = await store.FindSubscriptionAsync(organizationId, token);
        if (subscription is null) return null;
        var plan = await store.FindPlanAsync(subscription.PlanVersionId, token)
            ?? throw new InvalidOperationException("A assinatura referencia um plano SaaS inexistente.");
        var entitlements = await store.GetEntitlementsAsync(plan.Id, token);
        return new(plan.Id, plan.Code, plan.Name, plan.Version,
            entitlements.Order(StringComparer.Ordinal).ToArray(), subscription.Version);
    }

    public Task<OrganizationLifecycleResult> AssignPlanAsync(Guid organizationId, Guid planVersionId,
        long? expectedSubscriptionVersion, string correlationId, CancellationToken token) =>
        store.ExecuteSerializableAsync(async transactionalToken =>
        {
            var organization = await RequiredOrganization(organizationId, transactionalToken);
            if (organization.LifecycleStatus == OrganizationLifecycleStatus.Archived)
                throw new ConflictException("Uma organização arquivada não pode receber um plano SaaS.");
            var plan = await store.FindPlanAsync(planVersionId, transactionalToken)
                ?? throw new ResourceNotFoundException("Versão do plano SaaS não encontrada.");
            if (!plan.IsAvailable) throw new ConflictException("Esta versão do plano SaaS não está disponível.");
            var subscription = await store.FindSubscriptionAsync(organizationId, transactionalToken);
            if (subscription is null)
            {
                if (expectedSubscriptionVersion.HasValue)
                    throw new PreconditionFailedException("A assinatura SaaS foi alterada. Recarregue os dados.");
                subscription = OrganizationSaasSubscription.Create(organizationId, planVersionId,
                    actor.UserId, timeProvider.GetUtcNow());
                store.Add(subscription);
            }
            else
            {
                if (!expectedSubscriptionVersion.HasValue || subscription.Version != expectedSubscriptionVersion.Value)
                    throw new PreconditionFailedException("A assinatura SaaS foi alterada. Recarregue os dados.");
                try { subscription.Assign(planVersionId, actor.UserId, timeProvider.GetUtcNow(), expectedSubscriptionVersion.Value); }
                catch (DomainException exception) { throw new PreconditionFailedException(exception.Message); }
            }
            await AuditAndSave("organization.saas-plan-assigned", organizationId,
                $"Plano {plan.Code} v{plan.Version} atribuído.", correlationId, transactionalToken);
            return await Map(organization, subscription, transactionalToken);
        }, token);

    public Task<OrganizationLifecycleResult> ChangeStatusAsync(Guid organizationId,
        OrganizationLifecycleStatus target, long expectedVersion, string reason,
        string correlationId, CancellationToken token) => store.ExecuteSerializableAsync(async transactionalToken =>
    {
        var organization = await RequiredOrganization(organizationId, transactionalToken);
        if (organization.Version != expectedVersion)
            throw new PreconditionFailedException("A organização foi alterada. Recarregue os dados.");
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length is 0 or > 1000)
            throw new DomainException("Informe um motivo com até 1000 caracteres.");
        var subscription = await store.FindSubscriptionAsync(organizationId, transactionalToken);
        if (target == OrganizationLifecycleStatus.Active)
        {
            if (subscription is null) throw new ConflictException("Atribua um plano SaaS antes da ativação.");
            var entitlements = await store.GetEntitlementsAsync(subscription.PlanVersionId, transactionalToken);
            if (!entitlements.Contains(SaasEntitlements.BusinessAccess, StringComparer.Ordinal))
                throw new ConflictException("O plano SaaS não habilita o acesso às APIs de negócio.");
            if (organization.LifecycleStatus == OrganizationLifecycleStatus.Provisioning)
            {
                if (!await store.HasActiveOwnerAsync(organizationId, transactionalToken))
                    throw new ConflictException("A organização precisa ter um proprietário ativo.");
                if (!await store.IsOnboardingReadyAsync(organizationId, transactionalToken))
                    throw new ConflictException("O provisionamento obrigatório ainda não foi concluído.");
                organization.Activate();
                await store.MarkOnboardingActiveAsync(organizationId, timeProvider.GetUtcNow(), transactionalToken);
            }
            else organization.Reactivate();
        }
        else if (target == OrganizationLifecycleStatus.Suspended) organization.Suspend();
        else throw new DomainException("A transição administrativa solicitada não é permitida.");
        await AuditAndSave(target == OrganizationLifecycleStatus.Suspended
                ? "organization.suspended" : "organization.activated",
            organizationId, normalizedReason, correlationId, transactionalToken);
        return await Map(organization, subscription, transactionalToken);
    }, token);

    private async Task<Organization> RequiredOrganization(Guid id, CancellationToken token) =>
        await store.FindOrganizationAsync(id, token)
            ?? throw new ResourceNotFoundException("Organização não encontrada.");

    private async Task AuditAndSave(string action, Guid targetId, string reason,
        string correlationId, CancellationToken token)
    {
        store.Add(PlatformAuditEvent.Create(actor.UserId, "PlatformOperator", action,
            "Organization", targetId, "Succeeded", reason, timeProvider.GetUtcNow(), correlationId));
        await store.SaveChangesAsync(token);
    }

    private async Task<OrganizationLifecycleResult> Map(Organization organization,
        OrganizationSaasSubscription? subscription, CancellationToken token)
    {
        if (subscription is null) return new(organization.Id, organization.LifecycleStatus, organization.Version, null);
        var plan = await store.FindPlanAsync(subscription.PlanVersionId, token)
            ?? throw new InvalidOperationException("A assinatura referencia um plano SaaS inexistente.");
        var entitlements = await store.GetEntitlementsAsync(plan.Id, token);
        return new(organization.Id, organization.LifecycleStatus, organization.Version,
            new(plan.Id, plan.Code, plan.Name, plan.Version, entitlements.Order(StringComparer.Ordinal).ToArray(), subscription.Version));
    }
}
