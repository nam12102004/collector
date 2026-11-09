# Event Contracts And Delivery

## Contract Taxonomy

| Type | Example | Meaning |
|---|---|---|
| Domain event | `OrderConfirmedDomainEvent` | in-process fact emitted by domain state change |
| Integration event | `OrderConfirmedIntegrationEventV1` | cross-process immutable published fact |
| Command | `ReserveInventoryCommand` | request for one receiver/capability to perform work |

Use past tense for events, imperative verbs for commands, and the full suffix. Integration contracts contain serialized DTO values, never EF entities or Domain objects.

## Ownership And Versioning

Keep contracts in a producer-owned focused project or an established producer-owned contract boundary. Consumers reference contracts only, never producer Domain, EF, Application, or Infrastructure projects, and no global `Shared.Contracts` dump owns unrelated messages. In a microservice monorepo use a flat project such as `src/Product.Ordering.Contracts/`, include it in producer and consumer service solutions that need it, and use `ProjectReference`; do not publish a private NuGet by default. In a modular monolith preserve module ownership and create a separate contract project only when external consumers need a stable reference.

Put breaking version in the class name. Keep `V1` immutable after publication. Introduce `V2`, publish side by side, migrate consumers, observe usage, and retire `V1` only after confirmation. Review additive optional fields for serializer/consumer compatibility before keeping the same version.

## Transactional Publication

When a state change and event must agree, persist both the service state and outbox record in the same local database transaction. A publisher later sends the outbox message. Do not commit state and directly publish as two unrelated operations.

Consumers use inbox/deduplication or a unique key so redelivery does not duplicate business effects. Idempotency covers the business write, not only logging that a message was seen.

## Ordering And Replicated State

Message delivery order and entity state order are different concerns.

For an event that updates a local read model, include:

```text
EntityId
EntityVersion   # monotonic per entity/aggregate
business fields required by the consumer
```

The consumer stores the last applied version:

- same version: duplicate, ignore idempotently;
- lower version: stale, do not overwrite newer state;
- next expected version: apply atomically;
- unexpected gap: record telemetry and invoke the confirmed reconciliation mechanism.

Do not use `OccurredAtUtc` for ordering because clocks can differ. Use `MessageId` for delivery deduplication and `EntityVersion` for state ordering. Do not add entity versions to unrelated fire-and-forget notifications.

## Choreography, Saga, And Compensation

Use choreography for independent reactions to a fact. Use a saga/orchestrator only when a workflow requires durable state, timeout, retry, and compensation across steps. A saga does not create a distributed ACID transaction; every service still commits locally and compensates explicitly.

Ask before adding a saga. Name compensation actions in business terms and design them for idempotent retry.

## Retry And Fault Classification

Retry transient network, broker, lock, or dependency failures with bounded backoff. Do not repeatedly retry malformed contracts, validation failures, missing permanent configuration, or final business rejection.

Do not catch and swallow consumer exceptions. Route exhausted failures to the configured fault/dead-letter flow with trace/correlation context and operational ownership. Define who can replay, how idempotency is preserved, and how poison messages are quarantined.

## Verification Checklist

- Is the message a fact, command, or in-process domain event with the correct name?
- Is the producer and version owner clear?
- Can database state commit without losing the required event?
- Can a duplicate message repeat any side effect?
- Is ordering actually required, and is entity version handled when it is?
- Are permanent failures excluded from unbounded retry?
- Is fault/replay ownership defined without sensitive payload logging?
