using Dashboard.Api.Services;
using Dashboard.Shared;

namespace Dashboard.Api.Tests;

public class BillingPeriodParserTests
{
    [Theory]
    [InlineData("currentMonth", BillingPeriod.CurrentMonth)]
    [InlineData("previousMonth", BillingPeriod.PreviousMonth)]
    public void TryParse_ValidValues_ReturnsExpectedPeriod(string value, BillingPeriod expected)
    {
        var parsed = BillingPeriodParser.TryParse(value, out var period);

        Assert.True(parsed);
        Assert.Equal(expected, period);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CURRENTMONTH")]
    [InlineData("CurrentMonth")]
    [InlineData("nextMonth")]
    public void TryParse_InvalidValues_ReturnsFalse(string value)
    {
        var parsed = BillingPeriodParser.TryParse(value, out _);

        Assert.False(parsed);
    }
}
