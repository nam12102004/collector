---
name: dotnet-backend-standards
description: Strict, architecture-neutral engineering standards for .NET backend code. Use whenever Codex writes, reviews, refactors, or diagnoses C#, ASP.NET Core, EF Core, API contracts, persistence, background jobs, authorization, validation, logging, migrations, or backend tests. Detect and preserve the repository architecture; combine with an architecture-specific skill only when its activation evidence is satisfied.
---

# .NET Backend Standards

Apply strict quality gates without imposing a repository pattern, Clean Architecture, modular monolith, CQRS, Repository + Unit of Work, MediatR, or direct DbContext access by default. Use the confirmed AutoMapper baseline for DTO/entity boundaries without moving mapping into controllers or bypassing domain invariants.

## Mandatory Preflight

1. Read repository instructions, solution/project files, target framework, package versions, startup/DI, persistence setup, and the nearest equivalent feature and tests.
2. Read `references/architecture-selection.md` before deciding file placement or dependencies.
3. Read `references/csharp-conventions.md` before naming methods, helpers, services, clients, enums, constants, filters, repositories, or concurrency errors.
4. Record evidence for the selected architecture, persistence style, API style, validation mechanism, and test runner.
5. Preserve public contracts and architecture unless the user explicitly includes a change to them.
6. Search for existing implementations of the same business rule, authorization check, calculation, response type, and side effect before creating another.
7. Search solution/project references and DI registration for standalone reusable class libraries before implementing cross-cutting concerns such as typed application exceptions, permission resolution, authorization, current-user access, email, clocks, storage, telemetry, or messaging.
8. Inventory installed packages and built-in framework capabilities before writing a custom replacement. Read `references/dependency-catalog.md` before selecting or changing dependencies.

## Ask-First Gate

Stop and ask before implementation when architecture, fields, validation, API contract, permissions, or acceptance behavior cannot be proven. In a new project, ask about every material architecture and technology choice. If existing code violates these standards, report it and ask before expanding the task to refactor it.

## Reference Routing

