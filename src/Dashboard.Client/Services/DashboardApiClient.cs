using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dashboard.Client.Options;
using Dashboard.Shared;
using Microsoft.Extensions.Options;

namespace Dashboard.Client.Services;

public sealed class DashboardApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private readonly HttpClient _httpClient;
    private readonly string? _tenantId;
    private readonly string? _subscriptionId;

    public DashboardApiClient(HttpClient httpClient, IOptions<FoundryScopeOptions> foundryScopeOptions)
    {
        _httpClient = httpClient;
        _tenantId = foundryScopeOptions.Value.TenantId;
        _subscriptionId = foundryScopeOptions.Value.SubscriptionId;
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(AppendScopeQueryParameters("api/projects"), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectSummary>>(SerializerOptions, cancellationToken);
        return projects ?? [];
    }

    public async Task<BillingSummary> GetBillingAsync(
        string projectId,
        BillingPeriod period,
        CancellationToken cancellationToken)
    {
        var periodText = period switch
        {
            BillingPeriod.CurrentMonth => "currentMonth",
            BillingPeriod.PreviousMonth => "previousMonth",
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported billing period.")
        };

        var path = AppendScopeQueryParameters(
            $"api/projects/{Uri.EscapeDataString(projectId)}/billing?period={periodText}");
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var summary = await response.Content.ReadFromJsonAsync<BillingSummary>(SerializerOptions, cancellationToken);
        if (summary is null)
        {
            throw new ApiClientException((int)response.StatusCode, "API returned an empty billing response.");
        }

        return summary;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryExtractMessage(responseBody)
            ?? $"API request failed with status code {(int)response.StatusCode} ({response.StatusCode}).";
        throw new ApiClientException((int)response.StatusCode, message);
    }

    private static string? TryExtractMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString();
                }

                if (root.TryGetProperty("detail", out var detailElement))
                {
                    return detailElement.GetString();
                }

                if (root.TryGetProperty("title", out var titleElement))
                {
                    return titleElement.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return responseBody;
        }

        return responseBody;
    }

    private string AppendScopeQueryParameters(string path)
    {
        if (string.IsNullOrWhiteSpace(_tenantId) && string.IsNullOrWhiteSpace(_subscriptionId))
        {
            return path;
        }

        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var scopedPath = path;

        if (!string.IsNullOrWhiteSpace(_tenantId))
        {
            scopedPath = $"{scopedPath}{separator}tenantId={Uri.EscapeDataString(_tenantId)}";
            separator = '&';
        }

        if (!string.IsNullOrWhiteSpace(_subscriptionId))
        {
            scopedPath = $"{scopedPath}{separator}subscriptionId={Uri.EscapeDataString(_subscriptionId)}";
        }

        return scopedPath;
    }
}
