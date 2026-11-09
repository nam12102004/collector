namespace attendanceCollector.MockDevice.Contracts;

public static class MockDeviceContractConstant
{
    public const string SourceCode = "MOCK-DEVICE-001";
    public const string SourceName = "Máy chấm công giả lập";
    public const string DriverCode = "mock-device-push";
    public const string ApiKeyHeaderName = "X-Mock-Device-Key";
}

public sealed record MockDeviceAttendanceBatchV1(
    string SourceCode,
    IReadOnlyList<MockDeviceAttendanceRecordV1> Records);

public sealed record MockDeviceAttendanceRecordV1(
    string EventId,
    string EmployeeCode,
    string EmployeeName,
    DateTime DeviceLocalTime,
    int Status,
    int Punch,
    int? SourceRecordUid);

public sealed record MockDeviceAttendanceBatchResultV1(int RecordsReceived, int RecordsAdded);
