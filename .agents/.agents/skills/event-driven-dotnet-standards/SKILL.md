---
name: event-driven-dotnet-standards
description: Strict profile for cross-process .NET event-driven systems using MassTransit, versioned producer-owned integration contracts, outbox/inbox durability, idempotent consumers, retry/fault handling, choreography, and saga orchestration. Use only when asynchronous integration events or message workflows are confirmed; do not use for ordinary in-process events.
---

# Event-Driven .NET Standards

Apply `dotnet-backend-standards`; combine with `microservice-dotnet-standards` when services are independently deployed.

Read [contracts and delivery](references/contracts-and-delivery.md) completely when defining or changing message contracts, read-model replication, consumer reliability, outbox/inbox, saga behavior, retry, or replay.

## Activation Gate

Activate only for confirmed cross-process messaging, asynchronous integration events, or message workflows. Use MediatR or direct in-process dispatch only for confirmed non-durable in-process events; do not introduce MassTransit for local method decoupling.

## Message Semantics And Naming

- Name facts in past tense with the full suffix, for example `OrderCreatedIntegrationEventV1` and `OrderCreatedDomainEvent`.
- Name requests to perform work imperatively with `Command`, for example `ReserveInventoryCommand`.
- Do not use ambiguous names such as `OrderEvent` or `OrderMessage`.
- Domain events remain in process. Integration events cross process boundaries and contain contract DTO data, never EF entities or Domain objects.
- Put a breaking contract version in the class name (`V1`, `V2`). Keep a published version immutable; publish versions side by side during migration. Additive optional backward-compatible fields may remain in the same version after compatibility review.
- Keep integration contracts in a producer-owned focused contract project or an established producer-owned contract boundary. Consumers reference contracts only, never producer Domain, EF, Application, or Infrastructure objects, and contracts do not accumulate in a global `Shared.Contracts` dump. In a microservice monorepo use flat `src/<Product>.<Producer>.Contracts/`, include that project in each consuming service solution, and consume it with `ProjectReference`; do not create private NuGet packaging by default. In a modular monolith preserve module ownership and create a separate contract project only when external consumers need a stable reference.

## Choreography And Orchestration

- Use choreography for simple event notification where consumers can react independently.
- Consider a MassTransit saga/orchestrator for a multi-step workflow with explicit state, timeout, retry, and compensation.
- Ask before adding a saga/orchestrator. Do not create a central orchestrator for unrelated event flows or hide business logic in broker topology.

## Delivery And Transactions

- Use MassTransit `8.3.x` latest compatible patch only after broker/transport and durability choices are confirmed.
- Use transactional outbox when a database change and integration-event publication must be atomic. Do not publish directly and assume the two operations cannot diverge.
- Make every consumer idempotent using inbox/deduplication, a unique message/business key, or an equally strong repository pattern.
- Do not assume ordering unless the topology and key explicitly guarantee it. Make state transitions concurrency-safe.
- For events that maintain a replicated read model, include a monotonic entity version and apply the ordering/reconciliation rules in [contracts and delivery](references/contracts-and-delivery.md).

## Retry And Fault Handling

- Retry only transient failures with bounded policy. Do not retry malformed contracts, validation failures, permanent business rejection, or unsafe non-idempotent work indefinitely.
- Do not catch, log, and swallow consumer exceptions. Let exhausted failures reach the configured fault/dead-letter path with correlation and telemetry.
- Do not log sensitive message payloads. Record safe identifiers, message type/version, attempt, duration, and outcome.
- Define operational ownership for replay and poison messages before enabling automated replay.

## Completion Gate

- Add or update tests for consumer idempotency, actual retryable/permanent failure branches, version compatibility, and outbox/inbox behavior at the smallest meaningful level; do not execute them unless explicitly requested.
- Verify the event is published only after the source transaction is durably represented and no service/database ownership boundary is crossed.
- Build the affected solution and hand test/runtime commands to the user.
