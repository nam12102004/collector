namespace attendanceCollector.Domain.Entities;

public sealed class AttendanceDevice
{
    private AttendanceDevice() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string DriverCode { get; private set; } = string.Empty;
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public int CommunicationKey { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public string TimeZoneId { get; private set; } = "Asia/Bangkok";
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static AttendanceDevice Create(
        string code,
        string name,
        string driverCode,
        string host,
        int port,
        int communicationKey,
        int timeoutSeconds,
        string timeZoneId,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(driverCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (communicationKey < 0) throw new ArgumentOutOfRangeException(nameof(communicationKey));
        if (timeoutSeconds is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));

        return new AttendanceDevice
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Name = name.Trim(),
            DriverCode = driverCode.Trim(),
            Host = host.Trim(),
            Port = port,
            CommunicationKey = communicationKey,
            TimeoutSeconds = timeoutSeconds,
            TimeZoneId = timeZoneId.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)
        };
    }

    public void Update(
        string code,
        string name,
        string driverCode,
        string host,
        int port,
        int communicationKey,
        int timeoutSeconds,
        string timeZoneId,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(driverCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (communicationKey < 0) throw new ArgumentOutOfRangeException(nameof(communicationKey));
        if (timeoutSeconds is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));

        Code = code.Trim();
        Name = name.Trim();
        DriverCode = driverCode.Trim();
        Host = host.Trim();
        Port = port;
        CommunicationKey = communicationKey;
        TimeoutSeconds = timeoutSeconds;
        TimeZoneId = timeZoneId.Trim();
        UpdatedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }

    public void SetActive(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }
}
