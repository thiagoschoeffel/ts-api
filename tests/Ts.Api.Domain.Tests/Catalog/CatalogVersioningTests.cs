using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Production;

namespace Ts.Api.Domain.Tests.Catalog;

public sealed class CatalogVersioningTests
{
    [Fact]
    public void OfferAndProducibleUpdatesKeepStableIdentity()
    {
        var organizationId = Guid.NewGuid();
        var offer = CatalogOffer.Create(organizationId, "Prato", OfferFulfillmentMode.DailyProduction, "Descrição", 20, true);
        var producible = ProducibleItem.Create(organizationId, "Arroz", "Integral", "Acompanhamento", "g");
        var offerId = offer.Id; var producibleId = producible.Id;
        offer.Update("Prato completo", "Nova descrição", 25, false, true);
        producible.Update("Arroz integral", "Descrição", "Acompanhamento", "g", true);
        Assert.Equal(offerId, offer.Id); Assert.Equal(25, offer.BasePrice);
        Assert.Equal(producibleId, producible.Id); Assert.Equal("Arroz integral", producible.Name);
    }

    [Fact]
    public void CompositionVersionsAreImmutableSeparateSnapshots()
    {
        var organizationId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var first = ProducibleComposition.Publish(organizationId, itemId, 1, DateTimeOffset.UtcNow,
            [new ProducibleComponentDefinition("Arroz", 100, "g", [])]);
        var second = ProducibleComposition.Publish(organizationId, itemId, 2, DateTimeOffset.UtcNow.AddMinutes(1),
            [new ProducibleComponentDefinition("Arroz", 120, "g", [])]);
        Assert.Equal(100, Assert.Single(first.Components).Quantity);
        Assert.Equal(120, Assert.Single(second.Components).Quantity);
    }

    [Fact]
    public void RecursiveComponentPreservesTypedReference()
    {
        var reference = Guid.NewGuid();
        var composition = ProducibleComposition.Publish(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow,
            [new ProducibleComponentDefinition("Molho da casa", 1, "un", [], reference, "ProducibleItem")]);
        var component = Assert.Single(composition.Components);
        Assert.Equal(reference, component.ReferencedProducibleItemId);
        Assert.Equal("ProducibleItem", component.Kind);
    }
}
