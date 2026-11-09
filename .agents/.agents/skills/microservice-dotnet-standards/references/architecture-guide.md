# Microservice Architecture Guide

## Contents

- Decision Model
- Repository And Solution Shape
- Per-Service Code Architecture
- Public Edge, Gateway, And BFF
- Authentication And Authorization
- HTTP API Contracts And Versioning
- Communication Selection
- Data Ownership And Distributed Consistency
- Local Read Models
- Caching And Distributed Coordination
- Background And Scheduled Jobs
- Object Storage And Large Uploads
- Search And Index Ownership
- Multi-Tenancy
- Realtime UI
- Observability And Correlation
- Kubernetes Networking And Health
- Database Migrations
- Anti-Patterns
- Review Checklist

## Decision Model

Treat these as separate decisions:

| Dimension | Typical choices | Governing question |
|---|---|---|
| Deployment | monolith, microservices | Must this service build, deploy, scale, and fail independently? |
| Code organization | N-Layer, Clean, Vertical Slice | How much internal boundary enforcement does this service need? |
| Domain modeling | CRUD/application service, DDD | Are there real aggregate invariants and interacting business rules? |
| Integration | HTTP, MassTransit, gRPC | Is the interaction synchronous, asynchronous, streaming, or latency-sensitive? |

Do not select Clean Architecture, DDD, CQRS, event-driven messaging, or gRPC merely because microservices were selected.

## Repository And Solution Shape

Use one Git monorepo with one solution per service and a flat `src/` boundary:

```text
repository/
├── src/
│   ├── Ordering/
│   │   ├── Ordering.sln
│   │   └── Ordering.API/                 # or Clean projects when justified
│   ├── Inventory/
│   │   ├── Inventory.sln
│   │   └── Inventory.API/
│   ├── Product.Email/                    # focused technical library
│   └── Product.Ordering.Contracts/       # producer-owned messages
├── tests/
│   ├── Ordering/
│   └── Inventory/
└── deploy/
```

Also keep `src/<Product>.WebApp/` in this flat product source boundary. Add `src/<Product>.Gateway/` with its own solution/projects only when a YARP gateway or BFF is confirmed and independently deployable. Never substitute generic `frontend`, `web`, `client`, `gateway`, or `api-gateway` folder names.

Do not add `src/Services/`, `src/BuildingBlocks/`, `Shared`, or `Common`. Include each required flat producer-owned contract project in the producer and consuming service solutions and consume it with `ProjectReference`; do not create a private package by default. A service solution MUST NOT reference another service's API, Application, Domain, Infrastructure, entities, repositories, or DbContext.

## Per-Service Code Architecture

Use one-project N-Layer for a small CRUD/integration service. Consider Clean Architecture only when business rules, multiple adapters, framework-independent workflows, team boundaries, or test pressure make project boundaries valuable. Use Vertical Slice/CQRS only for a confirmed large use-case-oriented application after its own activation gate passes; ordinary feature folders do not prove it. Apply DDD only when its independent activation gate passes.

Example mixed monorepo:

```text
src/
├── Notifications/              # one-project N-Layer
│   ├── Notifications.sln
│   └── Notifications.API/
└── Ordering/                   # Clean Architecture + possible DDD
    ├── Ordering.sln
    ├── Ordering.API/
    ├── Ordering.Application/
    ├── Ordering.Domain/
    └── Ordering.Infrastructure/
```

Do not standardize every service on the most complex service's architecture.

## Public Edge, Gateway, And BFF

Use this production flow:

```text
Internet -> Nginx -> Angular static files
                  -> /api -> single API or private YARP -> service DNS
```

Nginx is the only public component and publishes 80/443. It serves the Angular build directly. Angular uses relative `/api`; environment-specific backend hosts belong in Nginx/deployment configuration, not the compiled frontend.

Use YARP only when a .NET application gateway adds routing transformations, authentication policy, rate limiting, BFF routing, or another confirmed application-level concern. Keep YARP free of databases, domain rules, and distributed workflow logic.

Use a BFF only when a frontend-specific contract or significant aggregation reduces chattiness or hides backend topology. A BFF may compose responses and map view models. It MUST NOT own core business rules, service databases, distributed transactions, or saga responsibility. If Angular can call a few stable gateway routes without a material problem, omit the BFF.

## Authentication And Authorization

Use an external OIDC/OAuth 2.0 identity provider such as the provider confirmed for the project. Do not create a token/password service by default.

```text
IdP issues token
  -> optional gateway validation
  -> target service validates issuer, audience, signature, and expiry
  -> controller permission attribute
  -> resource-level authorization in the owning service/policy
```

