# Kế hoạch lập trình API phía Collector cho HRM

> Cập nhật ngày 06/11/2026. Kế hoạch được đối chiếu với mã nguồn Collector và client HRM, sau đó đã triển khai phần source code theo yêu cầu. Các lựa chọn v1 và trạng thái kiểm chứng nằm ở mục 16 và [runbook](collector-integration-runbook.md). Integration tắt mặc định; chưa rollout hoặc nghiệm thu với HRM/thiết bị thật. Các mục **đề xuất** bên dưới ghi lại thiết kế và yêu cầu nghiệm thu môi trường.

## 1. Mục tiêu và phạm vi

Tài liệu này là đặc tả bàn giao cho đội phát triển **phía Collector**. Collector cần cung cấp API HTTPS để backend HRM chủ động đọc các trang log thô từ một thiết bị được cấp quyền.

Luồng chỉ là đọc theo yêu cầu. Collector không push dữ liệu sang HRM; không cần xây scheduler/checkpoint cho HRM. HRM không ghi raw log vào database trong use case này, không xử lý trùng, không ánh xạ nhân viên và không tính công. Collector tiếp tục giữ hệ thống dữ liệu riêng của mình; API chỉ truy vấn và trả dữ liệu.

Read-only ở đây nghĩa là không sửa attendance log hoặc kích hoạt thu nhận thiết bị. Việc ghi audit và trạng thái snapshot tạm phía Collector được phép nếu chọn thiết kế tại mục 12; không tạo checkpoint hoặc job đồng bộ cho HRM.

## 2. Contract cần triển khai

### Endpoint

```http
GET /api/v1/devices/{deviceId}/attendance-logs
X-API-Key: <credential riêng của HRM>
Accept: application/json
```

Không nhận URL Collector hoặc host từ request. `deviceId` là ID thiết bị Collector đã cấp cho client HRM, không phải HRM `sourceId`.

### Query parameters

| Tên | Quy tắc |
|---|---|
| `fromUtc` | ISO 8601 UTC, bắt buộc, có offset `Z`/`+00:00` |
| `toUtc` | ISO 8601 UTC, bắt buộc, lớn hơn `fromUtc` |
| `pageSize` | Số nguyên từ 1 đến 500 |
| `pageToken` | Không bắt buộc; opaque token tối đa 2048 ký tự |

Đề xuất mặc định `pageSize=500` khi thiếu để khớp mặc định phía HRM; HRM hiện luôn gửi tham số này. `pageToken` có mặt phải khác rỗng và không chứa control character. Từ chối tham số lặp có thể gây diễn giải khác nhau giữa proxy và ứng dụng. Dấu `+` trong `+00:00` phải được URL-encode thành `%2B`; parser phải nhận được cả dạng `Z` và `+00:00`, kể cả phần giây thập phân do HRM gửi bằng định dạng `O`.

Khoảng thời gian là `[fromUtc, toUtc)`: lấy log có thời điểm tương ứng từ mốc đầu, không lấy mốc cuối. Từ chối request sai định dạng, quá khoảng thời gian tối đa đã thống nhất với HRM, page size ngoài giới hạn hoặc token quá dài. HRM hiện giới hạn khoảng đọc tối đa 31 ngày và page size tối đa 500.

### Response thành công

HTTP `200`, `Content-Type: application/json`:

```json
{
  "deviceId": "clock-01",
  "items": [
    {
      "deviceUserId": "000123",
      "deviceUid": 123,
      "deviceName": null,
      "occurredAtLocal": "2026-11-06T08:02:15",
      "rawStatus": 1,
      "rawPunch": 0
    }
  ],
  "nextPageToken": null
}
```

Quy tắc payload:

- `deviceId` phải đúng thiết bị trong path.
- `items` luôn là mảng, không `null`, không vượt `pageSize`.
- `occurredAtLocal`, `rawStatus`, `rawPunch` bắt buộc.
- `deviceUserId` nullable, tối đa 64 ký tự; giữ dạng text và bảo toàn số 0 đầu.
- `deviceUid` là số nguyên 64-bit nullable theo DTO HRM; không phải ID sự kiện và không được tự sinh khi nguồn không có.
- `deviceName` nullable, tối đa 128 ký tự.
- Đề xuất chốt `deviceName` là tên user được thiết bị cung cấp, không phải tên máy; cần HRM xác nhận ngữ nghĩa tại P0 vì DTO hiện chỉ mô tả kiểu/giới hạn. Không lấy từ hồ sơ nhân sự HRM.
- `occurredAtLocal` là chuỗi ISO 8601 không `Z`/offset, có ngày giờ hợp lệ; giữ độ chính xác thời gian đang có, không đổi theo timezone hệ điều hành API.
- Chuỗi không được chứa control character.
- `rawStatus` và `rawPunch` trả nguyên giá trị thiết bị; không chuẩn hóa hoặc tự gán ý nghĩa.
- Không trả thông tin nội bộ, credential, cấu hình thiết bị, dữ liệu nhân viên không liên quan hoặc metadata không nằm trong contract.
- Trang cuối trả `nextPageToken: null`.
- Không có dữ liệu trả `200` với `items: []`, `nextPageToken: null`; không dùng `204` hoặc coi là lỗi.
- Response thành công không bọc bằng `SuccessResponse`, không trả `totalCount`, số trang hoặc entity EF trực tiếp. Thêm `Cache-Control: no-store` cho cả thành công và lỗi của API tích hợp.

## 3. Thiết kế xác thực và phân quyền

### 3.1. Baseline khớp HRM hiện tại

HRM hiện gửi API key qua HTTPS bằng header `X-API-Key`. Vì vậy baseline phía Collector cần:

1. Tạo một integration client riêng cho HRM, không dùng tài khoản quản trị hoặc tài khoản thiết bị.
2. Credential chỉ được gọi API đọc attendance.
3. Gán danh sách thiết bị cụ thể cho client; không mặc định cho phép mọi `deviceId`.
4. Kiểm tra credential trước khi truy vấn dữ liệu.
5. Kiểm tra quyền trên `deviceId` ở mỗi request, kể cả khi credential hợp lệ.
6. Thu hồi client/key phải có hiệu lực ngay hoặc theo SLA đã thống nhất.

Không dùng JWT đăng nhập người dùng HRM làm credential giữa hai server. Không gửi API key về browser.

### 3.2. Lưu và xoay vòng credential

