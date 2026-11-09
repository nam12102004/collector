# Deploy và lệnh vận hành Collector

Tài liệu tổng hợp các lệnh chạy Collector, MockDevice và Keycloak theo cấu hình hiện tại. Chạy lệnh từ thư mục gốc `attendanceCollector`. File `.env` là cấu hình local bị Git bỏ qua; không commit mật khẩu, API key, connection string hoặc certificate.

## 1. Chuẩn bị local

Tạo `.env` từ mẫu nếu chưa có, sau đó điền các secret local cần thiết:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

Compose local dùng PostgreSQL mặc định chỉ dành cho phát triển. Không tái sử dụng các giá trị local cho staging hoặc production. API tự chạy migration khi chạy trong Development.

## 2. Chạy Collector bằng Docker Compose

Build image local, khởi động PostgreSQL và API:

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

Địa chỉ thường dùng:

- WebApp/API: `http://localhost:4210`
- Liveness: `http://localhost:4210/health/live`
- Readiness: `http://localhost:4210/health/ready`
- PostgreSQL: `localhost:5434` nếu dùng `.env.example`; Compose mặc định là `5433` nếu không đặt `LOCAL_DB_PORT`.

Mở pgAdmin cùng PostgreSQL và API khi cần:

```powershell
docker compose --profile local-tools up --build -d
```

pgAdmin mặc định dùng `http://localhost:5050`. Đặt `PGADMIN_EMAIL` và `PGADMIN_PASSWORD` trong `.env` trước khi bật profile.

Các lệnh quản lý thường dùng:

```powershell
docker compose ps
docker compose logs -f api postgres
docker compose restart api
docker compose pull
docker compose down
```

`docker compose down` giữ các named volume. Chỉ dùng `docker compose down -v` khi chủ động xóa database local và key ring; dữ liệu trong volume sẽ mất.

## 3. Chạy Keycloak local

Đặt các biến sau trong `.env` với secret local riêng:

- `KEYCLOAK_DB_PASSWORD`
- `KEYCLOAK_ADMIN_PASSWORD`
- `KEYCLOAK_COLLECTOR_ADMIN_PASSWORD`
- `BOOTSTRAP_ADMIN_EMAIL`
- `BOOTSTRAP_ADMIN_EXTERNAL_SUBJECT`

Khởi động hoặc kiểm tra Keycloak:

```powershell
docker compose -f docker-compose.keycloak.local.yml up -d
docker compose -f docker-compose.keycloak.local.yml ps
docker compose -f docker-compose.keycloak.local.yml logs -f keycloak
```

Keycloak local dùng `http://localhost:8081`, realm `attendance-collector`. Cấu hình OIDC trong `.env` phải trỏ issuer về realm này. Dừng Keycloak mà vẫn giữ database:

```powershell
docker compose -f docker-compose.keycloak.local.yml down
```

## 4. Chạy MockDevice

MockDevice dùng database và volume riêng. Trước khi chạy, đặt `MOCK_DEVICE_DB_PASSWORD` và `MOCK_DEVICE_INGESTION_API_KEY` trong `.env`. Collector và MockDevice cần cùng network; khởi động Collector trước để Compose tạo network.

```powershell
docker compose -p attendance-mock-device --env-file .env -f docker-compose.mock-device.yml up --build -d
docker compose -p attendance-mock-device --env-file .env -f docker-compose.mock-device.yml ps
docker compose -p attendance-mock-device --env-file .env -f docker-compose.mock-device.yml logs -f mock-device
```

MockDevice UI thường ở `http://localhost:4300`. Có thể chọn một trong hai luồng:

- **Pull HTTP:** đăng ký source `MockDevice HTTP` trong Collector, host `mock-device`, port `8080`; Collector đọc các sự kiện MockDevice đã tạo.
- **Push:** dùng chức năng gửi của MockDevice. Giá trị `MOCK_DEVICE_INGESTION_API_KEY` phải giống với key được API Collector nhận; request dùng header `X-Mock-Device-Key`.

Dừng riêng MockDevice, giữ volume:

```powershell
docker compose -p attendance-mock-device --env-file .env -f docker-compose.mock-device.yml down
```

## 5. Chạy API và WebApp ngoài Docker

`.env` không được .NET tự nạp khi chạy bằng `dotnet run`; hãy cấu hình biến môi trường trong shell/IDE hoặc dùng Docker Compose. Ví dụ dưới đây dùng cổng DB local `5434` từ `.env.example`; connection string của native API phải được inject vào shell/IDE từ nguồn local, không commit giá trị đó:

```powershell
# Terminal 1: chạy PostgreSQL bằng Compose
docker compose up -d postgres

# Terminal 2: chạy API bằng launch profile http
$env:ConnectionStrings__Default = "<local PostgreSQL connection string on port 5434>"
dotnet run --project src/attendanceCollector.API --launch-profile http
```

API dev chạy ở port `5200`; Vite proxy các route `/auth`, `/api` và `/health` về API. Terminal thứ hai:

```powershell
Set-Location src/attendanceCollector.WebApp
npm ci
npm run dev
```

Vite thường phục vụ ở `http://localhost:4210`. Nếu chạy OIDC local, khởi động Keycloak và cấu hình OIDC redirect/origin khớp với URL trình duyệt đang dùng.

## 6. Build và publish image API

Compose staging/production dùng image đã push sẵn, không build source trên máy chủ deploy. Đặt tag phát hành cụ thể và đăng nhập registry bằng credential do tổ chức cấp:

