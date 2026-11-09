# DDD Modeling Guide

## Activation Decision

DDD is justified by domain complexity, not folder structure.

| Evidence | DDD signal |
|---|---|
| CRUD with field validation | weak; keep application service/entity model |
| Several interacting invariants | strong |
| Lifecycle/state transitions with protected rules | strong |
| Value concepts with equality/normalization/behavior | strong |
| Distinct bounded-context language | strong |
| Event required for meaningful domain reaction | possible |
| `Domain` project exists | none by itself |

Ask before introducing DDD into an existing CRUD model.

## Aggregate Boundaries

An aggregate is a transactional consistency boundary. Put together only state that must satisfy invariants atomically. Database foreign keys, UI screens, and object-navigation convenience do not define an aggregate.

Expose behavior that preserves invariants:

```text
order.Confirm(...)
order.Cancel(reason)
```

Avoid public setters that permit invalid transitions. Reference another aggregate by identity unless a confirmed invariant requires stronger local ownership.

## Value Objects

Create a value object when validation, normalization, equality, or behavior matters—for example Money, EmailAddress, or DateRange. Do not wrap every primitive solely to claim DDD compliance.

## Repositories And Services

Place aggregate repository interfaces in Domain and implementations in Infrastructure. A repository works with aggregate roots and intent-named operations, not transport DTOs or `IQueryable`.

Put behavior on the entity/value object when it naturally belongs there. Use a narrowly named domain service only when an operation is genuinely domain behavior but has no natural aggregate/value-object owner. Do not create a generic `DomainService` dumping ground.

Application services orchestrate use cases, transactions, external ports, and mapping. They do not duplicate aggregate invariants.

## Domain And Integration Events

Name an in-process fact `...DomainEvent`. Raise it only for a meaningful state change with a real consumer. Dispatch after the source commit when consumers must observe committed state; delivery is not durable.

For cross-process delivery, translate to a versioned `...IntegrationEventV1` outside the Domain and publish through MassTransit/outbox. Never serialize the Domain event or aggregate directly as an integration contract.

## Composition

DDD may compose with Clean Architecture, an established modular monolith, or microservices. It does not prescribe one deployment topology. A small microservice may use no DDD; a complex module inside a monolith may use it.

## Review Checklist

- Is DDD supported by real domain complexity?
- Does each aggregate boundary match an invariant/transaction boundary?
- Can public APIs place the aggregate in an invalid state?
- Do value objects have semantic value?
- Are repository interfaces Domain-owned and aggregate-focused?
- Are Domain and integration events distinct?
- Is Application orchestration separate from Domain rules?
