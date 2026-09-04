namespace Ts.Api.Application.Common;

public interface IOrganizationContext
{
    bool IsAvailable { get; }
    Guid OrganizationId { get; }
}
