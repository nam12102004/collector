using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.MockDevice.Contracts;

namespace attendanceCollector.Application.Services;

public sealed class AttendanceIngestionService(
    IAttendanceStore store,
    TimeProvider clock) : IAttendanceIngestionService
{
    private const int MaximumRecordsPerBatch = 500;

    public async Task<MockDeviceAttendanceBatchResultV1> ReceiveMockDeviceBatchAsync(
        MockDeviceAttendanceBatchV1 batch,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(batch.SourceCode, MockDeviceContractConstant.SourceCode, StringComparison.Ordinal))
        {
            throw new ArgumentException("The mock device source code is invalid.", nameof(batch));
        }
        if (batch.Records is null || batch.Records.Count is < 1 or > MaximumRecordsPerBatch
            || batch.Records.Any(record => record is null))
        {
            throw new ArgumentException($"A batch must contain between 1 and {MaximumRecordsPerBatch} records.", nameof(batch));
        }

        var device = await store.GetDeviceByCodeAsync(batch.SourceCode, cancellationToken)
            ?? throw new AttendanceSourceNotFoundException(Guid.Empty);
        if (!device.IsActive || !string.Equals(device.DriverCode, MockDeviceContractConstant.DriverCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The mock device source is inactive or is not configured for push ingestion.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(device.TimeZoneId);
        var receivedAtUtc = clock.GetUtcNow().UtcDateTime;
        var records = batch.Records.Select(record => ToLog(device, record, timeZone, receivedAtUtc)).ToArray();
        var run = AttendanceCollectionRun.Start(device.Id, receivedAtUtc);
        await store.AddCollectionRunAsync(run, cancellationToken);

        var recordsAdded = await store.AddLogsAsync(records, cancellationToken);
        run.Complete(records.Length, recordsAdded, clock.GetUtcNow().UtcDateTime);
        await store.UpdateCollectionRunAsync(run, cancellationToken);
        return new MockDeviceAttendanceBatchResultV1(records.Length, recordsAdded);
    }

    private static AttendanceLog ToLog(
        AttendanceDevice device,
        MockDeviceAttendanceRecordV1 record,
        TimeZoneInfo timeZone,
        DateTime receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(record.EventId) || record.EventId.Length > 128)
            throw new ArgumentException("EventId must contain between 1 and 128 characters.");
        if (record.EmployeeName is null || record.EmployeeName.Length > 200)
            throw new ArgumentException("EmployeeName cannot exceed 200 characters.");
        if (record.Status is < 0 or > 255 || record.Punch is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(record), "Status and Punch must be between 0 and 255.");

        var localTime = DateTime.SpecifyKind(record.DeviceLocalTime, DateTimeKind.Unspecified);
        if (timeZone.IsAmbiguousTime(localTime) || timeZone.IsInvalidTime(localTime))
            throw new ArgumentException("Device local time cannot be resolved unambiguously.");
        var occurredAtUtc = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
        var fingerprintInput = string.Join('|',
            device.Id.ToString("N"),
            record.SourceRecordUid?.ToString(CultureInfo.InvariantCulture) ?? record.EventId.Trim(),
            occurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
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
            localTime,
            timeZone.Id,
            (int)(localTime - DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Unspecified)).TotalMinutes);
    }
}
