using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace attendanceCollector.API.Configurations;

// Scaffolding never loads deployment credentials or starts the web host/migrations.
public sealed class AttendanceDesignTimeContextFactory : IDesignTimeDbContextFactory<AttendanceCollectorDbContext>
{
    public AttendanceCollectorDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AttendanceCollectorDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("COLLECTOR_MIGRATION_CONNECTION")
                ?? "Host=localhost;Database=collector_design;Username=design;Password=design")
            .Options);
}
