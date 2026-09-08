namespace Ts.Api.Api;

public enum ApiContextKind
{
    Identity,
    Platform,
    Business,
}

public sealed record ApiContextMetadata(ApiContextKind Kind);

public static class ApiContextEndpointExtensions
{
    public static TBuilder WithApiContext<TBuilder>(this TBuilder builder, ApiContextKind kind)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new ApiContextMetadata(kind));
        return builder;
    }
}