- Read [service ownership and readable workflows](references/service-boundaries.md) when routing API calls to their owning services, classifying or placing technical services, splitting/refactoring services or helpers, consolidating repeated subtype lookups, or changing production/simulator execution paths.
- Read `references/api-reliability.md` when touching endpoints, authentication, authorization, validation, persistence, transactions, concurrency, background jobs, or external calls.
- Read [application contracts](references/application-contracts.md) completely when creating or changing success responses, Problem Details, validation errors, pagination, AutoMapper profiles, idempotency, audit, data retention, soft/hard delete, rate limits, or outbound retry behavior.
- Read [conditional platform capabilities](references/platform-capabilities.md) completely when requirements or code touch search/Elasticsearch, multi-tenancy, SignalR/realtime, secrets, configuration providers, or their authorization, isolation, synchronization, and operational behavior.
- Read `references/building-blocks.md` before creating, reusing, reviewing, or changing a cross-project class library or reusable typed-exception, permission, authorization, current-user, email, clock, storage, telemetry, or messaging capability.
- Read `references/dependency-catalog.md` when scaffolding a backend, adding a package, or implementing validation, messaging, observability, health, OpenAPI, persistence, resilience, or tests.
- For telemetry registration, configuration, exporters, or health dependencies, apply the mandatory availability rules in [deployment-config-standards](../deployment-config-standards/SKILL.md#health-and-observability): OTEL is optional and must never prevent the application from starting or serving requests.
- Read `references/testing-verification.md` before implementing tests or declaring the work complete.

## Non-Negotiables

- MUST keep each controller action to transport binding, one call to the correct use-case/query service, and returning an HTTP response. By default, the controller wraps the service's application data/result in `SuccessResponse<T>` and returns `Ok(...)`; a shared MVC `ActionFilter` or Minimal API endpoint filter may do this when explicit endpoint metadata supplies the stable success code and fallback message. A read-only technical health/status action may use an existing inward status query directly; do not add a forwarding service solely for that call. Protocol execution and persistence access remain outside controllers.
- MUST have APIs call the application service that owns the use case/query directly; do not create or retain an intermediate service solely to call another service. A controller may inject multiple owning services while each request path calls one. Keep a coordinating service only when it owns real orchestration, policy, result aggregation or transaction boundaries; apply [API calls and service ownership](references/service-boundaries.md#api-calls-and-service-ownership).
- MUST express endpoint permission through the repository's authorization attribute or equivalent declarative metadata. Do not read claims, perform imperative permission checks, map DTOs, execute business rules, access persistence, or construct application payloads in controllers. Constructing the HTTP `SuccessResponse<T>` envelope at the API boundary is allowed.
- MUST validate untrusted input at a consistent boundary and enforce business invariants inside the business operation.
- MUST preserve authorization and tenant/user scoping on every affected read and write path.
- MUST derive tenant identity from trusted authenticated/server context and enforce it across every affected database, cache, search, file, job, event, audit, idempotency, and realtime boundary when multi-tenancy is active.
- MUST propagate `CancellationToken` through cancellable async I/O when the existing call chain supports it.
- MUST make a multi-write business operation atomic when partial completion would violate invariants.
- MUST handle a real concurrent state transition with an appropriate database or domain concurrency mechanism; an earlier read check alone is not sufficient.
- MUST page, stream, aggregate in the database, or otherwise bound data sets that can grow. Do not materialize an unbounded table for client-side slicing.
- MUST use parameterized data access and MUST NOT log secrets, tokens, credentials, or sensitive payloads.
- MUST use UTC or the repository clock abstraction for persisted/comparable timestamps.
- MUST use structured logging and retain exception context without exposing internals to clients.
- MUST route expected application failures through typed custom exceptions and one global exception handler. For new HTTP APIs, serialize errors as Problem Details with stable error-code constants and a trace identifier; preserve an established public error contract unless migration is explicitly approved.
- MUST update focused tests for changed behavior and verify migrations when the data model changes.
- MUST generate EF Core migrations for schema/model changes through the repository migration script or `dotnet ef migrations add` with the owning project, startup project, DbContext, and output directory. Never hand-create migration, designer, or model-snapshot files. Inspect the generated migration and snapshot, then verify there are no pending model changes; do not suppress `PendingModelChangesWarning`.
- MUST NOT introduce a new architectural abstraction solely to satisfy an example in a skill.
- Where Application/Infrastructure boundaries exist, MUST keep business policy and transaction ownership in Application, application-facing technical interfaces in the established inward layer, and technical implementations in Infrastructure. Apply [technical service ownership](references/service-boundaries.md#technical-services-and-business-ownership); using domain entities or a Unit of Work alone does not make an audit writer a business service.
- MUST place standalone supporting helper classes in `Helpers/` within their owning project/layer and name both the type and file `{Capability}Helper`. Apply the ownership, state/lifetime and dedicated-role distinctions in [helper conventions](references/csharp-conventions.md#helpers).
- MUST define named constants for reusable values and shared vocabularies such as error codes, permissions, statuses, audit categories, and business thresholds. A one-off internal string that is not user-visible, contractual, categorical, or reusable may remain inline.
- MUST follow the `Constant` and `Enum` suffix, explicit enum-value, string serialization/persistence, application-service, filter, client, and concurrency conventions in `references/csharp-conventions.md`.
- MUST return confirmed success results as HTTP 200 `SuccessResponse<T>` envelopes created at the API boundary around application data/results. Application services must not depend on the HTTP success-envelope type. Use Problem Details with real failure statuses for errors; do not reuse the success envelope for failures.
- MUST paginate growing collections in the backend/database and return `PageNumber`, `PageSize`, `TotalCount`, and backend-computed `TotalPages`.
- MUST use a suitable installed package or framework capability before implementing an equivalent custom mechanism. Ask before adding or replacing a package.
- MUST keep AutoMapper profiles scoped to the owning service/feature, inject `IMapper` into services/use cases rather than controllers, and remove private entity-to-DTO `Map`/`MapList` helpers. When a DTO needs runtime or calculated values, map an explicit typed mapping-source in the profile; do not replace the helper with a manually constructed projection class or inline DTO constructor in the service.
- MUST keep numbered workflow comments in service/use-case business logic; do not narrate obvious mapping configuration.
- MUST validate mailbox-style email input with `System.Net.Mail.MailAddress.TryCreate` and reject display-name forms by confirming the parsed address equals the trimmed input. MUST validate phone numbers with `libphonenumber-csharp` parsing plus `IsValidNumber`, using an explicit configured or domain-owned default region for national-format input; do not substitute regex-only phone validation.
- MUST treat a specced spreadsheet import/template as one versioned contract: the generated artifact, preview endpoint, parser, column mapping, lookup/enum conversion, validator, and persistence must agree. Accept only formats the parser truly decodes; bound file and preview sizes; reject missing/unknown/removed columns; preserve physical spreadsheet row numbers; return safe per-row failures; and keep each row's required writes atomic.
- MUST reuse a suitable standalone class library for cross-project capabilities instead of duplicating it inside an API or feature. If an existing library violates these standards, report it and ask before fixing, replacing, or bypassing it.

## Conditional Rules

- Use Repository + Unit of Work only when the selected architecture or existing codebase requires it.
- Use direct EF Core access in handlers/services only when it matches the selected architecture and keeps query/write logic testable and scoped.
- Do not treat pagination plus ordinary filtering as sufficient reason to add a repository. Extract persistence logic when reuse, joins/aggregation, dynamic criteria, data-scope policy, provider-specific optimization, object-graph loading, or loss of service readability creates a real boundary.
- Use the confirmed success envelope and AutoMapper baseline from `application-contracts.md`. Preserve an established incompatible public contract and ask before migration. Use API versioning, mediator pipelines, and DTO records only when required by the API contract or established repository convention. Apply FluentValidation and the other packages according to `references/dependency-catalog.md`.
- Keep Elasticsearch, multi-tenancy, SignalR, and external secret/configuration providers conditional. Apply `platform-capabilities.md`; do not infer them from architecture, project size, or commercial use alone.
- Apply module-boundary rules only after the modular-monolith activation gate is satisfied.
- Apply N-Layer, Clean Architecture, Vertical Slice/CQRS, microservice, event-driven, or DDD rules only after the corresponding profile activation gate is satisfied. These profiles describe independent dimensions and may compose when their gates pass.
- Create a new standalone class library only when no suitable one exists and the capability is genuinely cross-project. Keep its public surface focused and free of feature-specific domain/persistence details; never create a catch-all `Common`, `Helpers`, or `Utilities` project.

## Exception Protocol

If a MUST rule cannot be satisfied, do not silently weaken it. State:

1. the blocked rule;
2. repository evidence and concrete risk;
3. the smallest safe alternative;
4. required user decision, if the alternative changes scope or a public contract.

## Completion Gate

- Build the smallest affected solution/project.
- For CRUD/application changes, add or update service/use-case tests and derive failure cases from the actual exceptions and branches in that logic. Add broader/integration coverage only when correctness depends on shared behavior, provider behavior, serialization, middleware, or route wiring.
- Do not apply migrations to a database, execute tests, lint, application startup, runtime smoke checks, or browser tooling unless the user explicitly requests it. Scaffolding a required EF Core migration through the repository script or EF CLI is expected for schema/model changes; it does not apply the migration. Review endpoint contracts, authorization, data bounds, failure paths, and migration wiring statically.
- Report the build command and outcome, plus the test/runtime commands prepared but intentionally not run.
