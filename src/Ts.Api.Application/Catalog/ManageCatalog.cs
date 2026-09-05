using System.Text.Json;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Catalog;

public sealed record OfferComponentInput(Guid ComponentTypeId, decimal Quantity);
public sealed record OfferChoiceOptionInput(Guid ComponentTypeId, decimal Surcharge);
public sealed record OfferChoiceGroupInput(string Name, int MinimumSelections, int MaximumSelections,
    IReadOnlyCollection<OfferChoiceOptionInput> Options);
public sealed record OfferConfigurationInput(IReadOnlyCollection<OfferComponentInput> Components,
    IReadOnlyCollection<OfferChoiceGroupInput> ChoiceGroups, IReadOnlyCollection<Guid> AllowedAddonIds);
public sealed record CatalogOfferInput(string Name, string? Description, decimal BasePrice,
    OfferFulfillmentMode FulfillmentMode, bool RequiresMenuChoice, bool IsActive, OfferConfigurationInput Configuration);
public sealed record CatalogOfferView(Guid Id, string Name, string? Description, decimal BasePrice,
    OfferFulfillmentMode FulfillmentMode, bool RequiresMenuChoice, bool IsActive, int ConfigurationVersion,
    OfferConfigurationInput Configuration);
public sealed record ComponentTypeInput(string Name, string? Description, bool IsActive);
public sealed record ComponentTypeView(Guid Id, string Name, string? Description, bool IsActive);
public sealed record CatalogAddonInput(string Name, decimal Price, Guid? ProducibleItemId,
    decimal? OperationalQuantity, string? MeasurementUnit, bool IsActive);
public sealed record CatalogAddonView(Guid Id, string Name, decimal Price, Guid? ProducibleItemId,
    decimal? OperationalQuantity, string? MeasurementUnit, bool IsActive);
public sealed record ProducibleInput(string Name, string? Description, string Category, string MeasurementUnit, bool IsActive);
public sealed record ProducibleView(Guid Id, string Name, string? Description, string Category, string MeasurementUnit,
    bool IsActive, IReadOnlyCollection<CompositionView> Compositions);
public sealed record CompositionView(Guid Id, int Version, DateTimeOffset PublishedAt,
    IReadOnlyCollection<ProducibleComponentDefinition> Components);
public sealed record CatalogSnapshot(IReadOnlyCollection<CatalogOfferView> Offers,
    IReadOnlyCollection<ComponentTypeView> ComponentTypes, IReadOnlyCollection<CatalogAddonView> Addons);

