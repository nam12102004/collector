# API And Reliability Gates

## Contracts And Security

- Preserve route, method, serialized field names, nullability, status codes, and response shape unless a contract change is in scope.
- Keep controller actions to binding transport input, calling one use-case/query service, and returning an HTTP response. Apply permission declaratively through an attribute or equivalent endpoint metadata.
- Do not read claims, map models, evaluate permissions imperatively, execute business rules, or access persistence in controllers. Constructing the transport `SuccessResponse<T>` envelope around returned application data is allowed.
- Authenticate by default when that is the product policy. Mark anonymous endpoints explicitly and apply the repository's abuse controls.
- Enforce resource-level authorization and tenant/user scoping in the business operation or reusable policy, not only in UI visibility.
- Return safe client errors through the established exception/error pipeline. Never expose stack traces or database details.
- Make the business operation express expected failures with typed custom exceptions. Do not catch them in controllers.
- For a new HTTP API, map exceptions centrally with `IExceptionHandler` or the framework equivalent to RFC Problem Details. Include a stable `errorCode` constant and trace identifier as safe extensions. Preserve an established public custom error envelope and ask before migrating it.
- Return confirmed successful operations as HTTP 200 `SuccessResponse<T>` envelopes created at the API boundary. Do not wrap failures in the success envelope; apply the full contract in `application-contracts.md`.
- Validate request shape at the boundary and keep domain invariants inside the operation that changes state.
- Give FluentValidation failures stable field-level error codes and safe parameters so Angular can localize by code; backend messages remain non-localized fallbacks.
- Define constants for values that are reused or belong to a shared vocabulary/business rule. Keep only truly local, internal, non-contractual one-off strings inline.

## Dependencies

- Inventory installed packages and framework capabilities before writing custom validation, mapping, resilience, caching, pagination, or authorization infrastructure.
- Reuse a suitable installed capability. Ask before adding, removing, or replacing any dependency unless `dependency-catalog.md` explicitly marks it mandatory or pre-approved for the exact activation condition. Always ask before replacing an established conflicting package.
- If a useful installed library, wrapper, or structure has never been used in the repository, report it and ask before first adoption regardless of immediate scope. When multiple installed choices fit, prefer the nearest compliant canonical implementation; ask if none wins clearly. Never create a parallel structure silently.

## Exception Mapping

Use a small custom exception hierarchy whose base carries a stable error code and optional safe metadata. Keep HTTP response objects and controller types out of Domain/Application exceptions.

| Exception | HTTP status | Rule |
|---|---:|---|
| `BadRequestException` | 400 | Malformed, missing, or semantically invalid input not already represented by request validation. |
| `FluentValidation.ValidationException` | 400 | Request validation failures; return field errors when available. |
| `ForbiddenException` | 403 | Resource/business authorization that declarative endpoint permission cannot decide. |
| `NotFoundException` | 404 | Requested resource does not exist within the caller's allowed scope. |
| `ConflictException` | 409 | State, uniqueness, or concurrency conflict. |
| `ConcurrencyConflictException` | 409 | An EF/database optimistic-concurrency conflict translated at the nearest persistence boundary. |
| `BusinessRuleException` | 422 | Input is structurally valid but violates a processable business rule. |
| `TooManyRequestsException` | 429 | A service-owned quota or business threshold is exceeded. Include `Retry-After` when known. |
| unexpected `Exception` | 500 | Log with context and return a generic safe detail. |

Prefer ASP.NET Core rate-limiting middleware for transport-level request limits. Use `TooManyRequestsException` only when the service discovers a domain/application quota. Let authentication middleware produce ordinary 401 responses and declarative authorization produce ordinary 403 responses.

The service owns the error meaning by choosing the exception type, error code, and safe metadata. The global handler owns HTTP status, headers, Problem Details serialization, and trace correlation. Controllers MUST NOT contain `try/catch` for this pipeline.

## Persistence And Queries

- Keep query composition at the persistence boundary chosen by the architecture: repository, query service, handler, or application service.
- Do not classify ordinary paging plus filtering as complex by itself. Use the repository-extraction signals in `csharp-conventions.md`.
- Project only required fields for large reads and use no-tracking reads where appropriate.
- Bound pagination inputs and use deterministic ordering.
- Compute pagination, `TotalCount`, and `TotalPages` in the backend/database. Never return a growing full collection for Angular to slice/filter/sort.
- Back uniqueness and race-sensitive invariants with database constraints or concurrency controls where feasible.
- Treat migration compatibility, rollout order, and existing data as part of a schema change.

## Transactions And Concurrency

- Use one atomic transaction for writes that must succeed or fail together.
- Ensure nested helpers do not commit independently inside a larger operation.
- Re-check race-sensitive state inside the transaction and handle concurrency conflicts explicitly.
- Catch `DbUpdateConcurrencyException` only at the nearest persistence boundary, translate it to `ConcurrencyConflictException` with a stable error-code constant, and let the global handler return 409 Problem Details.
- Make message/job processing idempotent when retries or duplicate delivery are possible.

## External Calls And Background Work

- Set bounded timeouts and propagate cancellation.
- Retry only transient, safe operations; avoid retrying non-idempotent writes without an idempotency strategy.
- Do not hold a database transaction open across a slow network call unless the design explicitly requires and mitigates it.
- Log identifiers and outcomes with structured fields while excluding secrets and sensitive payloads.
