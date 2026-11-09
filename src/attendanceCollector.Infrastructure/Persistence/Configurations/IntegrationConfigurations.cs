using attendanceCollector.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace attendanceCollector.Infrastructure.Persistence.Configurations;

public sealed class IntegrationClientConfiguration : IEntityTypeConfiguration<IntegrationClient>
{
    public void Configure(EntityTypeBuilder<IntegrationClient> builder)
    {
        builder.ToTable("IntegrationClients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
public sealed class IntegrationKeyConfiguration : IEntityTypeConfiguration<IntegrationApiKey>
{
    public void Configure(EntityTypeBuilder<IntegrationApiKey> builder)
    {
        builder.ToTable("IntegrationApiKeys");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Verifier).HasMaxLength(64).IsRequired();
        builder.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class IntegrationGrantConfiguration : IEntityTypeConfiguration<IntegrationDeviceGrant>
{
    public void Configure(EntityTypeBuilder<IntegrationDeviceGrant> builder)
    {
        builder.ToTable("IntegrationDeviceGrants");
        builder.HasKey(x => new { x.ClientId, x.DeviceId });
        builder.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttendanceDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class IntegrationReadAuditConfiguration : IEntityTypeConfiguration<IntegrationReadAuditEntry>
{
    public void Configure(EntityTypeBuilder<IntegrationReadAuditEntry> builder)
    {
        builder.ToTable("IntegrationReadAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(64);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => new { x.ClientId, x.StartedAtUtc });
    }
}
public sealed class IntegrationAdminAuditConfiguration : IEntityTypeConfiguration<IntegrationAdminAuditEntry>
{
    public void Configure(EntityTypeBuilder<IntegrationAdminAuditEntry> builder)
    {
        builder.ToTable("IntegrationAdminAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Actor).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ClientId, x.OccurredAtUtc });
    }
}
public sealed class AttendanceSnapshotConfiguration : IEntityTypeConfiguration<AttendanceReadSnapshot>
{
    public void Configure(EntityTypeBuilder<AttendanceReadSnapshot> builder)
    {
        builder.ToTable("AttendanceReadSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FilterHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.HasIndex(x => new { x.ClientId, x.ExpiresAtUtc });
        builder.HasOne<IntegrationClient>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttendanceDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AttendanceSnapshotRowConfiguration : IEntityTypeConfiguration<AttendanceReadSnapshotRow>
{
    public void Configure(EntityTypeBuilder<AttendanceReadSnapshotRow> builder)
    {
        builder.ToTable("AttendanceReadSnapshotRows");
        builder.HasKey(x => new { x.SnapshotId, x.Ordinal });
        // Source limits are retained so invalid historical strings can be rejected explicitly.
        builder.Property(x => x.DeviceUserId).HasMaxLength(100);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.OccurredAtLocal).HasColumnType("timestamp without time zone");
        builder.HasOne<AttendanceReadSnapshot>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
    }
}