- Gateway authentication is defense at the public boundary, not a replacement for service validation and authorization.
- Keep claims/current-user and permission resolution behind the focused reusable authorization library. Controllers do not read claims.
- Never trust public `X-User-Id`, `X-Roles`, or similar identity headers.
- Use OAuth client credentials for machine-to-machine HTTP calls.
- Forward an end-user token only when the downstream service must make a user-context decision. Do not propagate it through every hop by habit.
- Give services explicit audiences/scopes. A background consumer acts as a service identity; it does not impersonate a user unless a designed delegated flow requires it.

## HTTP API Contracts And Versioning

Use `Asp.Versioning.Mvc` for controllers and `Asp.Versioning.Mvc.ApiExplorer` for API discovery/OpenAPI. Use the latest stable versions compatible with the target .NET version. Do not use the superseded `Microsoft.AspNetCore.Mvc.Versioning` package for new code.

Use URL-segment versioning:

```csharp
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsController : ControllerBase;
```

- Require the client to send a version; do not enable `AssumeDefaultVersionWhenUnspecified`.
- Generate an OpenAPI document per version.
- Add a version for a breaking contract change; keep versions side by side while consumers migrate.
- Do not increment for an additive backward-compatible change without a concrete compatibility reason.
- YARP routes the versioned URI; it does not silently transform one contract version into another.

## Communication Selection

| Need | Choose | Rule |
|---|---|---|
| Immediate simple result | typed HTTP client | bounded timeout, cancellation, resilience, safe retry only |
| Fact/event across processes | MassTransit | versioned contract, outbox/inbox when durability matters |
| Strongly typed high-throughput RPC or streaming | gRPC | ask first; require concrete evidence |
| Multi-step durable workflow | MassTransit saga | explicit state, timeout, retry, compensation; ask first |

Do not use events to imitate an RPC whose caller genuinely needs an immediate answer. Do not use synchronous HTTP for a notification that should survive temporary consumer unavailability. Do not add gRPC when HTTP or MassTransit already fits.

## Data Ownership And Distributed Consistency

Give every service its own logical database and database user. Multiple service databases may share one physical database server/cluster. A shared server does not permit cross-database queries, shared credentials, shared DbContexts, or shared migrations.

Atomic transactions stop at the service database boundary. Do not use two-phase commit by default. Use eventual consistency, outbox/inbox, idempotent retry, and compensation/saga for cross-service workflows.

Apply the base retention/soft-delete/hard-delete rules per service-owned data category. Each service purges its own database, files, indexes, and read models; a coordinator may publish a minimal versioned deletion request/event but never deletes another service's data directly. Track completion, retry, and reconciliation when a user/account deletion spans services.

```text
Ordering transaction commits order + outbox record
  -> publishes OrderCreatedIntegrationEventV1
  -> Inventory applies idempotently
  -> failure retries or enters compensation/fault flow
```

Never rollback or mutate another service's database directly.

## Local Read Models

Choose the data-access approach by freshness and availability:

| Requirement | Approach |
|---|---|
| Must be current at request time | call the owning service through a typed HTTP client |
| Frequent reads, eventual consistency acceptable | maintain a minimal local read model from integration events |
| Only a few fields are needed | replicate only those fields, not the source entity |

The local read model is not authoritative. Handle create, update, and delete semantics explicitly.

For events that update replicated state, include `EntityId` and a monotonic `EntityVersion`. Store the last applied version. Ignore duplicates and stale lower versions idempotently. Detect a version gap, emit telemetry, and run the confirmed reconciliation strategy. Do not order business state by `OccurredAtUtc`; clocks can differ. `MessageId` deduplicates delivery, while `EntityVersion` orders entity changes.

Do not add entity versions to notification events that have no state-ordering requirement.

## Caching And Distributed Coordination

Do not add Redis or another distributed cache by default. Prefer the service-owned database or a minimal event-fed local read model. Add distributed caching only with concrete evidence such as measured hot reads, shared ephemeral state, distributed rate-limit state, or another requirement that cannot be met locally.

The owning service defines cache keys, TTL, invalidation, tenancy isolation, and stampede protection. Cache is never authoritative, and cache failure MUST NOT weaken business correctness. Treat cached authorization/security data as high-risk: use bounded TTL, explicit invalidation, and safe isolation.

Do not use a Redis/distributed lock to enforce a business invariant across services or simulate a distributed transaction. Prefer ownership, database constraints, optimistic concurrency, idempotency, and serialized message processing.

