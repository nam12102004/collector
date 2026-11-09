CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE TABLE "AttendanceDevices" (
        "Id" uuid NOT NULL,
        "Code" character varying(32) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "DriverCode" character varying(64) NOT NULL,
        "Host" character varying(255) NOT NULL,
        "Port" integer NOT NULL,
        "CommunicationKey" integer NOT NULL,
        "TimeoutSeconds" integer NOT NULL,
        "TimeZoneId" character varying(100) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_AttendanceDevices" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE TABLE "AttendanceCollectionRuns" (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "CompletedAtUtc" timestamp with time zone,
        "Status" character varying(20) NOT NULL,
        "RecordsReceived" integer NOT NULL,
        "RecordsAdded" integer NOT NULL,
        "ErrorMessage" character varying(500),
        CONSTRAINT "PK_AttendanceCollectionRuns" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AttendanceCollectionRuns_AttendanceDevices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "AttendanceDevices" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE TABLE "AttendanceLogs" (
        "Id" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "Fingerprint" character varying(64) NOT NULL,
        "EmployeeCode" character varying(100) NOT NULL,
        "EmployeeName" character varying(200) NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        "ReceivedAtUtc" timestamp with time zone NOT NULL,
        "Status" integer NOT NULL,
        "Punch" integer NOT NULL,
        "SourceRecordUid" integer,
        CONSTRAINT "PK_AttendanceLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AttendanceLogs_AttendanceDevices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "AttendanceDevices" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE UNIQUE INDEX "IX_AttendanceDevices_Code" ON "AttendanceDevices" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE INDEX "IX_AttendanceCollectionRuns_DeviceId_StartedAtUtc" ON "AttendanceCollectionRuns" ("DeviceId", "StartedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE INDEX "IX_AttendanceLogs_DeviceId_OccurredAtUtc" ON "AttendanceLogs" ("DeviceId", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    CREATE UNIQUE INDEX "IX_AttendanceLogs_Fingerprint" ON "AttendanceLogs" ("Fingerprint");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106000100_InitialAttendanceCollection') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106000100_InitialAttendanceCollection', '10.0.12');
    END IF;
END $EF$;
COMMIT;

BEGIN;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109000100_PurgePersistedEmployeeCodes') THEN
        UPDATE "AttendanceLogs" SET "EmployeeCode" = '' WHERE "EmployeeCode" <> '';
        ALTER TABLE "AttendanceLogs" ALTER COLUMN "SourceRecordUid" TYPE bigint USING "SourceRecordUid"::bigint;
        CREATE INDEX "IX_AttendanceLogs_DeviceId_SourceRecordUid_OccurredAtUtc_Status_Punch"
            ON "AttendanceLogs" ("DeviceId", "SourceRecordUid", "OccurredAtUtc", "Status", "Punch");
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20261109000100_PurgePersistedEmployeeCodes', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    ALTER TABLE "AttendanceLogs" ADD "OccurredAtLocal" timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    ALTER TABLE "AttendanceLogs" ADD "TimeZoneIdAtCapture" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    ALTER TABLE "AttendanceLogs" ADD "UtcOffsetMinutesAtCapture" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "IntegrationAdminAudits" (
        "Id" uuid NOT NULL,
        "ClientId" uuid NOT NULL,
        "KeyId" uuid,
        "Actor" character varying(256) NOT NULL,
        "Action" character varying(64) NOT NULL,
        "Before" text,
        "After" text,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_IntegrationAdminAudits" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "IntegrationClients" (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "IsActive" boolean NOT NULL,
        "Scope" character varying(64) NOT NULL,
        "Version" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_IntegrationClients" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "IntegrationReadAudits" (
        "Id" uuid NOT NULL,
        "StartedAtUtc" timestamp with time zone NOT NULL,
        "ClientId" uuid,
        "DeviceId" uuid,
        "RequestId" character varying(128) NOT NULL,
        "FromUtc" timestamp with time zone,
        "ToUtc" timestamp with time zone,
        "PageSize" integer,
        "Outcome" character varying(32) NOT NULL,
        "HttpStatus" integer,
        "RowCount" integer NOT NULL,
        "DurationMilliseconds" bigint NOT NULL,
        "ErrorCode" character varying(64),
        CONSTRAINT "PK_IntegrationReadAudits" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "AttendanceReadSnapshots" (
        "Id" uuid NOT NULL,
        "ClientId" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "FilterHash" character varying(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "RowCount" integer NOT NULL,
        CONSTRAINT "PK_AttendanceReadSnapshots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AttendanceReadSnapshots_AttendanceDevices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "AttendanceDevices" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AttendanceReadSnapshots_IntegrationClients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "IntegrationClients" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "IntegrationApiKeys" (
        "Id" uuid NOT NULL,
        "ClientId" uuid NOT NULL,
        "Verifier" character varying(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ExpiresAtUtc" timestamp with time zone,
        "RevokedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_IntegrationApiKeys" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_IntegrationApiKeys_IntegrationClients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "IntegrationClients" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "IntegrationDeviceGrants" (
        "ClientId" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        CONSTRAINT "PK_IntegrationDeviceGrants" PRIMARY KEY ("ClientId", "DeviceId"),
        CONSTRAINT "FK_IntegrationDeviceGrants_AttendanceDevices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "AttendanceDevices" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_IntegrationDeviceGrants_IntegrationClients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "IntegrationClients" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE TABLE "AttendanceReadSnapshotRows" (
        "SnapshotId" uuid NOT NULL,
        "Ordinal" bigint NOT NULL,
        "DeviceUserId" character varying(100),
        "DeviceUid" bigint,
        "DeviceName" character varying(200),
        "OccurredAtLocal" timestamp without time zone,
        "RawStatus" integer NOT NULL,
        "RawPunch" integer NOT NULL,
        CONSTRAINT "PK_AttendanceReadSnapshotRows" PRIMARY KEY ("SnapshotId", "Ordinal"),
        CONSTRAINT "FK_AttendanceReadSnapshotRows_AttendanceReadSnapshots_Snapshot~" FOREIGN KEY ("SnapshotId") REFERENCES "AttendanceReadSnapshots" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_AttendanceLogs_DeviceId_OccurredAtUtc_Id" ON "AttendanceLogs" ("DeviceId", "OccurredAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_AttendanceReadSnapshots_ClientId_ExpiresAtUtc" ON "AttendanceReadSnapshots" ("ClientId", "ExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_AttendanceReadSnapshots_DeviceId" ON "AttendanceReadSnapshots" ("DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_AttendanceReadSnapshots_ExpiresAtUtc" ON "AttendanceReadSnapshots" ("ExpiresAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_IntegrationAdminAudits_ClientId_OccurredAtUtc" ON "IntegrationAdminAudits" ("ClientId", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_IntegrationApiKeys_ClientId" ON "IntegrationApiKeys" ("ClientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_IntegrationDeviceGrants_DeviceId" ON "IntegrationDeviceGrants" ("DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_IntegrationReadAudits_ClientId_StartedAtUtc" ON "IntegrationReadAudits" ("ClientId", "StartedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    CREATE INDEX "IX_IntegrationReadAudits_StartedAtUtc" ON "IntegrationReadAudits" ("StartedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106070529_HrmReadIntegration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106070529_HrmReadIntegration', '10.0.12');
    END IF;
END $EF$;
COMMIT;

