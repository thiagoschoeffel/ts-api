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

public interface IPlatformActorContext
{
    bool IsAvailable { get; }
    Guid UserId { get; }
    IReadOnlySet<string> Profiles { get; }
    IReadOnlySet<string> Capabilities { get; }
}
