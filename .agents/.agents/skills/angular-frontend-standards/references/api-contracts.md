# Angular API Contracts

## Success And Errors

Type the backend success envelope exactly:

```typescript
export interface SuccessResponse<T> {
  code: string;
  message: string;
  data: T;
}
```

Every successful operation uses HTTP 200. A no-payload action is `SuccessResponse<null>`. Errors remain non-200 Problem Details and MUST NOT be treated as success envelopes.

Localize display using `code`/`errorCode`. The backend `message`/Problem Details `detail` is a non-localized fallback for an unknown code. Keep a central response-code-to-i18n mapping or deterministic established convention; do not scatter switch statements across pages.

## Mutation Success Feedback

Give immediate success feedback for every user-initiated create, update, delete, transition, assignment, import, reorder, batch, or other mutation. A notification center entry, refreshed table, closed dialog, or changed button state does not replace transient success feedback.

- Prefer the repository's shared feedback service or response interceptor over page-level calls. When the API consistently returns `SuccessResponse<T>`, a centralized interceptor may handle successful `POST`, `PUT`, `PATCH`, and `DELETE` responses.
- Show feedback only after receiving a valid success envelope. Do not infer success from request completion, emit it optimistically, or show it for `GET`, `HEAD`, `OPTIONS`, errors, retries that have not completed, or unrelated external requests.
- Resolve the message from `code` through centralized i18n. Use a localized generic success message when the code is unknown; do not expose a non-localized backend `message` when a safe localized fallback exists.
- Reuse the installed component library's transient feedback primitive. Do not implement custom toast DOM, timers, or styling when the library already provides them.
- Prevent duplicate feedback. If centralized feedback covers a mutation, do not also emit a page-level success toast. A persistent result summary may remain when it adds information such as imported, skipped, or failed row counts.
- Keep system-generated alerts, assignments, reminders, and durable notification-center entries separate from mutation feedback; they serve different purposes.

When adding or reviewing a feature, inventory every write API call and verify that the shared mechanism covers it. Add focused tests for method filtering, success-envelope narrowing, localized known codes, unknown-code fallback, and single emission after an authenticated retry when this infrastructure changes.

## Validation Errors

Model field validation failures with stable code and safe parameters:

```typescript
export interface ValidationError {
  field: string;
  code: string;
  params: Record<string, string | number | boolean | null>;
}
```

Map `code + params` through Angular i18n and attach the result to the matching form control. Use the backend fallback only when the code is unknown. Do not display raw provider/exception text.

Centralize backend-field-to-control mapping, including the repository contract's PascalCase/camelCase convention and any nested property paths. Do not scatter casing fixes across feature pages. If a returned field cannot be mapped safely, surface a localized form-level error instead of silently discarding it.

Keep local validators aligned with simple confirmed presentation rules for immediate feedback, but treat backend validation as authoritative. Clear stale server/duplicate errors when the corresponding value changes.

For asynchronous duplicate or availability checks:

- Treat blur/change checks as UX hints; the write endpoint must recheck authoritatively.
- Cancel stale requests when practical or ignore responses whose submitted value no longer matches the current control value.
- Exclude the current record during Edit when the backend contract supports it.
- Prevent submission while a required check is pending, and attach a duplicate result to the affected control.
- Handle the write endpoint's typed 409 conflict even when the earlier preflight check passed. Use stable conflict metadata to mark the control and localize the message; do not parse backend detail text.

Add focused tests for field-casing/code mapping, unknown-field form errors, write-time duplicate conflicts, stale-error clearing, and stale asynchronous response handling when this behavior changes.

## Backend Pagination

```typescript
export interface PagedResponse<T> {
  items: readonly T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
```

Consume it as `SuccessResponse<PagedResponse<T>>`. Send filter, allowlisted sort, page number/page size, or cursor to the backend. Do not fetch a growing collection and use array slicing/client table pagination as the authoritative data source.

Use backend `totalCount`/`totalPages`; do not recompute them as contract truth. A valid out-of-range page returns an empty `items` list. Use a separate lookup endpoint for bounded dropdown data.

## SignalR

Add `@microsoft/signalr` only for confirmed realtime UI. Treat hub events as hints/notifications, not durable authoritative state. Authenticate/authorize the connection, keep payloads typed and small, and refetch/resync authoritative data after reconnect or detected gaps.

Do not use SignalR for backend-to-backend durable integration; that belongs to MassTransit. Ask for scale-out/backplane infrastructure when multiple backend instances are confirmed; do not add Redis automatically.

Keep connection ownership in one established core/realtime service rather than opening a connection per page/component. Define reconnect backoff, access-token refresh, subscription restoration, teardown, duplicate/out-of-order handling, and the authoritative refetch path. Do not let the client choose a tenant/user authorization group without a server-authorized subscription contract.

## Search

Call the backend search contract; never connect Angular directly to Elasticsearch or embed search credentials/configuration in the bundle. Debounce interactive input, cancel stale requests, preserve backend pagination/sort/filter semantics, and make loading/empty/error states explicit. For autocomplete, bound result count and ignore late responses that no longer match the active query.

## Tenant Context

Treat tenant context as an authenticated backend concern. The UI may display or request an authorized tenant switch, but it never proves access by sending a tenant ID. On a confirmed tenant switch, clear tenant-scoped feature state/cache, reconnect/resubscribe realtime safely, and refetch authoritative data so the previous tenant cannot leak into the next view.

## Runtime Configuration And Secrets

Angular runtime/build configuration contains public values only, such as issuer URL, public client ID, or relative API base. Never ship client secrets, service credentials, connection strings, Elasticsearch keys, vault tokens, or privileged API keys in source, environment files, assets, or the compiled bundle.