- Tạo key ngẫu nhiên có entropy cao; chỉ hiển thị đầy đủ đúng lúc tạo hoặc xoay vòng.
- Nếu xác thực bằng API key tự quản lý, chỉ lưu verifier/hash an toàn phía Collector; không ghi plaintext vào database, log hoặc audit. So sánh verifier theo cách chống timing leak.
- Tách định danh/key ID khỏi phần secret để tra cứu credential mà không phải thử mọi key.
- Hỗ trợ disable/revoke, ngày hết hạn, thời điểm tạo/đổi và người thực hiện.
- Quy trình xoay vòng nên cho phép key mới và key cũ cùng hiệu lực trong một khoảng ngắn: tạo key mới → cập nhật secret phía HRM → kiểm tra request → thu hồi key cũ.
- Không đưa key vào query string.

OAuth Client Credentials là lựa chọn khác **chỉ khi Collector đã có authorization server phù hợp**. Khi đó phải thống nhất token endpoint, issuer/audience, scope chỉ đọc, thời hạn token và cách xoay secret; HRM hiện chưa dùng cơ chế này nên cần thay đổi contract/client phía HRM trước khi chọn OAuth. mTLS là tăng cường tùy chọn, không thay thế kiểm tra quyền thiết bị.

### 3.3. Mã trạng thái xác thực/quyền

- `401 Unauthorized`: key thiếu, sai, hết hạn hoặc bị thu hồi.
- `403 Forbidden`: client có credential nhưng không có scope đọc hoặc không được phép đọc thiết bị đó.
- Không trả trong error body liệu key nào tồn tại, secret nào sai hoặc danh sách thiết bị client được cấp.
- Không redirect request API.

## 4. Xử lý timezone, truy vấn và phân trang

### 4.1. Timezone

Request dùng UTC nhưng contract response giữ `occurredAtLocal` không có offset. Collector hiện đã lưu `AttendanceLog.OccurredAtUtc`, nên lọc trực tiếp trên cột UTC bằng `>= fromUtc` và `< toUtc`; không cần đổi filter sang local hoặc áp dụng hàm timezone lên cột đang được đánh index. Timezone thiết bị được dùng khi thu nhận dữ liệu và khi khôi phục local cho dữ liệu lịch sử chưa lưu giờ gốc.

Hiện database chưa giữ `DeviceLocalTime` gốc hay timezone/offset tại lúc thu nhận. Nếu đổi `AttendanceDevice.TimeZoneId`, việc đổi UTC về local bằng cấu hình hiện tại có thể làm thay đổi giờ trả về của log cũ. Đề xuất lưu bổ sung local timestamp gốc và timezone/offset đã áp dụng lúc thu nhận cho cả luồng ZK và mock ingestion. Không cập nhật lại `OccurredAtUtc` hay fingerprint cũ chỉ để phục vụ API đọc. Dữ liệu cũ chỉ được backfill khi xác minh được timezone lịch sử; không coi cấu hình hiện tại là bằng chứng đủ.

Trước khi triển khai, đội Collector và HRM cần xác nhận:

- Nguồn timezone của mỗi thiết bị và ai chịu trách nhiệm cập nhật.
- Cách xử lý thiết bị không có timezone hoặc cấu hình timezone sai.
- Quy tắc khi timezone có daylight saving time. Timestamp local không có offset có thể mơ hồ vào thời điểm đổi giờ; không tự đoán offset.

Nếu có thiết bị thuộc timezone có giờ lặp/không tồn tại do DST, cần thống nhất cách biểu diễn bổ sung với HRM trước khi bật thiết bị đó. Không thay đổi schema response mà không cập nhật contract HRM.

### 4.2. Truy vấn và thứ tự ổn định

- Truy vấn chỉ đọc, lọc theo `deviceId` và trực tiếp trên `OccurredAtUtc` như mục 4.1; endpoint đọc database Collector, không gọi máy chấm công hoặc kích hoạt collection trong request HRM.
- Dùng index phù hợp trên thiết bị và timestamp để giới hạn chi phí truy vấn.
- Sắp xếp ổn định, có khóa phụ nội bộ để phân biệt nhiều log trùng timestamp; không coi `deviceUid` là event ID ổn định.
- Không tải toàn bộ khoảng thời gian vào bộ nhớ rồi mới phân trang.
- Chốt với HRM hành vi khi log mới được ghi trong lúc client đang đi qua nhiều trang. Nếu cam kết snapshot, không chỉ dùng `ReceivedAtUtc <= thời điểm trang đầu` hoặc `sequence <= max`: transaction đã cấp thời gian/sequence nhưng commit muộn vẫn có thể xuất hiện giữa các trang. Phương án cụ thể và giới hạn được nêu ở mục 12.3.

### 4.3. Page token an toàn

Token phải opaque với caller, tối đa 2048 ký tự, và được bảo vệ toàn vẹn. Nội dung token nên ràng buộc tối thiểu:

- client đã xác thực;
- `deviceId`;
- hash của `fromUtc`, `toUtc`, `pageSize` và filter;
- cursor/watermark;
- phiên bản token và thời điểm hết hạn.

Token gửi lại sai client, thiết bị hoặc filter phải bị từ chối. Không nhận cursor do caller tự tạo. Dùng authenticated encryption hoặc cơ chế bảo vệ tương đương nếu token chứa cursor nội bộ; chữ ký đơn thuần chỉ chống sửa đổi, không che nội dung. Không ghi token đầy đủ vào log. Ràng buộc với client ID ổn định, không ràng buộc riêng với key ID để rotation trong cùng client không làm hỏng lượt đọc. Mỗi trang vẫn kiểm tra trạng thái client, key và quyền thiết bị hiện hành; token không thay thế authorization.

## 5. Bảo vệ mạng và chống lạm dụng

- Chỉ phục vụ HTTPS với certificate hợp lệ; không bật chế độ bỏ qua kiểm tra certificate.
- Đặt API sau reverse proxy/TLS termination được quản lý; chỉ mở port cần thiết.
- Database Collector không được truy cập trực tiếp từ Internet.
- Nếu HRM có outbound IP ổn định, allowlist IP đó ở firewall/proxy Collector như lớp bổ sung; không dùng allowlist thay authentication.
- Tách UI quản trị khỏi API công khai; ưu tiên VPN hoặc mạng quản trị riêng.
- Rate limit theo integration client, có thể kết hợp IP; trả `429` và `Retry-After` khi bị giới hạn.
- Đặt timeout, giới hạn page size, khoảng thời gian, kích thước response và tài nguyên truy vấn.
- HRM hiện không tự retry. Collector không cần thiết kế cơ chế đồng bộ hoặc retry job cho use case này.

