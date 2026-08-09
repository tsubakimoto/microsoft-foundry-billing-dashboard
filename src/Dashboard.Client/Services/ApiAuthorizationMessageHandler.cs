using Dashboard.Client.Options;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Options;

namespace Dashboard.Client.Services;

public sealed class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public ApiAuthorizationMessageHandler(
        IAccessTokenProvider provider,
        NavigationManager navigationManager,
        IOptions<ClientAuthenticationOptions> authenticationOptions,
        IConfiguration configuration)
        : base(provider, navigationManager)
    {
        var apiBaseUri = ResolveApiBaseUri(configuration["Api:BaseUrl"], navigationManager.BaseUri);
        var scopes = authenticationOptions.Value.DefaultScopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .ToArray();

        ConfigureHandler(
            authorizedUrls: [apiBaseUri.AbsoluteUri],
            scopes: scopes);
    }

    private static Uri ResolveApiBaseUri(string? configuredBaseUrl, string hostBaseAddress)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return new Uri(hostBaseAddress, UriKind.Absolute);
        }

        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        if (Uri.TryCreate(new Uri(hostBaseAddress, UriKind.Absolute), configuredBaseUrl, out var relativeUri))
        {
            return relativeUri;
        }

        throw new InvalidOperationException(
            "Api:BaseUrl must be an absolute URI or a relative path resolvable from the host base address.");
    }
}
