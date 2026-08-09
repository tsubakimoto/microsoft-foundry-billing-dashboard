namespace Dashboard.Api.Options;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public bool Required { get; set; } = true;

    public string? Authority { get; set; }

    public string? Audience { get; set; }

    public string? TenantId { get; set; }
}
