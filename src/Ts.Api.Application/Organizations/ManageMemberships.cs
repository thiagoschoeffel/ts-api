using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Application.Organizations;

public sealed record MembershipResult(
    Guid UserId, string DisplayName, string ExternalSubject, OrganizationRole Role, bool IsActive);

public interface IMembershipStore
{
    Task<IReadOnlyList<(OrganizationMembership Membership, PlatformUser User)>> GetAsync(CancellationToken token);
    Task<PlatformUser?> FindUserBySubjectAsync(string subject, CancellationToken token);
    Task<OrganizationMembership?> FindAsync(Guid userId, CancellationToken token);
    void Add(OrganizationMembership membership);
    void Add(AuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class MembershipService(
    IMembershipStore store, IOrganizationContext organization, ICurrentUserContext currentUser,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyCollection<MembershipResult>> GetAsync(CancellationToken token) =>
        (await store.GetAsync(token)).Select(item => Map(item.Membership, item.User)).ToArray();

    public async Task<MembershipResult> SaveAsync(
        Guid? userId, string externalSubject, OrganizationRole role, bool isActive,
        string correlationId, CancellationToken token)
    {
        PlatformUser user;
        OrganizationMembership membership;
        if (userId is null)
        {
            var subject = externalSubject?.Trim() ?? string.Empty;
            user = await store.FindUserBySubjectAsync(subject, token)
                ?? throw new DomainException("A identidade ainda não existe na plataforma. Provisione-a no provedor OIDC antes de criar a associação.");
            if (await store.FindAsync(user.Id, token) is not null)
                throw new ConflictException("Essa identidade já possui associação com a organização.");
            membership = OrganizationMembership.Create(organization.OrganizationId, user.Id, role);
            if (!isActive) membership.Update(role, false);
            store.Add(membership);
        }
        else
        {
            membership = await store.FindAsync(userId.Value, token)
                ?? throw new ResourceNotFoundException("Associação de usuário não encontrada.");
            user = (await store.GetAsync(token)).Single(item => item.User.Id == userId.Value).User;
            membership.Update(role, isActive);
        }

        store.Add(AuditEvent.Create(organization.OrganizationId, currentUser.UserId,
            userId is null ? "membership.created" : "membership.updated", "OrganizationMembership",
            user.Id, timeProvider.GetUtcNow(), correlationId));
        await store.SaveChangesAsync(token);
        return Map(membership, user);
    }

    private static MembershipResult Map(OrganizationMembership membership, PlatformUser user) =>
        new(user.Id, user.DisplayName, user.ExternalSubject, membership.Role, membership.IsActive);
}