## 6. Audit và logging phía Collector

Ghi audit cho mỗi lần đọc, gồm metadata đủ truy vết:

- integration client ID, không phải API key;
- device ID;
- thời điểm và correlation/request ID;
- filter thời gian, page size;
- HTTP outcome, số dòng trả về và thời gian xử lý;
- nguyên nhân từ chối theo mã nội bộ đã chuẩn hóa.

Không ghi:

- API key, token hoặc secret;
- page token đầy đủ;
- raw log payload;
- thông tin xác thực thiết bị;
- query/body nhạy cảm hoặc stack trace trả cho caller.

Audit quản trị cần ghi ai tạo, sửa quyền thiết bị, xoay vòng hoặc thu hồi integration client. Quy định retention và quyền xem audit theo chính sách vận hành Collector.

## 7. Thứ tự triển khai đề xuất

1. **Thiết kế:** chốt schema, status code, timezone, pagination, auth method và giới hạn với HRM.
2. **Quản lý integration client:** lưu client, credential verifier, scope, thiết bị được cấp; thêm create/rotate/revoke và audit quản trị.
3. **Middleware xác thực/phân quyền:** xác thực client, kiểm tra read-only scope và quyền trên thiết bị trước khi gọi tầng dữ liệu.
4. **Read query:** xây query có filter, timezone, index và thứ tự ổn định; đảm bảo không có đường ghi dữ liệu qua endpoint này.
5. **Pagination:** triển khai token có bảo vệ, ràng buộc filter/client/device và expiry; kiểm thử thay đổi token.
6. **API và error contract:** serialize đúng response; ánh xạ lỗi; giới hạn response; không lộ nội bộ.
7. **Audit/metrics:** thêm audit metadata, correlation và cảnh báo rate/error mà không thu raw payload.
8. **Staging:** kết nối với HRM staging bằng key riêng, HTTPS thật và allowlist; chạy đủ test nghiệm thu.
9. **Production:** cấp credential riêng, kiểm tra firewall/certificate, bật theo từng client/device, theo dõi rồi mới mở rộng.

## 8. Kế hoạch kiểm thử và tiêu chí nghiệm thu

### Functional

- Lấy đúng thiết bị, khoảng thời gian và tối đa số dòng yêu cầu.
- Kiểm tra ranh giới `[fromUtc, toUtc)` và timezone thiết bị.
- Phân trang hết dữ liệu; token dùng lại với filter khác bị từ chối.
- Mã nhân viên dạng text giữ số 0 đầu; trường nullable và required đúng schema.
- Dữ liệu nhiều log cùng timestamp không bị mất hoặc lặp giữa các trang trong cùng snapshot.

### Security

- Key sai/thiếu/hết hạn/đã thu hồi bị 401; quyền thiếu hoặc thiết bị ngoài scope bị 403.
- Không truy cập được endpoint quản trị bằng client HRM.
- Thử path/device ID không hợp lệ, token giả/sửa/đổi client, filter quá rộng và page size quá lớn.
- TLS sai hoặc HTTP không mã hóa không được chấp nhận ở ingress production.
- Rate limit trả 429 và `Retry-After`; giới hạn response không thể bị vượt qua bằng nén.
- Kiểm tra log/audit không chứa key, token đầy đủ hoặc raw payload.
- Không có redirect ra host khác và không trả stack trace/database detail.

### Điều kiện Go/No-Go

Chỉ nghiệm thu khi:

1. HRM xác thực được bằng credential riêng qua HTTPS.
2. Credential chỉ đọc đúng thiết bị đã cấp và không có quyền quản trị.
3. Timezone, filter, thứ tự và pagination đã được kiểm chứng với dữ liệu thiết bị thật.
4. Thu hồi/xoay vòng credential đã diễn tập.
5. Kiểm thử âm tính, rate limit, audit và log đạt yêu cầu.
6. Firewall, reverse proxy, TLS, giám sát và quy trình xử lý sự cố đã sẵn sàng.

## 9. Ngoài phạm vi Collector API này

- Collector push dữ liệu sang HRM.
- Scheduler, sync job hoặc HRM checkpoint.
- Ghi raw log vào database HRM.
- Chống trùng, mapping nhân viên, tính công hoặc xử lý hậu kỳ.
- Thay đổi permission/role của HRM hoặc tự cấp quyền cho người dùng.

Contract phía HRM được đối chiếu tại `C:/workplace/aubot_hrm/docs/collector-api.md`, `CollectorAttendanceContracts.cs`, `CollectorOptions.cs`, `CollectorFetchLogsRequestValidator.cs` và `CollectorClientService.cs` trong workspace ngày 06/11/2026. Tài liệu HRM nằm ở repository riêng, chưa có bản sao `collector-api.md` trong repository Collector. Nếu Collector cần thay đổi path, auth header, schema hoặc giới hạn, hai đội phải cập nhật contract và kiểm thử tương thích trước khi triển khai production.

## 10. Đánh giá khoảng cách với mã nguồn Collector hiện tại

Các đường dẫn bên dưới tính từ root repository `attendanceCollector`. Kết quả dựa trên đọc mã nguồn, chưa phải kiểm thử hệ thống chạy thực tế.

Bảng này ghi hiện trạng **trước đợt triển khai**; xem mục 16 để biết kết quả mới, không dùng bảng này làm danh sách tính năng còn thiếu hiện tại.

