namespace attendanceCollector.Domain.Entities;

public sealed class AttendanceReadSnapshot
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid DeviceId { get; set; }
    public string FilterHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int RowCount { get; set; }
}

public sealed class AttendanceReadSnapshotRow
{
    public Guid SnapshotId { get; set; }
    public long Ordinal { get; set; }
    public string? DeviceUserId { get; set; }
    public long? DeviceUid { get; set; }
    public string? DeviceName { get; set; }
    public DateTime? OccurredAtLocal { get; set; }
    public int RawStatus { get; set; }
    public int RawPunch { get; set; }
}
