using Dashboard.Shared;

namespace Dashboard.Api.Services;

internal static class BillingPeriodParser
{
    public static bool TryParse(string? value, out BillingPeriod period)
    {
        switch (value)
        {
            case "currentMonth":
                period = BillingPeriod.CurrentMonth;
                return true;
            case "previousMonth":
                period = BillingPeriod.PreviousMonth;
                return true;
            default:
                period = default;
                return false;
        }
    }

    public static string ToQueryValue(BillingPeriod period)
    {
        return period switch
        {
            BillingPeriod.CurrentMonth => "currentMonth",
            BillingPeriod.PreviousMonth => "previousMonth",
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported billing period.")
        };
    }
}
