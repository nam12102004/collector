# Architecture Selection

Select architecture from repository evidence before applying placement or dependency rules. Do not infer architecture from aspirational documentation or folder names alone.

## Evidence Order

1. Direct user scope and repository `AGENTS.md` files.
2. Solution and project boundaries plus project references.
3. DI/module registration and application startup.
4. DbContext, migration, and transaction ownership.
5. Several existing features and tests, not one outlier.

## Independent Architecture Dimensions

Do not force one label to answer every architecture question. Select and compose only profiles whose evidence gates pass:

- code organization: N-Layer, Clean Architecture, established modular monolith, or Vertical Slice/CQRS;
- deployment topology: monolith or microservices;
- domain modeling: simple CRUD/application service or DDD;
- integration: synchronous calls or event-driven messaging.

For example, a microservice may use one-project N-Layer and event-driven integration without Clean Architecture or DDD. Another service in the same monorepo may use Clean Architecture plus DDD.

## Profiles

### N-Layer

Evidence usually includes one deployable backend project, one primary DbContext, and folders such as Controllers, Services, Repositories, Entities, and Data.

- Place new code beside the nearest compliant equivalent feature.
- Preserve the existing application/persistence boundary while enforcing the controller invariant: bind input, call one use-case/query service, and return an HTTP response around its result. The controller/API boundary may create `SuccessResponse<T>`; the application service returns application data/result. Report non-compliant legacy controllers and ask before refactoring them.
- Do not create bounded-context projects or module-owned DbContexts for an ordinary feature.
- Do not call the application a modular monolith merely because business areas have separate controllers or services.
- Apply `n-layer-dotnet-standards`. For new projects, N-Layer always means one backend project; multi-project API/Application/Domain/Infrastructure uses the Clean profile.

### Clean Architecture

Evidence usually includes API/Application/Domain/Infrastructure projects with inward dependency direction and framework-independent Domain/Application projects.

- Apply `clean-dotnet-standards` only when this evidence or direct user selection exists.
- Do not call every multi-project legacy layered system Clean Architecture without checking references and framework dependencies; preserve it and ask before migration.

### Modular Monolith

Require explicit user scope or strong evidence such as independently registered bounded-context modules, enforced project boundaries, and module-owned persistence or migration ownership.

- Apply `modular-dotnet-standards` only after its activation gate passes.
- Do not apply modular rules to a single-project layered application.

### Vertical Slice

Evidence usually includes feature/use-case folders containing endpoint, request, validation, handler, and tests, often with direct DbContext access per handler.

- Keep the change within its slice.
- Do not introduce generic repositories or service layers unless the repository already requires them.
- Share code only for genuine cross-slice policy or infrastructure.
- Apply `vertical-slice-dotnet-standards` only for a confirmed large CQRS/use-case-slice application. Do not use it for small CRUD or for a feature-first folder that still follows ordinary layering.

### Independent Service Or Microservice

Evidence includes an independently deployed service with its own API and data ownership. A repository with several deployables is not automatically a distributed microservice system.

- Preserve service ownership and published contracts.
- Treat network calls as failure-prone and apply timeouts, cancellation, retries only when safe, and idempotency where duplicate delivery matters.
- Do not introduce cross-service database access.
- Apply `microservice-dotnet-standards` for topology/ownership, then independently select one-project N-Layer, multi-project Clean Architecture, or confirmed large Vertical Slice/CQRS for each service. Do not infer Clean Architecture or Vertical Slice from microservice deployment.

### Event-Driven Integration

Require explicit asynchronous cross-process messaging or strong broker/consumer/outbox evidence.

- Apply `event-driven-dotnet-standards` for message semantics, contracts, delivery, idempotency, and orchestration.
- Do not apply it to ordinary in-process observer/event code.

### Domain-Driven Design

Require evidence of aggregate invariants, value objects with behavior, domain events, bounded-context language, or interacting business rules.

- Apply `ddd-dotnet-standards` as a modeling profile alongside the selected code/deployment profile.
- Do not create DDD ceremony for CRUD or infer DDD from a `Domain` project alone.

## Ambiguity Rule

When evidence conflicts, follow the nearest compliant working feature and the smallest change. Do not copy a known violation for consistency and do not perform architecture cleanup as part of an unrelated task; report the conflict and ask before expanding scope. In a new project with no canonical evidence, ask about every material architecture and technology choice before scaffolding.

## New Repository Placement

When scaffolding a confirmed .NET + Angular product, place deployable application projects under the repository-root `src/` directory. Use `src/<Product>.API/` and `src/<Product>.WebApp/` for a single API and Angular web application. Keep the solution entrypoint at the root. Ask for the product/domain name before scaffolding when it is not explicit, and do not substitute generic names such as `backend`, `frontend`, `client`, `web`, or `app`.

For multiple APIs or deployables, ask for service/bounded-context names and the deployment model before choosing names. Do not infer a modular monolith or microservice topology from the number of projects.

For a confirmed microservice monorepo, keep each service directly under `src/<Service>/` with its own solution and projects. Keep focused reusable technical libraries and producer-owned contract projects flat under `src/<Product>.<Capability>/` or `src/<Product>.<Producer>.Contracts/`; include each required contract project in its producer/consumer service solutions and consume it with `ProjectReference`, without private packaging by default. Do not add intermediate `Services` or `BuildingBlocks` folders.
