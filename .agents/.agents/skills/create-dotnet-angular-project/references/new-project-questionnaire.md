# New Project Questionnaire

Ask only unresolved questions, but cover every material category before scaffolding. Record answers in a decision ledger and ask the user to confirm the ledger as a whole.

## Product And Repository

- Product/domain name, solution name, root namespace, and repository name.
- Required deployables and whether the initial scope includes both API and Angular application.
- Solution format and test-project placement when no repository convention exists.

## Backend

- Target .NET version.
- Code architecture: one-project N-Layer, multi-project Clean Architecture, established modular monolith, or large-application Vertical Slice/CQRS. Ask separately about deployment topology, domain modeling, and integration style.
- Deployment topology: monolith or microservices. For microservices, confirm service names, one solution per service in the monorepo, and one-project N-Layer, multi-project Clean Architecture, or evidence-gated large Vertical Slice/CQRS for each service. Do not impose Clean Architecture or Vertical Slice on every service.
- Domain modeling: simple CRUD/application services or evidence-gated DDD.
- Integration: synchronous HTTP, event-driven MassTransit, or concrete-need gRPC. Do not add gRPC speculatively.
- Controller or Minimal API contract style. When controllers are selected, apply the strict thin-controller invariant.
- Database type and exact EF Core/provider choice, or explicit no-database scope.
- Migration ownership and execution approach.
- Authentication provider/protocol: external OIDC/OAuth, local application credentials, both, or another named protocol; include anonymous/public endpoints.
- Application account model: provider-owned identities only, an application-side User record, or hybrid; confirm whether an existing product identity/role contract or schema must be reused.
- Authorization model: role-only, role plus RolePermission matrix, claims/policy, tenant-scoped permissions, or a justified combination; identify permission ownership and API/navigation enforcement.
- Authentication framework choice: custom User/Role/Permission persistence/services versus ASP.NET Core Identity. Do not infer Identity from the .NET stack; if selected, record which local-credential capabilities are actually required and avoid unused password/reset/lockout behavior for an OIDC-only product.
- External identity mapping and lifecycle: stable `issuer + subject` key, email-linking rules, account provisioning, issuer/subject rotation behavior, and the data migration/rollback plan.
- User/Role/Permission integrity: uniqueness, assignment concurrency, deactivation/revocation, audit, and migration ownership.
- For microservices, confirm the external OIDC/OAuth 2.0 identity provider, per-service audience/scope, client-credentials machine identity, and the limited cases that require delegated user-token propagation.
- API versioning, pagination, success contract, Problem Details/error-code conventions, and interactive OpenAPI UI requirement. For confirmed versioned controller APIs, use `Asp.Versioning.Mvc` URL segments, require an explicit version, and generate per-version OpenAPI.
- Confirm the HTTP-200 `SuccessResponse<T>` envelope, Angular localization by stable success/error/validation codes, backend pagination metadata including `TotalPages`, and Problem Details failure statuses.
- Use AutoMapper `16.x` at no lower than `16.1.1` as the default mapper. Confirm Community/commercial license eligibility/key wiring; do not use vulnerable v14 as a no-license fallback.
- If MediatR is activated for confirmed in-process events or CQRS, use the latest stable compatible version and confirm Community/commercial license eligibility for v13 or later. Do not add it by default or silently downgrade to v12 to bypass licensing.
- Cross-project capabilities that need flat `src/<Product>.<Capability>/` projects, plus producer-owned `src/<Product>.<Producer>.Contracts/` projects for confirmed integration events. In a microservice monorepo, include each contract project in the relevant service solutions and consume it with `ProjectReference`; do not introduce private NuGet packaging by default.
- For capabilities explicitly reused across repositories, confirm a product-neutral package ID, public/private NuGet feed, target frameworks, semantic-version policy, release trigger, consumer authentication, and support/upgrade ownership. Do not copy authorization or other security-sensitive source manually between repositories.
- External HTTP, messaging, background jobs, caching, file storage, email, realtime, and scheduled work.
- For caching/coordination, confirm the measured/shared-state need before Redis and do not use distributed locks for cross-service business invariants.
- For scheduled work, confirm Quartz.NET, in-memory versus persistent clustered store, idempotency, and misfire behavior. Use MassTransit scheduling for messaging workflows.
- For file uploads, confirm object-storage provider, limits, bounded streaming, presigned direct upload, multipart/resume threshold, checksum, authorization, scanning, and orphan cleanup.
- For each microservice, confirm its logical database/provider/user, migration ownership, and whether databases share a physical server/cluster. Never share a logical database or credential across services.
- For cross-service data, confirm request-time HTTP versus a minimal event-fed local read model, plus entity-version/reconciliation behavior when ordering matters.
- Confirm idempotency only for writes whose duplicate retry remains unsafe after natural keys/constraints/concurrency. For business audit, confirm activation, fields, append-only ownership, retention, and read authorization.
- Define retention per data category, including trigger, duration, archive/anonymize/hard-delete action, legal hold, purge cadence, object/search/read-model cleanup, and backup lifecycle. For soft delete, confirm restore need, unique-value reuse, and a provider-correct filtered/partial/generated-key strategy.
- For search, keep database search for basic needs. When the full-text/autocomplete/fuzzy/ranking/facet/load gate passes, confirm Elasticsearch infrastructure, indexing ownership, authorization/tenant policy, asynchronous update/reconciliation, and reindex behavior. Use a global Search Service only for confirmed cross-context search.
- For multi-tenancy, confirm whether it exists and select shared-schema, schema-per-tenant, database-per-tenant, or hybrid isolation for each data boundary. Confirm trusted tenant resolution plus database/cache/search/file/job/event/audit/realtime isolation, provisioning, migration, backup/restore, and cross-tenant administration.