Use a distributed lock only after confirmation for narrow coordination such as a singleton scheduled operation. Define lease timeout, renewal/failure behavior, and a fencing strategy when stale owners could still write. If losing the lock can violate a critical invariant, redesign ownership instead of trusting the lock.

## Background And Scheduled Jobs

Use `BackgroundService` only for a simple processing loop that does not require durable scheduling and whose instance behavior is understood.

Use Quartz.NET for a confirmed recurring/scheduled job requirement. Add the latest stable version compatible with the target .NET version only when scheduling is needed; do not add Quartz to every service.

| Job requirement | Quartz store |
|---|---|
| Non-critical, one instance, schedule loss acceptable | in-memory store may be sufficient |
| Business-critical, retry/persistence required, or multiple instances | persistent store plus Quartz clustering |

Keep Quartz tables inside the owning service's logical database. Make jobs idempotent because misfires, retries, and failover can repeat execution. Choose a misfire policy explicitly when catch-up behavior affects business outcomes. Do not hold a database transaction across a long-running job or network workflow.

Use MassTransit delayed/scheduled messages for a messaging workflow. Do not replace them with Quartz merely because a delay exists.

## Object Storage And Large Uploads

Do not store uploaded files on an application instance's local filesystem. Store large binary content in the confirmed object-storage provider and keep domain metadata/object key in the owning service database. Ask for the provider and compatible client library; do not default to S3, Azure Blob, or another provider.

Always stream with bounded buffers and backpressure. Do not load a whole large file into memory. Put maximum size, chunk size, timeout, and provider settings in typed configuration/options rather than hardcoded constants.