| Thành phần hiện tại | Chênh lệch cần xử lý |
|---|---|
| `src/attendanceCollector.API/Controllers/AttendanceController.cs` | Chỉ có route admin `/api/v1/attendance/logs` theo policy `BootstrapAdmin`; cần controller và policy HRM riêng. |
| `AttendanceLogQuery`, `AttendanceLogDto`, `PageResult` | DTO admin có metadata nội bộ, filter tùy chọn, `page`/`totalCount`; không dùng trực tiếp làm response HRM. |
| `AttendanceCollectionService.ValidatePage` | Chỉ cho page size đến 100 và cho phép hai mốc bằng nhau; HRM cần 1–500, `from < to`, tối đa 31 ngày. |
| `AttendanceStore.GetLogsAsync` | Đang dùng `<= toUtc`, `Skip/Take`, `CountAsync`; cần query HRM riêng với mốc cuối loại trừ và snapshot/cursor. |
| `Domain/Entities/AttendanceLog.cs` | Có UTC/status/punch/UID nhưng chưa lưu giờ local gốc và timezone lịch sử. |
| `Domain/Entities/AttendanceDevice.cs` | Có `Id` GUID ổn định, `Code` có thể sửa, `TimeZoneId`, `IsActive`; cần chốt ID công khai và ý nghĩa active khi đọc lịch sử. |
| `AttendanceConfigurations.cs` | Có index `(DeviceId, OccurredAtUtc)`; cần đánh giá index thêm khóa thứ tự và index/bảng snapshot trên PostgreSQL. |
| ZK driver, collection và mock ingestion | Có trim chuỗi, suy ra user từ danh sách user, giới hạn chuỗi DB 100/200 khác contract 64/128; cần kiểm tra khả năng giữ raw trước khi bật thiết bị. |
| `MockDeviceApiKeyAuthenticationHandler.cs` | Một key ingestion từ cấu hình, không có client/scope/device grant; không tái sử dụng làm credential HRM. |
| `RateLimiterExtensions.cs`, `Program.cs` | Fixed window chung 100/phút; limiter đang chạy trước authentication, chưa phân vùng theo client và chưa thêm `Retry-After`. |
| `GlobalExceptionMiddleware.cs` | Có ProblemDetails cơ bản, chưa có mã lỗi/trace ID thống nhất cho integration; authentication và model validation cần cùng định dạng. |
| `AttendanceCollectorDbContext.cs` | Chỉ có devices/logs/collection runs; chưa có integration client/key/grant, read audit hoặc snapshot. |
| `Program.cs`, Compose staging/production | Có HTTPS redirect nhưng chưa cấu hình trusted forwarded headers hay ingress trong các file này; thiếu cấu hình token key ring, quota và audit của integration. |
| Ba project trong `tests/` | Chỉ có scaffold `.csproj`, chưa có file test; cần thêm project references, host và PostgreSQL test fixture thực sự. |

Không tăng giới hạn hoặc thay schema route admin chỉ để đáp ứng HRM. Thay đổi dùng chung như middleware, persistence và ingestion phải có kiểm tra hồi quy riêng.

## 11. Các quyết định contract bổ sung

### 11.1. ID thiết bị và ánh xạ sáu trường raw

Đề xuất dùng `AttendanceDevice.Id.ToString("D")` làm `deviceId` công khai của v1: ổn định, hợp lệ theo HRM (`1–128` ký tự ASCII chữ/số/`_`/`-`) và không phụ thuộc mã máy có thể sửa. Cấp GUID này cho HRM cấu hình binding `HRM sourceId → Collector deviceId`; hai GUID thuộc hai hệ thống, không được suy ra là bằng nhau. Ví dụ `clock-01` trong contract chỉ minh họa kiểu chuỗi. Nếu cần giữ alias như `clock-01`, phải thêm binding alias bất biến, unique; không tự fallback qua cả `Code` lẫn `Id`.

| Trường response | Nguồn dự kiến | Quy tắc |
|---|---|---|
| `deviceUserId` | `AttendanceLog.EmployeeCode` | Chỉ dùng khi xác minh đây là mã user thiết bị; không ánh xạ thành mã nhân viên HRM. |
| `deviceUid` | `AttendanceLog.SourceRecordUid` | Với ZK driver là UID user (record 16 byte giữ nguyên uint32); model lưu `long?`. Không coi đây là ID sự kiện. |
| `deviceName` | `AttendanceLog.EmployeeName` | Tên user lấy từ thiết bị lúc thu nhận, không lấy `AttendanceDevice.Name`; nullable khi không có. |
| `occurredAtLocal` | Local gốc được lưu bổ sung | Với dữ liệu cũ chỉ chuyển từ UTC khi timezone lịch sử được xác minh; `DateTimeKind.Unspecified`. |
| `rawStatus` | `AttendanceLog.Status` | Giữ số nguyên gốc, không diễn giải check-in/check-out. |
| `rawPunch` | `AttendanceLog.Punch` | Giữ số nguyên gốc, không chuẩn hóa theo enum nghiệp vụ. |

Lưu ý chất lượng dữ liệu:

- ZK định dạng 16 byte đang đọc user ID dạng số; số 0 đầu không tồn tại trong giá trị số và không thể phục hồi bằng cách tự padding. Định dạng 8 byte suy ra user ID qua danh sách user. Cần fixture 8/16/40 byte để xác định driver nào đáp ứng yêu cầu; user ID không xác định phải được biểu diễn nullable theo chính sách đã chốt.
- Không cắt ngắn mã/tên, loại control character, đổi UID thiếu thành `0`, hoặc bỏ qua bản ghi lỗi để tạo response hợp lệ. Đề xuất với trang có dữ liệu không tương thích trả `409 DATA_NOT_EXPORTABLE`, audit mã nguyên nhân và xử lý chất lượng dữ liệu tại Collector trước khi mở lại.
- Đề xuất chuẩn hóa giá trị rỗng do không có dữ liệu thành `null`, sau khi phân biệt được giá trị nguồn thật. Dữ liệu đã trim hoặc mất mã gốc không được tuyên bố là bản sao nguyên văn thiết bị.
- Collector hiện đã chống trùng bằng fingerprint trong khâu thu nhận. API trả các bản ghi đang lưu; không khôi phục bản ghi đã bị gộp hoặc cam kết trả byte stream nguyên bản của thiết bị.
- Đề xuất `IsActive=false` chỉ dừng thu nhận; quyền đọc lịch sử do client/device grant kiểm soát. Nếu muốn dừng cả đọc, phải có trạng thái chặn export riêng và revoke quyền/token tương ứng.

### 11.2. Error contract đề xuất phía Collector

Lỗi dùng `application/problem+json`, gồm `status`, `title`, `errorCode`, `traceId`; không chứa key, token, payload, tên bảng hoặc exception detail. Thiếu key, token sai và model binding lỗi phải đi qua cùng quy tắc này. Không dùng SPA fallback trả HTML `200` cho route `/api/*` không tồn tại; cần API fallback trả `404`.

