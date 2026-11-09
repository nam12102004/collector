using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Domain.Entities;

namespace attendanceCollector.Application.Interfaces;

public interface IAttendanceDeviceDriver
{
    string DriverCode { get; }
    string DisplayName { get; }
    string Description { get; }
    Task CheckConnectionAsync(AttendanceDevice device, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceAttendanceEntry>> FetchAsync(
        AttendanceDevice device,
        CancellationToken cancellationToken);
}