For a large/resumable upload, let the client send chunks directly to object storage using short-lived scoped presigned URLs (or the provider's equivalent such as Azure SAS):

```text
Angular -> backend: authorize and create upload session
backend -> storage: initiate multipart upload
backend -> Angular: presigned URL per part
Angular -> storage: upload each part directly, retry/resume independently
Angular -> backend: submit part numbers and ETags
backend -> storage: complete multipart upload
backend: verify object/size/checksum, then persist final metadata
```

Never expose long-lived storage credentials or public/unsigned write access to the client. Keep presigned URLs short-lived and limited to the expected object key/operation. For a small file, one presigned upload URL may be enough; use multipart only when size/reliability requires it.

Treat upload-session complete/cancel as idempotent. Do not hold a database transaction while bytes upload. Verify object existence, size, content type policy, and checksum before marking the domain attachment valid. Do not trust client filename or content type. Clean abandoned multipart sessions/orphan parts and add malware scanning when the risk/scope requires it.

Deleting metadata and object storage content is a cross-resource workflow: make it retryable/idempotent and reconcile partial failure.

## Search And Index Ownership

Apply the architecture-neutral search activation, client, security, reindex, and reconciliation rules from `dotnet-backend-standards/references/platform-capabilities.md` first.

Each service owns the index derived from its own database: mappings, analyzers, aliases, credentials, indexing consumer, reindex, and reconciliation. Another service MUST NOT write that index. Do not create a cross-domain shared index.

For confirmed global search across bounded contexts, create a dedicated Search Service that consumes versioned integration events and builds its own read model. It never reads service databases directly.

Persist business changes and outbox records even when Elasticsearch is unavailable. A Search Service or another service consumes versioned integration contracts; it never references the producer's projects or reads its database.

## Multi-Tenancy

Apply the architecture-neutral multi-tenancy activation, isolation choices, trusted-context, data-boundary, lifecycle, and test rules from `dotnet-backend-standards/references/platform-capabilities.md` first.

Select isolation per service/data boundary; different services may justify different models. Each service owns tenant-aware provisioning, migrations, credentials, backup/restore, and deletion for its logical database. Do not solve tenant isolation with cross-service queries or shared credentials.

Ask separately whether Elasticsearch uses a shared tenant-filtered index, index per tenant, or hybrid. Do not infer search isolation mechanically from database isolation.

## Realtime UI

Apply the architecture-neutral SignalR activation, authorization, contract, reconnect/resync, and provider rules from `dotnet-backend-standards/references/platform-capabilities.md` first.

In a microservice topology, put client-facing hubs at the owning API or confirmed gateway/BFF boundary. Backend services exchange durable facts through MassTransit; the UI boundary translates only the events it owns into SignalR notifications. Do not make every service publicly expose a hub or use SignalR as the service event bus.

## Observability And Correlation

Use W3C trace context for HTTP and OpenTelemetry instrumentation for ASP.NET Core, typed `HttpClient`, and MassTransit. Keep these identifiers distinct:

| Identifier | Meaning | Lifetime |
|---|---|---|
| `TraceId` | one distributed execution trace | request/message processing trace |
| `CorrelationId` | one business workflow | may cross messages, retries, and compensation |
| business ID | domain identity such as `OrderId` | domain lifecycle |

Propagate trace context using framework instrumentation. Carry messaging correlation through trusted message headers. Put required business identifiers in the event contract. Log safe structured fields including service name, environment, trace ID, correlation ID, message type/version, attempt, duration, and outcome. Never log tokens, claims, secrets, or sensitive bodies.

Do not replace a valid trusted correlation ID at each hop. Validate or regenerate untrusted public correlation input according to the edge policy.

## Kubernetes Networking And Health

Apply this section only when Kubernetes is explicitly in scope. Do not scaffold manifests, probes, Services, migration Jobs, or rollout policy for a project whose confirmed deployment scope excludes Kubernetes.

Place every service Deployment's replicas behind one Kubernetes Service DNS name and service port:

```text
YARP -> ordering-service:80 -> pod-1:8080
                            -> pod-2:8080
                            -> pod-3:8080
```

Route to Service DNS, never Pod IPs. Keep service APIs private. Expose only Nginx through the confirmed cluster entry mechanism.

- Liveness checks process health only; do not include database, broker, or downstream HTTP dependencies.
- Readiness checks startup state and only direct dependencies required to serve the component's primary role, commonly its own database.
- Do not put every downstream service into readiness; this creates cascading traffic removal.
- Include the broker in readiness only when the component cannot perform its primary role without it.
- Use a startup probe for slow startup/migration checks so liveness does not restart the pod prematurely.
- Return minimal public health status; keep dependency detail private.

## Database Migrations

Each service owns its migrations. Do not let every API replica apply production migrations at startup.

Run migrations once through the confirmed deployment mechanism, such as a GitHub Actions deployment step or—when Kubernetes is explicitly in scope—a dedicated Kubernetes Job. A failed migration blocks rollout. Application startup may check compatibility/pending migration state, but it does not mutate production schema.

Use expand-migrate-contract for rolling deployment:

1. Expand with backward-compatible schema.
2. Deploy code that can run while old and new replicas overlap.
3. Migrate/backfill data safely.
4. Remove old schema only after all consumers are migrated and confirmed.

Do not automatically run destructive down migrations when rolling back application code.

## Anti-Patterns

- A service references another service's Domain/Application project.
- Several services share one database/schema or credential.
- The gateway contains business workflows or database access.
- The BFF becomes the domain owner.
- Every service uses Clean Architecture, Vertical Slice/CQRS, or DDD regardless of complexity.
- Every synchronous call becomes gRPC without measured need.
- Every downstream dependency is included in readiness.
- Every replica races to run migrations.
- A local read model overwrites newer state using timestamps.
- A user token is forwarded through all internal calls without need.
- Redis is added without measured/shared-state need or used as a cross-service business lock.
- A durable multi-instance job uses only in-memory scheduling.
- A backend buffers an entire large file or gives the client public write access to storage.
- Elasticsearch is added for a basic filter box or one service writes another service's index.
- Tenant identity is trusted from arbitrary client input or one isolation model is assumed without confirmation.
- SignalR is treated as durable messaging or reconnect skips authoritative resync.

## Review Checklist

- Can each service be built, tested, migrated, and deployed independently from its own solution, with only the build executed by default under the repository verification policy?
- Is code architecture selected independently per service?
- Is Nginx the only public edge, serving Angular and relative `/api`?
- Does YARP/BFF have a narrow non-domain responsibility?
- Does each API validate the IdP token and apply declarative permission?
- Are machine calls authenticated with service identity?
- Does each service own a logical database, user, and migrations?
- Are cross-service workflows eventually consistent and idempotent?
- Is HTTP API version explicit and discoverable in OpenAPI?
- Are read-model versions and reconciliation handled where ordering matters?
- Are trace/correlation fields propagated without sensitive payload logging?
- Are health probes non-cascading and migrations single-run?
- Are cache/lock choices evidence-based and unable to weaken correctness?
- Are durable jobs idempotent and backed by the correct Quartz store?
- Do large uploads use bounded streaming and scoped presigned multipart URLs?
- Is Elasticsearch activated by real search needs and owned per service?
- Is tenant isolation explicitly selected and enforced across every data boundary?
- Is SignalR conditional, authorized, non-durable, and resynchronized?