| HTTP | `errorCode` đề xuất | Tình huống |
|---|---|---|
| 400 | `INVALID_REQUEST` | ID/thời gian sai, offset khác UTC, khoảng >31 ngày, page size sai, query lặp. |
| 400 | `INVALID_PAGE_TOKEN` | Token rỗng, sửa đổi, sai client/device/filter hoặc không đúng phiên bản. |
| 400 | `PAGE_TOKEN_EXPIRED` | Token hoặc snapshot hết hạn; caller bắt đầu lượt đọc mới. |
| 401 | `INVALID_CREDENTIAL` | Key thiếu/sai/hết hạn/revoke, client bị disable; cùng thông báo bên ngoài. |
| 403 | `ACCESS_DENIED` | Client hợp lệ nhưng thiếu scope hoặc device grant, không phân biệt thiết bị có tồn tại hay không. |
| 404 | `DEVICE_NOT_FOUND` | Chỉ sau khi đã xác nhận grant tương ứng; binding còn grant nhưng tài nguyên không còn khả dụng. |
| 409 | `DATA_NOT_EXPORTABLE` | Local time không xác minh được, dữ liệu không đáp ứng schema hoặc snapshot bị vô hiệu hóa do sửa dữ liệu. |
| 429 | `RATE_LIMITED` | Vượt quota request theo client, có `Retry-After` theo giây. |
| 503 | `READ_CAPACITY_EXCEEDED` | Hết slot truy vấn/snapshot; có `Retry-After` khi xác định được thời gian. |
| 503 | `READ_UNAVAILABLE` | Database timeout/tạm mất kết nối hoặc audit bắt buộc không ghi được. |
| 500 | `INTERNAL_ERROR` | Lỗi bất ngờ, trả trace ID để vận hành tra cứu. |

**Giới hạn tương thích hiện tại:** `CollectorClientService.EnsureSuccess` phía HRM chỉ phân biệt riêng `401`, `403`, `429` và redirect; `400/404/409/503/500` đều trở thành lỗi upstream chung `502` của HRM. HRM chưa đọc mã lỗi Collector và chưa tự bắt đầu lại khi token hết hạn. Muốn UX xử lý riêng các trường hợp này phải có đầu việc cập nhật HRM; không ghi nhận là đã hỗ trợ. HRM cũng không forward nguyên `Retry-After` từ Collector.

## 12. Thiết kế triển khai theo kiến trúc hiện có

### 12.1. Phân chia trách nhiệm và dữ liệu

| Tầng | Thành phần dự kiến |
|---|---|
| Domain | `IntegrationClient`, `IntegrationApiKey`, `IntegrationDeviceGrant`; trạng thái active/revoked/expiry, scope; bổ sung local/timezone lịch sử cho attendance log. |
| Application | `ICollectorAttendanceReadService`, request/result DTO riêng; validator; interfaces cho read store, credential verifier, page token, snapshot và audit. Không phụ thuộc ASP.NET/EF. |
| Infrastructure | EF configurations/migration, verifier/hash, token protection, truy vấn PostgreSQL/snapshot store, audit store; đăng ký qua `AddInfrastructure`. |
| API | `DeviceAttendanceLogsController`, `IntegrationApiKeyAuthenticationHandler`, policy `HrmAttendanceRead`, error mapping, rate limit theo client, endpoint quản trị. |
| Tests | Unit cho quy tắc, integration HTTP + PostgreSQL thật cho schema/query/snapshot, architecture tests kiểm tra phụ thuộc giữa tầng. |

Schema tối thiểu đề xuất:

- `IntegrationClients`: ID, tên, enabled, scope đọc `attendance.logs.read`, thời điểm tạo/sửa, phiên bản quyền phục vụ vô hiệu hóa cache nếu có.
- `IntegrationApiKeys`: key ID unique, client FK, verifier, created/expires/revoked timestamps, actor. Mỗi client có thể có nhiều key còn hiệu lực trong rotation.
- `IntegrationDeviceGrants`: `(ClientId, DeviceId)` unique, FK rõ ràng; chỉ admin có thể cấp/thu hồi. Disable client không xóa lịch sử audit.
- `IntegrationReadAudits`: request ID, phase/outcome, client/device, from/to/page size, count/duration, mã nguyên nhân; không chứa raw hoặc page token. Thời điểm ban đầu có thể chưa biết client khi authentication thất bại.
- Nếu chọn snapshot vật hóa: `AttendanceReadSnapshots` và rows theo ordinal, có client/device/filter hash, expiry, trạng thái; index expiry để dọn và `(SnapshotId, Ordinal)` để phân trang.

Credential đề xuất: key ID công khai + secret tối thiểu 32 byte ngẫu nhiên mật mã, encode dạng ASCII in được, tổng key nằm trong giới hạn HRM 32–4096 ký tự. Với secret entropy cao có thể dùng verifier SHA-256; so sánh fixed-time, giới hạn độ dài đầu vào trước xử lý. Không áp dụng cách lưu plaintext key mock vào client HRM. Bỏ qua credential từ query string; header `X-API-Key` lặp hoặc chứa nhiều giá trị phải bị từ chối.

### 12.2. Luồng request và thay đổi middleware

1. Trusted forwarded headers từ proxy đã cấu hình → chặn HTTP cho route integration tại ingress; không chuyển hướng request mang credential.
2. Correlation ID, IP limiter tại edge trước authentication, chống cache và xử lý lỗi.
3. Authentication chỉ bằng scheme integration cho route HRM; JWT/cookie admin và key mock không đủ để gọi route này.
4. Rate limiter phân vùng theo client sau authentication; client mới không thể tự tạo partition vô hạn qua header do caller cung cấp. Không xếp hàng lâu hơn deadline HRM.
5. Kiểm scope/device grant trước truy vấn attendance; validate filter và token; ghi audit yêu cầu.
6. Đọc một trang/snapshot trong giới hạn tài nguyên, kiểm tra lại quyền hiện hành trước khi trả raw nếu quyền có thể bị đổi trong lúc chạy; hoàn tất audit trước response.
7. Serialize allowlist DTO, kiểm giới hạn byte UTF-8, trả JSON + `Cache-Control: no-store`.

`Program.cs` hiện gọi `UseRateLimiter` trước `UseAuthentication`: cần điều chỉnh thứ tự cho limiter dùng client đã xác thực. IP limiter ở ingress vẫn giữ riêng. Handler phải có client ID claim và policy riêng, không thêm vai trò `BootstrapAdmin` cho integration.

