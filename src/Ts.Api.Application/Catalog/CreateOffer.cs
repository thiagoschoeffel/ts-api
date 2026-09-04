using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;

namespace Ts.Api.Application.Catalog;

public sealed record CreateOfferCommand(string Name, OfferFulfillmentMode FulfillmentMode);

public sealed record CreateOfferResult(Guid Id, string Name, OfferFulfillmentMode FulfillmentMode, bool IsActive);

public interface ICatalogOfferStore
{
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);
    Task AddAsync(CatalogOffer offer, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateOfferHandler(ICatalogOfferStore store, IOrganizationContext organizationContext)
{
    public async Task<CreateOfferResult> HandleAsync(
        CreateOfferCommand command,
        CancellationToken cancellationToken)
    {
        var name = command.Name?.Trim() ?? string.Empty;
        if (await store.NameExistsAsync(name, cancellationToken))
        {
            throw new ConflictException("Já existe uma oferta com esse nome.");
        }

        var offer = CatalogOffer.Create(organizationContext.OrganizationId, name, command.FulfillmentMode);
        await store.AddAsync(offer, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new CreateOfferResult(offer.Id, offer.Name, offer.FulfillmentMode, offer.IsActive);
    }
}
