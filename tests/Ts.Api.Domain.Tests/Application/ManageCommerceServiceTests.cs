using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Application.Commerce;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Application;

public sealed class ManageCommerceServiceTests
{
    [Fact]
    public async Task QuickAuthoring_CreatesActiveCustomerAndAppendsAddress()
    {
        var organizationId = Guid.NewGuid();
        var context = new OrganizationContext(organizationId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var database = new AppDbContext(options, context);
        var service = new CommerceService(new CommerceStore(database, context, TimeProvider.System), context, context, TimeProvider.System);

        var customerId = await service.CreateQuickCustomerAsync(
            new QuickCustomerInput("Maria Silva", "(47) 99999-0000"), CancellationToken.None);
        var addressId = await service.AddCustomerAddressAsync(customerId,
            new AddressInput("Casa", "Rua das Flores", "123", null, "Centro", "Rio Negrinho", "SC", "89297-515", null),
            CancellationToken.None);

        var address = await database.CustomerAddresses.SingleAsync();
        var customer = await database.Customers.SingleAsync();
        Assert.Equal(addressId, address.Id);
        Assert.Equal(customerId, address.CustomerId);
        Assert.Equal("Maria Silva", customer.Name);
        Assert.True(customer.IsActive);
        Assert.Equal("Rua das Flores", address.Street);
    }

    private sealed class OrganizationContext(Guid id) : IOrganizationContext, ICurrentUserContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId => id;
        public Guid UserId { get; } = Guid.NewGuid();
        public string CorrelationId => "commerce-tests";
    }
}