Đề xuất MVP đọc trạng thái key/client/grant từ DB mỗi request, không cache, để revoke có hiệu lực với request mới sau commit. Request đang chạy phải kiểm quyền lại ở bước 6; chốt giới hạn hiệu lực với request đã bắt đầu gửi response. Nếu dùng cache về sau, cần invalidation đa instance và SLA revoke đo được. Audit read ghi thất bại trước khi trả dữ liệu thì trả `503`; audit lỗi/hủy dùng deadline riêng có giới hạn, không giữ transaction DB trong suốt request mạng.

### 12.3. Snapshot và token: quyết định P0 trước khi code

Đề xuất MVP chọn **snapshot vật hóa trong PostgreSQL** để đạt tiêu chí trang nhất quán mà không giữ transaction giữa các HTTP request:

1. Request đầu lấy một MVCC snapshot trong transaction ngắn, lọc đúng device/UTC, sắp xếp tăng dần `(OccurredAtUtc, Id)` và ghi projection sáu trường cùng ordinal vào snapshot riêng. Không load toàn bộ tập dữ liệu vào RAM API; dùng truy vấn SQL có giới hạn.
2. Kiểm dữ liệu hợp lệ và giới hạn số row/byte snapshot trong transaction; chỉ publish snapshot sau khi hoàn tất. Dùng giới hạn `M+1` để phát hiện vượt mức và rollback, không publish tập bị cắt làm snapshot đầy đủ. Các giá trị giới hạn phải được chốt bằng tải thực tế.
3. Các trang đọc theo `(SnapshotId, Ordinal)`, lấy `pageSize+1` để xác định còn trang; trả tối đa `pageSize`. Token bảo vệ chứa snapshot ID, last ordinal, client/device, hash filter chuẩn hóa, version và expiry.
4. Token đề xuất sống 15 phút, expiry cố định theo snapshot; không gia hạn vô hạn theo từng trang. Request đồng thời dùng cùng token nhận cùng một trang trong TTL nếu quyền còn hiệu lực; không tiêu thụ token một lần. Thu hồi key/grant vẫn chặn đọc snapshot.
5. Log mới hoặc transaction thu nhận commit sau thời điểm tạo MVCC snapshot chỉ xuất hiện trong lượt đọc mới, kể cả event có `OccurredAtUtc` nằm trước cursor. Không mất/lặp trong tập snapshot đã chọn.
6. Snapshot chứa raw đã sao chép nên có cùng kiểm soát dữ liệu với bảng gốc; áp retention theo TTL và dọn định kỳ có quota. Nếu có yêu cầu xóa/cấm export dữ liệu, vô hiệu hóa snapshot liên quan ngay. Công việc dọn snapshot là housekeeping của Collector, không phải scheduler đồng bộ HRM.

Đánh đổi: snapshot tăng I/O và dung lượng, cần cap số snapshot còn sống theo client/toàn hệ thống, cap row/byte và cơ chế cleanup sau crash. Kiểm tải trang đầu trước khi chọn giá trị production. Nếu chi phí không đạt, hai đội có thể chốt contract yếu hơn dùng keyset + watermark và nêu rõ late insert có thể xuất hiện/bị bỏ qua; không được gọi cơ chế đó là snapshot nhất quán. Không dùng transaction mở trên connection giữa các lần gọi hoặc coi GUID ngẫu nhiên là thứ tự commit.

Nếu dùng ASP.NET Core Data Protection cho token, cấu hình purpose riêng, key ring bền vững và dùng chung cho mọi replica; bảo vệ key ring, giữ key giải mã ít nhất qua TTL token. Restart/scale-out/đổi pod không được làm token hợp lệ mất hiệu lực ngoài chính sách đã công bố. Chuẩn hóa from/to theo UTC ticks và page size số nguyên để `Z` và `+00:00` tương đương khi hash filter.

### 12.4. Quản trị integration client

Đề xuất API quản trị dưới `/api/v1/integration-clients`, chỉ dành cho admin Collector. Browser writes tiếp tục dùng antiforgery như route admin hiện có; không gọi các route này bằng key HRM.

| Thao tác đề xuất | Kết quả |
|---|---|
| `POST /` | Tạo client với scope đọc và grants thiết bị cụ thể; không wildcard mặc định. |
| `GET /`, `GET /{clientId}` | Danh sách, trạng thái, thiết bị và metadata key; không trả secret/verifier. |
| `PUT /{clientId}/device-grants` | Thay tập thiết bị được phép, có kiểm soát concurrent update và audit trước/sau bằng ID. |
| `POST /{clientId}/keys` | Tạo key đầu tiên hoặc key mới khi rotation; response secret đúng một lần, `no-store`, không ghi response body vào log. |
| `DELETE /{clientId}/keys/{keyId}` | Revoke key idempotent; không xóa dấu vết cấp key. |
| `PATCH /{clientId}/active` | Disable/enable client; disable chặn toàn bộ key. |

MVP có thể dùng API quản trị và runbook, chưa cần xây thêm UI. Key được chuyển vào secret store HRM qua kênh vận hành được bảo vệ; không xuất hiện trên WebApp HRM. Chỉ nạp key vào cấu hình HRM sau khi đã cấp đủ device grant và kiểm tra endpoint staging.

## 13. Giới hạn vận hành và kết nối thật với HRM

Các giá trị sau là baseline đề xuất, cần xác nhận ở staging trước production:

| Cấu hình | Baseline/quy tắc |
|---|---|
| Integration enabled | Tắt mặc định; khi bật phải validate đầy đủ cấu hình ở startup. |
| Khoảng đọc / page size | Cố định v1: tối đa 31 × 24 giờ, page size 1–500, mặc định 500. |
| Page token | Tối đa 2048 ký tự sau decode; TTL snapshot/token đề xuất 15 phút. Giới hạn URL ở proxy phải đủ cho token đã percent-encode. |
| Response | Đề xuất cap 1 MiB JSON UTF-8 trước nén, gồm envelope/token; HRM mặc định chấp nhận tối đa 5 MiB sau giải nén. Không cắt payload âm thầm để vừa cap. |
| Deadline | Đề xuất xử lý toàn request ≤20 giây, DB command timeout ≤10 giây, để nằm trong deadline HRM mặc định 30 giây. Truyền cancellation xuống EF/SQL; không ghi thành lỗi 500 khi caller hủy. |
| Quota | Đề xuất 60 request/phút/client, tối đa 2 request đang chạy/client; thêm cap toàn hệ thống, số snapshot, row/byte và chi phí trang đầu theo capacity staging. |
| Scale-out | Limiter/capacity dùng chung tại gateway hoặc distributed store nếu cam kết quota toàn cluster; in-memory limiter chỉ giới hạn từng process. |
| Audit / snapshot retention | Snapshot theo TTL; retention audit do vận hành chốt, có quyền xem và job dọn, không giữ raw trong audit. |

