# Vertical Slice And CQRS Guide

## When It Fits

Use Vertical Slice/CQRS for a large application whose use cases evolve independently and whose read/write models or dependencies differ materially. Do not introduce it for small CRUD to create one command/query/handler per trivial operation.

Strong evidence includes many distinct workflows, existing request/handler conventions, independently changing read/write paths, or a service layer that can no longer express clear use-case boundaries.

## Slice Ownership

```text
Features/Products/
├── CreateProduct/
│   ├── CreateProductCommand.cs
│   ├── CreateProductHandler.cs
│   ├── CreateProductValidator.cs
│   ├── CreateProductResponse.cs
│   └── CreateProductHandlerTests.cs
└── GetProducts/
    ├── GetProductsQuery.cs
    ├── GetProductsHandler.cs
    ├── GetProductsResponse.cs
    └── GetProductsHandlerTests.cs
```

A slice owns its request/use-case contract, validation, handler, mapping/result, and focused tests according to repository convention. Keep authorization, transaction, concurrency, and bounded-query guarantees inside the use-case flow.

Controller/endpoint flow remains:

```text
declarative permission -> bind -> one handler/use-case -> return result
```

## CQRS And MediatR

CQRS means command and query models can differ; it does not require separate databases, event sourcing, or MediatR. MediatR is optional and ask-first. Add the latest stable compatible version only when the repository needs a mediator pipeline or confirmed in-process event/CQRS dispatch. For v13 or later, confirm official Community License eligibility or a commercial license before adding the package; never silently downgrade to v12 to avoid that confirmation.

A handler may use DbContext directly when that is the selected pattern. Add a custom repository only for real persistence intent/complexity. Do not add `ProductService`, generic repositories, base handlers, or behaviors solely to reproduce N-Layer inside each slice.

## Shared Code

Share stable cross-slice policy/infrastructure, not arbitrary code with similar syntax. Duplication of a few mapping lines can be safer than coupling independent use cases through a premature abstraction. Extract when semantics and change reasons are genuinely shared.

## Migration Signals

Ask before converting N-Layer to Vertical Slice. Report concrete evidence: unrelated service dependencies, independent use-case changes, divergent read/write models, test setup pressure, or handler conventions already emerging. Do not use file length or entity count alone.

## Review Checklist

- Does each slice have a real use-case boundary?
- Is controller/endpoint behavior transport-only?
- Is MediatR justified, confirmed, and licensed for v13+ when applicable?
- Are persistence abstractions intentional rather than ceremonial?
- Are shared abstractions semantically stable across slices?
- Are small CRUD features being over-modeled?
