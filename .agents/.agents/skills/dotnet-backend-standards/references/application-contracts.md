# Application Contracts And Cross-Cutting Behavior

## Contents

- Success And Error Contracts
- Response Localization
- Validation Errors
- Contact Validation And Duplicate Identity
- Backend Pagination
- AutoMapper
- HTTP Idempotency
- Audit Trail
- Data Retention, Soft Delete, And Hard Delete
- Rate Limiting
- Outbound HTTP Retry Semantics
- Review Checklist

## Success And Error Contracts

Return every successful HTTP operation as status 200 with a `SuccessResponse<T>` envelope created at the HTTP/API boundary. Application services return application DTOs/results and do not depend on this HTTP envelope type:

```csharp
public sealed record SuccessResponse<T>(
    string Code,
    string Message,
    T Data);
```

```json
{
  "code": "PRODUCT_CREATED",
  "message": "Product created successfully",
  "data": { "id": "..." }
}
```

Create, query, update, delete, and action success all use 200. An operation without payload returns `data: null`; it does not use 204. A create response includes the identifier/data the client needs because the contract does not rely on a 201 Location header.

The controller binds input, calls one service, and wraps its returned data in the endpoint's stable success-code constant and safe fallback message:

```csharp
var data = await productService.GetProductByIdAsync(id, cancellationToken);
return Ok(new SuccessResponse<ProductDto>(
    ProductSuccessCodeConstant.ProductRetrieved,
    ProductMessageConstant.Retrieved,
    data));
```

This is transport-envelope construction, not DTO mapping or business logic. Do not put HTTP response types in Domain/Application contracts. A shared MVC `ActionFilter` (or Minimal API endpoint filter) may construct the envelope instead when endpoint metadata explicitly provides the success code/message. Such a filter must preserve errors/non-2xx results and already-enveloped responses, and must never double-wrap. Do not use an unconfigured global filter that guesses endpoint codes or messages.

Use Problem Details for failures with the real status (400, 403, 404, 409, 422, 429, 500). Never return an error inside a status-200 success envelope.

## Response Localization

Backend messages are non-localized safe fallbacks. Angular localizes success and application error display by stable `code`/`errorCode`. Do not rename a published code without treating it as a contract change.

Name containers such as `ProductSuccessCodeConstant` and `ProductErrorCodeConstant`. Do not embed secrets, internal exception details, or sensitive values in fallback messages or interpolation parameters.

## Validation Errors

Use FluentValidation error codes as the localization contract. Return field, stable code, and safe parameters:

```json
{
  "field": "name",
  "code": "PRODUCT_NAME_MAX_LENGTH",
  "params": { "maxLength": 200 }
}
```

Configure `.WithErrorCode(...)`. A backend text message is fallback/debug-safe context, not the primary display contract. Angular maps code plus parameters through i18n. Do not expose raw property paths, values, or provider errors when they reveal internals.

## Contact Validation And Duplicate Identity

Treat client-side or preflight duplicate checks as UX hints only. Revalidate email and phone input at the backend boundary and repeat the authoritative duplicate check inside the Create/Update operation before persistence. An earlier `AnyAsync` or blur-time request is not sufficient race protection.

- Define the accepted representation, default phone region, normalization, comparison, and storage strategy as one contract. Use the same canonical form for duplicate checks across every participating entity. Preserve the submitted representation only when the confirmed contract does not require canonical storage.
- Use `MailAddress.TryCreate` plus parsed-address equality for mailbox-style email input. Use `libphonenumber-csharp` parsing plus `IsValidNumber` for phone validity; a regex may enforce a confirmed presentation rule but is not the authoritative phone validator.
- Exclude the current record by stable identifier during Update duplicate checks.
- Protect uniqueness with a provider-correct database constraint when one table owns the unique namespace. When identity spans heterogeneous tables and one constraint cannot express it, use an architecture-appropriate serialized/locked transaction or a dedicated identity reservation model; document the remaining race behavior instead of presenting an application pre-check as atomic.
- Return a typed 409 conflict with a stable error code. When the UI must identify the affected control or existing record, expose only safe, stable metadata such as `field`, `entityType`, `displayName`, and `ownerName`; never expose the conflicting entity or sensitive contact payload wholesale.
- Keep preflight-check and write-time duplicate logic behind the same normalization and query implementation so they cannot drift.

## Backend Pagination

Paginate every collection that can grow in the backend/database. Angular MUST NOT fetch all rows and slice, filter, or sort server-owned data locally.