Các điểm triển khai cần đưa vào checklist:

- HRM hiện **chỉ cho phép public IP được allowlist**, chặn localhost/private/link-local; cấu hình `http://localhost:4200`, IP LAN, `host.docker.internal` hoặc tên service Docker không dùng được cho kết nối HRM hiện tại. Cần origin HTTPS có certificate hợp lệ, DNS answers đều thuộc allowlist public IP. Nếu muốn kết nối private qua VPN, phải có thay đổi policy HRM riêng trước khi nghiệm thu; không tắt TLS hoặc mở rộng allowlist tùy ý để vượt kiểm tra.
- Allowlist egress HRM → IP/port Collector khác với allowlist ingress Collector → outbound IP HRM. Ghi rõ cả hai chiều, chủ sở hữu và quy trình đổi IP/DNS.
- Proxy chỉ tin `Forwarded/X-Forwarded-*` từ địa chỉ proxy đã khai báo, giữ nguyên path/header và không log key hoặc query có page token. Giới hạn header/URI đủ cho key/token hợp lệ; không dùng request body logging cho route raw.
- Không để access log, APM, exception telemetry, response tracing thu raw payload hoặc full URL chứa token. Cần kiểm tra cả ingress và collector, không chỉ application logger.
- Bổ sung readiness riêng với liveness `/health/live`: DB/schema và dịch vụ cần cho integration phải sẵn sàng trước khi nhận traffic, không công khai chi tiết lỗi nội bộ.
- Theo dõi request count, latency, 401/403/429/5xx, query timeout, audit failure, snapshot count/size và cleanup lag; không dùng device ID/user ID/token làm label metric có số lượng giá trị không giới hạn.
- Migration hiện tự chạy trong startup. Với nhiều replica, cần bước migration một lần trước rollout hoặc cơ chế phối hợp được kiểm chứng; tránh cùng migrate/backfill khi tất cả replica khởi động.

## 14. Backlog triển khai có đầu ra kiểm chứng

| Ưu tiên | Công việc | Đầu ra / điều kiện hoàn thành |
|---|---|---|
| P0 | Chốt public device ID, tên user, nullable, dữ liệu lỗi, timezone cũ, DST và snapshot | Quyết định được ghi nhận; fixture thực tế từ các driver dự kiến bật; không còn trường raw phải đoán. |
| P0 | Chốt hạ tầng staging HRM ↔ Collector | HTTPS/public DNS/IP allowlist đáp ứng client HRM, owner secrets/audit và capacity đã xác định. |
| P1 | Migration clients/keys/grants/audit và local/timezone lịch sử | Migration additive, giữ nguyên IDs/fingerprint/UTC hiện có; integration test upgrade từ schema cũ có dữ liệu. |
| P1 | Quản trị key/client và authentication/authorization | Tạo/rotate/revoke được, one-time secret, deny-by-default, không dùng key HRM vào admin/ingestion. |
| P1 | Read service, query riêng, snapshot/token | Đúng `[from,to)`, 500 rows, thứ tự ổn định, không giữ transaction qua request, đạt giới hạn tài nguyên. |
| P1 | HTTP/error/limiter/audit | DTO allowlist, lỗi nhất quán, 429 + Retry-After, no-store, audit đầy đủ và không rò raw/secret/token. |
| P1 | Bộ test và kiểm tra hồi quy | Unit + integration PostgreSQL/HTTP pass; admin collection, source CRUD, mock ingestion và login vẫn hoạt động. |
| P2 | Tài liệu OpenAPI, runbook và staging E2E | Có request/response mẫu, cách cấp binding/key, rotation, xử lý hết hạn token và rollback; dùng HRM client thật. |
| P2 | Rollout từng thiết bị/client | Bật sau Go/No-Go, theo dõi metrics và capacity; tắt feature/revoke để rollback. |

Không ước lượng ngày công trước khi chốt P0 vì bảo toàn raw lịch sử và mức nhất quán snapshot quyết định khối lượng migration/query. Thiết kế mới cần thêm project references/test fixture; kết quả `dotnet test` của scaffold chưa đủ chứng minh có test được thực thi.

## 15. Bổ sung ca nghiệm thu, migration và bàn giao

Ngoài mục 8, cần các ca bắt buộc sau:

- **Parsing:** UTC `Z` và `%2B00:00`, giây thập phân từ HRM; từ chối offset khác UTC, không offset, from=to, 31 ngày + 1 tick, page size 0/501, số nguyên sai kiểu, token rỗng/quá dài/control và query lặp. Kiểm tra `pageSize=1` và `500` với dữ liệu thật.
- **Boundary:** event đúng from được lấy, đúng to bị loại; hai cửa sổ liền nhau không lặp; nhiều event cùng timestamp vẫn đi hết trang. Không dùng UID user làm khóa cursor.
- **Timezone/raw:** máy Bangkok, dữ liệu cũ đã/chưa xác minh timezone, đổi timezone thiết bị sau thu nhận, DST ambiguous/invalid, mã `000123`, Unicode và escaping JSON, null đúng nghĩa, chuỗi 64/65 và 128/129 ký tự, tên user khác tên máy, status/punch giữ nguyên.
- **Snapshot/concurrency:** insert/commit muộn, timestamp cũ, rollback transaction, sửa/xóa log hoặc yêu cầu xóa dữ liệu, request cùng token đồng thời, giới hạn snapshot vượt M, cleanup/restart trong lúc đọc; không publish snapshot bị cắt, không mất/lặp trong snapshot hợp lệ.
- **Token/auth:** thay byte token, sai version, đổi client/device/filter/page size, hết TTL, rotation key cùng client, revoke key/grant trong khi query chạy; restart và chuyển replica vẫn giải mã được token còn hạn. Key mock/JWT/cookie admin không thay được scheme integration.
- **Vận hành:** cap bytes trước nén, cancellation/DB timeout, audit unavailable, số snapshot lớn, quota theo client và toàn cluster, spoof forwarded headers, API path lạ trả JSON 404, HTTP không redirect và không phục vụ raw, no-store trên mọi outcome.
- **E2E HRM:** HRM gửi một request/trang với filter chính xác, dùng next token đến null; không tự retry; các lỗi Collector được HRM hiển thị theo khả năng hiện tại ở mục 11.2. Test negative không giả định HRM đã hiểu mã lỗi mới.
- **Hồi quy:** API admin vẫn giữ contract/giới hạn cũ; source/collection/mock ingestion/login/logout không bị policy mới hoặc thứ tự middleware làm hỏng. Dữ liệu old schema vẫn đọc được theo chính sách lịch sử đã chốt.

