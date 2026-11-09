namespace attendanceCollector.Domain.Entities;

public sealed class AttendanceLog
{
    private AttendanceLog() { }

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public string EmployeeCode { get; private set; } = string.Empty;
    public string EmployeeName { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public int Status { get; private set; }
    public int Punch { get; private set; }
    public long? SourceRecordUid { get; private set; }
    public DateTime? OccurredAtLocal { get; private set; }
    public string? TimeZoneIdAtCapture { get; private set; }
    public int? UtcOffsetMinutesAtCapture { get; private set; }

    public static AttendanceLog Create(
        Guid deviceId,
        string fingerprint,
        string employeeCode,
        string employeeName,
        DateTime occurredAtUtc,
        DateTime receivedAtUtc,
        int status,
        int punch,
        long? sourceRecordUid,
        DateTime? occurredAtLocal = null,
        string? timeZoneIdAtCapture = null,
        int? utcOffsetMinutesAtCapture = null) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = deviceId,
        Fingerprint = fingerprint,
        EmployeeCode = employeeCode,
        EmployeeName = employeeName,
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
        ReceivedAtUtc = DateTime.SpecifyKind(receivedAtUtc, DateTimeKind.Utc),
        Status = status,
        Punch = punch,
        SourceRecordUid = sourceRecordUid,
        OccurredAtLocal = occurredAtLocal.HasValue ? DateTime.SpecifyKind(occurredAtLocal.Value, DateTimeKind.Unspecified) : null,
        TimeZoneIdAtCapture = timeZoneIdAtCapture,
        UtcOffsetMinutesAtCapture = utcOffsetMinutesAtCapture
    };
}
