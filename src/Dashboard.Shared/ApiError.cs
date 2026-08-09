namespace Dashboard.Shared;

public sealed record ApiError(
    string Code,
    string Message);
