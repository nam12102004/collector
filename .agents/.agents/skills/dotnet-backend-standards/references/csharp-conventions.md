# C# Conventions

## Application Services

- Default to one capability service such as `IProductService` / `ProductService` for ordinary CRUD and queries.
- Do not split command/query services preemptively. When evidence shows independent read/write change patterns or a god service, split to `IProductCommandService` and `IProductQueryService` or narrower capability services.
- Do not add or retain a service as a forwarding facade, including `IProductService` after a split. APIs call the service that owns the operation directly; keep a coordinating service only when it owns real orchestration, transaction boundaries, policy or result aggregation. Apply [API calls and service ownership](service-boundaries.md#api-calls-and-service-ownership).
- A controller may inject multiple services, but each action calls exactly one service.
- Detect a god service by unrelated capabilities, dependencies used by disjoint method groups, costly test setup, and multiple independent reasons to change. Do not use line count alone, and do not move code to a repository merely to shorten a service.
- Apply [service ownership and readable workflows](service-boundaries.md) before extracting helpers or adapters. Keep the business sequence visible and place cohesive implementation details with their existing owner.

## Method Names

- Prefer the shortest clear name for the concrete operation and its subject, such as `OpenAsync`, `ConnectAsync`, or `LoadWarehouseMapAsync`. Read it with the owning type, parameters, return value, and body; omit context already obvious at the call site, and make the name match the actual behavior and side effects.
- Avoid filler qualifiers such as `Core`, `Internal`, `Detailed`, or `Safe` when they convey no functional distinction. Name the real operation or representation instead, for example `WriteFileAsync` or `GetSectionJsonAsync`; use an overload when it is the same operation with different parameters.
- Keep domain qualifiers needed to distinguish behavior. Do not list every implementation step in the name, shorten through cryptic abbreviations, or impose an arbitrary character limit.
- Do not introduce the `Ensure` prefix for authored methods. A normal method may validate inputs and throw; those behaviors alone do not justify a separate naming convention.
- Use `Validate...` when the method's responsibility is checking a rule. If it also creates, loads, connects, acknowledges a device result, or changes business state, name that operation explicitly; for example, `PrepareReleaseAsync` for release checks followed by a device ACK. Do not mechanically replace every `Ensure...` with `Validate...`.
- Name boolean queries with `Is`, `Has`, or `Can`. Use `Try...` for the established boolean-success pattern; name an operation returning a result code directly, such as `ArmScenario`. Retain the `Async` suffix for asynchronous methods according to repository conventions.
- Preserve framework/third-party API names and required overrides or interface implementations, such as EF Core's `EnsureSchema`. When renaming owned methods, update interfaces, callers, tests, and documentation together while preserving guards, exceptions, cancellation, and transaction behavior.

## Helpers

- Standalone classes whose responsibility is supporting another operation MUST live in `Helpers/` within the owning project/layer and use the suffix `Helper` for both type and filename, for example `Application/Helpers/EquipmentConfigurationHelper.cs` or `Infrastructure/Helpers/OpcNodeAddressHelper.cs`. Match the namespace to that path; do not leave helper classes in `Services`, `Requests`, or the project root.
- Keep each helper focused on one capability and inside its owning module. Update callers, imports/global usings, DI registrations, existing tests and live documentation links together when moving or renaming it.
- Use `static` only when the helper needs no instance state or DI lifetime. A stateful helper remains an instance class with its existing lifetime and synchronization; never turn it static merely to satisfy naming.
- Classify by responsibility. Application services keep business orchestration and transaction ownership. Dedicated roles such as constants, true extension-method containers, validators, handlers, repositories, mapping profiles and technical services/providers retain their role-specific naming and folders; `static` alone does not make them helpers.
- Keep cohesive private implementation methods with their only owner unless there is a real extraction boundary; this convention governs standalone supporting classes and does not require extracting every private method or nested implementation type.

## Filters And Clients

- Name a feature filter `{Feature}FilterParams`, for example `ProductFilterParams`. Name a reusable paging base `PagedFilterParams`; avoid an ambiguous bare `FilterParams`.
- Default to `I{Capability}Client` for an external-system or protocol wrapper, for example `IEmailClient` or `IInventoryClient`. Honor an explicit user/repository preference for the `Service` suffix on technical adapters; express the distinction through capability names, layer placement and dependencies.
- Name business/application orchestration `I{Capability}Service`, for example `IEmailService`. A service may render templates or apply policy and then call the client.
- Do not use `Sender` when the established `Client`/`Service` distinction applies.

## Mapping

- Use AutoMapper `16.x` at no lower than patched `16.1.1` as the default DTO/entity mapper after the license gate passes.
- Name a mapping profile `{Feature}MappingProfile` or follow the established repository profile suffix; keep it in Application/the owning feature and never inject `IMapper` into controllers.
- Keep one cohesive profile per owning service/feature; never collect unrelated service mappings in a shared god profile.
- Inject `IMapper` into the service/use case and remove private `Map`/`MapList` entity-to-DTO helpers. Use a small feature-owned mapping-source record when repository or policy data enriches the projection.
- Prefer `ProjectTo<TResponse>()` for EF read projection after filters/sorts and before materialization when the mapping is provider-translatable.
- Use explicit domain factories/behavior for protected aggregate creation/update; mapping MUST NOT bypass invariants.
- Avoid unreviewed `ReverseMap()` and validate configuration in tests.

## Service Comments

- Comment non-obvious business orchestration in the service/use case, not routine mapping configuration or self-evident assignments.
- For a multi-step workflow, use local numbered comments such as `// 1. Resolve references.`, `// 2. Apply business rules.`, and `// 3. Persist atomically.`. Restart numbering per workflow; do not number unrelated methods across a class.
- Keep comments current and intent-focused. Do not narrate every line or use numbered comments in AutoMapper profiles.

## Constants

- Name every constant container with the singular suffix `Constant`, for example `ProductErrorCodeConstant`, `ProductPermissionConstant`, `KpiThresholdConstant`, or `AuditActionConstant`.
- Use PascalCase for constant members. A serialized string value may use the contract's stable form such as `PRODUCT_NOT_FOUND`.
- Use `const` for compile-time primitive/string values and `static readonly` otherwise.
- Place a constant beside the capability that owns its vocabulary. A repository may use an established technical `Common/Constants/` folder, but every contained `...Constant` type must remain capability-scoped; do not create one global constants class or mix unrelated vocabularies.
- Use constants for reusable/shared error codes, permissions, statuses, audit vocabulary, and business thresholds. Keep truly local internal non-contractual one-off strings inline.
- Do not localize Angular-facing UI text in the backend. Stable or reused safe fallback response messages may use an owning constant container such as `ProductMessageConstant`; Angular still localizes the stable success/error code and uses the backend message only for unknown-code fallback. Keep truly one-off internal strings inline.

## Enums

- Name enum types with the suffix `Enum`, for example `ProductStatusEnum`.
- Declare every ordinary member with an explicit value spaced by ten: `Draft = 10`, `Active = 20`, `Archived = 30`. Never rely on ordinal assignment, renumber a published member, or reuse a retired number for a different meaning.
- Serialize enums through HTTP as strings and persist them with EF Core string conversion. Configure a suitable column length and migrate stored values when renaming a member.
- Treat an enum-member rename as an API and data migration. Do not silently map unknown API or database values to a default; invalid API input returns 400 Problem Details.
- Do not add `Unknown = 0` unless Unknown is a real domain state.
- Use an enum only for a finite, stable, closed set. Use configurable/reference data when users or operations can add values. Do not use enums for error codes, permission codes, routes, or thresholds.
- Do not introduce `[Flags]` without confirmation and a real independently combinable set. When confirmed, use `None = 0` and powers of two (`1, 2, 4, 8...`) instead of the ordinary increments-of-ten convention.

## Persistence And Concurrency

- Treat EF Core `DbContext` as an existing repository/unit-of-work implementation. Do not wrap simple CRUD mechanically.
- Add a custom repository when persistence logic has meaningful reuse, joins/grouping/aggregation/subqueries, complex dynamic filters/sorts, consistent object-graph or data-scope rules, provider-specific SQL/performance strategy, or it obscures the application service's business flow.
- A normal paged/filter/sort/projection query may remain in the application service in a confirmed N-Layer project. A repository exposes an intent-named operation and never leaks `IQueryable` merely to move composition elsewhere.
- Do not require a custom Unit of Work merely because a repository exists. Add one only when the architecture requires an abstraction or a use case coordinates multiple repositories/persistence operations.
- Translate `DbUpdateConcurrencyException` at the nearest persistence boundary into `ConcurrencyConflictException` with a stable error code such as `ConcurrencyConflict`; the global handler maps it to HTTP 409 Problem Details. Never expose EF/database details.
