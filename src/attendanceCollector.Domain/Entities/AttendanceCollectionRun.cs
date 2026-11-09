using attendanceCollector.Domain.Enums;

namespace attendanceCollector.Domain.Entities;

public sealed class AttendanceCollectionRun
{
    private AttendanceCollectionRun() { }

    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public AttendanceCollectionStatus Status { get; private set; }
    public int RecordsReceived { get; private set; }
    public int RecordsAdded { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static AttendanceCollectionRun Start(Guid deviceId, DateTime startedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = deviceId,
        StartedAtUtc = DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc),
        Status = AttendanceCollectionStatus.Running
    };

    public void Complete(int recordsReceived, int recordsAdded, DateTime completedAtUtc)
    {
        RecordsReceived = recordsReceived;
        RecordsAdded = recordsAdded;
        CompletedAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc);
        Status = AttendanceCollectionStatus.Succeeded;
        ErrorMessage = null;
    }

    public void Fail(string errorMessage, DateTime completedAtUtc)
    {
        CompletedAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc);
        Status = AttendanceCollectionStatus.Failed;
        ErrorMessage = errorMessage.Length <= 500 ? errorMessage : errorMessage[..500];
    }
}

