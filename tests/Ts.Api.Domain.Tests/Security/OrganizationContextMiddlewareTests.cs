using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ts.Api.Api;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Organizations;
using Ts.Api.Infrastructure.Persistence;

namespace Ts.Api.Domain.Tests.Security;

public sealed class OrganizationContextMiddlewareTests
{
    [Fact]
    public async Task Blocks_anonymous_api_calls()
    {
        var fixture = await Fixture.CreateAsync(withMembership: true);

        await fixture.InvokeAsync(authenticated: false, fixture.OrganizationId);

        Assert.Equal(StatusCodes.Status401Unauthorized, fixture.HttpContext.Response.StatusCode);
        Assert.False(fixture.NextWasCalled);
    }

    [Fact]
    public async Task Blocks_an_organization_without_membership()
    {
        var fixture = await Fixture.CreateAsync(withMembership: true);

        await fixture.InvokeAsync(authenticated: true, Guid.NewGuid());

        Assert.Equal(StatusCodes.Status403Forbidden, fixture.HttpContext.Response.StatusCode);
        Assert.False(fixture.NextWasCalled);
    }

    [Fact]
    public async Task Resolves_only_an_active_membership_and_uses_platform_user_as_actor()
    {
        var fixture = await Fixture.CreateAsync(withMembership: true);

        await fixture.InvokeAsync(authenticated: true, fixture.OrganizationId);

        Assert.True(fixture.NextWasCalled);
        Assert.Equal(fixture.OrganizationId, fixture.RequestContext.OrganizationId);
        Assert.Equal(fixture.UserId, fixture.RequestContext.UserId);
        Assert.False(string.IsNullOrWhiteSpace(fixture.RequestContext.CorrelationId));
    }

    [Fact]
    public async Task Session_discovery_resolves_the_user_without_requiring_an_organization()
    {
        var fixture = await Fixture.CreateAsync(withMembership: false);

        await fixture.InvokeAsync(authenticated: true, organizationId: null, path: "/api/session");

        Assert.True(fixture.NextWasCalled);
        Assert.Equal(fixture.UserId, fixture.RequestContext.UserId);
        Assert.Throws<InvalidOperationException>(() => fixture.RequestContext.OrganizationId);
    }

    private sealed class Fixture
    {
        private readonly AppDbContext database;
        private readonly OrganizationContextMiddleware middleware;

        private Fixture(AppDbContext database, HttpRequestContext requestContext,
            DefaultHttpContext httpContext, Guid organizationId, Guid userId, string subject)
        {
            this.database = database;
            RequestContext = requestContext;
            HttpContext = httpContext;
            OrganizationId = organizationId;
            UserId = userId;
            Subject = subject;
            middleware = new OrganizationContextMiddleware(_ =>
            {
                NextWasCalled = true;
                return Task.CompletedTask;
            });
        }

        public HttpRequestContext RequestContext { get; }
        public DefaultHttpContext HttpContext { get; }
        public Guid OrganizationId { get; }
        public Guid UserId { get; }
        public string Subject { get; }
        public bool NextWasCalled { get; private set; }

        public static async Task<Fixture> CreateAsync(bool withMembership)
        {
            var organization = Organization.Create("Organização A", "organizacao-a");
            var user = PlatformUser.Create(Guid.NewGuid().ToString(), "Usuário A");
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var database = new AppDbContext(options, new OrganizationContextFake(organization.Id));
            database.Organizations.Add(organization);
            database.Users.Add(user);
            if (withMembership)
            {
                database.OrganizationMemberships.Add(OrganizationMembership.Create(
                    organization.Id, user.Id, OrganizationRole.Operator));
            }
            await database.SaveChangesAsync();
            return new Fixture(database, new HttpRequestContext(), new DefaultHttpContext(),
                organization.Id, user.Id, user.ExternalSubject);
        }

        public async Task InvokeAsync(bool authenticated, Guid? organizationId, string path = "/api/orders")
        {
            HttpContext.Request.Path = path;
            HttpContext.Response.Body = new MemoryStream();
            HttpContext.RequestServices = new ServiceCollection()
                .AddLogging()
                .AddProblemDetails()
                .BuildServiceProvider();
            if (organizationId.HasValue)
                HttpContext.Request.Headers["X-Organization-Id"] = organizationId.Value.ToString();
            if (authenticated)
            {
                HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", Subject)], "test"));
            }
            await middleware.InvokeAsync(HttpContext, RequestContext, database);
        }
    }

    private sealed class OrganizationContextFake(Guid organizationId) : IOrganizationContext
    {
        public bool IsAvailable => true;
        public Guid OrganizationId { get; } = organizationId;
    }
}
