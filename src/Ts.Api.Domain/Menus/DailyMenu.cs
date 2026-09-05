using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Menus;

public enum DailyMenuStatus { Draft, Published }
public enum MenuAvailability { Available, SoldOut, Suspended }

public sealed class DailyMenu : ITenantOwned
{
    private readonly List<DailyMenuOption> _options = [];
    private readonly List<DailyMenuOffer> _offers = [];
    private DailyMenu() { }
    private DailyMenu(Guid organizationId, DateOnly date, IReadOnlyCollection<DailyMenuOptionDefinition> options,
        IReadOnlyCollection<DailyMenuOfferDefinition> offers, DateTimeOffset now)
    {
        if (options.Count == 0 || offers.Count == 0) throw new DomainException("O cardápio deve possuir opções e ofertas.");
        Id = Guid.NewGuid(); OrganizationId = organizationId; Date = date; Status = DailyMenuStatus.Draft;
        UpdatedAt = now; ReplaceChildren(options, offers);
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateOnly Date { get; private set; }
    public DailyMenuStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; } = 1;
    public IReadOnlyCollection<DailyMenuOption> Options => _options.AsReadOnly();
    public IReadOnlyCollection<DailyMenuOffer> Offers => _offers.AsReadOnly();
    public static DailyMenu Create(Guid organizationId, DateOnly date, IReadOnlyCollection<DailyMenuOptionDefinition> options,
        IReadOnlyCollection<DailyMenuOfferDefinition> offers, DateTimeOffset now) => organizationId == Guid.Empty
        ? throw new DomainException("A organização é obrigatória.") : new(organizationId, date, options, offers, now);
    public void Update(IReadOnlyCollection<DailyMenuOptionDefinition> options, IReadOnlyCollection<DailyMenuOfferDefinition> offers,
        DateTimeOffset now, long expectedVersion)
    {
        if (expectedVersion != Version) throw new DomainException("O cardápio foi alterado por outra pessoa.");
        if (options.Count == 0 || offers.Count == 0) throw new DomainException("O cardápio deve possuir opções e ofertas.");
        _options.Clear(); _offers.Clear(); ReplaceChildren(options, offers); UpdatedAt = now; Version++;
    }
    public void Publish(DateTimeOffset now, long expectedVersion)
    {
        if (expectedVersion != Version) throw new DomainException("O cardápio foi alterado por outra pessoa.");
        Status = DailyMenuStatus.Published; PublishedAt ??= now; UpdatedAt = now; Version++;
    }
    private void ReplaceChildren(IEnumerable<DailyMenuOptionDefinition> options, IEnumerable<DailyMenuOfferDefinition> offers)
    {
        foreach (var option in options) _options.Add(DailyMenuOption.Create(OrganizationId, Id, option));
        foreach (var offer in offers) _offers.Add(DailyMenuOffer.Create(OrganizationId, Id, offer));
        if (_options.Select(x => x.Category.ToUpperInvariant()).Distinct().Count() != _options.Count)
            throw new DomainException("As categorias do cardápio não podem se repetir.");
        if (_offers.Select(x => x.OfferId).Distinct().Count() != _offers.Count || _offers.Select(x => x.DisplayOrder).Distinct().Count() != _offers.Count)
            throw new DomainException("Ofertas e ordens do cardápio não podem se repetir.");
    }
}

public sealed class DailyMenuOption : ITenantOwned
{
    private DailyMenuOption() { }
    private DailyMenuOption(Guid organizationId, Guid menuId, DailyMenuOptionDefinition value)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; DailyMenuId = menuId; Category = Required(value.Category); ProducibleItemId = value.ProducibleItemId; Availability = value.Availability; }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DailyMenuId { get; private set; }
    public string Category { get; private set; } = "";
    public Guid ProducibleItemId { get; private set; }
    public MenuAvailability Availability { get; private set; }
    internal static DailyMenuOption Create(Guid organizationId, Guid menuId, DailyMenuOptionDefinition value) => value.ProducibleItemId == Guid.Empty || !Enum.IsDefined(value.Availability)
        ? throw new DomainException("A opção do cardápio é inválida.") : new(organizationId, menuId, value);
    private static string Required(string value) { var result = value?.Trim() ?? ""; return result.Length is > 0 and <= 100 ? result : throw new DomainException("A categoria do cardápio é inválida."); }
}

public sealed class DailyMenuOffer : ITenantOwned
{
    private DailyMenuOffer() { }
    private DailyMenuOffer(Guid organizationId, Guid menuId, DailyMenuOfferDefinition value)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; DailyMenuId = menuId; OfferId = value.OfferId; EffectivePrice = decimal.Round(value.EffectivePrice, 2); Availability = value.Availability; DisplayOrder = value.DisplayOrder; }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DailyMenuId { get; private set; }
    public Guid OfferId { get; private set; }
    public decimal EffectivePrice { get; private set; }
    public MenuAvailability Availability { get; private set; }
    public int DisplayOrder { get; private set; }
    internal static DailyMenuOffer Create(Guid organizationId, Guid menuId, DailyMenuOfferDefinition value) =>
        value.OfferId == Guid.Empty || value.EffectivePrice < 0 || value.DisplayOrder <= 0 || !Enum.IsDefined(value.Availability)
            ? throw new DomainException("A oferta do cardápio é inválida.") : new(organizationId, menuId, value);
}

public sealed record DailyMenuOptionDefinition(string Category, Guid ProducibleItemId, MenuAvailability Availability);
public sealed record DailyMenuOfferDefinition(Guid OfferId, decimal EffectivePrice, MenuAvailability Availability, int DisplayOrder);

public sealed class WeeklyMenuPlan : ITenantOwned
{
    private WeeklyMenuPlan() { }
    private WeeklyMenuPlan(Guid organizationId, DateOnly weekStart, string daysJson, DateTimeOffset now)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; WeekStart = weekStart; DaysJson = daysJson; CreatedAt = now; UpdatedAt = now; }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public DateOnly WeekStart { get; private set; }
    public string DaysJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static WeeklyMenuPlan Create(Guid organizationId, DateOnly weekStart, string daysJson, DateTimeOffset now) =>
        organizationId == Guid.Empty || string.IsNullOrWhiteSpace(daysJson) ? throw new DomainException("O planejamento semanal é inválido.") : new(organizationId, weekStart, daysJson, now);
    public void Update(string daysJson, DateTimeOffset now) { DaysJson = string.IsNullOrWhiteSpace(daysJson) ? throw new DomainException("O planejamento semanal é inválido.") : daysJson; UpdatedAt = now; }
}
