using Dashboard.Shared;

namespace Dashboard.Api.Services;

public sealed class SampleFoundryBillingService : IFoundryBillingService
{
    private static readonly IReadOnlyList<ProjectSummary> Projects =
    [
        new("foundry-project-alpha", "Foundry Project Alpha"),
        new("foundry-project-beta", "Foundry Project Beta")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<BillingPeriod, IReadOnlyList<ModelBillingItem>>> BillingData =
        new Dictionary<string, IReadOnlyDictionary<BillingPeriod, IReadOnlyList<ModelBillingItem>>>(StringComparer.Ordinal)
        {
            ["foundry-project-alpha"] = new Dictionary<BillingPeriod, IReadOnlyList<ModelBillingItem>>
            {
                [BillingPeriod.CurrentMonth] =
                [
                    new("gpt-4.1", 125.10m, "USD"),
                    new("gpt-4.1-mini", 42.35m, "USD"),
                    new("text-embedding-3-large", 19.25m, "USD")
                ],
                [BillingPeriod.PreviousMonth] =
                [
                    new("gpt-4.1", 98.60m, "USD"),
                    new("gpt-4.1-mini", 33.20m, "USD"),
                    new("text-embedding-3-large", 15.90m, "USD")
                ]
            },
            ["foundry-project-beta"] = new Dictionary<BillingPeriod, IReadOnlyList<ModelBillingItem>>
            {
                [BillingPeriod.CurrentMonth] =
                [
                    new("gpt-4o", 88.40m, "USD"),
                    new("o4-mini", 22.80m, "USD")
                ],
                [BillingPeriod.PreviousMonth] =
                [
                    new("gpt-4o", 74.00m, "USD"),
                    new("o4-mini", 18.10m, "USD")
                ]
            }
        };

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(
        FoundryRequestScope requestScope,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Projects);
    }

    public Task<BillingSummary> GetBillingSummaryAsync(
        string projectId,
        BillingPeriod period,
        FoundryRequestScope requestScope,
        CancellationToken cancellationToken)
    {
        if (!BillingData.TryGetValue(projectId, out var periodMap))
        {
            throw new KeyNotFoundException($"Project '{projectId}' was not found in sample billing data.");
        }

        if (!periodMap.TryGetValue(period, out var modelItems))
        {
            throw new KeyNotFoundException($"Billing data was not found for period '{period}' in project '{projectId}'.");
        }

        var totalAmount = modelItems.Sum(static item => item.Amount);
        var currency = modelItems.FirstOrDefault()?.Currency ?? "USD";
        var summary = new BillingSummary(projectId, period, totalAmount, currency, modelItems);
        return Task.FromResult(summary);
    }
}
