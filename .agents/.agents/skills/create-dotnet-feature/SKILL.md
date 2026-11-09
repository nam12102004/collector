---
name: create-dotnet-feature
description: Architecture-adaptive workflow for adding or extending a .NET backend feature in an existing application, including CRUD, commands, queries, endpoints, validation, persistence, migrations, and tests. Use for backend feature implementation in layered monoliths, modular monoliths, vertical-slice applications, or independent services; detect and preserve the repository architecture instead of introducing a preferred pattern.
---

# Create .NET Feature

Apply `dotnet-backend-standards` together with this workflow.

Read `dotnet-backend-standards/references/application-contracts.md` whenever the feature exposes an API, returns a list, validates input, maps DTOs, performs an outbound write, changes a uniqueness rule, or changes retention/soft-delete/hard-delete behavior. Treat its success envelope, backend pagination, localization, mapping, idempotency, audit, data-lifecycle, and retry rules as part of the feature contract.

Read `dotnet-backend-standards/references/platform-capabilities.md` whenever the feature searches beyond ordinary database filtering, introduces tenant context, pushes realtime updates, or reads secrets/external configuration. Confirm activation, provider, ownership, isolation, recovery, and operational behavior before implementation.

## 0. Evidence Checkpoint

Before editing, identify and report internally:

- selected architecture and supporting files;
- nearest canonical feature;
- API contract and authorization policy;
- persistence and transaction style;
- validation, mapping, error, and response conventions;
- test projects and relevant commands.
- installed packages/framework capabilities relevant to the use case.
- existing standalone class libraries and DI extensions that may already own any cross-cutting capability.

When scaffolding within a new full-stack repository, keep application projects under `src/` and apply the confirmed `<Product>.API` / `<Product>.WebApp` naming policy from `dotnet-backend-standards`. Ask for the product name and database provider when either is unknown.

If fields, validation, API contract, permission, or acceptance behavior cannot be proven from the request or repository, stop and ask. Do not change architecture, add a dependency, or refactor legacy violations without explicit confirmation.

## 1. Define The Slice

1. Translate the request into use cases and observable acceptance behavior.
2. Identify reads, writes, invariants, permissions, failure modes, concurrency risks, tenant scope, and retention/deletion effects.
3. Identify contract and schema compatibility constraints.
4. Keep the planned file set no broader than the use case requires.
5. Identify reusable values and shared vocabularies that require named constants.

### Query-Cost Checkpoint

When a request mentions parallel API calls, request fan-out, 409 conflicts, slow loading, dashboards, boards, summaries, or an overview/detail screen, define the performance acceptance criteria **before editing**:

1. Trace the current path from UI call to service and repository. Count HTTP requests and database commands separately; they are different problems.
2. State a bounded query budget for the use case. Specify whether the goal is fewer HTTP requests, fewer database commands, or both.
3. For a read that combines related collections, design a dedicated repository/query-service projection or bulk query first. Preserve data scope, permissions, ordering, and independent pagination in that read model.
4. Do not declare an optimization complete merely by replacing frontend `forkJoin`/parallel calls with one API endpoint whose service still fans out into the same per-section or per-item repository queries. That only moves the fan-out to the server.
5. Reject N+1 enrichment. Use set-based loading, joins, grouping, or a bounded fixed number of bulk queries. Document any deliberate fixed query count in the acceptance criteria.
6. Add an appropriate verification: inspect generated SQL/logging when available, or add an integration/query-count test when user scope and repository conventions authorize it. Do not claim the budget was verified if only compilation was run.

## 2. Follow The Selected Architecture

- Single-project layered monolith: extend the existing controller/service/repository or query-service shape.
- N-Layer: apply `n-layer-dotnet-standards`; keep the backend in one project and choose direct DbContext versus a custom repository from actual persistence complexity.
- Clean Architecture: apply `clean-dotnet-standards`; respect inward references and Domain-owned repository interfaces.
- Modular monolith: keep ownership inside the established module and apply `modular-dotnet-standards` only if its activation gate passes.
- Vertical Slice/CQRS: apply `vertical-slice-dotnet-standards` only after its large-application activation gate passes; keep endpoint/request/validator/handler/tests together.
- Independent service: apply `microservice-dotnet-standards` for topology plus the service's independently selected one-project N-Layer, multi-project Clean, or confirmed large Vertical Slice/CQRS profile; preserve service-owned contracts/data and treat network dependencies as unreliable boundaries.
- Event-driven and DDD: add their profiles only when their independent activation gates pass.

## 3. Implement From The Inside Out

1. Add or update domain rules and persistence mapping only when required.
2. Implement the business operation with authorization, validation, atomicity, cancellation, and bounded queries.
3. Reuse the existing standalone owner of cross-cutting behavior through `ProjectReference`. If none exists and the concern is genuinely cross-project, apply `dotnet-backend-standards/references/building-blocks.md` and create `src/<Product>.<Capability>/`; do not bury a reusable email, permission, authorization, clock, storage, or telemetry client inside the API project, and do not introduce NuGet packaging without explicit scope.
4. Make each controller action bind input, call exactly one use-case/query service, and return `Ok(new SuccessResponse<T>(code, message, data))` around the service's application data/result. Keep the HTTP success envelope at the API boundary; a shared MVC/endpoint filter is allowed when explicit endpoint metadata supplies its stable code and fallback message. Put permission only in authorization metadata; keep claims access, DTO mapping, business logic, and persistence outside the controller. Every success uses HTTP 200.
5. Wire DI, routes, validators, mappings, and migrations using existing registration mechanisms.
6. Route expected failures through typed custom exceptions and the global exception handler; do not add controller `try/catch` or construct Problem Details in controller actions.
7. Avoid speculative abstractions, unused interfaces, placeholder codes, and unrelated cleanup.

## 4. Prove Behavior

- Add focused tests for the service/use-case logic.
- Derive failure tests from the exceptions and branches that the implemented logic contains.
- Add integration-test code only when the user requests it or the established repository requires it. Use Moq for repository-backed services; use EF Core InMemory only for direct-DbContext behavior that does not depend on relational semantics, and never create real-provider tests without explicit user scope.
- Build the smallest affected project or solution. Do not apply migrations to a database, execute tests, start the application, run runtime checks, or use browser tooling unless explicitly requested. When an EF Core model/schema changes, scaffold its required migration through the repository script or EF CLI; never hand-create migration, designer, or snapshot files.
- Inspect migration/startup wiring, tenant isolation, search/realtime degradation and resync, secret-provider redaction, and bounded retention cleanup statically where applicable; hand runtime checks to the user.
- Compare the final public contract with the source requirement or existing client usage.

## Completion Gate

Do not declare completion until implementation, wiring, authored tests, and the build agree. Report all unexecuted test/runtime checks and resulting residual risk without claiming they passed.
