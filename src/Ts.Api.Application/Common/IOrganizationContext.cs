namespace Ts.Api.Application.Common;

public interface IOrganizationContext
{
    bool IsAvailable { get; }
    Guid OrganizationId { get; }
}

public interface ICurrentUserContext
{
    bool IsAvailable { get; }
    Guid UserId { get; }
    string CorrelationId { get; }
}
