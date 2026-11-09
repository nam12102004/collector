using attendanceCollector.Application.Contracts.Attendance;

namespace attendanceCollector.Application.Interfaces;

public interface IAttendanceCollectionService
{
    Task<AttendanceCollectionRunDto> CollectAsync(Guid sourceId, CancellationToken cancellationToken);
    Task<PageResult<AttendanceLogDto>> GetLogsAsync(AttendanceLogQuery query, CancellationToken cancellationToken);
    Task<PageResult<AttendanceCollectionRunDto>> GetRunsAsync(
        Guid? sourceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
