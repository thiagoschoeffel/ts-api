using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task QueryFilters_ReturnOnlyCurrentOrganizationData()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), databaseRoot)
            .Options;

        await using (var contextA = new AppDbContext(options, new OrganizationContextFake(organizationA)))
        {
            contextA.CatalogOffers.Add(CatalogOffer.Create(
                organizationA,
                "Oferta A",
                OfferFulfillmentMode.FrozenStock));
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = new AppDbContext(options, new OrganizationContextFake(organizationB)))
        {
            contextB.CatalogOffers.Add(CatalogOffer.Create(
                organizationB,
                "Oferta B",
                OfferFulfillmentMode.FrozenStock));
            await contextB.SaveChangesAsync();
        }

        await using var queryContextA = new AppDbContext(options, new OrganizationContextFake(organizationA));
        await using var queryContextB = new AppDbContext(options, new OrganizationContextFake(organizationB));

        Assert.Equal("Oferta A", (await queryContextA.CatalogOffers.SingleAsync()).Name);
        Assert.Equal("Oferta B", (await queryContextB.CatalogOffers.SingleAsync()).Name);
    }

    [Fact]
    public async Task SaveChanges_RejectsEntityFromAnotherOrganization()
    {
        var currentOrganizationId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(
            options,
            new OrganizationContextFake(currentOrganizationId));
        context.CatalogOffers.Add(CatalogOffer.Create(
            Guid.NewGuid(),
            "Outra organização",
            OfferFulfillmentMode.FrozenStock));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());

        Assert.Equal("Não é permitido gravar dados de outra organização.", exception.Message);
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
    }
}
