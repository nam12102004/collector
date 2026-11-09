---
name: microservice-dotnet-standards
description: Strict topology and ownership profile for independently deployable .NET microservices in a monorepo, including per-service solutions, data isolation, synchronous and asynchronous communication, API Gateway, Nginx public-edge, Kubernetes service exposure, resilience, and contract boundaries. Combine per service with one-project N-Layer, multi-project Clean Architecture, or confirmed large Vertical Slice/CQRS; do not impose one code architecture on every service.
---

# Microservice .NET Standards

Apply `dotnet-backend-standards` plus the confirmed per-service code profile. Apply `event-driven-dotnet-standards` when cross-process messaging is in scope and `deployment-config-standards` for deployment artifacts.

Read the [architecture guide](references/architecture-guide.md) completely when designing, scaffolding, reviewing, or materially changing microservice topology, communication, authentication, data ownership, API versioning, search/index ownership, tenant isolation, realtime boundaries, observability, Kubernetes health, or migrations. Apply the base `platform-capabilities.md` gates before the microservice-specific additions.

## Activation Gate

Activate only for an independently buildable/deployable service with its own ownership and data boundary, or when the user explicitly selects microservices. Multiple projects or APIs alone do not prove microservices.

Microservice is a deployment topology, not a code architecture. Select N-Layer, Clean Architecture, Vertical Slice/CQRS, and DDD independently per service. A small CRUD/integration service should normally remain one-project N-Layer after confirmation; do not impose Clean Architecture or Vertical Slice/CQRS.

Suggest Clean Architecture only with evidence such as meaningful domain rules, aggregate invariants/value objects/domain events, multiple adapters, framework-independent workflows, team boundary needs, or N-Layer mixing business logic with infrastructure. Never use endpoint/entity count alone, and ask before migration.

## Monorepo Shape

```text
repository/
├── src/
│   ├── Ordering/
│   │   ├── Ordering.sln
│   │   └── service projects
│   ├── Inventory/
│   │   ├── Inventory.sln
│   │   └── service projects
│   ├── <Product>.Email/
│   └── <Product>.Ordering.Contracts/
├── tests/
│   ├── Ordering/
│   └── Inventory/
└── deploy/
```

- Keep one Git repository and one solution per service. Do not require a root aggregate solution.
- Keep service folders directly under `src/`; do not add intermediate `Services` or `BuildingBlocks` folders.
- Do not reference another service's API, Application, Domain, Infrastructure, entities, repositories, or DbContext.
- A stable technical class library may sit flat under `src/<Product>.<Capability>/` and be referenced by multiple service solutions after the reusable-library discovery gate passes.
- Place producer-owned message contracts in a focused flat project such as `src/<Product>.Ordering.Contracts/`. Include it in each producer/consumer service solution that needs it and use `ProjectReference`; do not package it as a private NuGet by default. Never create a global `Shared.Contracts` dump.

## Communication

- Use typed resilient HTTP clients for simple synchronous calls that require an immediate result. Name them `I{Service}Client`, set bounded timeouts, propagate cancellation, and apply `Microsoft.Extensions.Http.Resilience` with retries only for safe/idempotent operations.
- Use MassTransit for asynchronous cross-process integration events. Do not use MediatR for durable delivery.
- Add gRPC only after confirmation and a concrete need such as high-throughput strongly typed RPC, streaming, or latency-sensitive internal communication. Do not add it speculatively when HTTP or MassTransit is sufficient.
- Never call another service's database. Treat network dependencies as unavailable/slow/duplicated and design timeout, idempotency, and failure behavior explicitly.
- Use an external OIDC/OAuth 2.0 identity provider. Use client credentials for machine calls and forward a user token only when the downstream service genuinely needs user context.
- Keep Redis/distributed cache, distributed locks, Quartz scheduling, object storage, Elasticsearch, SignalR, multi-tenancy, and external configuration providers conditional. Apply the architecture-neutral gates from `dotnet-backend-standards/references/platform-capabilities.md`, then the ownership/topology additions from the architecture guide.

## Gateway And Public Edge

- When external clients consume multiple services or need a single public contract, use a confirmed application gateway. Prefer YARP for a self-hosted .NET gateway when it adds routing transformations, auth policy, BFF behavior, or aggregation; do not add it for routing that Nginx/Kubernetes already satisfies.
- Keep gateway code free of domain logic and service databases. Gateway authentication does not replace declarative permission/authorization inside each service.
- Nginx is the only public edge. It serves the Angular production build directly, routes `/api/*` to YARP or a single backend, and is the only component publishing public 80/443 ports.
- Angular calls the relative `/api` base URL. Do not bake an environment backend hostname into the production bundle.
- Keep YARP, APIs, service pods, health, metrics, and telemetry private unless an explicitly protected operational route is confirmed.

## Conditional Kubernetes Shape

- Apply this section only when Kubernetes is explicitly in scope.
- Put replicas of one service behind one Kubernetes Service DNS name and service port; point YARP to that stable Service, never to Pod IPs.
- Use readiness to remove unavailable replicas from traffic and liveness only for process health.
- Do not expose every microservice as a public LoadBalancer/NodePort. Expose only the Nginx edge through the selected cluster entry mechanism.

## Completion Gate

- Build each affected service from its own solution. Keep service-scoped tests present, but do not execute tests, deployments, application startup, runtime checks, or browser tooling unless explicitly requested.
- Trace public traffic Nginx -> optional YARP -> Kubernetes/Docker service -> application.
- Verify no cross-service project/data reference, public port, unsafe retry, or contract ownership leak was introduced.
- Hand test/deployment/runtime commands to the user without claiming they passed.
