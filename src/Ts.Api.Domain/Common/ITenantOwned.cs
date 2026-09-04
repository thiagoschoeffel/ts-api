namespace Ts.Api.Domain.Common;

public interface ITenantOwned
{
    Guid OrganizationId { get; }
}
