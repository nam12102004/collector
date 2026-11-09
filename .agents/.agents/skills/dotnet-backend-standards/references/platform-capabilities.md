# Conditional Platform Capabilities

## Contents

- Activation Discipline
- Search
- Multi-Tenancy
- Realtime With SignalR
- Secrets And Configuration Providers
- Review Checklist

## Activation Discipline

Apply these rules to N-Layer, Clean Architecture, modular monoliths, Vertical Slice applications, and independent services. None of these capabilities implies microservices, Clean Architecture, DDD, CQRS, Redis, or Kubernetes.

Before implementation:

1. Prove the capability from requirements or repository evidence.
2. Inventory installed framework/library support and existing standalone owners.
3. Confirm any missing package, infrastructure, provider, public contract, and operational responsibility.
4. Keep the capability inside the detected architecture and owning business/data boundary.
5. Define failure, security, observability, retention, and test behavior before wiring infrastructure.

## Search

Use indexed database filtering, sorting, and prefix search for ordinary CRUD/admin requirements. A search box alone does not justify a search engine.

Activate Elasticsearch when requirements include full-text search over a meaningful corpus, fast autocomplete, fuzzy/typo tolerance, relevance ranking, language analyzers, faceting, complex multi-field search, or measured database-search load. Product scale, commercial use, a large team, and many users strengthen the signal but do not replace a concrete search requirement.

After confirmation:

- Use Elasticsearch as the default search engine and `Elastic.Clients.Elasticsearch` as the official .NET client at a version compatible with the cluster and target .NET version.
- Reuse one registered client instance and async APIs; keep endpoints/credentials in typed configuration and secrets.
- Keep the database authoritative. Do not fail the authoritative business transaction merely because indexing is unavailable.
- Update the index asynchronously through an outbox/event or another retryable durable mechanism when losing an index update would create stale results.
- Give one owning bounded context/deployable responsibility for mappings, analyzers, aliases, credentials, indexing, reindex, and reconciliation. Do not let unrelated features write the index directly.
- Use versioned mappings and alias swaps for safe reindex. Provide reconciliation/backfill and observable lag/failure behavior.
- Design autocomplete analyzers intentionally; do not use unbounded wildcard queries as a substitute.
- Index only required fields. Apply the same authorization, tenant isolation, and sensitive-data policy to indexing and querying as to authoritative reads.
- Expose search through the backend API. Angular does not connect directly to Elasticsearch.

Use a dedicated Search Service only for confirmed cross-context/global search. It consumes owned/versioned contracts and builds its own read model; it never reads another owner's database.

## Multi-Tenancy

Activate tenant rules only when the product is actually multi-tenant. Ask for isolation per data boundary; do not assume one universal model:

- shared database and schema with `TenantId`;
- schema per tenant;
- database per tenant;
- hybrid by tier, region, scale, or compliance.

Evaluate security/compliance, tenant count and growth, data residency, noisy-neighbor risk, per-tenant backup/restore, migration fan-out, connection-pool pressure, cross-tenant reporting, cost, and operational complexity before selecting.

Derive tenant identity from a validated token or trusted server-side context. Never trust an arbitrary public tenant header, route value, query field, or request body as authorization.

Propagate and enforce tenant context across every relevant boundary: queries, writes, uniqueness, cache keys, idempotency keys, search documents/queries, files/object keys, audit entries, background jobs, integration events, local read models, and realtime groups. Cross-tenant operations require explicit system/admin permission and audit where the operation is sensitive.

For shared-schema storage:

- include `TenantId` in tenant-local unique constraints;
- apply tenant filtering before projection/count/pagination;
- ensure raw SQL, bulk operations, repository methods, and background work cannot bypass scope;
- treat an EF global query filter as defense in depth, not the only authorization boundary.

Define tenant provisioning, suspension/deletion, migration, backup/restore, and key/secret ownership before automation. Test with at least two tenants and prove that IDs, filters, search, cache, files, jobs, events, audit, and realtime cannot cross isolation boundaries.

## Realtime With SignalR

Add ASP.NET Core SignalR and `@microsoft/signalr` only for a confirmed server-push UI requirement. Prefer SignalR over handwritten WebSocket infrastructure.

SignalR is non-durable and not authoritative:

- Persist business state before notifying clients.
- Use MassTransit for durable backend-to-backend integration; translate relevant state changes to SignalR only at the UI boundary.
- Treat hub events as invalidation/hints or typed state notifications. Angular refetches authoritative state after reconnect, a detected sequence gap, or an event it cannot reconcile safely.
- Authenticate the connection and authorize hub methods/subscriptions. Derive user/tenant/group membership on the server; never trust client-selected groups or `connectionId` as identity.
- Keep event names/contracts stable, version breaking payloads, keep payloads small, and avoid sensitive data.
- Handle cancellation, disconnects, slow clients, and burst/backpressure behavior. Do not block the business transaction on client delivery.

When multiple backend instances are confirmed, ask which supported scale-out/backplane provider fits the deployment. Do not add Redis automatically. Define reconnect, retry, token refresh, duplicate/out-of-order event, and resync behavior in the Angular client.

## Secrets And Configuration Providers

Use the built-in .NET configuration/options model. Keep ordinary non-secret settings in configuration and credentials/keys/tokens in an appropriate secret source. Never put secrets in constants, source code, Angular environment files, tracked appsettings, logs, telemetry, OpenAPI examples, test snapshots, container images, or build artifacts.

For local development, allow safe/disposable values in `appsettings.Development.json` and ignored `.env.local`; environment variables override appsettings. Commit one `.env.example` containing names/placeholders only.

For staging/production, ask for the hosting environment and secret/configuration provider before adding a package or provider-specific implementation. Valid choices may include GitHub Secrets for deployment injection, platform-managed environment/secret mounts, Azure Key Vault, AWS Secrets Manager/Parameter Store, Google Secret Manager, HashiCorp Vault, or another confirmed provider. Do not choose a universal default.

- Prefer workload identity/managed identity over stored cloud credentials when supported.
- Document configuration precedence and one authoritative source per secret; avoid conflicting copies across appsettings, environment variables, Compose, CI, and an external vault.
- Bind settings to typed options and validate required values/ranges at startup without printing values.
- Separate secret names/references from secret values. Keep stable configuration keys across environments.
- Define rotation/reload behavior. If runtime reload is unsupported or unsafe, use a controlled restart/rollout.
- Give each deployable the least-privilege identity and access only to its own secrets.
- Keep production secrets out of frontend bundles. Angular receives only public runtime configuration explicitly safe for every user.
- Verify names, provider wiring, identity, and failure behavior without exposing secret values.

Use `deployment-config-standards` whenever Docker, Compose, GitHub Actions, Nginx, or deployment environment artifacts are changed.

## Review Checklist

- Was the capability activated by evidence rather than project size or preference?
- Does basic database search remain the default until the Elasticsearch gate passes?
- Is authoritative state independent from search and SignalR availability?
- Are search ownership, reindex, reconciliation, authorization, and tenant isolation explicit?
- Is tenant isolation selected and enforced across every data boundary?
- Is SignalR authenticated, non-durable, small/typed, and followed by authoritative resync?
- Was scale-out/provider infrastructure confirmed rather than assumed?
- Are configuration precedence, typed validation, least privilege, rotation, and secret redaction correct?
