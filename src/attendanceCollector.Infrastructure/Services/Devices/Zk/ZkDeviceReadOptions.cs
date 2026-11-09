namespace attendanceCollector.Infrastructure.Services.Devices.Zk;

public sealed class ZkDeviceReadOptions
{
    public const string SectionName = "AttendanceDevices:Zkteco";

    public int MaximumRecordsPerRead { get; init; } = 50_000;
    public int TimeoutSeconds { get; init; } = 15;
}
