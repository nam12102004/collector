# Vận hành Collector API cho HRM

## Trạng thái và lựa chọn v1

Đã triển khai endpoint đọc, API quản trị client/key/grant, audit, snapshot PostgreSQL, token mã hóa, quota và migration. Integration **tắt mặc định**. Chưa triển khai lên môi trường đang chạy, chưa cấp credential thật hoặc kết nối HRM production.

- `deviceId` công khai là `AttendanceDevice.Id` dạng GUID `D`; không dùng mã máy có thể sửa. HRM cấu hình mapping source HRM → GUID Collector.
- API đọc database Collector, không gọi máy chấm công. `IsActive=false` của máy chỉ dừng thu nhận; quyền đọc lịch sử nằm ở device grant/client.
- `deviceName` lấy từ tên user do thiết bị cung cấp (`EmployeeName`), không phải tên máy. UID là UID user, không phải event ID. Kiểm tra ý nghĩa trên từng driver khi onboarding.
- Response chỉ có `deviceId`, `items` sáu trường, `nextPageToken`; không bọc envelope admin.
- Dữ liệu mới lưu giờ local gốc, timezone và offset lúc thu nhận. Giờ DST lặp/không tồn tại bị từ chối thu nhận; không đoán offset.
- Log cũ thiếu local gốc trả `409 DATA_NOT_EXPORTABLE`. Ngoại lệ duy nhất là log legacy của `mock-device-push`: mock publisher cố định `Asia/Bangkok`, nên API chiếu `OccurredAtLocal` từ UTC khi thiết bị cũng cấu hình cùng timezone; không sửa UTC hoặc fingerprint. Log cũ của driver khác vẫn cần metadata giờ local/timezone lúc thu nhận và không tự suy đoán từ cấu hình hiện tại.
- Không cắt ngắn chuỗi hoặc bỏ qua log lỗi. Khoảng có dữ liệu vượt giới hạn/control character cũng trả 409. Rỗng do thiếu user/tên được trả `null`; số 0 đầu giữ dạng text.
- Collector vốn đã dedupe lúc thu nhận; API chỉ trả bản ghi đang lưu. Các định dạng thiết bị chỉ có ID số không thể khôi phục số 0 đầu đã mất.
- HRM dùng `GET /api/v1/devices` để lấy ID/mã/tên/driver và trạng thái; host/port chỉ nằm ở Collector, không có trong contract.
- `POST /api/v1/devices/{deviceId}/collect` và `/connection` yêu cầu scope `attendance.logs.collect` cùng grant đúng thiết bị. Khi tạo client HRM, đặt `allowCollection=true` để cấp `attendance.logs.read attendance.logs.collect`; client khác mặc định chỉ có `attendance.logs.read` và tiếp tục chỉ đọc log/danh sách.
- Lệnh collection thủ công là độc lập với worker chạy theo lịch. HRM dùng collection response rồi đọc log qua endpoint snapshot hiện có.

## Chuẩn bị schema và cấu hình

1. Backup và kiểm tra restore. Review migration `HrmReadIntegration`: chỉ thêm ba cột nullable trên attendance log, index và các bảng integration/snapshot/audit. Không đổi GUID, UTC hay fingerprint cũ.
2. Chạy migration một lần trước khi rollout. Compose staging/production đặt `Database__ApplyMigrationsOnStartup=false`; Development giữ hành vi tự migrate. Design-time factory không khởi chạy web host và dùng connection giả nếu không có `COLLECTOR_MIGRATION_CONNECTION`.
3. Khi thực thi bằng EF CLI, inject connection vào biến `COLLECTOR_MIGRATION_CONNECTION` từ secret store trong phiên vận hành, rồi chạy lệnh dưới đây. Không đặt connection thật vào lịch sử lệnh hoặc output. SQL được EF sinh tại [collector-integration-migrations.sql](collector-integration-migrations.sql) để review; bản idempotent kiểm tra lịch sử migration.

```powershell
dotnet ef database update --project src/attendanceCollector.Infrastructure --startup-project src/attendanceCollector.API --context AttendanceCollectorDbContext
```

