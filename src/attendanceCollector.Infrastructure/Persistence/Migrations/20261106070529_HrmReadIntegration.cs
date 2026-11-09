using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace attendanceCollector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrmReadIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtLocal",
                table: "AttendanceLogs",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneIdAtCapture",
                table: "AttendanceLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UtcOffsetMinutesAtCapture",
                table: "AttendanceLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegrationAdminAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Before = table.Column<string>(type: "text", nullable: true),
                    After = table.Column<string>(type: "text", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationAdminAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationReadAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PageSize = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationReadAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceReadSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceReadSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceReadSnapshots_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceReadSnapshots_IntegrationClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Verifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationApiKeys_IntegrationClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationDeviceGrants",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationDeviceGrants", x => new { x.ClientId, x.DeviceId });
                    table.ForeignKey(
                        name: "FK_IntegrationDeviceGrants_AttendanceDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegrationDeviceGrants_IntegrationClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceReadSnapshotRows",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<long>(type: "bigint", nullable: false),
                    DeviceUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceUid = table.Column<long>(type: "bigint", nullable: true),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAtLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RawStatus = table.Column<int>(type: "integer", nullable: false),
                    RawPunch = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceReadSnapshotRows", x => new { x.SnapshotId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_AttendanceReadSnapshotRows_AttendanceReadSnapshots_Snapshot~",
                        column: x => x.SnapshotId,
                        principalTable: "AttendanceReadSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_DeviceId_OccurredAtUtc_Id",
                table: "AttendanceLogs",
                columns: new[] { "DeviceId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReadSnapshots_ClientId_ExpiresAtUtc",
                table: "AttendanceReadSnapshots",
                columns: new[] { "ClientId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReadSnapshots_DeviceId",
                table: "AttendanceReadSnapshots",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReadSnapshots_ExpiresAtUtc",
                table: "AttendanceReadSnapshots",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationAdminAudits_ClientId_OccurredAtUtc",
                table: "IntegrationAdminAudits",
                columns: new[] { "ClientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationApiKeys_ClientId",
                table: "IntegrationApiKeys",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationDeviceGrants_DeviceId",
                table: "IntegrationDeviceGrants",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationReadAudits_ClientId_StartedAtUtc",
                table: "IntegrationReadAudits",
                columns: new[] { "ClientId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationReadAudits_StartedAtUtc",
                table: "IntegrationReadAudits",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceReadSnapshotRows");

            migrationBuilder.DropTable(
                name: "IntegrationAdminAudits");

            migrationBuilder.DropTable(
                name: "IntegrationApiKeys");

            migrationBuilder.DropTable(
                name: "IntegrationDeviceGrants");

            migrationBuilder.DropTable(
                name: "IntegrationReadAudits");

            migrationBuilder.DropTable(
                name: "AttendanceReadSnapshots");

            migrationBuilder.DropTable(
                name: "IntegrationClients");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceLogs_DeviceId_OccurredAtUtc_Id",
                table: "AttendanceLogs");

            migrationBuilder.DropColumn(
                name: "OccurredAtLocal",
                table: "AttendanceLogs");

            migrationBuilder.DropColumn(
                name: "TimeZoneIdAtCapture",
                table: "AttendanceLogs");

            migrationBuilder.DropColumn(
                name: "UtcOffsetMinutesAtCapture",
                table: "AttendanceLogs");
        }
    }
}
