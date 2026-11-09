---
name: review-dotnet-code
description: Evidence-driven review workflow for .NET backend diffs, branches, pull requests, and local changes across layered monoliths, modular monoliths, vertical slices, and independent services. Use to find correctness, security, authorization, data-integrity, concurrency, contract, performance, architecture-fit, and test gaps without assuming modular-monolith rules.
---

# Review .NET Code

Apply `dotnet-backend-standards` and general code-review behavior. Review the diff; do not redesign unrelated code.

Read `dotnet-backend-standards/references/platform-capabilities.md` when the diff touches search, multi-tenancy, SignalR/realtime, or secrets/configuration providers. Read `application-contracts.md` for idempotency, audit, retention, soft/hard delete, and downstream cleanup.

## 1. Establish Evidence

1. Read the complete diff and changed-file context.
2. Identify the architecture from solution boundaries, dependencies, startup, and persistence ownership.
3. Read the nearest equivalent implementation and relevant tests.
4. Trace changed inputs through authorization, business rules, persistence, side effects, and outputs.
5. Apply architecture-specific rules only when their activation evidence passes.

## 2. Review Gates

- Correctness: edge cases, nullability, state transitions, calculations, time handling, and failure behavior.
- Security: authentication, resource authorization, tenant/user scoping, injection, secret exposure, and unsafe errors.
- Data integrity: atomic writes, constraints, concurrency races, idempotency, migration compatibility, and rollback behavior.
- Contracts: routes, serialization, response/status shape, validation, backward compatibility, and client impact.
- Spreadsheet import/template contract: when present, compare the generated workbook artifact and accepted columns with the governing SRS/official template; trace each column through parsing, lookup/enum conversion, validation, transaction boundaries, and persistence. Verify extension/content/size limits, bounded preview, stale/unknown/missing-column rejection, physical row-number preservation, safe per-row errors, distinct failed-row semantics, template no-cache behavior, and focused workbook/parser/service tests. Flag partial-row side effects (for example creating an account before a required lookup link fails) and parsers that claim an extension they cannot actually decode.
- Success/error contract: success is HTTP 200 `SuccessResponse<T>` created at the API boundary around application data; failures are Problem Details with real statuses, and codes—not localized backend text—drive Angular display.
- Validation/pagination: field failures expose stable code/params; growing collections are filtered/sorted/paged in the database and return backend-computed `TotalPages`.
- Mapping: when AutoMapper is selected or already active, enforce patched licensed/community-eligible AutoMapper 16.x, profiles outside controllers, provider-safe projection, invariant preservation, and configuration validation. Otherwise preserve the established mapper and flag only controller mapping, invariant bypass, scattered manual mapping, or an unapproved mapper replacement.
- Performance: unbounded reads, N+1 queries, premature materialization, missing cancellation, and unsafe retry behavior.
- Architecture fit: dependency direction and placement relative to the detected architecture, not a preferred architecture.
- Reusable building blocks: cross-cutting behavior reuses a suitable standalone class library instead of duplicating permission resolution, authorization, email, current-user, clock, storage, telemetry, or messaging code in a deployable. Flag a parallel implementation when an existing owner is present; if the owner violates mandatory standards, require confirmation before broad correction.
- Controller boundary: declarative permission only; bind input, call one use-case/query service, and return its result in an HTTP response. Constructing the `SuccessResponse<T>` envelope at the API boundary is allowed; claims access, DTO mapping, persistence, and business logic are not. Filters must use explicit endpoint metadata and preserve errors/already-enveloped results.
- Constants: reusable values and shared error/status/permission/audit/business vocabularies are named; local non-contractual one-off strings may remain inline.
- Naming/contracts: constant containers end in `Constant`; enums end in `Enum`, use explicit increments of ten, and serialize/persist as strings. `[Flags]` requires confirmation and powers-of-two values.
- Persistence: ordinary pagination/filtering does not force a repository; custom repositories represent real intent/complexity, and DbContext concurrency failures become typed 409 conflicts.
- Cross-cutting correctness: explicit idempotency is added only for remaining duplicate risk; audit is scoped/safe; soft-delete uniqueness matches provider/reuse semantics; retry follows operation idempotency and is not multiplied across layers.
- Conditional platforms: database search remains the default until the Elasticsearch gate passes; search and SignalR failures do not corrupt authoritative state; tenant context spans every boundary; realtime resync is defined; secrets use confirmed least-privilege providers and never leak; retention/legal-hold/purge/downstream cleanup are explicit and bounded.
- Dependencies: installed capabilities are reused and dependency changes were explicitly approved.
- Tests: changed behavior and risk-relevant failure paths have meaningful coverage.

## 3. Finding Standard

Report a finding only when it is actionable and supported by code evidence. Include:

- severity (`P0` critical through `P3` minor);
- file and tight line range;
- trigger or input that reaches the defect;
- concrete impact;
- smallest safe correction direction.

Do not report personal style preferences, speculative future problems, or modular-boundary violations when the repository is not modular.

## 4. Output

Lead with findings ordered by severity, then open questions, short summary, and permitted build/static checks run. List relevant tests as user-run commands without executing them unless explicitly requested. If there are no findings, say so and state residual verification risk.