4. Mount key ring bền vững, dùng chung giữa các replica. Compose đã gắn volume `/var/lib/collector/keys`; image tạo thư mục với quyền của app user. Với nhiều host, dùng volume dùng chung được bảo vệ phù hợp; named volume chỉ dùng chung trong cùng Docker host/project.
5. Ngoài Development phải mount PFX có RSA private key để mã hóa key ring. Inject password qua secret deployment. File cần app user đọc được, tránh mount thư mục chứa thêm bí mật không liên quan. Giữ certificate/private key ổn định khi rolling restart; thay certificate cần kế hoạch giải mã lại key ring hoặc drain hết token cũ trước khi chuyển ring mới. Không xóa ring khi restart/scale.
6. Cấu hình proxy/TLS, chỉ rõ IP proxy tin cậy. Danh sách rỗng sẽ tắt xử lý forwarded headers; caller không thể tự gửi `X-Forwarded-Proto` để bỏ qua HTTPS. Proxy gửi cả `X-Forwarded-For` và `X-Forwarded-Proto`; hiện xử lý một hop tin cậy.
7. Kiểm `/health/live` và `/health/ready`; readiness kiểm database và pending migration, trả 503 khi chưa sẵn sàng. Startup xác minh cấu hình limits, khả năng ghi key ring và mã hóa/giải mã token.

Cấu hình qua environment dùng tên .NET hai dấu gạch dưới. Các biến viết hoa ngắn trong Compose được nối tại ba file Compose hiện có:

```dotenv
COLLECTOR_INTEGRATION_ENABLED=false
COLLECTOR_TOKEN_CERTIFICATE_PATH=/run/secrets/collector-token.pfx
COLLECTOR_TOKEN_CERTIFICATE_PASSWORD=<inject-secret>
COLLECTOR_TRUSTED_PROXY_IP=<exact-proxy-IP>
```

Ví dụ override mount certificate, thay đường dẫn host theo nơi lưu secret thực tế:

```yaml
services:
  api:
    volumes:
      - type: bind
        source: /secure/collector-token.pfx
        target: /run/secrets/collector-token.pfx
        read_only: true
```

Native environment tương đương: `CollectorIntegration__Enabled`, `CollectorIntegration__KeyRingPath`, `CollectorIntegration__KeyEncryptionCertificatePath`, `CollectorIntegration__KeyEncryptionCertificatePassword`, `CollectorIntegration__TrustedProxyAddresses__0` (thêm `__1` nếu cần). Compose hiện nối một IP; thêm IP qua override. Các limit khác cũng dùng prefix `CollectorIntegration__`.

Local development được phép dùng HTTP chỉ khi đồng thời là môi trường Development và bật `COLLECTOR_ALLOW_DEVELOPMENT_HTTP=true` trong Compose, hoặc `CollectorIntegration__AllowDevelopmentHttp=true` khi chạy native. HRM local Compose kết nối qua `COLLECTOR_BASE_URL=http://host.docker.internal:4210`; staging/production phải dùng HTTPS endpoint Collector.

## Cấp client và key

Các route dưới `/api/v1/integration-clients` yêu cầu admin Collector, dùng cookie/JWT hiện có; browser writes cần antiforgery. MVC đã đăng ký filter antiforgery cho các route quản trị. Chưa thêm UI quản trị integration; dùng API từ phiên admin tin cậy.

Với browser, đọc `GET /auth/csrf` trong phiên cookie và gửi `requestToken` ở header `X-CSRF-TOKEN` cho các lệnh ghi. Không đưa key HRM vào frontend HRM. Admin Collector chỉ thấy secret một lần tại response tạo key; secret phải được chuyển ngay vào secret store của backend HRM.

