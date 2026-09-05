using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Finance;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Persistence;

public sealed class AuditPersistenceTests
{
    [Fact]
    public async Task Critical_write_records_trusted_actor_tenant_time_and_correlation()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 5, 2, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new RequestContextFake(organizationId, actorId, "corr-123");
        await using var database = new AppDbContext(options, context, context,
            new FixedTimeProvider(now));

        var movement = FinancialCreditMovement.Grant(organizationId, Guid.NewGuid(), 25m,
            "Ajuste autorizado", actorId, now);
        database.FinancialCreditMovements.Add(movement);
        await database.SaveChangesAsync();

        var audit = await database.AuditEvents.SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal("FinancialCredit.Granted", audit.Action);
        Assert.Equal(movement.Id, audit.ResourceId);
        Assert.Equal(now, audit.OccurredAt);
        Assert.Equal("corr-123", audit.CorrelationId);
    }

    private sealed class RequestContextFake(Guid organizationId, Guid userId, string correlationId)
        : IOrganizationContext, ICurrentUserContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
        public Guid UserId { get; } = userId;
        public string CorrelationId { get; } = correlationId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
