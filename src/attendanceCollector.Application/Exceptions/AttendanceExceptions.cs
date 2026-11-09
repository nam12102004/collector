namespace attendanceCollector.Application.Exceptions;

public sealed class AttendanceSourceNotFoundException(Guid id)
    : Exception($"Attendance source '{id}' was not found.");

public sealed class AttendanceSourceConflictException(string code)
    : Exception($"An attendance source with code '{code}' already exists.");

public sealed class UnsupportedAttendanceDriverException(string code)
    : Exception($"Attendance driver '{code}' is not registered.");
