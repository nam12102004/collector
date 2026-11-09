namespace attendanceCollector.Application.Contracts;

public sealed class CollectorIntegrationOptions
{
    public bool Enabled { get; init; }
    public int SnapshotLifetimeMinutes { get; init; } = 15;
    public int MaximumSnapshotRows { get; init; } = 100_000;
    public long MaximumSnapshotBytes { get; init; } = 64 * 1024 * 1024;
    public int MaximumSnapshotsPerClient { get; init; } = 4;
    public int MaximumSnapshotsTotal { get; init; } = 32;
    public int MaximumResponseBytes { get; init; } = 1024 * 1024;
    public int RequestsPerMinute { get; init; } = 60;
    public int ConcurrentRequestsPerClient { get; init; } = 2;
    public int ConcurrentRequestsTotal { get; init; } = 8;
    public int RequestTimeoutSeconds { get; init; } = 20;
    public int DatabaseTimeoutSeconds { get; init; } = 10;
}

public sealed class CollectorLogRequest
{
    public string FromUtc { get; init; } = "";
    public string ToUtc { get; init; } = "";
    public int PageSize { get; init; } = 500;
    public string? PageToken { get; init; }
}

public sealed record CollectorRawLog(string? DeviceUserId, long? DeviceUid, string? DeviceName,
    DateTime OccurredAtLocal, int RawStatus, int RawPunch);
public sealed record CollectorLogPage(string DeviceId, IReadOnlyList<CollectorRawLog> Items, string? NextPageToken);
public sealed record CollectorReadCriteria(Guid DeviceId, DateTime FromUtc, DateTime ToUtc, int PageSize, string FilterHash);
public sealed record CollectorPageCursor(int Version, Guid ClientId, Guid DeviceId, string FilterHash,
    Guid SnapshotId, long Ordinal, DateTime ExpiresAtUtc);
public sealed record CollectorSnapshotPage(Guid SnapshotId, DateTime ExpiresAtUtc,
    IReadOnlyList<CollectorRawLog> Items, long LastOrdinal, bool HasMore);
public sealed record IntegrationIdentity(Guid ClientId, Guid KeyId);

public sealed record CreateIntegrationClientRequest(string Name, IReadOnlyList<Guid> DeviceIds, bool AllowCollection = false);
public sealed record ReplaceIntegrationGrantsRequest(Guid Version, IReadOnlyList<Guid> DeviceIds);
public sealed record SetIntegrationActiveRequest(Guid Version, bool IsActive);
public sealed record CreateIntegrationKeyRequest(DateTimeOffset? ExpiresAtUtc = null);
public sealed record IntegrationClientDto(Guid Id, string Name, bool IsActive, Guid Version,
    IReadOnlyList<Guid> DeviceIds, IReadOnlyList<IntegrationKeyDto> Keys);
public sealed record IntegrationKeyDto(Guid Id, DateTime CreatedAtUtc, DateTime? ExpiresAtUtc, DateTime? RevokedAtUtc);
public sealed record IntegrationKeySecret(Guid KeyId, string ApiKey, DateTime? ExpiresAtUtc);
public sealed record IntegrationClientPage(IReadOnlyList<IntegrationClientDto> Items, int Page, int PageSize, int TotalCount);
public sealed record IntegrationReadAudit(Guid Id, DateTime StartedAtUtc, Guid? ClientId, Guid? DeviceId,
    string RequestId, DateTime? FromUtc, DateTime? ToUtc, int? PageSize);
