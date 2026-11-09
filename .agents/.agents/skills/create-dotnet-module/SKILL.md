---
name: create-dotnet-module
description: Strict workflow for adding a new bounded-context module to an existing .NET modular monolith. Use only when the user explicitly requests a new module and the repository already has a modular-monolith host and module conventions. Do not trigger for an ordinary feature, business-area folder, single-project layered monolith, vertical slice, or architecture migration that was not explicitly requested.
---

# Create .NET Module

Apply `dotnet-backend-standards` and `modular-dotnet-standards` with this workflow.

## Activation Gate

Before creating files, require both:

1. explicit user scope for a new bounded-context module; and
2. repository evidence for an existing modular-monolith host and at least one canonical module.

If either condition fails, do not scaffold a module. Use `create-dotnet-feature` for normal feature work or request architecture direction when module creation is genuinely required.

## 1. Discover Local Shape

1. Find the solution file, host API project, any minimal shared kernel used by canonical modules, existing modules, migration scripts, and test layout.
2. Copy naming and package references from the nearest existing module.
3. Pick the module name from the bounded context, not a technical layer.
4. Record the canonical module, host registration point, persistence ownership, migration command, and test convention before scaffolding.

## 2. Create Project And Folders

Create a class library project under the repo's module path, commonly:

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

Reference a shared-kernel project only when canonical modules use it and the new module needs one of its confirmed stable shared domain primitives. Do not reference it merely because it exists. Add package references only when not inherited centrally.

Reuse packages already selected by the canonical module. Ask before adding, removing, or replacing a dependency.

## 3. Add Global Usings

Follow existing repo style. Typical imports include ASP.NET versioning, AutoMapper, FluentValidation, EF Core, Microsoft configuration/DI/logging/options, and module Domain/Application namespaces. Include shared-kernel namespaces only when the optional reference gate above passes. Import/register MediatR only when it is already the confirmed module communication mechanism or its activation and v13+ license gates have passed; never add it merely because this workflow mentions it.

## 4. Add Module Registration

Create `{ModuleName}Module.cs`:

```csharp
namespace {Company}.{ModuleName};

public static class {ModuleName}Module
{
    public static IServiceCollection Add{ModuleName}Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof({ModuleName}Module).Assembly);

        return services;
    }
}
```

Add DbContext, repositories, Unit of Work, options, services, handlers, and clients using the repo's existing extension patterns.

Register AutoMapper once at the host/composition root. Extend the established central assembly scan with the new module marker instead of calling `AddAutoMapper` independently in every module. For AutoMapper 15 or later, preserve the required configuration action, for example `services.AddAutoMapper(configuration => { }, typeof(ExistingModule).Assembly, typeof({ModuleName}Module).Assembly);`. Obtain the license through the confirmed configuration path such as `AUTOMAPPER_LICENSE_KEY`; never hardcode it.

## 5. Add DbContext

Create `{ModuleName}DbContext` under `Infrastructure/Persistence/`:

```csharp
namespace {Company}.{ModuleName}.Infrastructure.Persistence;

public sealed class {ModuleName}DbContext(
    DbContextOptions<{ModuleName}DbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({ModuleName}DbContext).Assembly);
    }
}
```

Add `DbSet<T>` declarations if the repo uses them explicitly.

## 6. Wire Host

In the host API project:

- Reference the new module project.
- Register module DbContext using existing database extension/options.
- Add controller assembly scanning with `AddApplicationPart`, or call the module endpoint mapper if using Minimal APIs.
- Call `services.Add{ModuleName}Module(configuration)`.
- Add the module marker assembly to the host's single central AutoMapper registration, preserving its existing configuration and scan list.
- Add module migrations to dev startup only if existing modules do so.
- Add module to migration scripts or tooling map.

Keep host code as wiring only. Do not place module business logic in the host.

## 7. Add Tests

Create a test project or test folder matching local convention. Add at least a smoke test for DI registration when that pattern exists, plus focused tests as features are added.

## 8. Verify

- Build solution.
- Do not run focused tests, start the application, open Swagger, or launch browser/runtime tooling unless explicitly requested.
- When the module's EF Core model/schema changes, generate its migration through the repository script or EF CLI using that module's DbContext and migration project; never hand-create migration, designer, or snapshot files. Generate an empty migration only when the repository process requires it. Inspect generated operations and do not apply migrations as verification.
- Inspect controller/endpoint discovery and host registration statically.
- Confirm no forbidden project reference or cross-module persistence dependency was introduced.
- Report the build result and the test/Swagger/runtime checks left to the user.
