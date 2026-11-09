namespace attendanceCollector.Application.Contracts.Attendance;

public sealed record AttendanceDriverDto(string Code, string Name, string Description);

public sealed record AttendanceSourceDto(
    Guid Id,
    string Code,
    string Name,
    string DriverCode,
    string DriverName,
    string Host,
    int Port,
    int TimeoutSeconds,
    string TimeZoneId,
    bool IsActive);
