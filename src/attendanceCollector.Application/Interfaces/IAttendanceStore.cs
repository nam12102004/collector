using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Domain.Entities;

namespace attendanceCollector.Application.Interfaces;

public interface IAttendanceStore
{
    Task<IReadOnlyList<AttendanceDevice>> GetDevicesAsync(CancellationToken cancellationToken);
    Task<AttendanceDevice?> GetDeviceAsync(Guid id, CancellationToken cancellationToken);
    Task<AttendanceDevice?> GetDeviceByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> DeviceCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task AddDeviceAsync(AttendanceDevice device, CancellationToken cancellationToken);
    Task UpdateDeviceAsync(AttendanceDevice device, CancellationToken cancellationToken);
    Task AddCollectionRunAsync(AttendanceCollectionRun run, CancellationToken cancellationToken);
    Task UpdateCollectionRunAsync(AttendanceCollectionRun run, CancellationToken cancellationToken);
    Task<int> AddLogsAsync(IReadOnlyCollection<AttendanceLog> logs, CancellationToken cancellationToken);
    Task<PageResult<AttendanceLogDto>> GetLogsAsync(AttendanceLogQuery query, CancellationToken cancellationToken);
    Task<PageResult<AttendanceCollectionRunDto>> GetCollectionRunsAsync(
        Guid? sourceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
