using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ts.Api.Application.Common;
using Ts.Api.Application.Menus;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Menus;
using Ts.Api.Domain.Production;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ManageMenusHandlerTests
{
    [Fact]
    public async Task PublicationIsExplicitAndWeeklyDerivationDoesNotOverwriteExistingDay()
    {
        var organizationId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
        await using var database = new AppDbContext(options, new OrganizationContext(organizationId));
        var offer = CatalogOffer.Create(organizationId, "Prato do dia", OfferFulfillmentMode.DailyProduction,
            basePrice: 30, requiresMenuChoice: true);
        var producible = ProducibleItem.Create(organizationId, "Estrogonofe");
        database.AddRange(offer, producible); await database.SaveChangesAsync();
        var context = new OrganizationContext(organizationId);
        var service = new MenuService(new MenuStore(database), context, context, new FixedClock(now));
        var monday = new DateOnly(2026, 9, 7);
        var input = Menu(monday, offer.Id, producible.Id, 31);

        var draft = await service.SaveAsync(input, CancellationToken.None);
        Assert.Equal(DailyMenuStatus.Draft, draft.Status);
        var published = await service.PublishAsync(monday, draft.Version, CancellationToken.None);
        Assert.Equal(DailyMenuStatus.Published, published.Status);

        var changedPlanDay = Menu(monday, offer.Id, producible.Id, 99);
        var tuesday = Menu(monday.AddDays(1), offer.Id, producible.Id, 32);
        await service.SavePlanAsync(new WeeklyPlanInput(monday, [changedPlanDay, tuesday]), true, CancellationToken.None);

        var persistedMonday = await service.GetAsync(monday, CancellationToken.None);
        var persistedTuesday = await service.GetAsync(monday.AddDays(1), CancellationToken.None);
        Assert.Equal(31, Assert.Single(persistedMonday.Offers).EffectivePrice);
        Assert.Equal(DailyMenuStatus.Published, persistedMonday.Status);
        Assert.Equal(32, Assert.Single(persistedTuesday.Offers).EffectivePrice);
        Assert.Equal(DailyMenuStatus.Draft, persistedTuesday.Status);
    }

    [Fact]
    public async Task ImportReportsInvalidReferencesAndDoesNotPartiallyPersist()
    {
        var organizationId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
        await using var database = new AppDbContext(options, new OrganizationContext(organizationId));
        var context = new OrganizationContext(organizationId);
        var service = new MenuService(new MenuStore(database), context, context, new FixedClock(DateTimeOffset.UtcNow));
        var result = await service.ImportAsync([Menu(new DateOnly(2026, 9, 8), Guid.NewGuid(), Guid.NewGuid(), 20)], CancellationToken.None);
        Assert.Empty(result.CreatedDates);
        Assert.Single(result.Issues);
        Assert.Empty(await database.DailyMenus.ToListAsync());
    }

    private static DailyMenuInput Menu(DateOnly date, Guid offerId, Guid producibleId, decimal price) => new(date,
        [new MenuOptionInput("Tradicional", producibleId, MenuAvailability.Available)],
        [new MenuOfferInput(offerId, price, MenuAvailability.Available, 1)]);
    private sealed class OrganizationContext(Guid id) : IOrganizationContext, ICurrentUserContext
    { public bool IsAvailable => true; public Guid OrganizationId => id; public Guid UserId { get; } = Guid.NewGuid(); public string CorrelationId => "menu-tests"; }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
