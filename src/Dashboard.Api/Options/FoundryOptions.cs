using System.ComponentModel.DataAnnotations;

namespace Dashboard.Api.Options;

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";
    public const string SampleProvider = "Sample";
    public const string HttpProvider = "Http";

    [Required]
    public string Provider { get; set; } = SampleProvider;

    public string? Endpoint { get; set; }

    public string? TenantId { get; set; }

    public string? SubscriptionId { get; set; }

    public string[] AllowedTenantIds { get; set; } = [];

    public string[] AllowedSubscriptionIds { get; set; } = [];

    [Required]
    public string TenantIdQueryParameterName { get; set; } = "tenantId";

    [Required]
    public string SubscriptionIdQueryParameterName { get; set; } = "subscriptionId";

    [Required]
    public string TenantIdHeaderName { get; set; } = "x-ms-tenant-id";

    [Required]
    public string SubscriptionIdHeaderName { get; set; } = "x-ms-subscription-id";

    public bool IncludeTenantSubscriptionInQuery { get; set; } = true;

    public bool IncludeTenantSubscriptionInHeaders { get; set; } = true;

    public string? EntraScope { get; set; }

    [Required]
    public string ProjectsPath { get; set; } = "/projects";

    [Required]
    public string BillingPathTemplate { get; set; } = "/projects/{projectId}/billing?period={period}";

    [Range(1, 300)]
    public int CacheSeconds { get; set; } = 30;
}
