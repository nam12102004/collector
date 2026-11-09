using System.ComponentModel.DataAnnotations;

namespace attendanceCollector.Application.Contracts.Attendance;

public sealed class AttendanceSourceRequest
{
    [Required, StringLength(32, MinimumLength = 1)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string DriverCode { get; init; } = string.Empty;

    [Required, StringLength(255, MinimumLength = 1)]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 4370;

    [Range(0, int.MaxValue)]
    public int CommunicationKey { get; init; }

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 10;

    [Required, StringLength(100)]
    public string TimeZoneId { get; init; } = "Asia/Bangkok";
}
