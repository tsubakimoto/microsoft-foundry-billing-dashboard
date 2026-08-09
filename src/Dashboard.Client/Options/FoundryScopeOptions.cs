namespace Dashboard.Client.Options;

public sealed class FoundryScopeOptions
{
    public const string SectionName = "FoundryScope";

    public string? TenantId { get; set; }

    public string? SubscriptionId { get; set; }
}
