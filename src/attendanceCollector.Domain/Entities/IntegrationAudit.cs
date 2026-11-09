namespace attendanceCollector.Domain.Entities;

public sealed class IntegrationReadAuditEntry
{
    public Guid Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? DeviceId { get; set; }
    public string RequestId { get; set; } = "";
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? PageSize { get; set; }
    public string Outcome { get; set; } = "";
    public int? HttpStatus { get; set; }
    public int RowCount { get; set; }
    public long DurationMilliseconds { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class IntegrationAdminAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Guid? KeyId { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    // Only device IDs or the enabled flag; never a key, verifier or raw attendance data.
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
