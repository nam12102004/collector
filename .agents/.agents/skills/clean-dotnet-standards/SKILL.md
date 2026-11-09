---
name: clean-dotnet-standards
description: Strict profile for a confirmed multi-project .NET Clean Architecture backend with inward dependencies, mandatory technical-folder layouts inside API/Application/Domain/Infrastructure, framework-independent Domain/Application projects, Domain-owned repository interfaces, Infrastructure adapters, thin APIs, and application services. Use only when the user selects Clean Architecture or repository evidence proves these boundaries.
---

# Clean .NET Standards

Apply `dotnet-backend-standards` together with this profile.

Read the [architecture guide](references/architecture-guide.md) completely when selecting Clean Architecture, scaffolding project boundaries, placing repository interfaces, mapping filters/contracts, or reviewing dependency direction.

## Activation Gate

Activate only when the user confirms Clean Architecture or evidence proves inward dependencies across API, Application, Domain, and Infrastructure. Do not migrate a one-project N-Layer service or ordinary CRUD feature without confirmation.

## New Project Shape

```text
src/
├── <Product>.API/
├── <Product>.Application/
├── <Product>.Domain/
└── <Product>.Infrastructure/
```

Preserve established names in an existing repository.

## Internal Project Layout

For a new project, organize each compiled project by technical responsibility. Use this first-level layout and omit folders that have no real files:

```text
<Product>.API/
|- Controllers/
|- Common/
|- Configurations/
`- Program.cs

<Product>.Application/
|- Common/Constants/
|- Contracts/
|- Interfaces/
|- Services/
|- Validators/
|- Mappings/
`- DependencyInjection.cs

<Product>.Domain/
|- Common/
|- Entities/
|- Enums/
`- Interfaces/Repositories/

<Product>.Infrastructure/
|- Persistence/
|- Repositories/
|- Services/
`- DependencyInjection.cs
```

- Treat these technical folders as the default first-level organization. Do not create feature-first roots such as `Application/Leads`, `Domain/Customers`, or `Infrastructure/Privacy`. This also covers external-system integrations, including identity-provider/auth adapters: an HTTP client (or claims-transformation/token adapter) plus its own connection/auth options for one named outside system is still a *service*, not its own first-level subsystem — it belongs under `Infrastructure/Services/{Name}/` (e.g. `Services/Hrm/`, `Services/Authorization/`), and the adapter type takes a `...Service` suffix, not `...Client`. There is no first-level exception for any integration, including the identity provider.
- When file volume requires capability grouping, nest it below the technical responsibility, for example `Contracts/Leads/`, `Services/Leads/`, or `Entities/Sales/`; do not invert this to `Leads/Contracts/`.
- Place Application-owned constant containers under `Application/Common/Constants/`. Keep each `...Constant` type scoped to one vocabulary/capability; do not combine unrelated values into a generic constants class.
- Place service interfaces and Application-owned ports in `Application/Interfaces/`; place their implementations in `Application/Services/` or the owning Infrastructure technical folder.
- Place aggregate repository interfaces in `Domain/Interfaces/Repositories/` and implementations directly in `Infrastructure/Repositories/`.
- Keep EF Core configuration, DbContext, migrations, and persistence interceptors under `Infrastructure/Persistence/`.
- Keep authorization adapters under `Infrastructure/Services/Authorization/` — an identity-provider bridge is an integration like any other, not a first-level folder of its own. When the reusable building-block gate passes, place framework-independent authorization contracts (e.g. `ICurrentUserContext`) in a flat standalone capability project or confirmed cross-repository package; keep CRM-specific DbContext/identity-provider adapters in `Infrastructure/Services/Authorization/`.
- Reuse or extract a framework-independent application-exception package when the building-block gate passes. Keep product-specific error-code constants in `Application/Common/Constants/` and compose them with reusable typed exceptions instead of putting product rules in the shared package.
- Do not create empty ceremonial folders. Preserve a different established internal layout in an existing repository unless the user explicitly requests migration.

## Dependency Direction

```text
API -> Application
Infrastructure -> Application + Domain
Application -> Domain
Domain -> no outer project
```

- Domain and Application MUST NOT reference EF Core, ASP.NET Core, transport models, Infrastructure, or provider SDKs.
- API is transport and composition root: declarative permission, binding, one application-service call, result.
- Infrastructure implements persistence and external-client ports and owns EF mappings, DbContext, migrations, and provider configuration.

## Services And Repositories

- Default to `IProductService` in Application for ordinary commands and queries. Split only under the evidence rules in `csharp-conventions.md`.
- Place aggregate repository interfaces such as `IProductRepository` in Domain and implementations in Infrastructure.
- Keep Application `ProductFilterParams` and response contracts out of Domain. Translate them to domain-owned search criteria or intent-named repository parameters; Domain MUST NOT reference Application DTOs.
- Repository operations express domain/persistence intent and never expose `IQueryable` or Infrastructure types.
- Do not add a custom Unit of Work automatically. Define it only when Application must coordinate multiple repositories/persistence operations behind an abstraction.

## Domain Scope

- Clean Architecture does not automatically activate DDD. Use simple entities and application services for CRUD unless `ddd-dotnet-standards` independently passes.
- Keep validation of request shape in Application and real invariants in Domain when a domain model exists.

## Completion Gate

- Verify that first-level folders follow the confirmed technical layout and that feature-first roots were not introduced.
- Verify project references against the dependency diagram.
- Add or update tests for Application services and Domain rules without provider dependencies. Add provider-level tests only when the user explicitly requests them.
- Do not execute tests, startup, runtime checks, or browser tooling unless explicitly requested; build the affected solution and hand test commands to the user.
