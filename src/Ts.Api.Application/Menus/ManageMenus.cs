using System.Text.Json;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Menus;
using Ts.Api.Domain.Organizations;
using Ts.Api.Domain.Production;

namespace Ts.Api.Application.Menus;

public sealed record MenuOptionInput(string Category, Guid ProducibleItemId, MenuAvailability Availability);
public sealed record MenuOfferInput(Guid OfferId, decimal EffectivePrice, MenuAvailability Availability, int DisplayOrder);
public sealed record DailyMenuInput(DateOnly Date, IReadOnlyCollection<MenuOptionInput> Options, IReadOnlyCollection<MenuOfferInput> Offers, long? ExpectedVersion = null);
public sealed record MenuOptionView(Guid Id, string Category, Guid ProducibleItemId, string ProducibleName, MenuAvailability Availability);
public sealed record MenuOfferView(Guid Id, Guid OfferId, string OfferName, string? Description, decimal EffectivePrice,
    MenuAvailability Availability, int DisplayOrder, bool RequiresMenuChoice);
public sealed record DailyMenuView(Guid Id, DateOnly Date, DailyMenuStatus Status, DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt, long Version, IReadOnlyCollection<MenuOptionView> Options, IReadOnlyCollection<MenuOfferView> Offers);
public sealed record MenuImportIssue(DateOnly? Date, int Index, string Message);
public sealed record MenuImportResult(IReadOnlyCollection<DateOnly> CreatedDates, IReadOnlyCollection<DateOnly> SkippedDates,
    IReadOnlyCollection<MenuImportIssue> Issues);
