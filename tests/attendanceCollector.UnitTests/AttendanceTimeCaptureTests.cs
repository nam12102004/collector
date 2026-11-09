using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Services;
using attendanceCollector.Domain.Entities;
using Moq;

namespace attendanceCollector.UnitTests;

public sealed class AttendanceTimeCaptureTests
{
    [Fact]
    public async Task CollectionStoresOriginalLocalTimeAndAppliedOffset()
    {
        var local = new DateTime(2026, 11, 6, 8, 2, 15, DateTimeKind.Unspecified);
        var (store, service, device) = Create("Asia/Bangkok", local);
        AttendanceLog? captured = null;
        store.Setup(s => s.AddLogsAsync(It.IsAny<IReadOnlyCollection<AttendanceLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<AttendanceLog>, CancellationToken>((logs, _) => captured = Assert.Single(logs)).ReturnsAsync(1);
        await service.CollectAsync(device.Id, default);
        Assert.NotNull(captured);
        Assert.Equal(local, captured.OccurredAtLocal);
        Assert.Equal(DateTimeKind.Unspecified, captured.OccurredAtLocal!.Value.Kind);
        Assert.Equal(local.AddHours(-7), DateTime.SpecifyKind(captured.OccurredAtUtc, DateTimeKind.Unspecified));
        Assert.Equal("Asia/Bangkok", captured.TimeZoneIdAtCapture);
        Assert.Equal(420, captured.UtcOffsetMinutesAtCapture);
        Assert.Equal("000123", captured.EmployeeCode);
        Assert.Equal(7, captured.Status);
        Assert.Equal(9, captured.Punch);
    }

    [Theory]
    [InlineData(3, 8, 2, 30)]
    [InlineData(11, 1, 1, 30)]
    public async Task DstGapAndOverlapNeverGuessAnOffset(int month, int day, int hour, int minute)
    {
        var (store, service, device) = Create("America/New_York", new(2026, month, day, hour, minute, 0));
        var run = await service.CollectAsync(device.Id, default);
        Assert.Equal("Failed", run.Status);
        store.Verify(s => s.AddLogsAsync(It.IsAny<IReadOnlyCollection<AttendanceLog>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IAttendanceStore>, AttendanceCollectionService, AttendanceDevice) Create(string zone, DateTime local)
    {
        var now = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        var device = AttendanceDevice.Create("test", "Test", "test", "localhost", 4370, 0, 10, zone, now);
        var store = new Mock<IAttendanceStore>();
        store.Setup(s => s.GetDeviceAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var driver = new Mock<IAttendanceDeviceDriver>();
        driver.SetupGet(d => d.DriverCode).Returns("test");
        driver.Setup(d => d.FetchAsync(device, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DeviceAttendanceEntry("000123", "User", local, 7, 9, 123)]);
        return (store, new(store.Object, [driver.Object], new FixedClock(now)), device);
    }
}
