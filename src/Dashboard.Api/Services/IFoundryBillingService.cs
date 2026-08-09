using Dashboard.Shared;

namespace Dashboard.Api.Services;

public interface IFoundryBillingService
{
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(
        FoundryRequestScope requestScope,
        CancellationToken cancellationToken);

    Task<BillingSummary> GetBillingSummaryAsync(
        string projectId,
        BillingPeriod period,
        FoundryRequestScope requestScope,
        CancellationToken cancellationToken);
}
