using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using attendanceCollector.API.Extensions;
using attendanceCollector.Application.Common.Constants;
using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using attendanceCollector.Infrastructure.Services;
using attendanceCollector.Infrastructure.Persistence.DbContext;

namespace attendanceCollector.IntegrationTests;

public sealed class CollectorHttpTests(CollectorFixture fixture) : IClassFixture<CollectorFixture>
{
    private static readonly DateTime Start = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
    private const string From = "2026-11-06T00:00:00Z";
    private const string To = "2026-11-07T00:00:00Z";
    private static string Url(Guid device, int size = 2, string? token = null, string from = From, string to = To) =>
        $"/api/v1/devices/{device:D}/attendance-logs?fromUtc={Uri.EscapeDataString(from)}&toUtc={Uri.EscapeDataString(to)}&pageSize={size}"
        + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token));

    private async Task<(Guid DeviceId, Guid ClientId, string Key)> SeedAsync(CollectorFactory factory, int count = 3, string code = "000123", string name = "Device user", bool legacy = false)
    {
        using var scope = factory.Services.CreateScope();
        var credentials = scope.ServiceProvider.GetRequiredService<IIntegrationCredentialService>();
        await using var db = fixture.OpenDb();
        var device = AttendanceDevice.Create(Guid.NewGuid().ToString("N"), "Machine name", "test", "localhost", 4370, 0, 10, "Asia/Bangkok", DateTime.UtcNow);
        var client = IntegrationClient.Create("HRM test", CollectorIntegrationConstant.ReadScope, DateTime.UtcNow);
        var secret = credentials.Create(Guid.NewGuid(), null);
        db.AttendanceDevices.Add(device);
        db.IntegrationClients.Add(client);
        db.IntegrationDeviceGrants.Add(new() { ClientId = client.Id, DeviceId = device.Id });
        db.IntegrationApiKeys.Add(IntegrationApiKey.Create(secret.KeyId, client.Id, credentials.GetVerifier(secret.ApiKey), DateTime.UtcNow, null));
        for (var i = 0; i < count; i++)
            db.AttendanceLogs.Add(AttendanceLog.Create(device.Id, Guid.NewGuid().ToString("N").PadRight(64, '0'), code, name,
                Start, DateTime.UtcNow, 7, 9, 123, legacy ? null : Start.AddHours(7), legacy ? null : "Asia/Bangkok", legacy ? null : 420));
        await db.SaveChangesAsync();
        return (device.Id, client.Id, secret.ApiKey);
    }

    [PostgresFact]
    public async Task ContractPaginationFreezesLateInsertsAndPreservesRawFields()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        using var response = await http.GetAsync(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal(new[] { "deviceId", "items", "nextPageToken" }, json.RootElement.EnumerateObject().Select(x => x.Name));
        var page = JsonSerializer.Deserialize<CollectorLogPage>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("000123", page.Items[0].DeviceUserId);
        Assert.Equal("Device user", page.Items[0].DeviceName);
        Assert.Equal(7, page.Items[0].RawStatus);
        Assert.Equal(9, page.Items[0].RawPunch);
        Assert.Equal(7, page.Items[0].OccurredAtLocal.Hour);
        Assert.Equal(DateTimeKind.Unspecified, page.Items[0].OccurredAtLocal.Kind);
        Assert.Equal(6, json.RootElement.GetProperty("items")[0].EnumerateObject().Count());

        await using (var db = fixture.OpenDb())
        {
            db.AttendanceLogs.Add(AttendanceLog.Create(seed.DeviceId, Guid.NewGuid().ToString("N").PadRight(64, '0'), "late", "Late user",
                Start, DateTime.UtcNow, 1, 0, null, Start.AddHours(7), "Asia/Bangkok", 420));
            await db.SaveChangesAsync();
        }
        var next = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, token: page.NextPageToken));
        Assert.Single(next!.Items);
        Assert.DoesNotContain(next.Items, x => x.DeviceUserId == "late");
        Assert.Null(next.NextPageToken);
        var replay = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, token: page.NextPageToken));
        Assert.Equal(next.Items, replay!.Items);
        Assert.Equal(next.NextPageToken, replay.NextPageToken);
        await using var audits = fixture.OpenDb();
        var audit = await audits.IntegrationReadAudits.Where(a => a.ClientId == seed.ClientId).ToArrayAsync();
        Assert.Equal(3, audit.Length);
        Assert.All(audit, a => Assert.Equal(CollectorIntegrationConstant.Completed, a.Outcome));
        var serialized = JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(seed.Key, serialized);
        Assert.DoesNotContain(page.NextPageToken!, serialized);
        Assert.DoesNotContain("000123", serialized);
    }

    [PostgresFact]
    public async Task UtcBoundariesAndSubMicrosecondCutoffsAreCorrect()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory, 1);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        Assert.Single((await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, from: "2026-11-05T00:00:00Z", to: "2026-11-06T00:00:00.0000001Z")))!.Items);
        Assert.Empty((await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, from: "2026-11-06T00:00:00.0000001Z")))!.Items);
        Assert.Empty((await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, from: "2026-11-05T00:00:00Z", to: From)))!.Items);
        Assert.Single((await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, from: "2026-11-06T00:00:00+00:00")))!.Items);
    }

    [PostgresFact]
    public async Task CredentialsAndDeviceGrantsAreEnforced()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
        http.DefaultRequestHeaders.Add("X-API-Key", "mock-test-key-only");
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
        http.DefaultRequestHeaders.Remove("X-API-Key");
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(Url(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/integration-clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/v1/attendance/logs")).StatusCode);
        await using var db = fixture.OpenDb();
        await db.IntegrationDeviceGrants.Where(g => g.ClientId == seed.ClientId).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
    }

    [PostgresFact]
    public async Task RotationKeepsSnapshotUsableButRevokedKeyFailsImmediately()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        var page = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId));
        string replacement;
        using (var scope = factory.Services.CreateScope())
        {
            var credentials = scope.ServiceProvider.GetRequiredService<IIntegrationCredentialService>();
            var secret = credentials.Create(Guid.NewGuid(), null);
            replacement = secret.ApiKey;
            var store = scope.ServiceProvider.GetRequiredService<IIntegrationStore>();
            await store.CreateKeyAsync(IntegrationApiKey.Create(secret.KeyId, seed.ClientId, credentials.GetVerifier(secret.ApiKey), DateTime.UtcNow, null), "test-admin", default);
            await store.RevokeKeyAsync(seed.ClientId, Guid.ParseExact(seed.Key[..32], "N"), "test-admin", DateTime.UtcNow, default);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(Url(seed.DeviceId, token: page!.NextPageToken))).StatusCode);
        http.DefaultRequestHeaders.Remove("X-API-Key");
        http.DefaultRequestHeaders.Add("X-API-Key", replacement);
        Assert.Single((await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, token: page!.NextPageToken)))!.Items);
    }

    [PostgresFact]
    public async Task ChangedFilterAndTamperedTokenAreRejected()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        var page = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId, 1, page!.NextPageToken))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId, token: page.NextPageToken + "!"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId, token: ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId, token: " "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId) + "&pageSize=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId, 501))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/does-not-exist")).StatusCode);
        using var unsupported = await http.PostAsync(Url(seed.DeviceId), null);
        Assert.Equal(HttpStatusCode.NotFound, unsupported.StatusCode);
        Assert.Equal("application/problem+json", unsupported.Content.Headers.ContentType!.MediaType);
    }

    [PostgresFact]
    public async Task MissingLocalHistoryIsRejectedWithoutGuessingTimezone()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory, 1, legacy: true);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        using var response = await http.GetAsync(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(CollectorIntegrationConstant.InvalidData, await response.Content.ReadAsStringAsync());
        await using var db = fixture.OpenDb();
        Assert.False(await db.AttendanceReadSnapshots.AnyAsync(s => s.ClientId == seed.ClientId));
    }

    [PostgresFact]
    public async Task UnicodeLimitsUseUtf16AndControlCharactersAreRejected()
    {
        foreach (var code in new[] { string.Concat(Enumerable.Repeat("😀", 33)), "bad\u0085code" })
        {
            using var factory = new CollectorFactory(fixture.ConnectionString);
            using var http = factory.Client();
            var seed = await SeedAsync(factory, 1, code);
            http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
            Assert.Equal(HttpStatusCode.Conflict, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
        }
    }

    [PostgresFact]
    public async Task SnapshotOverflowRollsBackInsteadOfTruncatingTheResult()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["CollectorIntegration:MaximumSnapshotRows"] = "1" });
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
        await using var db = fixture.OpenDb();
        Assert.False(await db.AttendanceReadSnapshots.AnyAsync(s => s.ClientId == seed.ClientId));
    }

    [PostgresFact]
    public async Task RateLimitReturnsRetryAfterAndDoesNotLeakPayload()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["CollectorIntegration:RequestsPerMinute"] = "1" });
        using var http = factory.Client();
        var seed = await SeedAsync(factory, 0);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
        using var rejected = await http.GetAsync(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.True(rejected.Headers.CacheControl!.NoStore);
        Assert.Contains(CollectorIntegrationConstant.RateLimited, await rejected.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task PlainHttpIsRejectedWithoutRedirect()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        http.BaseAddress = new Uri("http://localhost");
        http.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        http.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.5");
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        using var response = await http.GetAsync(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [PostgresFact]
    public async Task AdminCanProvisionOneTimeKeyAndConcurrentGrantEditsAreRejected()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory, 0);
        using var checkScope = factory.Services.CreateScope();
        Assert.NotNull(await checkScope.ServiceProvider.GetRequiredService<IIntegrationStore>().GetClientAsync(seed.ClientId, default));
        var cookieOptions = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AuthenticationExtensions.CookieScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "integration-test-admin"), new Claim("email", "admin@example.test")], AuthenticationExtensions.CookieScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15) }, AuthenticationExtensions.CookieScheme);
        http.DefaultRequestHeaders.Add("Cookie", "attendanceCollector.Auth=" + cookieOptions.TicketDataFormat.Protect(ticket));
        using var csrf = await http.GetAsync("/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        using var csrfJson = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        http.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfJson.RootElement.GetProperty("requestToken").GetString());
        // TestServer's cookie handler captures the antiforgery cookie, while this header retains the session.
        var antiforgeryCookie = csrf.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("attendanceCollector.Antiforgery=", StringComparison.Ordinal)).Split(';')[0];
        http.DefaultRequestHeaders.Remove("Cookie");
        http.DefaultRequestHeaders.Add("Cookie", "attendanceCollector.Auth=" + cookieOptions.TicketDataFormat.Protect(ticket) + "; " + antiforgeryCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(Url(seed.DeviceId))).StatusCode);
        using var created = await http.PostAsJsonAsync("/api/v1/integration-clients", new CreateIntegrationClientRequest("HRM provisioned", [seed.DeviceId]));
        Assert.True(created.StatusCode == HttpStatusCode.OK, string.Join(Environment.NewLine, factory.Errors));
        var client = (await created.Content.ReadFromJsonAsync<IntegrationClientDto>())!;
        var issued = await http.PostAsJsonAsync($"/api/v1/integration-clients/{client.Id}/keys", new CreateIntegrationKeyRequest());
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var key = (await issued.Content.ReadFromJsonAsync<IntegrationKeySecret>())!;
        var listed = await http.GetStringAsync($"/api/v1/integration-clients/{client.Id}");
        Assert.DoesNotContain(key.ApiKey, listed);
        Assert.DoesNotContain("verifier", listed, StringComparison.OrdinalIgnoreCase);
        // Issuance advances Version; an edit based on the pre-issuance version must fail.
        var stale = await http.PutAsJsonAsync($"/api/v1/integration-clients/{client.Id}/device-grants", new ReplaceIntegrationGrantsRequest(client.Version, []));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var raw = factory.Client();
        raw.DefaultRequestHeaders.Add("X-API-Key", key.ApiKey);
        Assert.Equal(HttpStatusCode.OK, (await raw.GetAsync(Url(seed.DeviceId))).StatusCode);
    }

    [PostgresFact]
    public async Task MigrationPreservesLegacyRowsAndModelHasNoPendingChanges()
    {
        await using var db = fixture.OpenDb();
        var legacy = await db.AttendanceLogs.SingleAsync(l => l.Id == fixture.LegacyLogId);
        Assert.Equal("legacy-user", legacy.EmployeeCode);
        Assert.Equal(Start, legacy.OccurredAtUtc);
        Assert.Null(legacy.OccurredAtLocal);
        Assert.Null(legacy.TimeZoneIdAtCapture);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task AuditCompletionFailurePreventsRawResponse()
    {
        using var source = new CollectorFactory(fixture.ConnectionString);
        using var factory = source.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIntegrationAuditStore>();
            services.AddScoped<IIntegrationAuditStore>(provider => new FailingCompletionAudit(
                new IntegrationAuditStore(provider.GetRequiredService<IDbContextFactory<AttendanceCollectorDbContext>>())));
        }));
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        // Seed against the same database; credentials are independent of the fault-injected audit writer.
        var seed = await SeedAsync(source, 1);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        using var response = await http.GetAsync(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("000123", body);
        Assert.DoesNotContain("items", body);
        Assert.Contains(CollectorIntegrationConstant.Unavailable, body);
    }

    [PostgresFact]
    public async Task EncryptedKeyRingWorksAcrossServerInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "collector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=collector-test-key", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
            var certificatePath = Path.Combine(directory, "test.pfx");
            await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx, "test-only"));
            var settings = new Dictionary<string, string?> { ["CollectorIntegration:KeyRingPath"] = Path.Combine(directory, "keys"),
                ["CollectorIntegration:KeyEncryptionCertificatePath"] = certificatePath, ["CollectorIntegration:KeyEncryptionCertificatePassword"] = "test-only" };
            using var first = new CollectorFactory(fixture.ConnectionString, settings);
            using var http = first.Client();
            var seed = await SeedAsync(first);
            http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
            var page = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId));
            using var second = new CollectorFactory(fixture.ConnectionString, settings);
            using var replica = second.Client();
            replica.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
            Assert.Single((await replica.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, token: page!.NextPageToken)))!.Items);
            var keyXml = await File.ReadAllTextAsync(Directory.GetFiles(Path.Combine(directory, "keys"), "key-*.xml").Single());
            Assert.Contains("EncryptedData", keyXml);
            Assert.DoesNotContain("<masterKey", keyXml);
        }
        finally { Directory.Delete(directory, true); }
    }

    [PostgresFact]
    public async Task GrantRevocationAndClientDisableBlockExistingSnapshots()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        var page = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId));
        await using var db = fixture.OpenDb();
        await db.IntegrationDeviceGrants.Where(g => g.ClientId == seed.ClientId).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(Url(seed.DeviceId, token: page!.NextPageToken))).StatusCode);
        await db.IntegrationClients.Where(c => c.Id == seed.ClientId).ExecuteUpdateAsync(u => u.SetProperty(c => c.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(Url(seed.DeviceId, token: page!.NextPageToken))).StatusCode);
    }

    [PostgresFact]
    public async Task PageSize500AndSameTimestampRecordsAreComplete()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory, 501);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        var page = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, 500));
        Assert.Equal(500, page!.Items.Count);
        var last = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId, 500, page.NextPageToken));
        Assert.Single(last!.Items);
        Assert.Null(last.NextPageToken);
    }

    [PostgresFact]
    public async Task ResponseByteLimitFailsBeforeAnyRawBytesAreReturned()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["CollectorIntegration:MaximumResponseBytes"] = "1024" });
        using var http = factory.Client();
        var seed = await SeedAsync(factory, 20);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        using var response = await http.GetAsync(Url(seed.DeviceId, 500));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("000123", await response.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task RateQuotaIsPartitionedByAuthenticatedClient()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["CollectorIntegration:RequestsPerMinute"] = "1" });
        var a = await SeedAsync(factory, 0);
        var b = await SeedAsync(factory, 0);
        using var first = factory.Client();
        using var second = factory.Client();
        first.DefaultRequestHeaders.Add("X-API-Key", a.Key);
        second.DefaultRequestHeaders.Add("X-API-Key", b.Key);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync(Url(a.DeviceId))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.GetAsync(Url(a.DeviceId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync(Url(b.DeviceId))).StatusCode);
    }

    [PostgresFact]
    public async Task ExpiredSnapshotsAreRejectedAndCleanupCascadesToRows()
    {
        using var factory = new CollectorFactory(fixture.ConnectionString);
        using var http = factory.Client();
        var seed = await SeedAsync(factory);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        var page = await http.GetFromJsonAsync<CollectorLogPage>(Url(seed.DeviceId));
        await using var db = fixture.OpenDb();
        var id = await db.AttendanceReadSnapshots.Where(s => s.ClientId == seed.ClientId).Select(s => s.Id).SingleAsync();
        await db.AttendanceReadSnapshots.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(Url(seed.DeviceId, token: page!.NextPageToken))).StatusCode);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICollectorReadStore>().DeleteExpiredSnapshotsAsync(DateTime.UtcNow, default);
        Assert.False(await db.AttendanceReadSnapshotRows.AnyAsync(r => r.SnapshotId == id));
    }

    [PostgresFact]
    public async Task RequestDeadlineCancelsReadWithoutReturningRaw()
    {
        using var source = new CollectorFactory(fixture.ConnectionString, new Dictionary<string, string?>
        { ["CollectorIntegration:RequestTimeoutSeconds"] = "1", ["CollectorIntegration:DatabaseTimeoutSeconds"] = "1" });
        using var factory = source.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICollectorReadStore>();
            services.AddScoped<ICollectorReadStore, BlockingReadStore>();
        }));
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var seed = await SeedAsync(source, 1);
        http.DefaultRequestHeaders.Add("X-API-Key", seed.Key);
        using var response = await http.GetAsync(Url(seed.DeviceId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(CollectorIntegrationConstant.Unavailable, body);
        Assert.DoesNotContain("items", body);
    }
}

internal sealed class FailingCompletionAudit(IIntegrationAuditStore inner) : IIntegrationAuditStore
{
    public Task StartAsync(IntegrationReadAudit audit, CancellationToken cancellationToken) => inner.StartAsync(audit, cancellationToken);
    public Task CompleteAsync(Guid auditId, Guid? clientId, int status, int count, long durationMilliseconds, string? errorCode, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Test audit failure.");
}

internal sealed class BlockingReadStore : ICollectorReadStore
{
    public async Task<CollectorSnapshotPage> CreateSnapshotAsync(Guid clientId, CollectorReadCriteria criteria, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("The test read must be cancelled.");
    }
    public Task<CollectorSnapshotPage> ReadSnapshotAsync(CollectorPageCursor cursor, int pageSize, DateTime nowUtc, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task<int> DeleteExpiredSnapshotsAsync(DateTime nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);
}
