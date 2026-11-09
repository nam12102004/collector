# N-Layer Architecture Guide

## Purpose And Boundaries

Use N-Layer for a backend whose complexity does not justify separate API/Application/Domain/Infrastructure projects. It remains one deployable project; folders create code-navigation boundaries, not independently compiled dependency boundaries.

```text
src/<Product>.API/
├── Controllers/
├── Services/
├── Interfaces/
│   └── Repositories/    # repository contracts when activated
├── Repositories/        # flat implementations when activated
├── Data/
├── Entities/
├── Contracts/
├── Validators/
└── Mappings/
```

Preserve established folder names and avoid empty ceremonial folders.

When a custom repository is activated in this one-project profile, use this placement:

```text
Interfaces/Repositories/IProductRepository.cs
Repositories/ProductRepository.cs
```

Keep implementations directly under `Repositories/`; do not add an extra implementation subfolder unless the established repository already has one.

## Request Flow

```text
permission attribute
  -> controller binds transport input
  -> one IProductService call
  -> DbContext or intent-named repository
  -> service returns response
  -> controller returns it
```

The controller does not read claims, map DTOs, construct application payloads, access persistence, or catch expected exceptions. It may wrap the service result in the HTTP `SuccessResponse<T>` envelope at the API boundary.

## Persistence Decision

| Situation | Keep DbContext in service | Extract custom repository |
|---|:---:|:---:|
| Simple CRUD | yes | no |
| Paging + ordinary filters/sort/projection | yes | no |
| Reused persistence query | possible | usually |
| Joins/grouping/aggregation/subqueries | possible for small one-off | usually |
| Complex dynamic criteria/data scope | no | yes |
| Provider-specific SQL/performance strategy | no | yes |
| Consistent object-graph loading rule | no | yes |
| Query hides business flow in service | no | yes |

Do not use line count alone. A repository method names intent and returns a bounded result; it does not expose `IQueryable` so the caller still owns all EF composition.

DbContext already provides repository and Unit of Work behavior. Do not wrap `Add`, `Find`, `Update`, `Remove`, or `SaveChanges` mechanically. A custom repository does not automatically require `IUnitOfWork`.

## Service Growth

Start with `IProductService`. Keep it while methods represent one cohesive capability. Split only when dependencies/method groups have independent reasons to change, tests require unrelated setup, or distinct capabilities emerge.

Possible splits include `IProductPricingService`, `IProductImportService`, or—when read/write evolution is truly independent—`IProductCommandService` and `IProductQueryService`. Do not keep a forwarding `IProductService` facade unless it owns real orchestration, transaction, or policy.

Moving query code into a repository only to shorten `ProductService` does not fix a god service.

## Transactions And Concurrency

One `SaveChangesAsync` call is transactional for its normal relational write set. Use an explicit transaction for multiple saves/steps that must be atomic. Do not keep a transaction open over slow network I/O.

Use a concurrency token when two actors can update the same protected state. Translate `DbUpdateConcurrencyException` at the service/repository persistence boundary into `ConcurrencyConflictException` carrying a `...Constant` error code. The global handler returns 409 Problem Details.

## Testing And Review

Add tests for the application service's business behavior and actual exception branches. Use Moq when the service depends on repository interfaces. Use EF Core InMemory only when the service/handler directly depends on DbContext and the behavior does not require relational semantics. Never use it to prove transactions, migrations, foreign keys, uniqueness, provider SQL, or concurrency. Add real-provider tests only when the user explicitly requests them.

Reject these anti-patterns:

- controller business logic;
- repository wrappers with no persistence intent;
- a `ProductService` that owns unrelated capabilities;
- explicit transactions around every CRUD operation;
- pagination performed after materializing an unbounded table;
- EF concurrency exceptions escaping to the API.
