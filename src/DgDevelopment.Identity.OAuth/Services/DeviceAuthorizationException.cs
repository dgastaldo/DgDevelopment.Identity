namespace DgDevelopment.Identity.OAuth.Services;

public sealed class DeviceAuthorizationException : InvalidOperationException
{
    public string ErrorCode { get; }

    public DeviceAuthorizationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DeviceAuthorizationException()
        : this("invalid_request", "Device authorization request failed.")
    {
    }

    public DeviceAuthorizationException(string message)
        : this("invalid_request", message)
    {
    }

    public DeviceAuthorizationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = "invalid_request";
    }
}