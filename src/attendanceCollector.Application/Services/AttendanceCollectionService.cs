using System.Globalization;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Application.Contracts.Attendance;
using attendanceCollector.Domain.Entities;

namespace attendanceCollector.Application.Services;

public sealed class AttendanceCollectionService(
    IAttendanceStore store,
    IEnumerable<IAttendanceDeviceDriver> drivers,
    TimeProvider clock) : IAttendanceCollectionService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> DeviceLocks = new();

    private readonly IReadOnlyDictionary<string, IAttendanceDeviceDriver> _drivers = drivers
        .ToDictionary(driver => driver.DriverCode, StringComparer.OrdinalIgnoreCase);

    public async Task<AttendanceCollectionRunDto> CollectAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var deviceLock = DeviceLocks.GetOrAdd(sourceId, static _ => new SemaphoreSlim(1, 1));
        await deviceLock.WaitAsync(cancellationToken);
        try
        {
            return await CollectCoreAsync(sourceId, cancellationToken);
        }
        finally
        {
            deviceLock.Release();
        }
    }

    private async Task<AttendanceCollectionRunDto> CollectCoreAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var device = await store.GetDeviceAsync(sourceId, cancellationToken)
            ?? throw new AttendanceSourceNotFoundException(sourceId);
        if (!device.IsActive)
        {
            throw new InvalidOperationException("This attendance source is disabled.");
        }
        if (!_drivers.TryGetValue(device.DriverCode, out var driver))
        {
            throw new UnsupportedAttendanceDriverException(device.DriverCode);
        }

        var run = AttendanceCollectionRun.Start(device.Id, clock.GetUtcNow().UtcDateTime);
        await store.AddCollectionRunAsync(run, cancellationToken);

        try
        {
            var records = await driver.FetchAsync(device, cancellationToken);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(device.TimeZoneId);
            var receivedAtUtc = clock.GetUtcNow().UtcDateTime;
            var logs = records
                .Select(record => ToLog(device, record, timeZone, receivedAtUtc))
                .GroupBy(log => log.Fingerprint, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var recordsAdded = await store.AddLogsAsync(logs, cancellationToken);

            run.Complete(records.Count, recordsAdded, clock.GetUtcNow().UtcDateTime);
            await store.UpdateCollectionRunAsync(run, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            run.Fail("Collection was cancelled.", clock.GetUtcNow().UtcDateTime);
            await store.UpdateCollectionRunAsync(run, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            run.Fail(exception.Message, clock.GetUtcNow().UtcDateTime);
            await store.UpdateCollectionRunAsync(run, CancellationToken.None);
        }

        return await MapRunAsync(run, cancellationToken);
    }

    public Task<PageResult<AttendanceLogDto>> GetLogsAsync(
        AttendanceLogQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            throw new ArgumentException("FromUtc must be earlier than or equal to ToUtc.");
        }

        return store.GetLogsAsync(query, cancellationToken);
    }

    public Task<PageResult<AttendanceCollectionRunDto>> GetRunsAsync(
        Guid? sourceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        return store.GetCollectionRunsAsync(sourceId, page, pageSize, cancellationToken);
    }

    private static AttendanceLog ToLog(
        AttendanceDevice device,
        DeviceAttendanceEntry record,
        TimeZoneInfo timeZone,
        DateTime receivedAtUtc)
    {
        var deviceLocalTime = DateTime.SpecifyKind(record.DeviceLocalTime, DateTimeKind.Unspecified);
        if (timeZone.IsAmbiguousTime(deviceLocalTime) || timeZone.IsInvalidTime(deviceLocalTime))
            throw new InvalidOperationException("Device local time cannot be resolved unambiguously.");
        var occurredAtUtc = TimeZoneInfo.ConvertTimeToUtc(deviceLocalTime, timeZone);
        var fingerprintInput = string.Join('|',
            device.Id.ToString("N"),
            record.SourceRecordUid?.ToString(CultureInfo.InvariantCulture) ?? "uid-unavailable",
            occurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
            record.Status.ToString(CultureInfo.InvariantCulture),
            record.Punch.ToString(CultureInfo.InvariantCulture));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));

        return AttendanceLog.Create(
            device.Id,
            fingerprint,
            string.Empty,
            record.EmployeeName.Trim(),
            occurredAtUtc,
            receivedAtUtc,
            record.Status,
            record.Punch,
            record.SourceRecordUid,
            deviceLocalTime,
            timeZone.Id,
            (int)(deviceLocalTime - DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Unspecified)).TotalMinutes);
    }

    private async Task<AttendanceCollectionRunDto> MapRunAsync(
        AttendanceCollectionRun run,
        CancellationToken cancellationToken)
    {
        var device = await store.GetDeviceAsync(run.DeviceId, cancellationToken);
        return new AttendanceCollectionRunDto(
            run.Id,
            run.DeviceId,
            device?.Code ?? string.Empty,
            device?.Name ?? string.Empty,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.Status.ToString(),
            run.RecordsReceived,
            run.RecordsAdded,
            run.ErrorMessage);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
    }
}
