using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using attendanceCollector.Infrastructure.Services.Devices.Push;
using Microsoft.EntityFrameworkCore;

namespace attendanceCollector.API.Services;

public sealed class HourlyAttendanceCollectionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<HourlyAttendanceCollectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var nextHour = new DateTimeOffset(
                now.UtcDateTime.Date.AddHours(now.UtcDateTime.Hour + 1),
                TimeSpan.Zero);

            try
            {
                await Task.Delay(nextHour - now, clock, stoppingToken);
                await CollectUnderSchedulerLockAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Hourly attendance collection cycle failed.");
            }
        }
    }

    private async Task CollectUnderSchedulerLockAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AttendanceCollectorDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();

        await using var acquire = connection.CreateCommand();
        acquire.CommandText = "SELECT pg_try_advisory_lock(7306110602)";
        if (await acquire.ExecuteScalarAsync(cancellationToken) is not true)
        {
            logger.LogInformation("Another Collector instance owns this hourly collection cycle.");
            return;
        }

        try
        {
            await CollectActiveDevicesAsync(cancellationToken);
        }
        finally
        {
            await using var release = connection.CreateCommand();
            release.CommandText = "SELECT pg_advisory_unlock(7306110602)";
            await release.ExecuteScalarAsync(CancellationToken.None);
        }
    }

    private async Task CollectActiveDevicesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<AttendanceDevice> devices;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IAttendanceStore>();
            devices = await store.GetDevicesAsync(cancellationToken);
        }

        foreach (var device in devices.Where(device => device.IsActive
                     && device.DriverCode != PushAttendanceDeviceDriver.DriverCodeValue))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var collections = scope.ServiceProvider.GetRequiredService<IAttendanceCollectionService>();
                var run = await collections.CollectAsync(device.Id, cancellationToken);
                if (string.Equals(run.Status, "Succeeded", StringComparison.Ordinal))
                {
                    logger.LogInformation(
                        "Hourly attendance collection completed for device {DeviceId}: {RecordsReceived} received, {RecordsAdded} new.",
                        device.Id, run.RecordsReceived, run.RecordsAdded);
                }
                else
                {
                    logger.LogWarning(
                        "Hourly attendance collection failed for device {DeviceId}: {ErrorMessage}",
                        device.Id, run.ErrorMessage);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Hourly attendance collection failed for device {DeviceId}.", device.Id);
            }
        }
    }
}
