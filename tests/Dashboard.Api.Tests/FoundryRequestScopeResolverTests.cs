using System.Security.Claims;
using Dashboard.Api.Options;
using Dashboard.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Dashboard.Api.Tests;

public class FoundryRequestScopeResolverTests
{
    [Fact]
    public void Resolve_UsesConfiguredScope_WhenRequestScopeIsEmpty()
    {
        var resolver = CreateResolver(
            new FoundryOptions
            {
                Provider = FoundryOptions.HttpProvider,
                TenantId = "tenant-a",
                SubscriptionId = "sub-a"
            },
            new AuthenticationOptions { Required = false });

        var context = new DefaultHttpContext();

        var resolved = resolver.Resolve(context.Request, tenantId: null, subscriptionId: null);

        Assert.Equal("tenant-a", resolved.ResolvedTenantId);
        Assert.Equal("sub-a", resolved.ResolvedSubscriptionId);
        Assert.Equal("tenant-a", resolved.RequestScope.TenantId);
        Assert.Equal("sub-a", resolved.RequestScope.SubscriptionId);
    }

    [Fact]
    public void Resolve_ThrowsForbidden_WhenTenantIsOutsideAllowList()
    {
        var resolver = CreateResolver(
            new FoundryOptions
            {
                Provider = FoundryOptions.HttpProvider,
                TenantId = "tenant-a",
                SubscriptionId = "sub-a",
                AllowedTenantIds = ["tenant-a"],
                AllowedSubscriptionIds = ["sub-a"]
            },
            new AuthenticationOptions { Required = false });

        var context = new DefaultHttpContext();

        var exception = Assert.Throws<FoundryProviderException>(() =>
            resolver.Resolve(context.Request, tenantId: "tenant-b", subscriptionId: "sub-a"));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("foundry_scope_forbidden", exception.ErrorCode);
    }

    [Fact]
    public void Resolve_ThrowsConfigurationError_WhenHttpScopeCannotBeResolved()
    {
        var resolver = CreateResolver(
            new FoundryOptions
            {
                Provider = FoundryOptions.HttpProvider
            },
            new AuthenticationOptions { Required = false });

        var context = new DefaultHttpContext();

        var exception = Assert.Throws<FoundryProviderException>(() =>
            resolver.Resolve(context.Request, tenantId: null, subscriptionId: null));

        Assert.Equal(StatusCodes.Status500InternalServerError, exception.StatusCode);
        Assert.Equal("foundry_scope_required", exception.ErrorCode);
    }

    [Fact]
    public void Resolve_ThrowsForbidden_WhenJwtTenantClaimDoesNotMatchFoundryTenant()
    {
        var resolver = CreateResolver(
            new FoundryOptions
            {
                Provider = FoundryOptions.HttpProvider,
                TenantId = "tenant-a",
                SubscriptionId = "sub-a"
            },
            new AuthenticationOptions { Required = true });

        var context = new DefaultHttpContext
        {
            User = CreatePrincipal("tenant-b")
        };

        var exception = Assert.Throws<FoundryProviderException>(() =>
            resolver.Resolve(context.Request, tenantId: "tenant-a", subscriptionId: "sub-a"));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("jwt_tenant_mismatch", exception.ErrorCode);
    }

    [Fact]
    public void Resolve_AcceptsMappedTenantClaimType_WhenAuthenticationIsRequired()
    {
        var resolver = CreateResolver(
            new FoundryOptions
            {
                Provider = FoundryOptions.HttpProvider,
                TenantId = "tenant-a",
                SubscriptionId = "sub-a"
            },
            new AuthenticationOptions { Required = true });

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("http://schemas.microsoft.com/identity/claims/tenantid", "tenant-a")],
                    "test"))
        };

        var resolved = resolver.Resolve(context.Request, tenantId: "tenant-a", subscriptionId: "sub-a");

        Assert.Equal("tenant-a", resolved.ResolvedTenantId);
        Assert.Equal("sub-a", resolved.ResolvedSubscriptionId);
    }

    private static FoundryRequestScopeResolver CreateResolver(
        FoundryOptions foundryOptions,
        AuthenticationOptions authenticationOptions)
    {
        return new FoundryRequestScopeResolver(
            Microsoft.Extensions.Options.Options.Create(foundryOptions),
            Microsoft.Extensions.Options.Options.Create(authenticationOptions));
    }

    private static ClaimsPrincipal CreatePrincipal(string tenantId)
    {
        var identity = new ClaimsIdentity([new Claim("tid", tenantId)], "test");
        return new ClaimsPrincipal(identity);
    }
}
