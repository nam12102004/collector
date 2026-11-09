using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace attendanceCollector.Infrastructure.Repositories;

public sealed class AttendanceStore(AttendanceCollectorDbContext db) : IAttendanceStore
{
    public async Task<IReadOnlyList<AttendanceDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
        await db.AttendanceDevices.AsNoTracking().OrderBy(device => device.Name).ToListAsync(cancellationToken);

    public Task<AttendanceDevice?> GetDeviceAsync(Guid id, CancellationToken cancellationToken) =>
        db.AttendanceDevices.SingleOrDefaultAsync(device => device.Id == id, cancellationToken);

    public Task<AttendanceDevice?> GetDeviceByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.AttendanceDevices.SingleOrDefaultAsync(device => device.Code == code, cancellationToken);

    public Task<bool> DeviceCodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.AttendanceDevices.AnyAsync(
            device => device.Code == code && (!exceptId.HasValue || device.Id != exceptId.Value),
            cancellationToken);

    public async Task AddDeviceAsync(AttendanceDevice device, CancellationToken cancellationToken)
    {
        db.AttendanceDevices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDeviceAsync(AttendanceDevice device, CancellationToken cancellationToken)
    {
        db.AttendanceDevices.Update(device);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCollectionRunAsync(AttendanceCollectionRun run, CancellationToken cancellationToken)
    {
        db.AttendanceCollectionRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCollectionRunAsync(AttendanceCollectionRun run, CancellationToken cancellationToken)
    {
        db.AttendanceCollectionRuns.Update(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AddLogsAsync(IReadOnlyCollection<AttendanceLog> logs, CancellationToken cancellationToken)
    {
        var added = 0;
        foreach (var batch in logs.Chunk(400))
        {
            var fingerprints = batch.Select(log => log.Fingerprint).Distinct().ToArray();
            var candidateUids = batch.Where(log => log.SourceRecordUid.HasValue)
                .Select(log => log.SourceRecordUid!.Value).Distinct().ToArray();
            var existing = await db.AttendanceLogs
                .Where(log => fingerprints.Contains(log.Fingerprint)
                    || (log.SourceRecordUid.HasValue && candidateUids.Contains(log.SourceRecordUid.Value)
                        && batch.Select(x => x.DeviceId).Contains(log.DeviceId)))
                .Select(log => new { log.Fingerprint, log.DeviceId, log.SourceRecordUid, log.OccurredAtUtc, log.Status, log.Punch })
                .ToHashSetAsync(cancellationToken);
            var newLogs = batch.Where(log => !existing.Any(row => row.Fingerprint == log.Fingerprint
                || (log.SourceRecordUid.HasValue && row.DeviceId == log.DeviceId
                    && row.SourceRecordUid == log.SourceRecordUid && row.OccurredAtUtc == log.OccurredAtUtc
                    && row.Status == log.Status && row.Punch == log.Punch))).ToArray();
            if (newLogs.Length == 0) continue;

            db.AttendanceLogs.AddRange(newLogs);
            await db.SaveChangesAsync(cancellationToken);
            added += newLogs.Length;
        }

        return added;
    }

    public async Task<PageResult<AttendanceLogDto>> GetLogsAsync(
        AttendanceLogQuery query,
        CancellationToken cancellationToken)
    {
        var rows = from log in db.AttendanceLogs.AsNoTracking()
                   join device in db.AttendanceDevices.AsNoTracking() on log.DeviceId equals device.Id
                   select new { log, device };
        if (query.SourceId.HasValue) rows = rows.Where(row => row.log.DeviceId == query.SourceId.Value);
        if (!string.IsNullOrWhiteSpace(query.EmployeeCode))
        {
            var code = query.EmployeeCode.Trim();
            rows = rows.Where(row => row.log.EmployeeCode.Contains(code));
        }
        if (query.FromUtc.HasValue) rows = rows.Where(row => row.log.OccurredAtUtc >= query.FromUtc.Value.UtcDateTime);
        if (query.ToUtc.HasValue) rows = rows.Where(row => row.log.OccurredAtUtc <= query.ToUtc.Value.UtcDateTime);

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(row => row.log.OccurredAtUtc)
            .ThenByDescending(row => row.log.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new AttendanceLogDto(
                row.log.Id,
                row.device.Id,
                row.device.Code,
                row.device.Name,
                row.log.EmployeeCode,
                row.log.EmployeeName,
                row.log.OccurredAtUtc,
                row.log.Status,
                row.log.Punch,
                row.log.SourceRecordUid))
            .ToListAsync(cancellationToken);

        return new PageResult<AttendanceLogDto>(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<PageResult<AttendanceCollectionRunDto>> GetCollectionRunsAsync(
        Guid? sourceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = from run in db.AttendanceCollectionRuns.AsNoTracking()
                   join device in db.AttendanceDevices.AsNoTracking() on run.DeviceId equals device.Id
                   select new { run, device };
        if (sourceId.HasValue) rows = rows.Where(row => row.run.DeviceId == sourceId.Value);

        var totalCount = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(row => row.run.StartedAtUtc)
            .ThenByDescending(row => row.run.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new AttendanceCollectionRunDto(
                row.run.Id,
                row.device.Id,
                row.device.Code,
                row.device.Name,
                row.run.StartedAtUtc,
                row.run.CompletedAtUtc,
                row.run.Status.ToString(),
                row.run.RecordsReceived,
                row.run.RecordsAdded,
                row.run.ErrorMessage))
            .ToListAsync(cancellationToken);

        return new PageResult<AttendanceCollectionRunDto>(items, totalCount, page, pageSize);
    }
}
