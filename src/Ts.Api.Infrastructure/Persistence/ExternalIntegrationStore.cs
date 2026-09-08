using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ts.Api.Application.Organizations;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class ExternalIntegrationStore(AppDbContext database) : IExternalIntegrationStore
{
    public Task<Organization?> FindOrganizationAsync(Guid id, CancellationToken token) =>
        database.Organizations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, token);

    public async Task<IReadOnlyCollection<ExternalIntegrationConnection>> ListAsync(Guid organizationId,
        CancellationToken token) => await database.ExternalIntegrationConnections.IgnoreQueryFilters().AsNoTracking()
        .Where(item => item.OrganizationId == organizationId).OrderBy(item => item.Provider)
        .ThenBy(item => item.Id).ToArrayAsync(token);

    public Task<ExternalIntegrationConnection?> FindWhatsAppAsync(Guid organizationId,
        CancellationToken token) => database.ExternalIntegrationConnections.IgnoreQueryFilters()
        .SingleOrDefaultAsync(item => item.OrganizationId == organizationId
            && item.Provider == ExternalIntegrationProvider.WhatsApp, token);

    public Task<ExternalIntegrationConnection?> FindConnectionAsync(Guid connectionId,
        CancellationToken token) => database.ExternalIntegrationConnections.IgnoreQueryFilters()
        .SingleOrDefaultAsync(item => item.Id == connectionId, token);

    public Task<ExternalIntegrationSecret?> FindSecretAsync(Guid id, CancellationToken token) =>
        database.ExternalIntegrationSecrets.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == id, token);

    public async Task<bool> TryRegisterWebhookAsync(ExternalIntegrationWebhookReceipt receipt,
        CancellationToken token)
    {
        database.Add(receipt);
        try { await database.SaveChangesAsync(token); return true; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            database.Entry(receipt).State = EntityState.Detached; return false;
        }
    }

    public void Add(object entity) => database.Add(entity);
    public Task SaveChangesAsync(CancellationToken token) => database.SaveChangesAsync(token);
}
