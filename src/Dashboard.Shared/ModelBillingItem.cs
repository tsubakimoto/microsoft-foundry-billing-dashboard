namespace Dashboard.Shared;

public sealed record ModelBillingItem(
    string ModelName,
    decimal Amount,
    string Currency);
