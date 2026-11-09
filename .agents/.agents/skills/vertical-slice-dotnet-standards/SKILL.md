---
name: vertical-slice-dotnet-standards
description: Strict profile for a confirmed large .NET application organized by use-case slices and CQRS, with request/handler/validator/result/tests colocated and no speculative generic service/repository layers. Use only when the user explicitly selects Vertical Slice/CQRS or repository evidence proves it; do not use for ordinary small CRUD.
---

# Vertical Slice .NET Standards

Apply `dotnet-backend-standards` together with this profile.

Read the [architecture guide](references/architecture-guide.md) completely when selecting Vertical Slice/CQRS, defining slice contents, choosing MediatR, sharing code, or reviewing slice boundaries.

## Activation Gate

Activate only when the user selects Vertical Slice/CQRS or a large established application clearly organizes code by independent command/query use cases. Do not introduce it for a small CRUD application or merely because a feature folder exists.

Use evidence such as independently changing use cases, materially different read/write models, many workflows, or an existing request/handler pipeline. Ask before migration.

## Slice Shape

```text
Features/
└── Products/
    ├── CreateProduct/
    │   ├── CreateProductCommand.cs
    │   ├── CreateProductHandler.cs
    │   ├── CreateProductValidator.cs
    │   └── CreateProductResponse.cs
    └── GetProducts/
        ├── GetProductsQuery.cs
        ├── GetProductsHandler.cs
        └── GetProductsResponse.cs
```

- Keep request, validation, handler, result, mapping, and focused tests with the use case according to repository convention.
- A controller/endpoint binds transport data, invokes exactly one handler/use case, and returns the result. Permission remains declarative.
- Do not add `ProductService`, generic repositories, or shared base handlers merely to imitate another architecture.
- Share only real policy/infrastructure reused across slices; do not erase slice ownership with catch-all abstractions.

## CQRS And Mediation

- Separate commands and queries when their models, dependencies, or change patterns justify it; do not create ceremonial pairs for trivial operations.
- MediatR is not required by Vertical Slice. Add it only after confirmation, the dependency-catalog activation gate passes, and v13+ Community/commercial license eligibility is confirmed. Do not silently pin v12 merely to bypass that decision.
- A handler may use DbContext directly when the architecture permits it. Extract an intent-named repository only for a real persistence boundary, not for wrapping EF Core.

## Completion Gate

- Add or update tests for handlers/use cases and their actual exception/failure branches; do not execute them unless explicitly requested.
- Verify each slice owns its contract and does not bypass authorization, transaction, concurrency, or data-bound rules.
- Build the affected solution and hand test/runtime commands to the user.
