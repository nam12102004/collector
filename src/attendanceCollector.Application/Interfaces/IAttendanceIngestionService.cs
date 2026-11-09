using attendanceCollector.MockDevice.Contracts;

namespace attendanceCollector.Application.Interfaces;

public interface IAttendanceIngestionService
{
    Task<MockDeviceAttendanceBatchResultV1> ReceiveMockDeviceBatchAsync(
        MockDeviceAttendanceBatchV1 batch,
        CancellationToken cancellationToken);
}
