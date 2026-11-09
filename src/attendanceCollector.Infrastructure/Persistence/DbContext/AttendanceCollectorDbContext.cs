using attendanceCollector.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace attendanceCollector.Infrastructure.Persistence.DbContext;

public sealed class AttendanceCollectorDbContext(DbContextOptions<AttendanceCollectorDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
    public DbSet<AttendanceCollectionRun> AttendanceCollectionRuns => Set<AttendanceCollectionRun>();
    public DbSet<IntegrationClient> IntegrationClients => Set<IntegrationClient>();
    public DbSet<IntegrationApiKey> IntegrationApiKeys => Set<IntegrationApiKey>();
    public DbSet<IntegrationDeviceGrant> IntegrationDeviceGrants => Set<IntegrationDeviceGrant>();
    public DbSet<IntegrationReadAuditEntry> IntegrationReadAudits => Set<IntegrationReadAuditEntry>();
    public DbSet<IntegrationAdminAuditEntry> IntegrationAdminAudits => Set<IntegrationAdminAuditEntry>();
    public DbSet<AttendanceReadSnapshot> AttendanceReadSnapshots => Set<AttendanceReadSnapshot>();
    public DbSet<AttendanceReadSnapshotRow> AttendanceReadSnapshotRows => Set<AttendanceReadSnapshotRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceCollectorDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
