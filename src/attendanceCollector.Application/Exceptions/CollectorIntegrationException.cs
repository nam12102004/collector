namespace attendanceCollector.Application.Exceptions;

public sealed class CollectorIntegrationException(int status, string errorCode, string message, int? retryAfterSeconds = null)
    : Exception(message)
{
    public int Status { get; } = status;
    public string ErrorCode { get; } = errorCode;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
