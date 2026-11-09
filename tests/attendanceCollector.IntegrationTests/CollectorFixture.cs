using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.AspNetCore.Authorization;
using attendanceCollector.API.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace attendanceCollector.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLLECTOR_TEST_POSTGRES")))
            Skip = "Set COLLECTOR_TEST_POSTGRES to a disposable local collector_test PostgreSQL instance.";
    }
}

public sealed class CollectorFixture : IAsyncLifetime
{
    private readonly string database = "collector_test_" + Guid.NewGuid().ToString("N");
    private string? adminConnection;
    public string ConnectionString { get; private set; } = "";
    public Guid LegacyLogId { get; } = Guid.NewGuid();

    public AttendanceCollectorDbContext OpenDb() => new(new DbContextOptionsBuilder<AttendanceCollectorDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("COLLECTOR_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var options = new NpgsqlConnectionStringBuilder(adminConnection);
        if (!options.Database!.StartsWith("collector_test", StringComparison.Ordinal) || options.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("Tests require a disposable localhost collector_test database.");
        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
            await create.ExecuteNonQueryAsync();
        }
        options.Database = database;
        ConnectionString = options.ConnectionString;
        await using var db = OpenDb();
        // Verify upgrade from the actual pre-integration schema, retaining historical data.
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261106000100_InitialAttendanceCollection");
        var device = attendanceCollector.Domain.Entities.AttendanceDevice.Create("legacy", "Legacy", "test", "localhost", 4370, 0, 10, "Asia/Bangkok", DateTime.UtcNow);
        db.AttendanceDevices.Add(device);
        await db.SaveChangesAsync();
        var at = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AttendanceLogs" ("Id", "DeviceId", "Fingerprint", "EmployeeCode", "EmployeeName", "OccurredAtUtc", "ReceivedAtUtc", "Status", "Punch")
            VALUES ({LegacyLogId}, {device.Id}, {'L'.ToString().PadRight(64, 'L')}, {"legacy-user"}, {"Legacy user"}, {at}, {at}, 1, 0)
            """);
        await migrator.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(adminConnection) || string.IsNullOrWhiteSpace(ConnectionString)) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

public sealed class CollectorFactory(string connection, IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    private readonly string keyPath = Path.Combine(Path.GetTempPath(), "collector-tests", Guid.NewGuid().ToString("N"));
    public ConcurrentQueue<string> Errors { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.AddProvider(new DiagnosticLogProvider(Errors)));
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connection,
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["CollectorIntegration:Enabled"] = "true",
                ["CollectorIntegration:KeyRingPath"] = keyPath,
                ["CollectorIntegration:MaximumSnapshotsTotal"] = "128",
                ["CollectorIntegration:MaximumSnapshotsPerClient"] = "32",
                ["CollectorIntegration:RequestsPerMinute"] = "500",
                ["BOOTSTRAP_ENABLED"] = "true",
                ["BOOTSTRAP_ADMIN_EXTERNAL_SUBJECT"] = "integration-test-admin",
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@example.test",
                ["MOCK_DEVICE_INGESTION_API_KEY"] = "mock-test-key-only",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            };
            if (overrides is not null) foreach (var item in overrides) settings[item.Key] = item.Value;
            configuration.AddInMemoryCollection(settings);
        });
        // The existing bootstrap setup captures configuration before the test host applies overrides.
        // Retain the same identity/role checks while configuring only this test host.
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IClaimsTransformation>(_ => new BootstrapAdminClaimsTransformation("admin@example.test", "integration-test-admin"));
            services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy("BootstrapAdmin", policy => policy
                .RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole("BootstrapAdmin")
                    && context.User.FindFirst("sub")?.Value == "integration-test-admin"
                    && context.User.FindFirst("email")?.Value == "admin@example.test")));
        });
    }

    public HttpClient Client() => CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(keyPath)) Directory.Delete(keyPath, true);
    }
}

internal sealed class DiagnosticLogProvider(ConcurrentQueue<string> messages) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(messages);
    public void Dispose() { }
    private sealed class DiagnosticLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) messages.Enqueue(formatter(state, exception)); }
    }
}
