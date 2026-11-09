using attendanceCollector.Domain.Entities;
using attendanceCollector.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace attendanceCollector.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AttendanceCollectorDbContext))]
[Migration("20261106000100_InitialAttendanceCollection")]
public sealed class InitialAttendanceCollection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AttendanceDevices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                DriverCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                Port = table.Column<int>(type: "integer", nullable: false),
                CommunicationKey = table.Column<int>(type: "integer", nullable: false),
                TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AttendanceDevices", row => row.Id));

        migrationBuilder.CreateTable(
            name: "AttendanceCollectionRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                RecordsReceived = table.Column<int>(type: "integer", nullable: false),
                RecordsAdded = table.Column<int>(type: "integer", nullable: false),
                ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceCollectionRuns", row => row.Id);
                table.ForeignKey(
                    name: "FK_AttendanceCollectionRuns_AttendanceDevices_DeviceId",
                    column: row => row.DeviceId,
                    principalTable: "AttendanceDevices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AttendanceLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                EmployeeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Punch = table.Column<int>(type: "integer", nullable: false),
                SourceRecordUid = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceLogs", row => row.Id);
                table.ForeignKey(
                    name: "FK_AttendanceLogs_AttendanceDevices_DeviceId",
                    column: row => row.DeviceId,
                    principalTable: "AttendanceDevices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_AttendanceDevices_Code", table: "AttendanceDevices", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_AttendanceCollectionRuns_DeviceId_StartedAtUtc", table: "AttendanceCollectionRuns", columns: new[] { "DeviceId", "StartedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_AttendanceLogs_DeviceId_OccurredAtUtc", table: "AttendanceLogs", columns: new[] { "DeviceId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_AttendanceLogs_Fingerprint", table: "AttendanceLogs", column: "Fingerprint", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AttendanceCollectionRuns");
        migrationBuilder.DropTable(name: "AttendanceLogs");
        migrationBuilder.DropTable(name: "AttendanceDevices");
    }
}
