using Ts.Api.Domain.Common;
using Ts.Api.Domain.Organizations;

namespace Ts.Api.Domain.Tests.Domain;

public sealed class OrganizationInvitationTests
{
    [Fact]
    public void Create_NormalizesEmailAndNeverExposesToken()
    {
        var now = DateTimeOffset.Parse("2026-09-08T12:00:00Z");
        var invitation = OrganizationInvitation.Create(Guid.NewGuid(), "  Pessoa@Example.COM ",
            OrganizationRole.Administrator, new string('A', 64), Guid.NewGuid(), now, now.AddDays(7));

        Assert.Equal("pessoa@example.com", invitation.NormalizedEmail);
        Assert.Equal(new string('A', 64), invitation.TokenHash);
        Assert.True(invitation.IsPending(now));
    }

    [Fact]
    public void Accept_IsSingleUse()
    {
        var now = DateTimeOffset.Parse("2026-09-08T12:00:00Z");
        var invitation = OrganizationInvitation.Create(Guid.NewGuid(), "pessoa@example.com",
            OrganizationRole.Operator, new string('B', 64), Guid.NewGuid(), now, now.AddDays(7));
        invitation.Accept(Guid.NewGuid(), now.AddMinutes(1));

        Assert.Throws<DomainException>(() => invitation.Accept(Guid.NewGuid(), now.AddMinutes(2)));
    }

    [Fact]
    public void MembershipUpdate_RequiresExpectedVersion()
    {
        var membership = OrganizationMembership.Create(Guid.NewGuid(), Guid.NewGuid(), OrganizationRole.Owner);
        membership.Update(OrganizationRole.Owner, true, 1);

        Assert.Equal(2, membership.Version);
        Assert.Throws<DomainException>(() => membership.Update(OrganizationRole.Administrator, true, 1));
    }
}
