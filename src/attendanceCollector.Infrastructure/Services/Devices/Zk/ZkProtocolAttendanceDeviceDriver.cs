using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace attendanceCollector.Infrastructure.Services.Devices.Zk;

public sealed class ZkProtocolAttendanceDeviceDriver : IAttendanceDeviceDriver
{
    private readonly int _maximumRecordsPerRead;
    private readonly ZkDeviceReadOptions _options;

    public ZkProtocolAttendanceDeviceDriver(IOptions<ZkDeviceReadOptions> options)
    {
        _options = options.Value;
        _maximumRecordsPerRead = Math.Clamp(_options.MaximumRecordsPerRead, 1, 200_000);
    }
    public string DriverCode => "zk-compatible-tcp";
    public string DisplayName => "ZK-compatible TCP";
    public string Description => "Reads ZK-compatible machines with 8-, 16-, or at least 40-byte attendance records.";

    public Task CheckConnectionAsync(AttendanceDevice device, CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateEndpoint(device.Host, device.Port);
        using var client = new ZkClient(device.Host, device.Port, device.CommunicationKey,
            Math.Clamp(_options.TimeoutSeconds, 1, 60), _maximumRecordsPerRead);
        client.Connect();
    }, cancellationToken);

    public async Task<IReadOnlyList<DeviceAttendanceEntry>> FetchAsync(
        AttendanceDevice device,
        CancellationToken cancellationToken)
    {
        ValidateEndpoint(device.Host, device.Port);
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var client = new ZkClient(device.Host, device.Port, device.CommunicationKey,
                Math.Clamp(_options.TimeoutSeconds, 1, 60), _maximumRecordsPerRead);
            client.Connect();
            var records = client.GetAttendance();

            return (IReadOnlyList<DeviceAttendanceEntry>)records.Select(record =>
                new DeviceAttendanceEntry(
                    record.UserId,
                    string.Empty,
                    record.Timestamp,
                    record.Status,
                    record.Punch,
                    record.Uid > 0 ? record.Uid : null)).ToArray();
        }, cancellationToken);
    }

    private void ValidateEndpoint(string host, int port)
    {
        if (!IPAddress.TryParse(host, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !IsPrivateIpv4(address)
            || port is < 1 or > 65535)
            throw new InvalidOperationException("The device endpoint must use a private IPv4 address and a valid port.");
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
