using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Dashboard.Client;
using Dashboard.Client.Options;
using Dashboard.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services
    .AddOptions<ClientAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(ClientAuthenticationOptions.SectionName))
    .Validate(
        options => !options.Enabled
                   || (!string.IsNullOrWhiteSpace(options.Authority)
                       && !string.IsNullOrWhiteSpace(options.ClientId)
                       && options.DefaultScopes.Count > 0),
        "Authentication:Authority, Authentication:ClientId and Authentication:DefaultScopes are required when Authentication:Enabled=true.")
    .ValidateOnStart();

builder.Services
    .AddOptions<FoundryScopeOptions>()
    .Bind(builder.Configuration.GetSection(FoundryScopeOptions.SectionName));

var authOptions = builder.Configuration
    .GetSection(ClientAuthenticationOptions.SectionName)
    .Get<ClientAuthenticationOptions>() ?? new ClientAuthenticationOptions();

if (authOptions.Enabled)
{
    builder.Services.AddMsalAuthentication(options =>
    {
        var configuredOptions = options.ProviderOptions;
        configuredOptions.Authentication.Authority = authOptions.Authority!;
        configuredOptions.Authentication.ClientId = authOptions.ClientId!;
        configuredOptions.Authentication.ValidateAuthority = authOptions.ValidateAuthority;

        foreach (var scope in authOptions.DefaultScopes.Where(static scope => !string.IsNullOrWhiteSpace(scope)))
        {
            configuredOptions.DefaultAccessTokenScopes.Add(scope);
        }

        foreach (var scope in authOptions.AdditionalScopesToConsent.Where(static scope => !string.IsNullOrWhiteSpace(scope)))
        {
            configuredOptions.AdditionalScopesToConsent.Add(scope);
        }
    });
}

var apiBaseAddress = ResolveApiBaseUri(builder.Configuration["Api:BaseUrl"], builder.HostEnvironment.BaseAddress);
var apiClientBuilder = builder.Services.AddHttpClient<DashboardApiClient>(client => client.BaseAddress = apiBaseAddress);
if (authOptions.Enabled)
{
    apiClientBuilder.AddHttpMessageHandler<ApiAuthorizationMessageHandler>();
    builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
}

await builder.Build().RunAsync();

static Uri ResolveApiBaseUri(string? configuredBaseUrl, string hostBaseAddress)
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
