namespace Dashboard.Shared;

public sealed record BillingSummary(
    string ProjectId,
    BillingPeriod Period,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<ModelBillingItem> ModelItems);
