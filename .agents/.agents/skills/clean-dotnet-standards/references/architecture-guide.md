# Clean Architecture Guide

## Purpose And Activation

Use Clean Architecture when separate compiled boundaries protect meaningful application/domain logic from frameworks and adapters. Do not select it merely because a service has several entities or endpoints.

Strong reasons include complex business rules, multiple adapters, framework-independent workflows, team ownership boundaries, or tests repeatedly obstructed by infrastructure coupling.

## Project And Dependency Shape

```text
src/
├── Product.API/
├── Product.Application/
├── Product.Domain/
└── Product.Infrastructure/
```

```text
API -> Application
Infrastructure -> Application + Domain
Application -> Domain
Domain -> nothing outward
```

Domain and Application do not reference ASP.NET Core, EF Core, broker SDKs, Infrastructure, or provider models. API is the transport/composition root. Infrastructure owns DbContext, EF mappings, migrations, and external provider clients.

## Internal Technical Layout

Use technical responsibility as the first folder boundary inside each project:

| Project | Required placement when applicable |
|---|---|
| API | `Controllers/`, `Common/`, `Configurations/` |
| Application | `Common/Constants/`, `Contracts/`, `Interfaces/`, `Services/`, `Validators/`, `Mappings/` |
| Domain | `Common/`, `Entities/`, `Enums/`, `Interfaces/Repositories/` |
| Infrastructure | `Persistence/`, `Repositories/`, `Services/` |

Keep `DependencyInjection.cs` at the Application or Infrastructure project root. Keep `Program.cs` at the API root. Put DbContext, EF mappings, migrations, and interceptors below `Infrastructure/Persistence/`.

Place Application-owned constant containers in `Application/Common/Constants/`; keep the types capability-scoped. When a framework-independent typed-exception hierarchy is reused across repositories, consume a focused package and keep product-specific error vocabularies in the consuming Application constants folder.

Do not place a capability directly at a project root (`Application/Leads/`, `Domain/Privacy/`, or `Infrastructure/Customers/`). If a technical folder becomes crowded, group by capability one level below it, such as `Services/Leads/` or `Contracts/Privacy/`. Omit unused folders rather than creating placeholders.

External-system integrations (an HTTP client plus its own connection/auth options adapting to one named outside system, e.g. an HRM directory API or the OIDC identity provider) are services, not a separate root: place them under `Infrastructure/Services/`, nested by capability (`Services/Hrm/`, `Services/Authorization/`) once file volume justifies it, and name the adapter type with a `...Service` suffix rather than `...Client`. This applies uniformly, including to authorization/identity-provider adapters — there is no first-level `Infrastructure/Authorization/` exception.

For an established repository with a different working layout, preserve it and ask before migration. For a new project, or after explicit migration approval, this technical layout is mandatory.

## End-To-End Flow

```text
Controller
  -> IProductService (Application)
  -> IProductRepository (Domain contract)
  -> ProductRepository + DbContext (Infrastructure)
  -> response contract (Application)
```

Keep the controller to declarative permission, binding, one service call, and returning its result.

## Repository Ownership

Place aggregate repository interfaces in Domain because they express how the application persists/retrieves domain-owned aggregates. Infrastructure implements them.

Domain MUST NOT reference `ProductFilterParams` or response DTOs from Application. Application owns transport/use-case filter contracts and translates them into domain-owned criteria or intent-named repository parameters.

```text
ProductFilterParams (Application)
  -> ProductSearchCriteria (Domain, only when domain meaningful)
  -> IProductRepository.SearchPagedAsync(...)
```

Do not create a generic `IQueryable` port. Do not put UI-specific projections or HTTP types in Domain. Prefer clear intent and accept that highly optimized read paths may need a separately confirmed design rather than violating dependency direction silently.

## Services And Unit Of Work

Default to `IProductService` in Application for ordinary command/query behavior. Split only when god-service evidence appears. Do not apply CQRS/Vertical Slice automatically.

Do not create `IUnitOfWork` merely because DbContext exists. Add a Domain/Application-visible unit-of-work abstraction only when a use case must coordinate multiple repositories/persistence actions and the boundary adds real value.

## DDD Independence

A Domain project does not prove DDD. Use simple entities for CRUD. Activate `ddd-dotnet-standards` only when invariants, aggregates, value objects, domain events, or ubiquitous-language complexity exists.

## Verification Checklist

- Do first-level project folders follow the technical layout without feature-first roots?
- Are project references inward-only?
- Are EF/ASP.NET/provider packages absent from Domain/Application?
- Are repository interfaces in Domain and implementations in Infrastructure?
- Are Application filters/responses kept out of Domain?
- Does API contain only transport/composition behavior?
- Is Clean Architecture justified by complexity rather than ceremony?
