using attendanceCollector.MockDevice.Contracts;
using attendanceCollector.MockDevice.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MockDevice")
    ?? throw new InvalidOperationException("ConnectionStrings:MockDevice must be configured.");
var collectorUrl = builder.Configuration["MOCK_DEVICE_COLLECTOR_URL"]
    ?? throw new InvalidOperationException("MOCK_DEVICE_COLLECTOR_URL must be configured.");
var ingestionApiKey = builder.Configuration["MOCK_DEVICE_INGESTION_API_KEY"]
    ?? throw new InvalidOperationException("MOCK_DEVICE_INGESTION_API_KEY must be configured.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<MockDeviceService>(client =>
{
    client.BaseAddress = new Uri(collectorUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton(new MockDeviceOptions(ingestionApiKey));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

await using (var connection = await app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync())
await using (var command = new NpgsqlCommand(
    await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", "001_schema.sql")),
    connection))
{
    await command.ExecuteNonQueryAsync();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    return Results.Ok(new { status = "Ready" });
});

app.MapGet("/api/v1/mock-device/config", (IConfiguration configuration) => Results.Ok(new
{
    collectorHost = configuration["MOCK_DEVICE_CONNECT_HOST"] ?? "mock-device",
    port = configuration.GetValue("MOCK_DEVICE_CONNECT_PORT", 8080)
}));

app.MapPost("/api/v1/mock-device/send", async (
    MockDeviceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.SendAsync(cancellationToken)));

app.MapPost("/api/v1/mock-device/generate", async (
    MockDeviceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(new { recordsAdded = await service.GenerateEventsAsync(cancellationToken) }));

app.MapGet("/api/v1/device/attendance", async (
    MockDeviceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAttendanceRecordsAsync(cancellationToken)));

app.MapGet("/api/v1/mock-device/events", async (
    int page,
    int pageSize,
    MockDeviceService service,
    CancellationToken cancellationToken) =>
{
    if (page < 1 || pageSize is < 1 or > 100)
    {
        return Results.BadRequest(new { error = "page must be positive and pageSize must be between 1 and 100." });
    }

    return Results.Ok(await service.GetEventsAsync(page, pageSize, cancellationToken));
});

app.Run();

public sealed record MockDeviceOptions(string IngestionApiKey);