| Lệnh | Payload/kết quả |
|---|---|
| `POST /api/v1/integration-clients` | `{"name":"HRM staging","deviceIds":["<collector-guid>"],"allowCollection":true}`; trả client và `version`. `allowCollection` mặc định false; chỉ bật cho HRM nếu cần pull/check kết nối. Tập rỗng không cấp thiết bị nào. Tối đa 1.000 grant/client. |
| `GET /api/v1/integration-clients?page=1&pageSize=50` | Danh sách có phân trang, page size tối đa 100. |
| `GET /api/v1/integration-clients/{id}` | Grants và metadata 100 key gần nhất; không có secret/verifier. Key ID cũ vẫn revoke được nếu đã ghi nhận. |
| `POST /api/v1/integration-clients/{id}/keys` | `{}` hoặc `{"expiresAtUtc":"...Z"}`; trả `keyId`, `apiKey`, expiry đúng một lần. Tối đa hai key còn hiệu lực để rotation. |
| `PUT /api/v1/integration-clients/{id}/device-grants` | `{"version":"<current-version>","deviceIds":[...]}`; thay tập quyền. |
| `PATCH /api/v1/integration-clients/{id}/active` | `{"version":"<current-version>","isActive":false}`; disable chặn mọi key. |
| `DELETE /api/v1/integration-clients/{id}/keys/{keyId}` | Thu hồi idempotent; giữ lịch sử audit. |

Tạo/revoke key và thay quyền làm tăng `version`. Lấy lại client trước khi sửa grant/active; phiên bản cũ nhận 409. Các thao tác quản trị và audit được commit cùng transaction, có khóa client để bảo vệ cập nhật đồng thời.

HRM dùng `X-API-Key` trên HTTPS. Key có key ID công khai + 32 byte secret ngẫu nhiên; database chỉ lưu SHA-256 verifier của key entropy cao, so sánh fixed-time. Không dùng key mock ingestion, JWT người dùng hoặc cookie admin vào endpoint đọc HRM.

## Kiểm tra đọc và phân trang

```http
GET /api/v1/devices/<collector-guid>/attendance-logs?fromUtc=2026-11-05T17%3A00%3A00Z&toUtc=2026-11-06T17%3A00%3A00Z&pageSize=500
X-API-Key: <secret-injected-by-HRM-backend>
Accept: application/json
```

- Khoảng `[fromUtc,toUtc)` tối đa 31 × 24 giờ, page size 1–500, mặc định 500. Chỉ nhận UTC `Z`/`+00:00`, encode dấu `+` thành `%2B`. Tham số lặp/ngoài contract bị từ chối.
- Snapshot vật hóa projection tại trang đầu bằng một câu `INSERT ... SELECT` theo MVCC, thứ tự `(OccurredAtUtc, Id)`. Không giữ transaction/connection giữa HTTP request. Log đến/commit sau đó chỉ thuộc lượt đọc mới.
- Dùng token với đúng client/device/filter/page size; token ràng buộc client ID nên đổi key cùng client vẫn đọc tiếp được. Mỗi trang kiểm quyền và key từ DB, kiểm lại quyền sau query; request đã bắt đầu gửi response không thể thu hồi ngược dữ liệu đã gửi.
- Snapshot/token TTL cố định 15 phút. Token cùng snapshot có thể dùng lại trong TTL, trả cùng trang. Hết hạn cần bắt đầu lượt mới; HRM hiện chưa có UX tự restart và ánh xạ phần lớn lỗi upstream thành 502 chung.
- Tập rỗng trả `200`, `items: []`, `nextPageToken: null`. Trang cuối token null. Không truyền token vào URL/host khác hoặc log token.
- Worker dọn tối đa 100 snapshot hết hạn mỗi phút, cascade rows; kiểm tra cleanup lag nếu điều chỉnh capacity vượt mặc định. Khi vận hành sửa/xóa dữ liệu attendance hoặc cấm export, xóa snapshot header liên quan trong cùng quy trình để vô hiệu hóa dữ liệu đã sao chép.

## Limits, audit và giám sát

Mặc định mỗi process: 60 request/phút/client, hai request đồng thời/client, tám request đồng thời tổng, không queue. Request quota trả 429 + `Retry-After`; hết concurrency/snapshot capacity trả 503 + `Retry-After` khi biết. Quota toàn cluster cần gateway/distributed limiter bên ngoài; SQL khóa quota snapshot được chia sẻ qua PostgreSQL.

Snapshot tối đa 100.000 row và 64 MiB theo ước lượng bảo thủ, bốn snapshot/client, 32 tổng. Vượt cap rollback hoàn toàn, không publish phần dữ liệu bị cắt. Response tối đa 1 MiB UTF-8 trước nén; request deadline 20 giây, command timeout 10 giây. Các limit có thể cấu hình trong khoảng được kiểm tra ở startup, cần load test theo dữ liệu thật trước khi tăng.

