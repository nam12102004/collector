# Architecture

Use these defaults for a modular monolith. Rename projects to match the target repo.

## Project Roles

- Host API project: ASP.NET Core entrypoint, middleware, authentication, versioning, Swagger, controller assembly scanning, SignalR or realtime hubs, host-level read models, module registration.
- Module projects: independent class libraries for bounded contexts. Each module contains Domain, Application, Infrastructure, and Controllers or Endpoints.
- Optional shared kernel project: only stable domain primitives or concepts that multiple modules genuinely share and that do not belong to one bounded context. Keep it minimal and independent of concrete modules. Exclude generic repository abstractions, middleware, response contracts, feature constants or DTOs, integration events, and provider-specific infrastructure. Repository interfaces remain in the owning module Domain, producer-owned integration contracts remain with the producer, and host middleware or technical capabilities remain in the host or a focused capability library.

## Module Shape

```text
src/{Company}.{ModuleName}/
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Models/
├── Application/
│   ├── DTOs/
│   ├── Interfaces/
│   ├── Features/
│   ├── Validators/
│   └── Mappers/
├── Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/
│   │   └── Repositories/
│   └── Config/
├── Controllers/ or Endpoints/
├── {ModuleName}Module.cs
└── GlobalUsing.cs
```

## Boundaries

- One module may reference the optional minimal shared kernel only for a confirmed shared domain primitive, not another module's persistence layer.
- Do not query another module's tables through its DbContext.
- If a module needs data from another module, publish events, use shared contracts, or let the host aggregate read models.
- Controllers/endpoints belong to the module that owns the use case.
- Treat controllers and endpoints as transport adapters only: bind transport input, rely on declarative permission metadata, call exactly one use-case/query service, and form the HTTP response. They may wrap returned application data in `SuccessResponse<T>`; they must not map DTOs or execute application logic. For a read-only technical health/status endpoint, an existing inward status query may be that single call; do not create a forwarding service only to satisfy a layer convention. Keep protocol execution and persistence access out of the controller.
- Never inject or resolve `DbContext`, `IUnitOfWork`, generic/custom repositories, or persistence implementations from controllers/endpoints. This includes constructor injection, action parameters such as `[FromServices] IUnitOfWork`, and `HttpContext.RequestServices`.
- Move claims access, permission evaluation, query composition, joins, role aggregation, application mapping, and persistence calls into the appropriate application service/handler backed by repositories or `IUnitOfWork`. Keep HTTP envelope construction (`SuccessResponse<T>`) at the API boundary.
- Host registers module assemblies with `AddApplicationPart` or endpoint mapping extension methods.

## Application And Technical Adapter Ownership

- Apply [service ownership and readable workflows](../../dotnet-backend-standards/references/service-boundaries.md) when classifying or splitting module services. Application keeps policy and durable execution decisions; Infrastructure implements technical capabilities.
- Follow [technical service ownership](../../dotnet-backend-standards/references/service-boundaries.md#technical-services-and-business-ownership): keep application-facing interfaces inward and implementations such as audit/history writers, health providers, credential adapters and sequence-reservation storage in the owning module's Infrastructure. Keep audit criteria, event-routing policy and commit ownership in Application. Preserve scoped Unit of Work sharing and DI lifetimes; domain entities and I/O alone do not determine the layer.
- Declare execution ports in the module's established inward interfaces location and register their Infrastructure implementations in the module composition root. Keep this change inside the owning module; do not add a host-level orchestration layer or a cross-module protocol abstraction without a demonstrated need.
- Keep repository methods focused on intent-named queries and projections. A reusable subtype lookup does not make execution eligibility or protocol readiness repository responsibilities.

## Host Pipeline

Keep middleware order deliberate:

1. CORS
2. Global exception handler / problem details
3. HTTPS redirection
4. Rate limiting
5. Authentication
6. Authorization
7. Controllers/endpoints/hubs

Place rate limiting before authentication so anonymous, unauthenticated endpoints (login, token exchange) are throttled too, not just endpoints that already require a valid principal.

## Persistence

- Each module owns a DbContext and migrations history.
- Repository implementations live in module Infrastructure.
- Repository interfaces live in the owning module's Domain project. Infrastructure implements them; do not put persistence ports in controllers or copy them into a shared catch-all kernel.
- Transactions are explicit for multi-write operations: a business operation with more than one logical write commits through one `BeginTransactionAsync`/`CommitTransactionAsync` (or the DbContext execution strategy's transaction), never through several independent `SaveChangesAsync` calls. Side-effect helpers (audit logging, notifications) invoked inside that operation stage changes on the same context — they do not call `SaveChangesAsync` themselves, and calling one inside a loop must not turn a bulk operation into N partial commits.
- Entities with a state transition two actors can race on (a lock/approval flag and the data it protects) carry an optimistic concurrency token; the nearest persistence boundary translates the resulting `DbUpdateConcurrencyException` into `ConcurrencyConflictException` for the global 409 mapping.

## Communication

- In-process module notifications: an established direct dispatcher, MediatR `INotification`, or equivalent. Add MediatR only after the base activation and v13+ license gates pass.
- Cross-process integration: outbox/inbox plus a message broker when reliability matters.
- Producer-owned integration contracts are stable and small. Preserve module ownership; create a separate focused contract project only when external consumers need a stable reference. Avoid sharing EF entities or Domain objects.
