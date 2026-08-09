using Dashboard.Api.Services;
using Dashboard.Shared;

namespace Dashboard.Api.Tests;

public class SampleFoundryBillingServiceTests
{
    private readonly SampleFoundryBillingService _service = new();

    [Fact]
    public async Task GetProjectsAsync_ReturnsProjects()
    {
        var projects = await _service.GetProjectsAsync(new FoundryRequestScope(null, null), CancellationToken.None);

        Assert.NotEmpty(projects);
        Assert.Contains(projects, project => project.ProjectId == "foundry-project-alpha");
    }

    [Fact]
    public async Task GetBillingSummaryAsync_AggregatesModelAmounts()
    {
        var summary = await _service.GetBillingSummaryAsync(
            "foundry-project-alpha",
            BillingPeriod.CurrentMonth,
            new FoundryRequestScope(null, null),
            CancellationToken.None);

        var expectedTotal = summary.ModelItems.Sum(item => item.Amount);
        Assert.Equal(expectedTotal, summary.TotalAmount);
        Assert.Equal("USD", summary.Currency);
    }

    [Theory]
    [InlineData(BillingPeriod.CurrentMonth)]
    [InlineData(BillingPeriod.PreviousMonth)]
    public async Task GetBillingSummaryAsync_SetsRequestedPeriod(BillingPeriod requestedPeriod)
    {
        var summary = await _service.GetBillingSummaryAsync(
            "foundry-project-beta",
            requestedPeriod,
            new FoundryRequestScope(null, null),
            CancellationToken.None);

        Assert.Equal(requestedPeriod, summary.Period);
    }
}