```powershell
$env:IMAGE_REGISTRY = "ghcr.io/aubot-it"
$env:IMAGE_TAG = "<release-tag>"
docker login ghcr.io
docker build --pull -t "$env:IMAGE_REGISTRY/attendance-collector-api:$env:IMAGE_TAG" .
docker push "$env:IMAGE_REGISTRY/attendance-collector-api:$env:IMAGE_TAG"
```

Không dùng tag `local` cho release. `IMAGE_TAG` ở máy build phải trùng `IMAGE_TAG` trong file môi trường deploy.

## 7. Chuẩn bị staging/production

Tạo `.env.staging` hoặc `.env.production` từ nguồn secret/config của môi trường; không dùng `.env.example` local làm cấu hình deploy nguyên trạng. Compose yêu cầu các biến dưới đây (ngoài biến integration tùy chọn):

| Nhóm | Biến bắt buộc |
| --- | --- |
| Image | `IMAGE_REGISTRY`, `IMAGE_TAG` |
| Bind/port | `EDGE_BIND_ADDRESS`, `STAGING_APP_PORT` hoặc `PRODUCTION_APP_PORT` |
| Database TLS | `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD` |
| Allowed hosts | `ALLOWED_HOSTS` |
| OIDC | `OIDC_ISSUER`, `OIDC_METADATA_ADDRESS`, `OIDC_CLIENT_ID`, `OIDC_REDIRECT_URI`, `OIDC_POST_LOGOUT_REDIRECT_URI`, `OIDC_AUDIENCE`, `OIDC_VALIDATE_AUDIENCE` |
| Bootstrap admin | `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_EXTERNAL_SUBJECT` |
| Observability | `OTEL_SERVICE_VERSION` |

Ngoài ra, đăng ký redirect URI HTTPS trên IdP, xác nhận Collector có network route tới các máy chấm công, chuẩn bị TLS ingress/firewall, backup database và secret store. Hai Compose file đặt `Database__ApplyMigrationsOnStartup=false`; không khởi chạy nhiều replica để cùng migrate.

Nếu bật HRM integration, đọc [runbook integration](collector-integration-runbook.md) để cấu hình key ring bền vững, certificate mã hóa key, trusted proxy, API key rotation và grant thiết bị. Không bật integration cho tới khi các thành phần secret/TLS cần thiết sẵn sàng.

## 8. Migration và deploy staging

Backup database và xác nhận phương án restore trước khi nâng schema. Chạy migration **một lần** bằng EF CLI từ source checkout; inject `COLLECTOR_MIGRATION_CONNECTION` từ secret store vào môi trường của phiên deploy, không đặt secret trực tiếp trong command line hoặc commit vào file:

```powershell
dotnet ef database update --project src/attendanceCollector.Infrastructure --startup-project src/attendanceCollector.API --context AttendanceCollectorDbContext
```

Kiểm tra cấu hình Compose mà không in các giá trị đã resolve:

```powershell
docker compose --env-file .env.staging -f docker-compose.staging.yml config --quiet
```

Sau khi migration thành công và image đã được push:

```powershell
docker compose --env-file .env.staging -f docker-compose.staging.yml pull
docker compose --env-file .env.staging -f docker-compose.staging.yml up -d
docker compose --env-file .env.staging -f docker-compose.staging.yml ps
docker compose --env-file .env.staging -f docker-compose.staging.yml logs -f api
```

Xác minh `/health/live` và `/health/ready` qua endpoint staging. Readiness trả 503 nếu không truy cập được database hoặc còn pending migration. Sau deploy, kiểm tra OIDC login, source/driver, collect thử trên thiết bị được phép, run history và kết nối HRM staging nếu integration được bật.

## 9. Deploy production

Thực hiện cùng quy trình: backup/restore check, migration một lần, xác nhận image tag và `.env.production`, sau đó:

```powershell
docker compose --env-file .env.production -f docker-compose.production.yml config --quiet
docker compose --env-file .env.production -f docker-compose.production.yml pull
docker compose --env-file .env.production -f docker-compose.production.yml up -d
docker compose --env-file .env.production -f docker-compose.production.yml ps
docker compose --env-file .env.production -f docker-compose.production.yml logs -f api
```

Compose publish port `${EDGE_BIND_ADDRESS}:${PRODUCTION_APP_PORT}`. Reverse proxy/TLS thường được cấu hình ở hạ tầng bên ngoài Compose. Giới hạn truy cập port theo thiết kế mạng, không phơi PostgreSQL ra public internet.

Rollback binary bằng cách trỏ lại `IMAGE_TAG` tương thích schema rồi chạy `up -d`. Không tự rollback hoặc xóa cột/bảng migration khi chưa có kế hoạch tương thích dữ liệu; xem [runbook](collector-integration-runbook.md) trước khi thay đổi schema hoặc key ring.

## 10. Lệnh kiểm tra và troubleshooting

```powershell
# Trạng thái và log API
docker compose ps
docker compose logs --tail 200 api

# Health từ host
Invoke-WebRequest http://localhost:4210/health/live
Invoke-WebRequest http://localhost:4210/health/ready

# Xem cấu hình Compose đã validate (không dùng lệnh này nếu không muốn in secret ra terminal/log)
docker compose config

# Build và test solution (chạy khi cần kiểm chứng source thay đổi)
dotnet build attendanceCollector.slnx -m:1 -c Release
dotnet test attendanceCollector.slnx --no-build --no-restore -m:1 -c Release
```

Nếu API unhealthy, xem log `api` và `postgres`, kiểm tra database credentials/network, readiness và migration. Nếu pull thiết bị lỗi, xem lịch sử collection cùng log API; hướng dẫn chi tiết network/driver nằm trong [mô tả phân hệ máy chấm công](attendance-devices.md). Với lỗi HRM key, grant, snapshot hoặc proxy, dùng [runbook integration](collector-integration-runbook.md).
