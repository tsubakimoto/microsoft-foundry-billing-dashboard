namespace Dashboard.Client.Options;

public sealed class ClientAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public bool Enabled { get; set; } = true;

    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    public bool ValidateAuthority { get; set; } = true;

    public List<string> DefaultScopes { get; set; } = [];

    public List<string> AdditionalScopesToConsent { get; set; } = [];
}