```csharp
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

Compute `TotalPages` in the backend after validating `PageSize > 0`:

```csharp
var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
```

- `TotalCount == 0` produces `TotalPages == 0`.
- Apply the same authorization/data scope and filters to `TotalCount` and `Items`.
- Apply `Where`, deterministic `OrderBy`, then `Skip/Take` or keyset conditions before materialization.
- Validate `PageNumber >= 1` and page size against a min/max. A fixed API limit belongs in a `...Constant`; an environment-dependent operational limit belongs in typed options.
- Allow only explicit sort fields; never compose raw SQL/field access from client input.
- A valid page beyond current data returns 200 with an empty `Items` list.
- Use offset pagination for ordinary CRUD/admin tables. Use cursor/keyset pagination for large/high-write datasets, deep paging, or infinite scroll. Always include a unique tie-breaker such as `CreatedAt, Id`.
- Cursor data is opaque/validated. Do not require `TotalCount` for cursor pagination when it is prohibitively expensive.
- Use a separate endpoint for a genuinely small bounded lookup/dropdown.

Wrap `PagedResponse<T>` inside `SuccessResponse<PagedResponse<T>>`.

## AutoMapper

Use AutoMapper as the default mapper for DTO/entity boundaries. Use a patched AutoMapper `16.x` version no lower than `16.1.1`; prefer the latest compatible patched `16.x`. Before adding it, confirm Community/commercial license eligibility and provide the key through `AUTOMAPPER_LICENSE_KEY`/secret configuration. Do not pin vulnerable pre-license v14.

- Define explicit profiles in Application/the owning feature. Do not inject `IMapper` into controllers.
- Keep profiles scoped to one owning service/feature. Do not create a cross-feature god profile; split existing aggregate profiles before extending them.
- Do not implement private `Map`/`MapList` entity-to-DTO helpers or inline entity-to-DTO constructors in services. Use `IMapper`; when a DTO needs query-enriched values, map an explicit `{Feature}...MappingSource` record.
- Use `ProjectTo<TResponse>()` after filter/sort and before materialization for EF reads when the LINQ provider supports the mapping.
- Use `IMapper.Map` for request/entity mapping that does not bypass domain behavior.
- For DDD aggregates, call factories/behavior; never map a transport request directly over protected aggregate state.
- Do not use `ReverseMap()` without verifying both directions and writable members.
- Validate configuration in tests with `AssertConfigurationIsValid()`.
- Keep mapping expressions provider-translatable when using `ProjectTo`; do not rely on DI resolvers/converters that cannot run in SQL projection.

## HTTP Idempotency

Do not add an idempotency table/middleware to every write. First inspect natural idempotency, unique constraints, optimistic concurrency, stable transaction/reference IDs, and existing deduplication.

Add explicit `Idempotency-Key` storage only when client/network retry can repeat a costly or irreversible side effect despite those controls, such as payment, booking, checkout, submit/import, or another important create.

When used, store key, request fingerprint, status/result, and retention atomically with the business write. Same key/same payload returns the previous result; same key/different payload returns 409. Memory/cache-only storage is insufficient when restart could break correctness.

Concurrency protects competing state updates; idempotency protects duplicate delivery/retry after an ambiguous response. Do not confuse them.

## Audit Trail

Use business audit only for confirmed sensitive/important changes such as permission, approval, KPI result, payment, configuration, admin action, or important state transition. Do not audit every CRUD mechanically. Technical logs do not replace audit.

Record actor or service identity, action, entity type/id, UTC timestamp, correlation ID, outcome, and only safe changed fields/before-after values required by policy. Keep audit append-only through normal application flow. Do not record passwords, tokens, secrets, or entire sensitive payloads.

The business-owning service owns audit policy: what to record, when, and in which transaction, unless a centralized Audit Service is explicitly confirmed. Where Application/Infrastructure boundaries exist, keep the recording interface inward and its technical persistence implementation in Infrastructure; using business history entities does not change that placement. Stage audit entries on the same scoped Unit of Work and let the owning use case commit, preserving explicit execution checkpoints. Apply [technical service ownership](service-boundaries.md#technical-services-and-business-ownership). Ask for retention and read authorization. Use `...AuditActionConstant` and other scoped `...Constant` vocabularies.

## Data Retention, Soft Delete, And Hard Delete

Before implementing retention or deletion, confirm a policy per data category rather than one global duration. Record the business owner, retention trigger, duration, archive/anonymize/delete action, legal-hold behavior, read/export authorization, purge cadence, and evidence/observability required. Treat primary data, PII, audit, idempotency records, files/object storage, search indexes, local read models, telemetry, and backups as separate categories.

Do not add soft delete globally. Use hard delete when data may be removed and no restore/history requirement exists. Use soft delete only for a real restore, history, legal retention, or reference-stability requirement. PII erasure requires hard delete or anonymization, not a hidden soft-deleted row.

Ask whether a unique value may be reused after soft delete and which database provider is selected.

When reuse is allowed, prefer a provider-supported filtered/partial unique index over active rows:

```text
UNIQUE (TenantId, NormalizedCode) WHERE IsDeleted = false
```

`IsDeleted` is the index predicate, not normally part of the unique key. `UNIQUE(Code, IsDeleted)` incorrectly allows only one deleted duplicate. If the provider lacks filtered indexes, confirm a generated nullable active key, reservation table, or another provider-correct design. Database constraints—not an `AnyAsync` pre-check—are the final race protection.

Restoring into an active-value collision returns a typed 409 conflict and requires rename/merge/cancel.

Run purge/archive work in bounded batches with cancellation, idempotency, concurrency safety, metrics, and restartability. Use Quartz.NET only when a durable recurring schedule is confirmed; do not keep a request open while purging a large data set. Apply legal hold before selecting purge candidates and authorize any manual purge/restore endpoint explicitly.

Clean object storage, Elasticsearch, caches, and local read models through retryable/idempotent eventual cleanup. The owning service/deployable emits only the minimum deletion/tombstone information consumers need; it never deletes another owner's database directly. Reconcile partial cleanup and define what the user sees while cleanup is pending.

Backup deletion is governed by the backup lifecycle and may not be immediate. Document backup retention/expiry and restore-time re-deletion or anonymization behavior; do not promise immediate physical erasure from immutable backups unless the confirmed platform can guarantee it. Keep audit retention separate from business-row retention and never remove data under a legal hold.

## Rate Limiting

Use Nginx/gateway for coarse public IP/client limits and ASP.NET Core built-in rate limiting for endpoint-specific limits. A business quota discovered by the service throws `TooManyRequestsException` and maps to 429 Problem Details.

Use distributed rate-limit state only when multiple instances require one global quota. Do not add Redis without that evidence. Honor `Retry-After` when known and avoid leaking tenant/account quota details.

## Outbound HTTP Retry Semantics

Retry by operation semantics and duplicate protection, not HTTP method/status mechanically.

| Operation | Automatic retry rule |
|---|---|
| GET/HEAD/OPTIONS | bounded transient retry when the endpoint has no side effect |
| PUT | only when the same request sets the same desired state and server concurrency is correct |
| DELETE | only when repeat delete is success/no-op by contract |
| POST | no default retry; require idempotency/deduplication/natural safety |
| PATCH | no default retry; require proof the patch is idempotent |
| upload part | retry when part number/object key makes repeat safe |
| upload completion | retry only when finalization is idempotent |

Do not assume timeout means the server did not commit. Treat 400/403/404/409/422 as permanent. Refresh auth at most through the confirmed auth flow rather than general 401 retry. Retry 429 only when safe and respect `Retry-After`. Retry connection failure/502/503/504 only for safe/idempotent work. Do not blanket-retry 500.

Use exponential backoff with jitter, a total timeout budget, cancellation, and per-client/operation policy through `Microsoft.Extensions.Http.Resilience`. Do not retry a non-replayable streaming body. Choose one primary retry layer; prevent Nginx, YARP, and HttpClient from multiplying attempts. Emit metrics for attempt, timeout, circuit-open, and final failure without logging sensitive bodies.

## Review Checklist

- Is every success HTTP 200 with an API-boundary-created envelope and every error Problem Details?
- Does Angular localize by stable codes rather than backend text?
- Do validation failures carry field/code/parameters?
- Is growing data paged in the database with backend-computed TotalPages?
- Are AutoMapper version, license, profiles, projection, and tests correct?
- Is idempotency added only where duplicate delivery remains unsafe?
- Is audit scoped, append-only, authorized, and safe?
- Are retention, legal hold, purge, downstream cleanup, and backup behavior explicit per data category?
- Does soft-delete uniqueness match reuse/provider semantics, and is hard delete/anonymization used when required?
- Are rate limiting and retries placed once and driven by operation semantics?