public sealed record WeeklyPlanInput(DateOnly WeekStart, IReadOnlyCollection<DailyMenuInput> Days);
public sealed record WeeklyPlanView(Guid Id, DateOnly WeekStart, IReadOnlyCollection<DailyMenuInput> Days,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public interface IMenuStore
{
    Task<IReadOnlyList<DailyMenu>> GetMenusAsync(DateOnly? from, DateOnly? to, CancellationToken token);
    Task<DailyMenu?> FindMenuAsync(DateOnly date, CancellationToken token);
    Task<IReadOnlyList<CatalogOffer>> GetOffersAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<IReadOnlyList<ProducibleItem>> GetProduciblesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);
    Task<WeeklyMenuPlan?> FindWeeklyPlanAsync(DateOnly weekStart, CancellationToken token);
    void Add(object entity);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class MenuService(IMenuStore store, IOrganizationContext organization, ICurrentUserContext currentUser, TimeProvider clock)
{
    public async Task<IReadOnlyCollection<DailyMenuView>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken token) =>
        await MapMany(await store.GetMenusAsync(from, to, token), token);
    public async Task<DailyMenuView> GetAsync(DateOnly date, CancellationToken token) =>
        (await MapMany([await store.FindMenuAsync(date, token) ?? throw new ResourceNotFoundException("Cardápio não encontrado.")], token)).Single();
    public async Task<DailyMenuView> SaveAsync(DailyMenuInput input, CancellationToken token)
    {
        await Validate(input, token); var existing = await store.FindMenuAsync(input.Date, token);
        if (existing is null) { existing = Create(input); store.Add(existing); }
        else { if (input.ExpectedVersion is not null && input.ExpectedVersion != existing.Version) throw new ConflictException("O cardápio foi alterado por outra pessoa.");
            existing.Update(MapOptions(input), MapOffers(input), clock.GetUtcNow(), existing.Version); }
        await store.SaveChangesAsync(token); return (await MapMany([existing], token)).Single();
    }
    public async Task<DailyMenuView> PublishAsync(DateOnly date, long expectedVersion, CancellationToken token)
    {
        var menu = await store.FindMenuAsync(date, token) ?? throw new ResourceNotFoundException("Cardápio não encontrado.");
        if (menu.Version != expectedVersion) throw new ConflictException("O cardápio foi alterado por outra pessoa.");
        var now = clock.GetUtcNow(); menu.Publish(now, expectedVersion);
        store.Add(AuditEvent.Create(organization.OrganizationId, currentUser.UserId, "DailyMenu.Published", "DailyMenu",
            menu.Id, now, currentUser.CorrelationId));
        await store.SaveChangesAsync(token); return (await MapMany([menu], token)).Single();
    }
    public async Task<MenuImportResult> ImportAsync(IReadOnlyCollection<DailyMenuInput> inputs, CancellationToken token)
    {
        var created = new List<DateOnly>(); var skipped = new List<DateOnly>(); var issues = new List<MenuImportIssue>();
        var candidates = new List<DailyMenu>();
        var seenDates = new HashSet<DateOnly>();
        foreach (var (input, index) in inputs.Select((value, index) => (value, index)))
        {
            if (!seenDates.Add(input.Date)) { issues.Add(new(input.Date, index, "A data está repetida na importação.")); continue; }
            if (await store.FindMenuAsync(input.Date, token) is not null) { skipped.Add(input.Date); continue; }
            try { await Validate(input, token); candidates.Add(Create(input)); created.Add(input.Date); }
            catch (Exception exception) when (exception is DomainException or ResourceNotFoundException) { issues.Add(new(input.Date, index, exception.Message)); }
        }
        if (issues.Count == 0) { foreach (var candidate in candidates) store.Add(candidate); await store.SaveChangesAsync(token); }
        else created.Clear();
        return new(created, skipped, issues);
    }
    public async Task<WeeklyPlanView> SavePlanAsync(WeeklyPlanInput input, bool deriveDrafts, CancellationToken token)
    {
        if (input.WeekStart.DayOfWeek != DayOfWeek.Monday) throw new DomainException("O planejamento deve começar em uma segunda-feira.");
        if (input.Days.Select(x => x.Date).Distinct().Count() != input.Days.Count
            || input.Days.Any(x => x.Date < input.WeekStart || x.Date > input.WeekStart.AddDays(6)))
            throw new DomainException("Os dias do planejamento devem ser únicos e pertencer à semana informada.");
        foreach (var day in input.Days) await Validate(day, token);
        var json = JsonSerializer.Serialize(input.Days); var plan = await store.FindWeeklyPlanAsync(input.WeekStart, token);
        if (plan is null) { plan = WeeklyMenuPlan.Create(organization.OrganizationId, input.WeekStart, json, clock.GetUtcNow()); store.Add(plan); }
        else plan.Update(json, clock.GetUtcNow());
        if (deriveDrafts) foreach (var day in input.Days)
            if (await store.FindMenuAsync(day.Date, token) is null) store.Add(Create(day));
        await store.SaveChangesAsync(token); return new(plan.Id, plan.WeekStart, input.Days, plan.CreatedAt, plan.UpdatedAt);
    }
    public async Task<WeeklyPlanView> GetPlanAsync(DateOnly weekStart, CancellationToken token)
    {
        var plan = await store.FindWeeklyPlanAsync(weekStart, token) ?? throw new ResourceNotFoundException("Planejamento semanal não encontrado.");
        var days = JsonSerializer.Deserialize<IReadOnlyCollection<DailyMenuInput>>(plan.DaysJson) ?? [];
        return new(plan.Id, plan.WeekStart, days, plan.CreatedAt, plan.UpdatedAt);
    }
    private DailyMenu Create(DailyMenuInput input) => DailyMenu.Create(organization.OrganizationId, input.Date, MapOptions(input), MapOffers(input), clock.GetUtcNow());
    private static DailyMenuOptionDefinition[] MapOptions(DailyMenuInput input) => input.Options.Select(x => new DailyMenuOptionDefinition(x.Category, x.ProducibleItemId, x.Availability)).ToArray();
    private static DailyMenuOfferDefinition[] MapOffers(DailyMenuInput input) => input.Offers.Select(x => new DailyMenuOfferDefinition(x.OfferId, x.EffectivePrice, x.Availability, x.DisplayOrder)).ToArray();
    private async Task Validate(DailyMenuInput input, CancellationToken token)
    {
        if (input.Options.Count == 0 || input.Offers.Count == 0) throw new DomainException("O cardápio deve possuir opções e ofertas.");
        var offers = await store.GetOffersAsync(input.Offers.Select(x => x.OfferId).Distinct().ToArray(), token);
        var producibles = await store.GetProduciblesAsync(input.Options.Select(x => x.ProducibleItemId).Distinct().ToArray(), token);
        if (offers.Count != input.Offers.Select(x => x.OfferId).Distinct().Count() || offers.Any(x => !x.IsActive)) throw new DomainException("O cardápio referencia uma oferta inexistente ou inativa.");
        if (producibles.Count != input.Options.Select(x => x.ProducibleItemId).Distinct().Count() || producibles.Any(x => !x.IsActive)) throw new DomainException("O cardápio referencia um item produzível inexistente ou inativo.");
    }
    private async Task<IReadOnlyCollection<DailyMenuView>> MapMany(IReadOnlyCollection<DailyMenu> menus, CancellationToken token)
    {
        var offerIds = menus.SelectMany(x => x.Offers).Select(x => x.OfferId).Distinct().ToArray();
        var producibleIds = menus.SelectMany(x => x.Options).Select(x => x.ProducibleItemId).Distinct().ToArray();
        var offers = (await store.GetOffersAsync(offerIds, token)).ToDictionary(x => x.Id);
        var producibles = (await store.GetProduciblesAsync(producibleIds, token)).ToDictionary(x => x.Id);
        return menus.Select(menu => new DailyMenuView(menu.Id, menu.Date, menu.Status, menu.PublishedAt, menu.UpdatedAt, menu.Version,
            menu.Options.Select(x => new MenuOptionView(x.Id, x.Category, x.ProducibleItemId, producibles.GetValueOrDefault(x.ProducibleItemId)?.Name ?? "Item indisponível", x.Availability)).ToArray(),
            menu.Offers.OrderBy(x => x.DisplayOrder).Select(x => new MenuOfferView(x.Id, x.OfferId, offers.GetValueOrDefault(x.OfferId)?.Name ?? "Oferta indisponível",
                offers.GetValueOrDefault(x.OfferId)?.Description, x.EffectivePrice, x.Availability, x.DisplayOrder,
                offers.GetValueOrDefault(x.OfferId)?.RequiresMenuChoice ?? false)).ToArray())).ToArray();
    }
}
