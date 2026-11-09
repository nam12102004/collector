using attendanceCollector.Application.Contracts;
using attendanceCollector.Application.Interfaces;

namespace attendanceCollector.API.Services;

public sealed class CollectorSnapshotCleanupService(IServiceScopeFactory scopes, CollectorIntegrationOptions options,
    TimeProvider clock, ILogger<CollectorSnapshotCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(options.DatabaseTimeoutSeconds));
                await scope.ServiceProvider.GetRequiredService<ICollectorReadStore>()
                    .DeleteExpiredSnapshotsAsync(clock.GetUtcNow().UtcDateTime, deadline.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogWarning("Snapshot cleanup failed: {ErrorType}", exception.GetType().Name);
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
        }
    }
}
