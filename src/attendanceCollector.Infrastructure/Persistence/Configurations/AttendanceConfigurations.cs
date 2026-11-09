using attendanceCollector.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace attendanceCollector.Infrastructure.Persistence.Configurations;

public sealed class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
{
    public void Configure(EntityTypeBuilder<AttendanceDevice> builder)
    {
        builder.ToTable("AttendanceDevices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Code).HasMaxLength(32).IsRequired();
        builder.Property(device => device.Name).HasMaxLength(120).IsRequired();
        builder.Property(device => device.DriverCode).HasMaxLength(64).IsRequired();
        builder.Property(device => device.Host).HasMaxLength(255).IsRequired();
        builder.Property(device => device.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.HasIndex(device => device.Code).IsUnique();
    }
}

public sealed class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceLog> builder)
    {
        builder.ToTable("AttendanceLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(log => log.EmployeeCode).HasMaxLength(100).IsRequired();
        builder.Property(log => log.EmployeeName).HasMaxLength(200).IsRequired();
        builder.Property(log => log.OccurredAtLocal).HasColumnType("timestamp without time zone");
        builder.Property(log => log.TimeZoneIdAtCapture).HasMaxLength(100);
        builder.HasIndex(log => log.Fingerprint).IsUnique();
        builder.HasIndex(log => new { log.DeviceId, log.OccurredAtUtc });
        builder.HasIndex(log => new { log.DeviceId, log.OccurredAtUtc, log.Id });
        builder.HasIndex(log => new { log.DeviceId, log.SourceRecordUid, log.OccurredAtUtc, log.Status, log.Punch });
        builder.HasOne<AttendanceDevice>()
            .WithMany()
            .HasForeignKey(log => log.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AttendanceCollectionRunConfiguration : IEntityTypeConfiguration<AttendanceCollectionRun>
{
    public void Configure(EntityTypeBuilder<AttendanceCollectionRun> builder)
    {
        builder.ToTable("AttendanceCollectionRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(run => run.ErrorMessage).HasMaxLength(500);
        builder.HasIndex(run => new { run.DeviceId, run.StartedAtUtc });
        builder.HasOne<AttendanceDevice>()
            .WithMany()
            .HasForeignKey(run => run.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
