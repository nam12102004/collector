namespace attendanceCollector.Application.Common.Constants;

public static class CollectorIntegrationConstant
{
    public const string Scheme = "HrmApiKey";
    public const string Policy = "HrmAttendanceRead";
    public const string ReadScope = "attendance.logs.read";
    public const string CollectScope = "attendance.logs.collect";
    public const string ReadCollectScope = ReadScope + " " + CollectScope;
    public const string ClientClaim = "integration_client_id";
    public const string KeyClaim = "integration_key_id";
    public const int MaximumPageSize = 500;
    public const int MaximumRangeDays = 31;
    public const int MaximumTokenLength = 2048;
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidToken = "INVALID_PAGE_TOKEN";
    public const string ExpiredToken = "PAGE_TOKEN_EXPIRED";
    public const string InvalidCredential = "INVALID_CREDENTIAL";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string NotFound = "DEVICE_NOT_FOUND";
    public const string InvalidData = "DATA_NOT_EXPORTABLE";
    public const string RateLimited = "RATE_LIMITED";
    public const string Capacity = "READ_CAPACITY_EXCEEDED";
    public const string Unavailable = "READ_UNAVAILABLE";
    public const string Conflict = "CONCURRENT_UPDATE";
    public const string InternalError = "INTERNAL_ERROR";
    public const string Requested = "Requested";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string ClientCreated = "INTEGRATION_CLIENT_CREATED";
    public const string GrantsChanged = "INTEGRATION_GRANTS_CHANGED";
    public const string ActiveChanged = "INTEGRATION_ACTIVE_CHANGED";
    public const string KeyCreated = "INTEGRATION_KEY_CREATED";
    public const string KeyRevoked = "INTEGRATION_KEY_REVOKED";
}
