using attendanceCollector.Application.Interfaces;
using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Services.Devices.Push;
using attendanceCollector.Infrastructure.Services.Devices.MockDevice;
using attendanceCollector.Infrastructure.Services.Devices.Zk;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using attendanceCollector.Infrastructure.Repositories;
using attendanceCollector.Infrastructure.Services;
using attendanceCollector.MockDevice.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace attendanceCollector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextFactory<AttendanceCollectorDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.")));
        services.AddScoped<IAttendanceStore, AttendanceStore>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddScoped<ICollectorReadStore, CollectorReadStore>();
        services.AddScoped<IIntegrationCredentialService, IntegrationCredentialService>();
        services.AddScoped<IIntegrationAuditStore, IntegrationAuditStore>();
        services.AddScoped<IAttendanceDeviceDriver, ZkProtocolAttendanceDeviceDriver>();
        var zkSection = configuration.GetSection(ZkDeviceReadOptions.SectionName);
        var maximumRecordsPerRead = int.TryParse(zkSection["MaximumRecordsPerRead"], out var configuredMaximumRecords)
            ? configuredMaximumRecords
            : 50_000;
        var timeoutSeconds = int.TryParse(zkSection["TimeoutSeconds"], out var configuredTimeout)
            ? configuredTimeout
            : 15;
        services.AddSingleton<IOptions<ZkDeviceReadOptions>>(Options.Create(new ZkDeviceReadOptions
        {
            MaximumRecordsPerRead = maximumRecordsPerRead,
            TimeoutSeconds = timeoutSeconds
        }));
        services.AddScoped<IAttendanceDeviceDriver, MockDeviceHttpAttendanceDeviceDriver>();
        services.AddScoped<IAttendanceDeviceDriver, PushAttendanceDeviceDriver>();
        return services;
    }

    public static async Task ApplyAttendanceMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceCollectorDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.AttendanceDevices.AnyAsync(device => device.Code == MockDeviceContractConstant.SourceCode, cancellationToken))
        {
            db.AttendanceDevices.Add(AttendanceDevice.Create(
                MockDeviceContractConstant.SourceCode,
                MockDeviceContractConstant.SourceName,
                MockDeviceContractConstant.DriverCode,
                "mock-device",
                1,
                0,
                10,
                "Asia/Bangkok",
                DateTime.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
