using System.Net.Http.Json;
using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.MockDevice.Contracts;

namespace attendanceCollector.Infrastructure.Services.Devices.MockDevice;

public sealed class MockDeviceHttpAttendanceDeviceDriver : IAttendanceDeviceDriver
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };
    public const string DriverCodeValue = "mock-device-http";
    public string DriverCode => DriverCodeValue;
    public string DisplayName => "MockDevice HTTP";
    public string Description => "Reads attendance records from the standalone MockDevice service over HTTP using its IP and port.";

    public async Task CheckConnectionAsync(AttendanceDevice device, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(device.TimeoutSeconds));
        var address = new UriBuilder(Uri.UriSchemeHttp, device.Host, device.Port, "/health/live").Uri;
        using var response = await Client.GetAsync(address, timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<DeviceAttendanceEntry>> FetchAsync(
        AttendanceDevice device,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(device.TimeoutSeconds));
        var address = new UriBuilder(Uri.UriSchemeHttp, device.Host, device.Port, "/api/v1/device/attendance").Uri;
        var records = await Client.GetFromJsonAsync<MockDeviceAttendanceRecordV1[]>(address, timeout.Token)
            ?? throw new InvalidOperationException("MockDevice returned an empty attendance response.");

        return records.Select(record => new DeviceAttendanceEntry(
            record.EmployeeCode,
            record.EmployeeName,
            record.DeviceLocalTime,
            record.Status,
            record.Punch,
            record.SourceRecordUid)).ToArray();
    }
}
