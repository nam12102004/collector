namespace attendanceCollector.Application.Contracts.Attendance;

public sealed record DeviceAttendanceEntry(
    string EmployeeCode,
    string EmployeeName,
    DateTime DeviceLocalTime,
    int Status,
    int Punch,
    long? SourceRecordUid);
