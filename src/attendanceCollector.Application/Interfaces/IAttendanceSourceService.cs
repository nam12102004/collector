using attendanceCollector.Application.Contracts.Attendance;

namespace attendanceCollector.Application.Interfaces;

public interface IAttendanceSourceService
{
    IReadOnlyList<AttendanceDriverDto> GetSupportedDrivers();
    Task<IReadOnlyList<AttendanceSourceDto>> GetSourcesAsync(CancellationToken cancellationToken);
    Task<AttendanceSourceDto> AddAsync(AttendanceSourceRequest request, CancellationToken cancellationToken);
    Task<AttendanceSourceDto> UpdateAsync(Guid id, AttendanceSourceRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
