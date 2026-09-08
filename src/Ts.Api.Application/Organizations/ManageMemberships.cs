using System.Security.Cryptography;
using System.Text;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Application.Organizations;

public sealed record MembershipResult(Guid UserId, string DisplayName, string? Email,
    OrganizationRole Role, bool IsActive, long Version);
public sealed record InvitationResult(Guid Id, string Email, OrganizationRole Role, DateTimeOffset ExpiresAt,
    DateTimeOffset? EmailSentAt, long Version);
public sealed record InvitationAcceptanceResult(Guid UserId, Guid OrganizationId, OrganizationRole Role);

public interface IInvitationEmailSender
{
    Task<string> SendAsync(Guid invitationId, string email, string organizationName,
        OrganizationRole role, string token, CancellationToken cancellationToken);
}

public interface IMembershipStore
{
    Task<IReadOnlyList<(OrganizationMembership Membership, PlatformUser User)>> GetAsync(CancellationToken token);
    Task<IReadOnlyList<OrganizationInvitation>> GetInvitationsAsync(CancellationToken token);
    Task<Organization> GetOrganizationAsync(CancellationToken token);
    Task<PlatformUser?> FindUserBySubjectAsync(string subject, CancellationToken token);
    Task<OrganizationMembership?> FindAsync(Guid organizationId, Guid userId, CancellationToken token);
    Task<OrganizationInvitation?> FindPendingInvitationAsync(string normalizedEmail, CancellationToken token);
    Task<OrganizationInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken token);
    Task<int> CountActiveOwnersAsync(Guid exceptUserId, CancellationToken token);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token);
    void Add(PlatformUser user);
    void Add(OrganizationMembership membership);
    void Add(OrganizationInvitation invitation);
    void Add(AuditEvent auditEvent);
    Task SaveChangesAsync(CancellationToken token);
}

public sealed class MembershipService(IMembershipStore store, IOrganizationContext organization,
    ICurrentUserContext currentUser, IIdentityOrganizationScope identityOrganizationScope,
    IInvitationEmailSender emailSender, TimeProvider timeProvider)
{
    public async Task<IReadOnlyCollection<MembershipResult>> GetAsync(CancellationToken token) =>
        (await store.GetAsync(token)).Select(item => Map(item.Membership, item.User)).ToArray();
    public async Task<IReadOnlyCollection<InvitationResult>> GetInvitationsAsync(CancellationToken token) =>
        (await store.GetInvitationsAsync(token)).Select(Map).ToArray();

    public async Task<InvitationResult> InviteAsync(string email, OrganizationRole role,
        string correlationId, CancellationToken token)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedEmail = OrganizationInvitation.NormalizeEmail(email);
        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var tokenHash = HashToken(plainToken);
        var invitation = await store.ExecuteSerializableAsync(async transactionalToken =>
        {
            var previous = await store.FindPendingInvitationAsync(normalizedEmail, transactionalToken);
            if (previous is not null)
            {
                previous.Revoke(now);
                await store.SaveChangesAsync(transactionalToken);
            }
            var created = OrganizationInvitation.Create(organization.OrganizationId, email, role, tokenHash,
                currentUser.UserId, now, now.AddDays(7));
            store.Add(created);
            store.Add(AuditEvent.Create(organization.OrganizationId, currentUser.UserId,
                previous is null ? "membership-invitation.created" : "membership-invitation.reissued",
                "OrganizationInvitation", created.Id, now, correlationId));
            await store.SaveChangesAsync(transactionalToken);
            return created;
        }, token);
        var org = await store.GetOrganizationAsync(token);
        var messageId = await emailSender.SendAsync(invitation.Id, invitation.Email, org.Name,
            invitation.Role, plainToken, token);
        invitation.MarkSent(messageId, timeProvider.GetUtcNow());
        await store.SaveChangesAsync(token);
        return Map(invitation);
    }

    public Task<MembershipResult> UpdateAsync(Guid userId, OrganizationRole role, bool isActive,
        long expectedVersion, string correlationId, CancellationToken token) =>
        store.ExecuteSerializableAsync<MembershipResult>(async transactionalToken =>
        {
            var membership = await store.FindAsync(organization.OrganizationId, userId, transactionalToken)
                ?? throw new ResourceNotFoundException("Associação de usuário não encontrada.");
            if (membership.Version != expectedVersion)
                throw new PreconditionFailedException("A associação foi alterada. Recarregue os dados.");
            if (membership.Role == OrganizationRole.Owner && membership.IsActive
                && (!isActive || role != OrganizationRole.Owner)
                && await store.CountActiveOwnersAsync(userId, transactionalToken) == 0)
                throw new ConflictException("A organização precisa manter ao menos um proprietário ativo.");
            membership.Update(role, isActive, expectedVersion);
            var user = (await store.GetAsync(transactionalToken)).Single(item => item.User.Id == userId).User;
            store.Add(AuditEvent.Create(organization.OrganizationId, currentUser.UserId,
                "membership.updated", "OrganizationMembership", user.Id,
                timeProvider.GetUtcNow(), correlationId));
            await store.SaveChangesAsync(transactionalToken);
            return Map(membership, user);
        }, token);

    public Task<InvitationAcceptanceResult> AcceptAsync(string token, string subject, string email,
        string displayName, string correlationId, CancellationToken cancellationToken) =>
        store.ExecuteSerializableAsync<InvitationAcceptanceResult>(async transactionalToken =>
        {
            var normalizedEmail = OrganizationInvitation.NormalizeEmail(email);
            var invitation = await store.FindInvitationByTokenHashAsync(HashToken(token), transactionalToken)
                ?? throw new ResourceNotFoundException("Convite não encontrado.");
            if (!string.Equals(invitation.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
                throw new ConflictException("O convite pertence a outro e-mail.");
            var now = timeProvider.GetUtcNow();
            if (!invitation.IsPending(now)) throw new ConflictException("O convite expirou, foi revogado ou já foi utilizado.");
            identityOrganizationScope.SelectOrganizationForInvitation(invitation.OrganizationId);
            var user = await store.FindUserBySubjectAsync(subject.Trim(), transactionalToken);
            if (user is null) { user = PlatformUser.Create(subject, displayName, normalizedEmail); store.Add(user); }
            else user.BindEmail(normalizedEmail);
            if (await store.FindAsync(invitation.OrganizationId, user.Id, transactionalToken) is not null)
                throw new ConflictException("O usuário já possui associação com esta organização.");
            store.Add(OrganizationMembership.Create(invitation.OrganizationId, user.Id, invitation.Role));
            invitation.Accept(user.Id, now);
            store.Add(AuditEvent.Create(invitation.OrganizationId, user.Id, "membership-invitation.accepted",
                "OrganizationInvitation", invitation.Id, now, correlationId));
            await store.SaveChangesAsync(transactionalToken);
            return new(user.Id, invitation.OrganizationId, invitation.Role);
        }, cancellationToken);

    internal static string HashToken(string token) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(token?.Trim() ?? string.Empty)));
    private static MembershipResult Map(OrganizationMembership membership, PlatformUser user) =>
        new(user.Id, user.DisplayName, user.Email, membership.Role, membership.IsActive, membership.Version);
    private static InvitationResult Map(OrganizationInvitation invitation) =>
        new(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt, invitation.EmailSentAt, invitation.Version);
}
