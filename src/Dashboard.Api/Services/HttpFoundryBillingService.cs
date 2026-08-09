using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using Dashboard.Api.Options;
using Dashboard.Shared;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Dashboard.Api.Services;

public sealed class HttpFoundryBillingService : IFoundryBillingService
{
    private readonly HttpClient _httpClient;
    private readonly FoundryOptions _options;
    private readonly TokenCredential _credential;

    public HttpFoundryBillingService(
        HttpClient httpClient,
        IOptions<FoundryOptions> options,
        TokenCredential credential)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _credential = credential;
    }

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(
        FoundryRequestScope requestScope,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(requestScope);
        var requestUri = BuildUri(_options.ProjectsPath, scope);
        return GetAsync<IReadOnlyList<ProjectSummary>>(requestUri, scope, cancellationToken);
    }

    public Task<BillingSummary> GetBillingSummaryAsync(
        string projectId,
        BillingPeriod period,
        FoundryRequestScope requestScope,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(requestScope);

        var billingPath = _options.BillingPathTemplate
            .Replace("{projectId}", Uri.EscapeDataString(projectId), StringComparison.Ordinal)
            .Replace("{period}", BillingPeriodParser.ToQueryValue(period), StringComparison.Ordinal);
        var requestUri = BuildUri(billingPath, scope);

        return GetAsync<BillingSummary>(requestUri, scope, cancellationToken);
    }

    private ResolvedScope ResolveScope(FoundryRequestScope requestScope)
    {
        var tenantId = string.IsNullOrWhiteSpace(requestScope.TenantId)
            ? _options.TenantId
            : requestScope.TenantId;
        var subscriptionId = string.IsNullOrWhiteSpace(requestScope.SubscriptionId)
            ? _options.SubscriptionId
            : requestScope.SubscriptionId;

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw new FoundryProviderException(
                StatusCodes.Status400BadRequest,
                "tenantId and subscriptionId must be provided via query string or Foundry:TenantId/Foundry:SubscriptionId when Foundry:Provider is 'Http'.",
                errorCode: "foundry_scope_required");
        }

        return new ResolvedScope(tenantId, subscriptionId);
    }

    private Uri BuildUri(string pathOrRelativeUri, ResolvedScope scope)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw CreateConfigurationException("Foundry:Endpoint must be configured when Foundry:Provider is set to 'Http'.");
        }

        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw CreateConfigurationException("Foundry:Endpoint must be an absolute URI.");
        }

        Uri requestUri;
        try
        {
            requestUri = Uri.TryCreate(pathOrRelativeUri, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri
                : new Uri(endpoint, pathOrRelativeUri);
        }
        catch (UriFormatException ex)
        {
            throw CreateConfigurationException(
                $"Invalid URI produced by Foundry path configuration. Path='{pathOrRelativeUri}'.",
                "foundry_uri_invalid",
                ex);
        }

        if (!_options.IncludeTenantSubscriptionInQuery)
        {
            return requestUri;
        }

        var uriWithScopeQuery = QueryHelpers.AddQueryString(
            requestUri.ToString(),
            new Dictionary<string, string?>
            {
                [_options.TenantIdQueryParameterName] = scope.TenantId,
                [_options.SubscriptionIdQueryParameterName] = scope.SubscriptionId
            });

        if (!Uri.TryCreate(uriWithScopeQuery, UriKind.Absolute, out var resolvedUri))
        {
            throw CreateConfigurationException(
                $"Invalid URI produced after applying tenant/subscription query parameters. URI='{uriWithScopeQuery}'.",
                "foundry_uri_invalid");
        }

        return resolvedUri;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        Uri requestUri,
        ResolvedScope scope,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        if (_options.IncludeTenantSubscriptionInHeaders)
        {
            request.Headers.TryAddWithoutValidation(_options.TenantIdHeaderName, scope.TenantId);
            request.Headers.TryAddWithoutValidation(_options.SubscriptionIdHeaderName, scope.SubscriptionId);
        }

        if (!string.IsNullOrWhiteSpace(_options.EntraScope))
        {
            var accessToken = await _credential.GetTokenAsync(
                new TokenRequestContext([_options.EntraScope]),
                cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }

        return request;
    }

    private async Task<T> GetAsync<T>(
        Uri requestUri,
        ResolvedScope scope,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = await CreateRequestAsync(requestUri, scope, cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateNonSuccessExceptionAsync(response, requestUri, cancellationToken);
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            if (payload is null)
            {
                throw new FoundryProviderException(
                    StatusCodes.Status502BadGateway,
                    $"Foundry HTTP provider returned an empty payload from '{requestUri}'.");
            }

            return payload;
        }
        catch (HttpRequestException ex)
        {
            throw new FoundryProviderException(
                StatusCodes.Status502BadGateway,
                $"Failed to call Foundry HTTP provider at '{requestUri}'. {ex.Message}",
                ex);
        }
        catch (CredentialUnavailableException ex)
        {
            throw new FoundryProviderException(
                StatusCodes.Status502BadGateway,
                $"No Entra credential is available to call Foundry HTTP provider at '{requestUri}'.",
                ex,
                errorCode: "entra_credential_unavailable");
        }
        catch (AuthenticationFailedException ex)
        {
            throw new FoundryProviderException(
                StatusCodes.Status502BadGateway,
                $"Failed to acquire an Entra access token for scope '{_options.EntraScope}' while calling '{requestUri}'.",
                ex,
                errorCode: "entra_token_acquisition_failed");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FoundryProviderException(
                StatusCodes.Status504GatewayTimeout,
                $"Timeout while calling Foundry HTTP provider at '{requestUri}'.",
                ex);
        }
        catch (NotSupportedException ex)
        {
            throw new FoundryProviderException(
                StatusCodes.Status502BadGateway,
                $"Foundry HTTP provider returned unsupported content at '{requestUri}'.",
                ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new FoundryProviderException(
                StatusCodes.Status502BadGateway,
                $"Foundry HTTP provider returned invalid JSON at '{requestUri}'. {ex.Message}",
                ex);
        }
        catch (FoundryProviderException)
        {
            throw;
        }
    }

    private static async Task<FoundryProviderException> CreateNonSuccessExceptionAsync(
        HttpResponseMessage response,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var truncatedBody = responseBody.Length > 500
            ? responseBody[..500]
            : responseBody;

        var statusCode = response.StatusCode == HttpStatusCode.NotFound
            ? StatusCodes.Status404NotFound
            : (int)response.StatusCode >= 500
                ? StatusCodes.Status502BadGateway
                : (int)response.StatusCode;

        var message = $"Foundry HTTP provider returned {(int)response.StatusCode} ({response.StatusCode}) for '{requestUri}'.";
        if (!string.IsNullOrWhiteSpace(truncatedBody))
        {
            message = $"{message} Body: {truncatedBody}";
        }

        return new FoundryProviderException(
            statusCode,
            message,
            errorCode: "foundry_http_non_success");
    }

    private static FoundryProviderException CreateConfigurationException(
        string message,
        string errorCode = "foundry_configuration_invalid",
        Exception? innerException = null)
    {
        return innerException is null
            ? new FoundryProviderException(
                StatusCodes.Status500InternalServerError,
                message,
                errorCode: errorCode,
                isConfigurationError: true)
            : new FoundryProviderException(
                StatusCodes.Status500InternalServerError,
                message,
                innerException,
                errorCode: errorCode,
                isConfigurationError: true);
    }

    private sealed record ResolvedScope(string TenantId, string SubscriptionId);
}