public interface ICatalogManagementStore
{
    Task<IReadOnlyList<CatalogOffer>> GetOffersAsync(CancellationToken token);
    Task<CatalogOffer?> FindOfferAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<CatalogOfferVersion>> GetOfferVersionsAsync(IReadOnlyCollection<Guid> offerIds, CancellationToken token);
    Task<IReadOnlyList<ComponentType>> GetComponentTypesAsync(CancellationToken token);
    Task<ComponentType?> FindComponentTypeAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<CatalogAddon>> GetAddonsAsync(CancellationToken token);
    Task<CatalogAddon?> FindAddonAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<ProducibleItem>> GetProduciblesAsync(CancellationToken token);
    Task<ProducibleItem?> FindProducibleAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<ProducibleComposition>> GetCompositionsAsync(IReadOnlyCollection<Guid> producibleIds, CancellationToken token);
    Task<bool> OfferNameExistsAsync(string name, Guid? exceptId, CancellationToken token);
    Task<bool> ComponentTypeNameExistsAsync(string name, Guid? exceptId, CancellationToken token);
    Task<bool> AddonNameExistsAsync(string name, Guid? exceptId, CancellationToken token);
    Task<bool> ProducibleNameExistsAsync(string name, Guid? exceptId, CancellationToken token);
    void Add(object entity);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class GetCatalogHandler(ICatalogManagementStore store)
{
    public async Task<CatalogSnapshot> HandleAsync(CancellationToken token)
    {
        var offers = await store.GetOffersAsync(token);
        var versions = await store.GetOfferVersionsAsync(offers.Select(x => x.Id).ToArray(), token);
        return new(offers.Select(offer => Map(offer, versions.Where(x => x.OfferId == offer.Id)
                .OrderByDescending(x => x.Version).FirstOrDefault())).ToArray(),
            (await store.GetComponentTypesAsync(token)).Select(x => new ComponentTypeView(x.Id, x.Name, x.Description, x.IsActive)).ToArray(),
            (await store.GetAddonsAsync(token)).Select(x => new CatalogAddonView(x.Id, x.Name, x.Price, x.ProducibleItemId,
                x.OperationalQuantity, x.MeasurementUnit, x.IsActive)).ToArray());
    }
    internal static CatalogOfferView Map(CatalogOffer offer, CatalogOfferVersion? version) => new(offer.Id, offer.Name,
        offer.Description, offer.BasePrice, offer.FulfillmentMode, offer.RequiresMenuChoice, offer.IsActive,
        version?.Version ?? 0, version is null ? EmptyConfiguration : JsonSerializer.Deserialize<OfferConfigurationInput>(version.ConfigurationJson)!);
    internal static readonly OfferConfigurationInput EmptyConfiguration = new([], [], []);
}

public sealed class SaveOfferHandler(ICatalogManagementStore store, IOrganizationContext organization, TimeProvider clock)
{
    public async Task<CatalogOfferView> HandleAsync(Guid? id, CatalogOfferInput input, CancellationToken token)
    {
        if (await store.OfferNameExistsAsync(input.Name.Trim(), id, token)) throw new ConflictException("Já existe uma oferta com esse nome.");
        await ValidateConfiguration(input.Configuration, id, token);
        CatalogOffer offer;
        if (id is Guid offerId)
        {
            offer = await store.FindOfferAsync(offerId, token) ?? throw new ResourceNotFoundException("Oferta não encontrada.");
            if (offer.FulfillmentMode != input.FulfillmentMode) throw new DomainException("A modalidade de atendimento de uma oferta existente não pode ser alterada.");
            offer.Update(input.Name, input.Description, input.BasePrice, input.RequiresMenuChoice, input.IsActive);
        }
        else
        {
            offer = CatalogOffer.Create(organization.OrganizationId, input.Name, input.FulfillmentMode,
                input.Description, input.BasePrice, input.RequiresMenuChoice);
            store.Add(offer);
            if (!input.IsActive) offer.Deactivate();
        }
        var versions = await store.GetOfferVersionsAsync([offer.Id], token);
        var version = CatalogOfferVersion.Create(organization.OrganizationId, offer.Id,
            versions.Select(x => x.Version).DefaultIfEmpty().Max() + 1, JsonSerializer.Serialize(input.Configuration), clock.GetUtcNow());
        store.Add(version); await store.SaveChangesAsync(token);
        return GetCatalogHandler.Map(offer, version);
    }
    private async Task ValidateConfiguration(OfferConfigurationInput input, Guid? offerId, CancellationToken token)
    {
        var types = await store.GetComponentTypesAsync(token); var addons = await store.GetAddonsAsync(token);
        var activeTypes = types.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        var activeAddons = addons.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        if (offerId is Guid existingId)
        {
            var previous = (await store.GetOfferVersionsAsync([existingId], token)).MaxBy(x => x.Version);
            if (previous is not null)
            {
                var configuration = JsonSerializer.Deserialize<OfferConfigurationInput>(previous.ConfigurationJson)!;
                activeTypes.UnionWith(configuration.Components.Select(x => x.ComponentTypeId));
                activeTypes.UnionWith(configuration.ChoiceGroups.SelectMany(x => x.Options).Select(x => x.ComponentTypeId));
                activeAddons.UnionWith(configuration.AllowedAddonIds);
            }
        }
        if (input.Components.Any(x => !activeTypes.Contains(x.ComponentTypeId) || x.Quantity <= 0)
            || input.ChoiceGroups.Any(g => string.IsNullOrWhiteSpace(g.Name) || g.MinimumSelections < 0 || g.MaximumSelections < g.MinimumSelections
                || g.Options.Count < g.MaximumSelections || g.Options.Any(o => !activeTypes.Contains(o.ComponentTypeId) || o.Surcharge < 0))
            || input.AllowedAddonIds.Any(x => !activeAddons.Contains(x)))
            throw new DomainException("A configuração da oferta referencia tipos, escolhas ou adicionais inválidos.");
    }
}

public sealed class SaveComponentTypeHandler(ICatalogManagementStore store, IOrganizationContext organization)
{
    public async Task<ComponentTypeView> HandleAsync(Guid? id, ComponentTypeInput input, CancellationToken token)
    {
        if (await store.ComponentTypeNameExistsAsync(input.Name.Trim(), id, token)) throw new ConflictException("Já existe um tipo de componente com esse nome.");
        ComponentType entity;
        if (id is Guid value) { entity = await store.FindComponentTypeAsync(value, token) ?? throw new ResourceNotFoundException("Tipo de componente não encontrado."); entity.Update(input.Name, input.Description, input.IsActive); }
        else { entity = ComponentType.Create(organization.OrganizationId, input.Name, input.Description); if (!input.IsActive) entity.Update(input.Name, input.Description, false); store.Add(entity); }
        await store.SaveChangesAsync(token); return new(entity.Id, entity.Name, entity.Description, entity.IsActive);
    }
}

public sealed class SaveAddonHandler(ICatalogManagementStore store, IOrganizationContext organization)
{
    public async Task<CatalogAddonView> HandleAsync(Guid? id, CatalogAddonInput input, CancellationToken token)
    {
        if (await store.AddonNameExistsAsync(input.Name.Trim(), id, token)) throw new ConflictException("Já existe um adicional com esse nome.");
        if (input.ProducibleItemId is Guid producible && (await store.FindProducibleAsync(producible, token))?.IsActive != true)
            throw new DomainException("O item produzível do adicional não existe ou está inativo.");
        CatalogAddon entity;
        if (id is Guid value) { entity = await store.FindAddonAsync(value, token) ?? throw new ResourceNotFoundException("Adicional não encontrado."); entity.Update(input.Name, input.Price, input.ProducibleItemId, input.OperationalQuantity, input.MeasurementUnit, input.IsActive); }
        else { entity = CatalogAddon.Create(organization.OrganizationId, input.Name, input.Price, input.ProducibleItemId, input.OperationalQuantity, input.MeasurementUnit); if (!input.IsActive) entity.Update(input.Name, input.Price, input.ProducibleItemId, input.OperationalQuantity, input.MeasurementUnit, false); store.Add(entity); }
        await store.SaveChangesAsync(token); return new(entity.Id, entity.Name, entity.Price, entity.ProducibleItemId, entity.OperationalQuantity, entity.MeasurementUnit, entity.IsActive);
    }
}

public sealed class GetProduciblesHandler(ICatalogManagementStore store)
{
    public async Task<IReadOnlyCollection<ProducibleView>> HandleAsync(CancellationToken token)
    {
        var items = await store.GetProduciblesAsync(token); var compositions = await store.GetCompositionsAsync(items.Select(x => x.Id).ToArray(), token);
        return items.Select(item => new ProducibleView(item.Id, item.Name, item.Description, item.Category, item.MeasurementUnit, item.IsActive,
            compositions.Where(x => x.ProducibleItemId == item.Id).OrderByDescending(x => x.Version)
                .Select(x => new CompositionView(x.Id, x.Version, x.PublishedAt, x.Components.Select(c => new ProducibleComponentDefinition(c.Name, c.Quantity, c.MeasurementUnit, c.Markers, c.ReferencedProducibleItemId, c.Kind)).ToArray())).ToArray())).ToArray();
    }
}

public sealed class SaveProducibleHandler(ICatalogManagementStore store, IOrganizationContext organization)
{
    public async Task<ProducibleView> HandleAsync(Guid? id, ProducibleInput input, CancellationToken token)
    {
        if (await store.ProducibleNameExistsAsync(input.Name.Trim(), id, token)) throw new ConflictException("Já existe um item produzível com esse nome.");
        ProducibleItem entity;
        if (id is Guid value) { entity = await store.FindProducibleAsync(value, token) ?? throw new ResourceNotFoundException("Item produzível não encontrado."); entity.Update(input.Name, input.Description, input.Category, input.MeasurementUnit, input.IsActive); }
        else { entity = ProducibleItem.Create(organization.OrganizationId, input.Name, input.Description, input.Category, input.MeasurementUnit); if (!input.IsActive) entity.Update(input.Name, input.Description, input.Category, input.MeasurementUnit, false); store.Add(entity); }
        await store.SaveChangesAsync(token); return new(entity.Id, entity.Name, entity.Description, entity.Category, entity.MeasurementUnit, entity.IsActive, []);
    }
}