Audit đọc lưu Requested trước authentication/query và completion trước khi gửi raw. Lỗi audit chặn trả raw; failure/cancellation completion có deadline riêng hai giây. Audit chỉ lưu request/client/device/filter/outcome/count/duration/error code, không lưu payload, API key hay token. Khi integration đang tắt, trả 503 trước khi truy cập database. Audit chưa có API xem hoặc tự purge; owner vận hành phải chốt retention/quyền đọc theo chính sách và quản lý riêng với TTL snapshot.

Metrics đã khai báo bằng `System.Diagnostics.Metrics`, meter `attendanceCollector.Integration`: `collector.hrm.read.requests`, `collector.hrm.read.duration` (ms), `collector.hrm.audit.failures`. Chỉ gắn HTTP status, không gắn user/device/client/token. Chưa cấu hình OTLP exporter, dashboard hoặc hệ thống cảnh báo bên ngoài; có thể nối meter này vào pipeline quan sát của môi trường.

Tại ingress/APM, loại header `X-API-Key`, full query string, request/response body của route raw và response tạo key khỏi logging/tracing. Tắt log request URL tự động có page token. IP limiter/allowlist phải đặt tại edge trước request audit/authentication để hạn chế request rác.

## Xoay vòng, thu hồi và rollback

1. Tạo key mới trong cùng client, giữ key cũ còn hiệu lực.
2. Cập nhật secret backend HRM, recreate/restart HRM nếu inject bằng environment. Gửi request staging xác minh header/binding và đọc tiếp token đang có.
3. Revoke key cũ tại Collector; request mới bằng key cũ phải nhận 401. Không ghi key vào ticket/audit.
4. Khi có sự cố, disable client hoặc thu hồi grant/key; chỉ tắt integration toàn cục khi cần. Thay cấu hình Enabled yêu cầu restart.
5. Rollback binary tương thích schema additive, không drop các bảng/cột mới hoặc đổi fingerprint/UTC. Giữ audit và key ring; không tự purge dữ liệu attendance.

HRM hiện yêu cầu HTTPS/public IP allowlist; không dùng tên service Docker/localhost/IP private. Collector ingress allowlist địa chỉ egress HRM là lớp bổ sung. Production cần TLS/public DNS/IP đúng cấu hình HRM, proxy IP tin cậy, certificate, key ring chung, backup, retention audit, cảnh báo và thử tải. Các bước này cần thông tin vận hành thật và chưa được thực hiện trong lần triển khai source code này.

## Kiểm thử lại

```powershell
dotnet build attendanceCollector.slnx -m:1 -c Release
dotnet test attendanceCollector.slnx --no-build --no-restore -m:1 -c Release
dotnet ef migrations has-pending-model-changes --project src/attendanceCollector.Infrastructure --startup-project src/attendanceCollector.API --context AttendanceCollectorDbContext
```

Integration tests cần biến `COLLECTOR_TEST_POSTGRES` trỏ tới PostgreSQL **tạm tại localhost**, database gốc tên bắt đầu `collector_test`, user có quyền tạo/xóa database test. Không có biến này, các test PostgreSQL được skip có lý do. Fixture tự tạo database GUID riêng, nâng cấp từ migration cũ có dữ liệu rồi chạy migration mới; cuối suite xóa đúng database test đó. Không dùng database Collector thật.

Các test bao phủ contract HTTP, UTC/DST/precision, snapshot/late insert, token/filter/expiry/replica, Unicode, quyền thiết bị/revoke/rotation, audit fail-closed, cap row/response, rate limit theo client, antiforgery quản trị, migration giữ log cũ và hướng phụ thuộc kiến trúc. Kiểm chứng với driver thiết bị thật, HRM staging, TLS public, tải production và dashboard vẫn thuộc nghiệm thu môi trường.

Kết quả ngày 06/11/2026: Release build không warning/error; 52 test pass, không skip (31 unit, 20 HTTP/PostgreSQL 17 thật, một architecture). EF không có pending model changes; SQL migration idempotent chạy thành công hai lần trên database tạm. Compose local/staging/production validate bằng placeholder. Không áp migration hoặc thay dữ liệu tại database đang chạy.
