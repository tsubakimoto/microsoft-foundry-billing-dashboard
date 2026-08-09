using System.Security.Claims;
using Dashboard.Api.Options;
using Microsoft.Extensions.Options;

namespace Dashboard.Api.Services;

public sealed class FoundryRequestScopeResolver
{
    private const string TenantIdClaimType = "tid";
    private const string MappedTenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";

    private readonly FoundryOptions _foundryOptions;
    private readonly AuthenticationOptions _authenticationOptions;

    public FoundryRequestScopeResolver(
        IOptions<FoundryOptions> foundryOptions,
        IOptions<AuthenticationOptions> authenticationOptions)
    {
        _foundryOptions = foundryOptions.Value;
        _authenticationOptions = authenticationOptions.Value;
    }

    public ResolvedFoundryRequestScope Resolve(HttpRequest request, string? tenantId, string? subscriptionId)
    {
        var requestedTenantId = ResolveScopeValue(tenantId, request.Headers[_foundryOptions.TenantIdHeaderName]);
        var requestedSubscriptionId = ResolveScopeValue(subscriptionId, request.Headers[_foundryOptions.SubscriptionIdHeaderName]);

        if (_authenticationOptions.Required)
        {
            ValidateTenantClaim(request.HttpContext.User);
        }

        var resolvedTenantId = string.IsNullOrWhiteSpace(requestedTenantId) ? _foundryOptions.TenantId : requestedTenantId;
        var resolvedSubscriptionId = string.IsNullOrWhiteSpace(requestedSubscriptionId)
            ? _foundryOptions.SubscriptionId
            : requestedSubscriptionId;

        if (_foundryOptions.Provider == FoundryOptions.HttpProvider)
        {
            ValidateRequiredHttpScope(resolvedTenantId, resolvedSubscriptionId);
            ValidateAllowList(resolvedTenantId!, resolvedSubscriptionId!);
        }

        return new ResolvedFoundryRequestScope(
            new FoundryRequestScope(resolvedTenantId, resolvedSubscriptionId),
            resolvedTenantId,
            resolvedSubscriptionId);
    }

    private void ValidateTenantClaim(ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(_foundryOptions.TenantId))
        {
            throw new FoundryProviderException(
                StatusCodes.Status500InternalServerError,
                "Foundry:TenantId must be configured when Authentication:Required=true.",
                errorCode: "foundry_tenant_configuration_required",
                isConfigurationError: true);
        }

        var tokenTenantId = user.FindFirstValue(TenantIdClaimType) ?? user.FindFirstValue(MappedTenantIdClaimType);
        if (string.IsNullOrWhiteSpace(tokenTenantId))
        {
            throw new FoundryProviderException(
                StatusCodes.Status403Forbidden,
                "Authenticated token must include tenant claim 'tid'.",
                errorCode: "jwt_tenant_claim_missing");
        }

        if (!string.Equals(tokenTenantId, _foundryOptions.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new FoundryProviderException(
                StatusCodes.Status403Forbidden,
                "Authenticated tenant claim does not match configured Foundry tenant.",
                errorCode: "jwt_tenant_mismatch");
        }
    }

    private void ValidateRequiredHttpScope(string? tenantId, string? subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw new FoundryProviderException(
                StatusCodes.Status500InternalServerError,
                "Foundry HTTP provider requires tenant/subscription scope. Set Foundry:TenantId and Foundry:SubscriptionId or pass allowed request scope values.",
                errorCode: "foundry_scope_required",
                isConfigurationError: true);
        }
    }

    private void ValidateAllowList(string tenantId, string subscriptionId)
    {
        var allowedTenantIds = BuildAllowList(_foundryOptions.AllowedTenantIds, _foundryOptions.TenantId);
        var allowedSubscriptionIds = BuildAllowList(_foundryOptions.AllowedSubscriptionIds, _foundryOptions.SubscriptionId);

        if (allowedTenantIds.Count == 0 || allowedSubscriptionIds.Count == 0)
        {
            throw new FoundryProviderException(
                StatusCodes.Status500InternalServerError,
                "Foundry allowlist is not configured. Set Foundry:AllowedTenantIds/Foundry:AllowedSubscriptionIds (or Foundry:TenantId/Foundry:SubscriptionId).",
                errorCode: "foundry_allowlist_not_configured",
                isConfigurationError: true);
        }

        if (!allowedTenantIds.Contains(tenantId, StringComparer.OrdinalIgnoreCase) ||
            !allowedSubscriptionIds.Contains(subscriptionId, StringComparer.OrdinalIgnoreCase))
        {
            throw new FoundryProviderException(
                StatusCodes.Status403Forbidden,
                "Requested tenantId/subscriptionId is not allowed.",
                errorCode: "foundry_scope_forbidden");
        }
    }

    private static HashSet<string> BuildAllowList(IEnumerable<string>? allowListValues, string? fallbackValue)
    {
        var values = (allowListValues ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim());

        var set = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(fallbackValue))
        {
            set.Add(fallbackValue.Trim());
        }

        return set;
    }

    private static string? ResolveScopeValue(string? queryValue, string? headerValue)
    {
        return !string.IsNullOrWhiteSpace(queryValue)
            ? queryValue
            : !string.IsNullOrWhiteSpace(headerValue)
                ? headerValue
                : null;
    }
}

public sealed record ResolvedFoundryRequestScope(
    FoundryRequestScope RequestScope,
    string? ResolvedTenantId,
    string? ResolvedSubscriptionId);
