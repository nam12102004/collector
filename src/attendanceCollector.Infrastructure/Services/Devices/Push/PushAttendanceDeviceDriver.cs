using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.MockDevice.Contracts;

namespace attendanceCollector.Infrastructure.Services.Devices.Push;

public sealed class PushAttendanceDeviceDriver : IAttendanceDeviceDriver
{
    public const string DriverCodeValue = MockDeviceContractConstant.DriverCode;

    public string DriverCode => DriverCodeValue;
    public string DisplayName => "Mock device (push)";
    public string Description => "Receives simulated punches from the separately running mockDevice service.";

    public Task CheckConnectionAsync(AttendanceDevice device, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A push source does not connect to a device.");

    public Task<IReadOnlyList<DeviceAttendanceEntry>> FetchAsync(
        AttendanceDevice device,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("This source sends attendance data to the API; it cannot be collected as a pull device.");
}