Migration/rollback:

1. Thống kê bằng truy vấn chỉ đọc số log thiếu local lịch sử, chuỗi vượt giới hạn, timezone không hợp lệ; ghi số lượng tổng hợp, không xuất raw vào log vận hành.
2. Backup và thử restore trên staging; migration additive, columns mới nullable cho dữ liệu cũ, index và backfill có kế hoạch khóa/thời gian riêng.
3. Thu nhận mới lưu đủ thông tin trước khi bật read API. Chỉ backfill những nguồn có bằng chứng timezone/giá trị gốc; cách xử lý phần còn lại phải được HRM xác nhận, không tự điền giả.
4. Rollback bằng tắt integration/revoke quyền và quay lại phiên bản tương thích schema additive; giữ audit và key ring cần thiết. Không drop dữ liệu/sửa fingerprint hàng loạt để rollback.

Bộ bàn giao cuối cùng gồm: OpenAPI v1, danh sách binding ID đã cấp (không có secret), cấu hình mẫu bằng placeholder, hướng dẫn create/rotate/revoke key, chính sách snapshot/expiry, evidence test với HRM thật, EXPLAIN và tải trang đầu/trang tiếp theo, dashboard/cảnh báo, owner xử lý sự cố và runbook rollback. Credential production được bàn giao riêng qua secret store.

## 16. Kết quả triển khai source code ngày 06/11/2026

Đã bổ sung API `GET /api/v1/devices/{deviceId}/attendance-logs`, client/key/device grants, API quản trị, SHA-256 verifier cho key ngẫu nhiên 256-bit, snapshot PostgreSQL và token Data Protection riêng có key ring bền vững. Public device ID dùng GUID `D`; `deviceName` lấy tên user trên máy. Đã thêm audit trước/sau đọc theo cơ chế fail-closed, quota/concurrency theo client, deadline, response cap, metrics nội bộ, readiness và lỗi ProblemDetails. API quản trị attendance cũ giữ DTO/filter/giới hạn cũ.

Thu nhận ZK/mock lưu thêm local/timezone/offset lịch sử, từ chối DST mơ hồ/không tồn tại. Migration chỉ thêm cột nullable, bảng mới và index, không tự backfill. Dữ liệu cũ thiếu local trả 409 thay vì tự đoán. Test DST cũng phát hiện và sửa lỗi cũ `AttendanceCollectionRun.Fail()` gán nhầm `Running`; MVC được bổ sung đăng ký filter antiforgery để route quản trị ghi hoạt động đúng.

Đã build Release không warning/error; 52 test pass, gồm 31 unit, 20 HTTP/PostgreSQL thật và một architecture test. Bộ test bao gồm nâng cấp từ schema cũ có dữ liệu, snapshot/token/rotation, byte/row limits, quota theo client, timeout, audit failure chặn raw, key ring mã hóa dùng chung hai instance và UTC/DST/Unicode. EF báo không còn pending model changes. Ba cấu hình Compose local/staging/production validate bằng placeholder. PostgreSQL kiểm thử tách riêng, không dùng database Collector đang chạy.

Tài liệu bàn giao: [runbook cấu hình và rotation](collector-integration-runbook.md), [OpenAPI v1](collector-api.openapi.yaml), [SQL migration idempotent do EF sinh](collector-integration-migrations.sql). Packages bổ sung phục vụ migration/test và đồng bộ runtime: EF Design/Relational 10.0.12, MVC.Testing 10.0.11, Moq 4.20.72; không thêm framework mapping hoặc thay contract success của HRM.

Chưa thực hiện các đầu việc cần môi trường thật: rollout, migrate database đang chạy, cấp secret thật, TLS/public DNS/IP allowlist HRM, xác minh từng định dạng driver/thiết bị, hiệu năng production/EXPLAIN, dashboard/OTLP/cảnh báo, retention/quyền xem audit và diễn tập vận hành. [Runbook](collector-integration-runbook.md) mô tả cấu hình và giới hạn để tiếp tục nghiệm thu; integration vẫn tắt mặc định.

## 17. Điều chỉnh lưu attendance nội bộ ngày 09/11/2026

Theo yêu cầu, Collector không lưu mã nhân viên trong `AttendanceLogs`. Migration `20261109000100_PurgePersistedEmployeeCodes` xóa giá trị cũ; lượt thu nhận mới ghi mã rỗng để giữ tương thích schema hiện tại. Fingerprint mới dựa trên device ID, UID thiết bị, UTC timestamp, status và punch; mock ingestion dùng event ID ổn định nếu không có UID. Khi không có event ID/UID thì fallback dùng tuple còn lại, nên các log hoàn toàn trùng tuple không thể phân biệt.

ZK driver được chỉnh theo luồng đọc HRM: chỉ đọc attendance logs, không tải danh sách user; record 8 byte trả user code rỗng, 16 byte giữ số đầu record dạng text và UID 32-bit nguyên gốc, 40 byte trở lên giữ user field trong bộ nhớ. Mã user không được ghi DB. `AttendanceDevices:Zkteco:MaximumRecordsPerRead` mặc định 50.000 và clamp 1–200.000; timeout clamp 1–60 giây. Endpoint thiết bị phải là IPv4 private literal; không còn allowlist IP/port cụ thể. Payload giới hạn 64 MiB, kích thước record phải chia hết cho count và số record giải mã phải khớp count. UID lưu bigint để giữ đủ uint32 của record 16 byte. Hỗ trợ record attendance 40 byte trở lên.

Collector giữ metadata giờ local/timezone/UTC cho persistence nội bộ; HRM raw reader chỉ trả timestamp local. Giới hạn snapshot API 100.000 rows/64 MiB vẫn là giới hạn riêng của endpoint đọc và không thay đổi bởi giới hạn thu nhận 200.000.

Đây là thay đổi persistence/collection nội bộ. API/DTO hiện hành chưa được thiết kế lại; trường mã user trong kết quả đọc sẽ rỗng theo quy định không lưu mã nhân viên. Truy vấn lọc theo mã nhân viên không còn hữu ích. Contract outbound HRM vẫn cần xử lý riêng.
