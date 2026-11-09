---
name: ddd-dotnet-standards
description: Strict evidence-gated Domain-Driven Design profile for .NET domains with aggregates, invariants, value objects, domain events, bounded contexts, and ubiquitous language. Combine with Clean Architecture, modular monolith, or microservice profiles only when genuine domain complexity exists; do not add DDD ceremony to simple CRUD.
---

# DDD .NET Standards

Apply `dotnet-backend-standards` plus the confirmed code/deployment profile.

Read the [modeling guide](references/modeling-guide.md) completely when deciding whether DDD applies, defining aggregates/value objects/domain events, placing repositories, or reviewing an existing domain model.

## Activation Gate

Activate only when domain complexity is proven by aggregate invariants, interacting business rules, value concepts with behavior, domain events, bounded-context language, or lifecycle/state transitions that a CRUD model cannot safely express. Do not activate because a project has a Domain folder or because Clean Architecture/microservices were selected.

Report the evidence and ask before introducing DDD into an existing CRUD model.

## Modeling Rules

- Use the business's ubiquitous language in types and operations. Do not invent generic pattern names that obscure domain meaning.
- Define aggregate boundaries from invariants and transactional consistency, not database relationships.
- Change aggregate state through behavior that enforces invariants; avoid public setters that permit invalid state.
- Use a value object only when equality, validation, normalization, or domain behavior makes it more than a renamed primitive.
- Keep cross-aggregate consistency eventual unless a confirmed invariant requires one transaction within one ownership boundary.
- Place aggregate repository interfaces in Domain and implementations in Infrastructure. Repositories work with aggregate roots and intent-named operations; they do not expose IQueryable or transport DTOs.

## Events

- Name domain events in past tense with `DomainEvent`, for example `OrderConfirmedDomainEvent`.
- Raise domain events only for meaningful state changes with real in-process consumers. Do not create events for every setter/CRUD operation.
- Dispatch in-process domain events after the source transaction commits when side effects must observe committed state. Treat them as non-durable.
- Translate a domain event into a versioned integration event through the application/infrastructure boundary when cross-process delivery is required. Use MassTransit plus outbox rather than publishing the Domain object.

## Boundaries

- DDD is a modeling profile, not a required folder/project topology. It may compose with Clean Architecture, an established modular monolith, or a microservice.
- Keep Application responsible for use-case orchestration and transport mapping; keep Domain free of HTTP, EF Core, broker, and UI concerns.
- Do not create an anemic `DomainService` dumping ground. Use an entity/value object when behavior belongs there and a narrowly named domain service only for a domain operation that has no natural owner.

## Completion Gate

- Add or update Domain-level tests for invariants and state transitions without framework dependencies; do not execute them unless explicitly requested.
- Verify aggregate boundaries, transaction scope, repository ownership, and domain/integration-event separation against real business language.
- Build the affected solution and hand test/runtime commands to the user.
