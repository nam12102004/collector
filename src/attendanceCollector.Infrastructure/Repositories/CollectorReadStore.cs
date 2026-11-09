using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Exceptions;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace attendanceCollector.Infrastructure.Repositories;

public sealed class CollectorReadStore(AttendanceCollectorDbContext db, CollectorIntegrationOptions options) : ICollectorReadStore
{
    public async Task<CollectorSnapshotPage> CreateSnapshotAsync(Guid clientId, CollectorReadCriteria criteria,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        db.Database.SetCommandTimeout(options.DatabaseTimeoutSeconds);
        // PostgreSQL stores microseconds, while request/token timestamps may contain 100ns ticks.
        nowUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % 10, DateTimeKind.Utc);
        var fromSql = CeilingMicrosecond(criteria.FromUtc);
        var toSql = CeilingMicrosecond(criteria.ToUtc);
        // Serialize capacity reservation across replicas. READ COMMITTED gives each statement a fresh
        // snapshot after the lock; the single INSERT ... SELECT below freezes the entire read set.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7306110601)", cancellationToken);
        if (await db.AttendanceReadSnapshots.CountAsync(s => s.ExpiresAtUtc > nowUtc, cancellationToken) >= options.MaximumSnapshotsTotal
            || await db.AttendanceReadSnapshots.CountAsync(s => s.ClientId == clientId && s.ExpiresAtUtc > nowUtc, cancellationToken) >= options.MaximumSnapshotsPerClient)
            throw Capacity();

