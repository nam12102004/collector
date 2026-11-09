---
name: n-layer-dotnet-standards
description: Strict profile for a confirmed single-project .NET N-Layer backend using controllers, application services, conditional repositories, EF Core, validation, and focused tests. Use only when the user selects N-Layer or repository evidence proves one deployable backend project organized by technical layers. Do not use for Clean Architecture, modular monoliths, or vertical slices.
---

# N-Layer .NET Standards

Apply `dotnet-backend-standards` together with this profile.

Read the [architecture guide](references/architecture-guide.md) completely when selecting N-Layer, scaffolding its project shape, deciding direct DbContext versus Repository/Unit of Work, splitting a service, or reviewing transaction/concurrency behavior.

## Activation Gate

Activate only when the user confirms N-Layer or evidence shows one backend project with technical layers and a shared application/persistence boundary. Existing folder names may vary; preserve them. Do not activate because a controller and service happen to exist inside another architecture.

For a new project, N-Layer means one backend project. If the user wants API/Application/Domain/Infrastructure projects, use `clean-dotnet-standards` instead.

## Responsibility Flow

```text
Controller -> Application Service -> DbContext or custom Repository
```

- Keep controller actions to transport binding, one service call, and returning its result. Permission is declarative metadata only.
- Default to `IProductService` / `ProductService` for an ordinary feature. Apply the god-service split rules from `dotnet-backend-standards/references/csharp-conventions.md` only when evidence warrants them.
- Keep request/response contracts, validators, mapping, entities, persistence, and services in their established technical folders. Do not create feature-within-feature folder repetition or empty folders.

## Persistence Choice

- For simple CRUD and an ordinary paged/filter/sort/projection query, inject the DbContext directly into the application service when that matches the project.
- Do not create a generic repository that merely wraps EF Core CRUD or leaks `IQueryable`.
- Add an intent-named custom repository when query/write persistence has meaningful reuse, complex joins/grouping/aggregation/subqueries, dynamic criteria, data-scope/object-graph rules, provider-specific optimization, or obscures the service's business flow.
- In this one-project N-Layer profile, place repository interfaces under `Interfaces/Repositories/` and keep implementations flat under `Repositories/`. Do not introduce Clean Architecture project boundaries merely to achieve this placement.
- Do not use line count alone. Pagination plus normal filtering is not sufficient by itself.
- Treat DbContext as the default Unit of Work. Add a custom Unit of Work only for an actual abstraction/coordination requirement; a custom repository does not automatically require one.

## Transactions And Concurrency

- Rely on one `SaveChangesAsync` transaction for one logical write when sufficient.
- Use an explicit transaction when a use case requires multiple saves/steps to commit atomically. Do not hold it open across a slow network call.
- Use concurrency tokens for real concurrent-update risk. Translate `DbUpdateConcurrencyException` into `ConcurrencyConflictException` with a `...Constant` error code; return 409 through the global handler.

## Completion Gate

- Add or update tests for the application service and its actual failure branches/exceptions; do not execute them unless explicitly requested.
- Use Moq for a service that depends on repository interfaces. Use EF Core InMemory only when the service/handler directly depends on DbContext and the tested behavior does not require relational-provider semantics.
- Verify controller thinness, bounded queries, transaction behavior, concurrency mapping, and the selected persistence boundary.
- Build the affected solution and hand test/runtime commands to the user.
