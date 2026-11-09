---
name: review-modular-code
description: Specialized review workflow for changes inside an established .NET modular monolith. Use only when the user explicitly requests a modular review or repository evidence proves independently owned modules and module persistence boundaries. Combine with review-dotnet-code; do not apply modular findings to a layered monolith, vertical slice, or independent service.
---

# Review Modular Code

Apply `review-dotnet-code`, `dotnet-backend-standards`, and `modular-dotnet-standards` with this specialized checklist.

## Activation Gate

Confirm explicit modular scope or strong repository evidence for independently registered modules and ownership boundaries. If the gate fails, stop this checklist and use `review-dotnet-code`. Never report a modular-boundary finding merely because a repository has business-area folders.

## 1. Understand The Diff

Read changed files, nearest existing patterns, touched tests, and local instructions. Identify whether the change is module code, host wiring, shared kernel, persistence, API, messaging, or tests.

## 2. Placement

- One-module behavior belongs inside that module.
- Host project should only wire modules, expose host read models, configure middleware, and host realtime hubs.
- An optional shared kernel should contain only stable domain primitives or concepts genuinely shared by modules. Flag generic repositories, middleware, response contracts, feature constants/DTOs, integration events, or provider infrastructure placed there; those retain their owning module, producer, host, or focused-capability boundary.
- No module should depend on another module's Infrastructure or DbContext.

## 3. Architecture

- Services do not inject DbContext directly.
- Repositories do not expose `IQueryable<T>`.
- A method with more than one `SaveChangesAsync`/`CompleteAsync` call, or that calls a self-committing helper (audit log, notification) more than once, runs inside one explicit transaction — not as sequential, independently-committed writes that can leave partial state on failure.
- Entities gating a racy state transition (lock/approval flags and the data they protect) carry a concurrency token, and the nearest persistence boundary translates `DbUpdateConcurrencyException` into `ConcurrencyConflictException` for the global 409 mapping.
- No business rule — especially an authorization/scoping check such as "can this user access X" — is implemented independently in more than one file. Grep for a near-duplicate before approving a new one.
- A service touched by this diff has not grown to cover more than one unrelated responsibility (e.g. CRUD plus file import plus unrelated side effects) as a result of the change.
- Cross-module communication uses events, MediatR notifications, or shared contracts.
- EF entities are not shared as API contracts between modules.
- Messaging changes use outbox/inbox or existing reliability pattern when durability matters.

## 4. API And Contracts

- Each controller action binds transport input, calls one use-case/query service, and returns an HTTP response around its result.
- Permission is declarative through an attribute or equivalent metadata. Controllers may construct the `SuccessResponse<T>` transport envelope; they do not read claims, map DTOs, construct application payloads, execute business rules, or access persistence.
- Endpoints are authenticated by default.
- Anonymous or credential-exchange endpoints (login, token exchange) carry an explicit rate-limit policy, not just a default/global one assumed to exist.
- API versioning is consistent.
- Successful responses use the repo response envelope created at the API boundary. Filters must use explicit endpoint metadata and preserve errors/already-enveloped results.
- Request binding is explicit.
- Success/error codes are constants.
- Validation is handled by validators, not scattered service checks.

## 5. Style

- No abbreviation blacklist violations such as `repo`, `ctx`, `req`, `res`, `ct`.
- Async methods end in `Async`.
- Services/controllers/validators are sealed unless intentional.
- DTOs are records.
- Dates use UTC.
- Logging is structured.
- Mapping follows repo pattern.

## 6. Tests

- CRUD/application behavior has focused service/use-case tests.
- Failure tests correspond to actual exceptions and branches in the changed logic.
- Unit tests mock repositories and Unit of Work rather than EF in-memory.
- Write-path tests verify `SaveChangesAsync`.
- Not-found/error paths are covered.
- Build/test commands are run or clearly reported as not run.

## 7. Output Format

Report findings first. Use this shape:

```text
Findings
- [P1] file:line - Issue and concrete risk.
- [P2] file:line - Issue and concrete risk.

Open Questions
- Question if needed.

Summary
- Short change summary only after findings.

Tests
- Commands run or not run.
```

If no issues are found, say so clearly and mention residual risk or missing verification.

Every finding must identify the violated module boundary or profile rule and the repository evidence that makes the rule applicable.
