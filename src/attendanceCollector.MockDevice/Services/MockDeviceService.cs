using System.Net.Http.Json;
using attendanceCollector.MockDevice.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace attendanceCollector.MockDevice.Services;

public sealed class MockDeviceService(
    NpgsqlDataSource dataSource,
    HttpClient httpClient,
    MockDeviceOptions options,
    TimeProvider clock)
{
    private const string DeviceTimeZoneId = "Asia/Bangkok";
    private const int MaximumPendingBatchSize = 500;

    public async Task<MockDeviceSendResultV1> SendAsync(CancellationToken cancellationToken)
    {
        await GenerateEventsAsync(cancellationToken);

        var records = await GetPendingEventsAsync(cancellationToken);
        if (records.Count == 0)
        {
            return new MockDeviceSendResultV1(0, 0, 0, null);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/attendance-ingestion/mock-device")
        {
            Content = JsonContent.Create(new MockDeviceAttendanceBatchV1(
                MockDeviceContractConstant.SourceCode,
                records))
        };
        request.Headers.TryAddWithoutValidation(MockDeviceContractConstant.ApiKeyHeaderName, options.IngestionApiKey);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MockDeviceAttendanceBatchResultV1>(cancellationToken)
            ?? throw new InvalidOperationException("Attendance collector returned an empty response.");

        await MarkSentAsync(records.Select(record => record.EventId).ToArray(), clock.GetUtcNow().UtcDateTime, cancellationToken);
        return new MockDeviceSendResultV1(records.Count, result.RecordsReceived, result.RecordsAdded, clock.GetUtcNow());
    }

    public async Task<int> GenerateEventsAsync(CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(DeviceTimeZoneId);
        var localDate = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone).Date;
        var employees = new (string Code, string Name)[]
        {
            ("EMP-1001", "Nguyễn An"), ("EMP-1002", "Trần Bình"),
            ("EMP-1003", "Lê Chi"), ("EMP-1004", "Phạm Dũng"),
            ("EMP-1005", "Hoàng Giang"), ("EMP-1006", "Võ Hạnh"),
            ("EMP-1007", "Đặng Khoa"), ("EMP-1008", "Bùi Lan"),
            ("EMP-1009", "Đỗ Minh"), ("EMP-1010", "Phan Ngọc")
        };
        var events = new List<MockDeviceAttendanceRecordV1>();
        var employeeCount = Random.Shared.Next(3, 7);
        foreach (var employee in employees.OrderBy(_ => Random.Shared.Next()).Take(employeeCount))
        {
            var arrival = Random.Shared.Next(7 * 60 + 15, 9 * 60 + 1);
            var departure = Random.Shared.Next(16 * 60 + 30, 19 * 60 + 1);
            var uid = Random.Shared.Next(100_000, 2_000_000_000);
            events.Add(NewRandomEvent(localDate, employee.Code, employee.Name, arrival, uid));
            events.Add(NewRandomEvent(localDate, employee.Code, employee.Name, departure, uid + 1));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var record in events)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO mock_device_events
                    (event_id, employee_code, employee_name, device_local_time, status, punch,
                     source_record_uid, created_at_utc, sent_at_utc)
                VALUES (@eventId, @employeeCode, @employeeName, @deviceLocalTime, @status, @punch,
                        @sourceRecordUid, @createdAtUtc, NULL)
                ON CONFLICT (event_id) DO NOTHING
                """, connection, transaction);
            command.Parameters.AddWithValue("eventId", NpgsqlDbType.Varchar, record.EventId);
            command.Parameters.AddWithValue("employeeCode", NpgsqlDbType.Varchar, record.EmployeeCode);
            command.Parameters.AddWithValue("employeeName", NpgsqlDbType.Varchar, record.EmployeeName);
            command.Parameters.AddWithValue("deviceLocalTime", NpgsqlDbType.Timestamp, record.DeviceLocalTime);
            command.Parameters.AddWithValue("status", NpgsqlDbType.Integer, record.Status);
            command.Parameters.AddWithValue("punch", NpgsqlDbType.Integer, record.Punch);
            command.Parameters.AddWithValue("sourceRecordUid", NpgsqlDbType.Integer, record.SourceRecordUid!.Value);
            command.Parameters.AddWithValue("createdAtUtc", NpgsqlDbType.TimestampTz, clock.GetUtcNow());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return events.Count;
    }

    public async Task<MockDeviceEventPageV1> GetEventsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = new NpgsqlCommand("SELECT COUNT(*) FROM mock_device_events", connection);
        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = new NpgsqlCommand("""
            SELECT event_id, employee_code, employee_name, device_local_time, status, punch,
                   source_record_uid, created_at_utc, sent_at_utc
            FROM mock_device_events
            ORDER BY created_at_utc DESC, event_id DESC
            OFFSET @offset LIMIT @limit
            """, connection);
        command.Parameters.AddWithValue("offset", NpgsqlDbType.Integer, (page - 1) * pageSize);
        command.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, pageSize);

        var items = new List<MockDeviceEventV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MockDeviceEventV1(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Unspecified),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.GetDateTime(7),
                reader.IsDBNull(8) ? null : reader.GetDateTime(8)));
        }

        return new MockDeviceEventPageV1(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<MockDeviceAttendanceRecordV1>> GetAttendanceRecordsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT event_id, employee_code, employee_name, device_local_time, status, punch, source_record_uid
            FROM mock_device_events
            ORDER BY device_local_time, event_id
            LIMIT 1000
            """, connection);

        var records = new List<MockDeviceAttendanceRecordV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MockDeviceAttendanceRecordV1(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Unspecified),
                reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return records;
    }

    private async Task<IReadOnlyList<MockDeviceAttendanceRecordV1>> GetPendingEventsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT event_id, employee_code, employee_name, device_local_time, status, punch, source_record_uid
            FROM mock_device_events
            WHERE sent_at_utc IS NULL
            ORDER BY created_at_utc, event_id
            LIMIT @limit
            """, connection);
        command.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, MaximumPendingBatchSize);

        var records = new List<MockDeviceAttendanceRecordV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MockDeviceAttendanceRecordV1(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Unspecified),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        return records;
    }

    private async Task MarkSentAsync(
        string[] eventIds,
        DateTime sentAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE mock_device_events
            SET sent_at_utc = @sentAtUtc
            WHERE event_id = ANY(@eventIds) AND sent_at_utc IS NULL
            """, connection);
        command.Parameters.AddWithValue("sentAtUtc", NpgsqlDbType.TimestampTz, sentAtUtc);
        command.Parameters.AddWithValue("eventIds", NpgsqlDbType.Array | NpgsqlDbType.Varchar, eventIds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static MockDeviceAttendanceRecordV1 NewRandomEvent(
        DateTime localDate,
        string employeeCode,
        string employeeName,
        int minutesAfterMidnight,
        int uid)
    {
        var occurredAt = localDate.AddMinutes(minutesAfterMidnight);
        var eventId = $"{MockDeviceContractConstant.SourceCode}-{Guid.NewGuid():N}";
        var isArrival = minutesAfterMidnight < 12 * 60;
        return new MockDeviceAttendanceRecordV1(
            eventId,
            employeeCode,
            employeeName,
            DateTime.SpecifyKind(occurredAt, DateTimeKind.Unspecified),
            isArrival ? 0 : 1,
            isArrival ? 0 : 1,
            uid);
    }
}

public sealed record MockDeviceSendResultV1(
    int Queued,
    int RecordsReceived,
    int RecordsAdded,
    DateTimeOffset? SentAtUtc);

public sealed record MockDeviceEventV1(
    string EventId,
    string EmployeeCode,
    string EmployeeName,
    DateTime DeviceLocalTime,
    int Status,
    int Punch,
    int? SourceRecordUid,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc);

public sealed record MockDeviceEventPageV1(
    IReadOnlyList<MockDeviceEventV1> Items,
    int TotalCount,
    int Page,
    int PageSize);
