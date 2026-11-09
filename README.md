# attendanceCollector

.NET 10 Clean Architecture scaffold with Keycloak OIDC login and JWT-protected APIs.

## HRM Collector API

The HRM integration lists granted devices without returning host/port, triggers a manual collection, checks connectivity, and reads stored logs through a dedicated API key and per-device grants. The integration is disabled by default outside the local setup. See the [setup and rotation runbook](docs/collector-integration-runbook.md), [OpenAPI contract](docs/collector-api.openapi.yaml), and [implementation plan](docs/collector-server-implementation-plan.md).

For summarized local, staging, and production commands, see [deploy and operations](docs/deployment.md). For device network and data-flow details, see [attendance devices](docs/attendance-devices.md).

## Local setup

The ignored `.env` file contains disposable local defaults. Start PostgreSQL and the API with:

```powershell
docker compose up --build -d
```

Compose serves the WebApp and API together at `http://localhost:4210`; open `/` for the admin page and use `/health/live` for liveness. Port `4200` is reserved for HRM. Collector PostgreSQL is published on `localhost:5433`, leaving HRM's host PostgreSQL port `5432` available. PostgreSQL uses a named volume. Start pgAdmin as well with:

```powershell
docker compose --profile local-tools up --build -d
```

pgAdmin is at `http://localhost:5050`.

The frontend is a separate TypeScript/Vite module in `src/attendanceCollector.WebApp`. For local development, run the API and WebApp in separate terminals:

```powershell
dotnet run --project src/attendanceCollector.API --launch-profile http
cd src/attendanceCollector.WebApp
npm ci
npm run dev
```

Open `http://localhost:4210`. Vite proxies `/auth`, `/api`, and `/health` to the API on port `5200`, keeping Keycloak cookies and callbacks same-origin with the WebApp. For deployment, the Dockerfile builds the WebApp and packages its static output with the API; `docker compose up --build -d` serves both from port `4210`.

By default, `.env` points the API at Collector's standalone Keycloak on port `8081`. Its realm import creates the local browser client and bootstrap admin. OIDC metadata is fetched through `host.docker.internal`; backchannel requests to Keycloak's public `localhost` issuer are routed through that internal address while browser redirects retain the public URL. To use the shared HRM Keycloak instead, set `OIDC_ISSUER`, `OIDC_METADATA_ADDRESS`, `OIDC_CLIENT_ID`, and `OIDC_AUDIENCE` in `.env` to the HRM values and register Collector's callback and origin on that client. For the standalone Collector client, these URLs are registered automatically:

- Valid redirect URI: `http://localhost:4210/auth/callback`
- Valid post-logout redirect URI: `http://localhost:4210/signout-callback-oidc`
- Web origin: `http://localhost:4210`

The logout callback is where Keycloak returns to the API; after it completes, the API sends the browser to the configured `OIDC_POST_LOGOUT_REDIRECT_URI` (`/login`). If the Keycloak client is confidential, set `OIDC_CLIENT_SECRET` in the ignored `.env` or deployment secret store.

Start Collector's standalone Keycloak and its PostgreSQL database with:

```powershell
docker compose -f docker-compose.keycloak.local.yml up -d
```

This standalone server is published on `http://localhost:8081` by default, leaving port `8080` available for the shared HRM Keycloak. Keycloak data persists in its own PostgreSQL volume; Compose waits for PostgreSQL health before starting Keycloak and exposes Keycloak readiness as a health check. Realm `attendance-collector` and client `attendance-collector-webapp` are imported automatically. Set `KEYCLOAK_HOST_PORT` in `.env` to change its host port. The local admin username is `collector-admin` and its password comes from `KEYCLOAK_COLLECTOR_ADMIN_PASSWORD`.

## Login and logout

Open `http://localhost:4210/` in a browser and sign in through Keycloak. The API redirects using Authorization Code flow with PKCE. Keycloak returns to `/auth/callback`; the OIDC handler validates the response and creates an HttpOnly cookie session.

- `GET /auth/me` reads the browser cookie session.
- `GET /auth/csrf` returns a request token for logout.
- `POST /auth/logout` requires that token in the `X-CSRF-TOKEN` header and signs out from the app and Keycloak.
- `GET /api/users/me` remains protected by a Keycloak access token sent as `Authorization: Bearer <token>`.

The API's JWT bearer and browser OIDC flows are separate schemes. `Application` defines `ICurrentUser`; the API adapts the authenticated principal to that interface so use cases don't depend on ASP.NET or Keycloak types. Controllers delegate login/logout handling to `AuthenticationFlowService`; current-user lookup is handled by `GetCurrentUserQueryHandler` in `Application`.

