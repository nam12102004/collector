using Microsoft.EntityFrameworkCore.Migrations;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace attendanceCollector.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AttendanceCollectorDbContext))]
[Migration("20261109000100_PurgePersistedEmployeeCodes")]
public sealed class PurgePersistedEmployeeCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"AttendanceLogs\" SET \"EmployeeCode\" = '' WHERE \"EmployeeCode\" <> '';");
        migrationBuilder.AlterColumn<long>(
            name: "SourceRecordUid",
            table: "AttendanceLogs",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_AttendanceLogs_DeviceId_SourceRecordUid_OccurredAtUtc_Status_Punch",
            table: "AttendanceLogs",
            columns: new[] { "DeviceId", "SourceRecordUid", "OccurredAtUtc", "Status", "Punch" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Purged employee codes cannot be restored.
        migrationBuilder.DropIndex(
            name: "IX_AttendanceLogs_DeviceId_SourceRecordUid_OccurredAtUtc_Status_Punch",
            table: "AttendanceLogs");
        migrationBuilder.AlterColumn<int>(
            name: "SourceRecordUid",
            table: "AttendanceLogs",
            type: "integer",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);
    }
}
