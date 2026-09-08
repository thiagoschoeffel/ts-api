namespace Ts.Api.Api;

public enum ApiContextKind
{
    Identity,
    Platform,
    Business,
    Webhook,
}

public sealed record ApiContextMetadata(ApiContextKind Kind);
public sealed record AllowUnregisteredIdentityMetadata;

public static class ApiContextEndpointExtensions
{
    public static TBuilder WithApiContext<TBuilder>(this TBuilder builder, ApiContextKind kind)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new ApiContextMetadata(kind));
        return builder;
    }
}

public static class IdentityRegistrationEndpointExtensions
{
    public static TBuilder AllowUnregisteredIdentity<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AllowUnregisteredIdentityMetadata());
        return builder;
    }
}