## Bootstrap admin

The bootstrap admin is identified by both `BOOTSTRAP_ADMIN_EXTERNAL_SUBJECT` and `BOOTSTRAP_ADMIN_EMAIL`; a matching authenticated principal receives the `BootstrapAdmin` role. An empty `BOOTSTRAP_ENABLED` enables this mapping when both values are present; `false` disables it. The supplied local values are `admin@aubot.local` and `7b978450-848e-4b7f-830c-c8feb5fe9db2`. `GET /api/users/bootstrap-admin` requires this policy.

## Attendance sources and collection logs

The admin page lets a bootstrap administrator register attendance machines using four visible fields: name, type, IP, and port. `MockDevice HTTP` reads from the separately running simulator over HTTP; with the shared Docker network, use host `mock-device` and port `8080`. `ZK-compatible TCP` connects to real MockMWS devices, typically on port `4370`; when the collector runs in Docker and MockMWS runs on the Windows host, use `host.docker.internal` and the device's port. Both drivers supply communication key `0`, timeout `10` seconds, and time zone `Asia/Bangkok` as defaults.

The separately deployable `attendanceCollector.MockDevice` service includes a dedicated UI at `http://localhost:4300` and its own `mockDevice` PostgreSQL database. Start the collector with `docker compose up --build -d`, then start MockDevice in its own Compose project with `docker compose -p attendance-mock-device --env-file .env -f docker-compose.mock-device.yml up --build -d`. In the MockDevice UI, create sample attendance records. In Collector, register a source with type `MockDevice HTTP`, host `mock-device`, and port `8080`, then collect it like a device. The records are read from MockDevice over the shared private Docker network. The optional “send” API remains available for push-ingestion workflows. Each service retains its own database and volume.

MockDevice and its wire contracts live in the single `attendanceCollector.MockDevice` project. `Contracts/` contains the shared DTOs and constants; Application links those source files so Collector can use the same contract without depending on the simulator executable.

Active pull devices are collected automatically at the start of each UTC hour (UTC+07 local hour boundaries align). Push-only sources are excluded. Administrators can also click **Lấy dữ liệu** in a machine's row to pull that machine immediately; the log/history view focuses on that machine. A PostgreSQL advisory lock elects one Collector instance to run each hourly cycle; manual collection remains available and is serialized per device within the process. The adapter reads the device, converts local attendance times using the configured time zone, and stores new records in PostgreSQL. A device-and-event fingerprint plus a database unique index prevents repeated hourly reads from adding duplicate rows. Collection history records received versus newly stored counts and errors. The page shows attendance logs with machine, employee, and date filters, plus collection history. Database schema migrations are applied on startup in Development. Staging/production require a separate migration step before rollout; `/health/ready` returns 503 while migrations are pending. Set `Database__ApplyMigrationsOnStartup` explicitly only when a single migration owner is guaranteed.

Relevant API routes (all require the bootstrap administrator):

- `GET /api/v1/attendance-sources/drivers` lists implemented drivers.
- `GET|POST /api/v1/attendance-sources` lists or registers machines; `PUT /{id}` updates one and `PATCH /{id}/active` toggles it.
- `POST /api/v1/attendance-sources/{id}/collect` collects from the selected machine.
- `GET /api/v1/attendance/logs` and `GET /api/v1/attendance/collections` return paginated attendance and collection logs.

Browser writes require the antiforgery token from `GET /auth/csrf` in `X-CSRF-TOKEN`.

OTLP and backup variables are included in `.env` for deployment planning, but exporters and a scheduled backup runner are not wired into the application yet.

## Staging and production

Build and publish the container image separately, then deploy with the matching Compose file and a deployment environment file/secret store:

```powershell
docker compose --env-file .env.staging -f docker-compose.staging.yml up -d
docker compose --env-file .env.production -f docker-compose.production.yml up -d
```

These Compose files use required `${NAME:?message}` substitutions for image identity, database connection, OIDC issuer/metadata/audience, callback URLs, host binding, application port, and bootstrap identity. HTTPS metadata is required in both environments. Store secrets in a secret manager and use HTTPS OIDC redirect URLs outside local development.

## Run without Docker

```powershell
dotnet run --project src/attendanceCollector.API
```

This uses the `http` launch profile on port `5200`; the API connects to local Collector PostgreSQL on port `5433`. Override OIDC settings with environment variables such as `OIDC_ISSUER`, `OIDC_METADATA_ADDRESS`, `OIDC_CLIENT_ID`, `OIDC_AUDIENCE`, and `OIDC_REDIRECT_URI`.