## Frontend

- Angular version when not constrained by an existing workspace.
- Installed or selected component-library shell behavior: sidebar or top navigation, collapse behavior, mobile breakpoint/drawer behavior, route-driven menu, breadcrumbs, header actions, and theme direction. Do not assume NG-ZORRO or add a separate ProLayout package.
- Theme/design tokens and responsive expectations. Reuse Tailwind when it is already installed; otherwise confirm the styling choice.
- State strategy for local, feature, and global state.
- Authentication/OIDC client and permission-driven navigation behavior.
- i18n requirement and supported locales.
- Chart library, 3D requirement/Three.js, rich text, upload, maps, or other specialized UI capabilities.
- Realtime requirement/SignalR, authorized server-derived groups, typed event contract, reconnect/gap resync behavior, and confirmed scale-out provider when multiple instances are in scope.
- Unit/e2e test runner to scaffold and the browser/accessibility checklist the user will run. Do not execute tests or open/automate a browser unless explicitly requested.

## Operations

- Environments, Docker/Compose/Kubernetes scope, domains, ports, TLS termination, and Nginx as the sole public edge. Confirm that Nginx serves the Angular build directly and proxies relative `/api` to the backend or YARP.
- Commit one `.env.example`; keep `.env.local/.staging/.production` ignored and pair them with confirmed compose artifacts. Use GitHub Secrets for deploy-time values and environment-suffixed Compose project names.
- For microservices, confirm whether YARP adds application-gateway/BFF behavior beyond Nginx/Kubernetes routing; do not install it for simple routing alone.
- GitHub Actions build/deploy targets, runners, registries, and secret sources. Confirm whether runtime secrets come from deployment-injected environment/mounts or a named external provider; define configuration precedence, workload identity, rotation/restart, least privilege, and startup validation.
- OpenTelemetry Collector/backend, OTLP endpoint strategy, sampling, retention responsibility, and environment-specific service identity.
- Liveness/readiness paths and which dependencies readiness must check.
- Production migration execution (dedicated Kubernetes Job or GitHub Actions step), rolling-deployment compatibility, and expand-migrate-contract requirements.

## Acceptance

- Initial features and explicitly out-of-scope capabilities.
- Required success/failure behavior and performance/security constraints.
- Build commands that must pass before handoff, plus test/lint/runtime/preview commands the user will run unless explicit execution is requested.
