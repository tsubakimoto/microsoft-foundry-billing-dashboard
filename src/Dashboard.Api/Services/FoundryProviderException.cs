namespace Dashboard.Api.Services;

public sealed class FoundryProviderException : Exception
{
    public FoundryProviderException(
        int statusCode,
        string message,
        string errorCode = "foundry_provider_error",
        bool isConfigurationError = false)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        IsConfigurationError = isConfigurationError;
    }

    public FoundryProviderException(
        int statusCode,
        string message,
        Exception innerException,
        string errorCode = "foundry_provider_error",
        bool isConfigurationError = false)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        IsConfigurationError = isConfigurationError;
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }

    public bool IsConfigurationError { get; }
}