        var snapshot = new AttendanceReadSnapshot { Id = Guid.NewGuid(), ClientId = clientId, DeviceId = criteria.DeviceId,
            FilterHash = criteria.FilterHash, CreatedAtUtc = nowUtc, ExpiresAtUtc = nowUtc.AddMinutes(options.SnapshotLifetimeMinutes) };
        db.AttendanceReadSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);

        // Copy only M+1 rows in the database, including ties, before detecting a capacity overflow.
        // There is no late-commit watermark assumption and no full-range materialization in API memory.
        // Legacy mock-push rows can be projected from UTC because that producer fixes its timezone to Asia/Bangkok.
        // Other legacy rows still require capture-time local-time metadata and remain non-exportable without it.
        var rowLimit = options.MaximumSnapshotRows + 1;
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AttendanceReadSnapshotRows"
                ("SnapshotId", "Ordinal", "DeviceUserId", "DeviceUid", "DeviceName", "OccurredAtLocal", "RawStatus", "RawPunch")
            SELECT {snapshot.Id}, row_number() OVER (ORDER BY l."OccurredAtUtc", l."Id"),
                NULLIF(l."EmployeeCode", ''), l."SourceRecordUid"::bigint, NULLIF(l."EmployeeName", ''),
                COALESCE(l."OccurredAtLocal", CASE
                    WHEN d."DriverCode" = 'mock-device-push' AND d."TimeZoneId" = 'Asia/Bangkok'
                    THEN timezone('Asia/Bangkok', l."OccurredAtUtc")
                    ELSE NULL
                END), l."Status", l."Punch"
            FROM "AttendanceLogs" AS l
            INNER JOIN "AttendanceDevices" AS d ON d."Id" = l."DeviceId"
            WHERE l."DeviceId" = {criteria.DeviceId} AND l."OccurredAtUtc" >= {fromSql} AND l."OccurredAtUtc" < {toSql}
            ORDER BY l."OccurredAtUtc", l."Id"
            LIMIT {rowLimit}
            """, cancellationToken);
        if (count > options.MaximumSnapshotRows) throw Capacity();

        var rows = db.AttendanceReadSnapshotRows.Where(r => r.SnapshotId == snapshot.Id);
        // Count UTF-16 units like HRM/.NET, including two units for supplementary Unicode characters.
        const string supplementary = "[\U00010000-\U0010FFFF]";
        const string controls = "[\u0001-\u001F\u007F-\u009F]";
        var invalid = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM "AttendanceReadSnapshotRows" r
            WHERE r."SnapshotId" = {snapshot.Id} AND (
                r."OccurredAtLocal" IS NULL OR NOT isfinite(r."OccurredAtLocal") OR
                length(regexp_replace(r."DeviceUserId", {supplementary}, 'xx', 'g')) > 64 OR r."DeviceUserId" ~ {controls} OR
                length(regexp_replace(r."DeviceName", {supplementary}, 'xx', 'g')) > 128 OR r."DeviceName" ~ {controls})
            LIMIT 1
            """).ToArrayAsync(cancellationToken);
        if (invalid.Length != 0)
            throw new CollectorIntegrationException(409, CollectorIntegrationConstant.InvalidData, "Stored device data cannot be exported using this contract.");
        var estimatedBytes = await rows.SumAsync(r => (long)(256 + (r.DeviceUserId == null ? 0 : r.DeviceUserId.Length * 12)
            + (r.DeviceName == null ? 0 : r.DeviceName.Length * 12)), cancellationToken);
        if (estimatedBytes > options.MaximumSnapshotBytes) throw Capacity();

        snapshot.RowCount = count;
        if (count == 0) db.AttendanceReadSnapshots.Remove(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return count == 0 ? new(snapshot.Id, snapshot.ExpiresAtUtc, [], 0, false)
            : await ReadRowsAsync(snapshot, 0, criteria.PageSize, cancellationToken);
    }

    public async Task<CollectorSnapshotPage> ReadSnapshotAsync(CollectorPageCursor cursor, int pageSize,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        db.Database.SetCommandTimeout(options.DatabaseTimeoutSeconds);
        var snapshot = await db.AttendanceReadSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == cursor.SnapshotId
            && s.ClientId == cursor.ClientId && s.DeviceId == cursor.DeviceId && s.FilterHash == cursor.FilterHash, cancellationToken);
        if (snapshot is null || snapshot.ExpiresAtUtc <= nowUtc)
            throw new CollectorIntegrationException(400, CollectorIntegrationConstant.ExpiredToken, "The page token has expired. Start a new read.");
        if (snapshot.ExpiresAtUtc != cursor.ExpiresAtUtc || cursor.Ordinal >= snapshot.RowCount)
            throw new CollectorIntegrationException(400, CollectorIntegrationConstant.InvalidToken, "The page token is invalid.");
        return await ReadRowsAsync(snapshot, cursor.Ordinal, pageSize, cancellationToken);
    }

    private async Task<CollectorSnapshotPage> ReadRowsAsync(AttendanceReadSnapshot snapshot, long ordinal, int pageSize,
        CancellationToken cancellationToken)
    {
        var rows = await db.AttendanceReadSnapshotRows.AsNoTracking().Where(r => r.SnapshotId == snapshot.Id && r.Ordinal > ordinal)
            .OrderBy(r => r.Ordinal).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        var count = Math.Min(rows.Length, pageSize);
        // A snapshot may be invalidated/deleted by an operator; never return an incomplete read as final.
        if (count != Math.Min(pageSize, snapshot.RowCount - ordinal))
            throw new CollectorIntegrationException(409, CollectorIntegrationConstant.InvalidData, "The read snapshot is no longer available.");
        var items = rows.Take(count).Select(r => new CollectorRawLog(r.DeviceUserId, r.DeviceUid, r.DeviceName,
            DateTime.SpecifyKind(r.OccurredAtLocal!.Value, DateTimeKind.Unspecified), r.RawStatus, r.RawPunch)).ToArray();
        return new(snapshot.Id, snapshot.ExpiresAtUtc, items, count == 0 ? ordinal : rows[count - 1].Ordinal, rows.Length > pageSize);
    }

    public Task<int> DeleteExpiredSnapshotsAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        db.AttendanceReadSnapshots.Where(s => s.ExpiresAtUtc <= nowUtc).OrderBy(s => s.ExpiresAtUtc).Take(100).ExecuteDeleteAsync(cancellationToken);

    private static CollectorIntegrationException Capacity() =>
        new(503, CollectorIntegrationConstant.Capacity, "Read capacity is busy. Narrow the range or try again later.", 30);

    private static DateTime CeilingMicrosecond(DateTime value) => value.Ticks % 10 == 0 ? value
        : value.Ticks > DateTime.MaxValue.Ticks - 10 ? DateTime.MaxValue
        : new DateTime(value.Ticks + 10 - value.Ticks % 10, DateTimeKind.Utc);
}
