namespace attendanceCollector.Application.Contracts.Attendance;

public sealed class AttendanceLogQuery
{
    public Guid? SourceId { get; init; }
    public string? EmployeeCode { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record AttendanceLogDto(
    Guid Id,
    Guid SourceId,
    string SourceCode,
    string SourceName,
    string EmployeeCode,
    string EmployeeName,
    DateTime OccurredAtUtc,
    int Status,
    int Punch,
    long? SourceRecordUid);

public sealed record AttendanceCollectionRunDto(
    Guid Id,
    Guid SourceId,
    string SourceCode,
    string SourceName,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Status,
    int RecordsReceived,
    int RecordsAdded,
    string? ErrorMessage);

public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
