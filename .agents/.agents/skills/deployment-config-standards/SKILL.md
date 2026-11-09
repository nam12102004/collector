---
name: deployment-config-standards
description: Strict environment and deployment-configuration standards for Docker Compose, Dockerfiles, Nginx, standalone identity providers, local pgAdmin, GitHub Actions deploy workflows, health probes, and OpenTelemetry export wiring. Use whenever Codex writes, reviews, or edits docker-compose*.yml, Dockerfiles, reverse-proxy config, environment selection, ports/TLS, database administration tooling, secrets, liveness/readiness, OTLP settings, or deployment workflows; verify the exact artifact-to-environment path across dev, staging, and production.
---

# Deployment Config Standards

Use this skill to keep environment-specific configuration honest across dev/staging/production so a file meant for one environment can never silently ship another environment's behavior.

## First Move

1. Read every `docker-compose*.yml` in the repo side by side — never edit one in isolation. A variable that is templated (`${VAR:-default}`) in one file and hardcoded in another is the most direct way this repo has shipped a dev-mode config as production.
2. Open the relevant GitHub Actions workflow (`ci.yml`, `staging.yml`, `production.yml`) and confirm exactly which compose file(s) its deploy step ships. Reviewing a file that looks right while a differently-named file is what actually gets deployed is a release-blocking mistake.
3. Build an environment matrix covering workflow, deployed compose/config artifact, environment selector, secret source, public ports, TLS termination, environment-gated security/operational behavior, health endpoints, and OpenTelemetry export before changing behavior.

## Non-Negotiables

- Give every environment-selecting variable (`ASPNETCORE_ENVIRONMENT`, `NODE_ENV`, or equivalent) an explicit environment-appropriate default in each Compose/config artifact. A local artifact may default to `Development`; staging and production artifacts must default to `Production` or fail closed when unset. Never let a staging/production deployment resolve `Development`. Inspect the actual application for environment-gated CORS, OpenAPI/Swagger, authentication, seed data, detailed errors, diagnostics, or other security/operational behavior and verify the resolved environment is safe for that artifact.
- Every variable in a compose file follows the same convention as its neighbors. If every other variable in the file is `${VAR:-default}` and one is a bare literal, treat that as a drift signal, not an intentional choice — either fix it or justify it with an explicit comment.
- TLS/HTTPS claims are verified, not assumed. If a reverse-proxy config (e.g. `nginx.proxy.conf`) listens on 443, confirm `ssl_certificate`/`ssl_certificate_key` are actually configured, or that TLS is deliberately terminated upstream (load balancer/tunnel) — "listens on 443" alone does not mean TLS is terminated, and the gap should be documented, not left implicit.
- Secrets (client secrets, admin passwords, connection strings) never get hardcoded into a tracked compose file or a realm/seed JSON outside a clearly-named dev-only seed file. Production values flow through CI secrets (GitHub Secrets) or an untracked `.env`.
- Never print secret values during investigation or verification. Verify secret names and wiring only.
- Do not change production exposure, TLS termination, credential flow, or deployment target as incidental cleanup; require explicit scope when behavior changes.

## Environment Files And Compose

- Commit exactly one `.env.example` containing the complete superset of required variable names with placeholders or safe defaults.
- Keep actual `.env.local`, `.env.staging`, and `.env.production` files untracked and listed in `.gitignore`. Create them from `.env.example`; never commit real staging/production values.
- Keep corresponding `docker-compose.local.yml`, `docker-compose.staging.yml`, and `docker-compose.production.yml` artifacts when those environments are in scope. Each compose artifact loads/injects the matching environment values and preserves the same variable vocabulary.
- Allow `appsettings.Development.json` to contain safe local defaults and disposable development credentials. Environment variables override appsettings. Production MUST NOT fall back to a development credential/default.
- Let GitHub Actions source deployment values from GitHub Secrets and inject/generate environment configuration on the deployment target without printing secrets or storing them in build artifacts.
- Prefer an environment-suffixed `COMPOSE_PROJECT_NAME` such as `product-staging` and Compose-generated names when scaling may occur. A fixed `container_name` with an environment suffix is allowed only for a confirmed single-instance topology because it prevents ordinary Compose scaling.
- Use Compose service names for internal DNS; application configuration MUST NOT call generated container names.

## External Secrets And Configuration Providers

- Apply `dotnet-backend-standards/references/platform-capabilities.md` before introducing an external provider. Confirm the hosting platform and provider; do not default to Azure Key Vault, AWS Secrets Manager/Parameter Store, Google Secret Manager, HashiCorp Vault, Docker secrets, or another implementation.
- Document effective precedence from appsettings through environment variables, mounted values, and the external provider. Keep one authoritative source for each production secret and fail startup when required configuration is missing/invalid.
- Prefer workload/managed identity and least-privilege per-deployable access. Do not bootstrap a vault client with a long-lived cloud credential stored in the same tracked or injected configuration it is meant to replace.
- Keep secret values out of image layers, Angular bundles, workflow logs, Compose interpolation output, generated artifacts, health details, and telemetry. Verify names/references and access behavior without printing values.
- Define rotation behavior. Use provider reload only when the application/provider supports safe refresh; otherwise perform a controlled restart/rollout. Confirm what happens when the provider is temporarily unavailable and whether startup or an already-running process can use a safe cached value.
- Keep application runtime secrets separate from CI deployment credentials. GitHub Secrets may authenticate deployment or inject confirmed runtime values, but it does not automatically become the application's long-term configuration provider.

## Public Edge And Routing

- Treat Nginx as the only public application edge and the only application component publishing ports 80/443. A confirmed standalone identity provider such as Keycloak remains a separate public security boundary with its own endpoint; do not proxy it through the application Nginx.
- Serve the Angular production build directly from Nginx. Route SPA navigation to `index.html`, but never route `/api/*` failures to the SPA fallback.
- Route relative `/api/*` to the single backend or to a private YARP gateway in a confirmed microservice topology. Do not bake an environment-specific backend hostname into the Angular production bundle.
- Keep Angular build artifacts, migration containers, YARP, backend APIs, microservices, health, metrics, and telemetry on the private Docker/Kubernetes network unless a protected operational route is explicitly approved.
- For this repository's local full-stack topology, publish Nginx on one application port, serve Angular at `/`, proxy `/api/*` to the private API, and proxy `/pgadmin/*` to a private pgAdmin container. Keep PostgreSQL private and reachable only through Compose DNS by the API, migrations, bootstrap jobs, and pgAdmin. Never proxy the PostgreSQL wire protocol through an HTTP Nginx location.
- Run local Keycloak as a standalone public container on its own port while retaining private-network access for backend metadata discovery. Configure the browser/public issuer URL separately from the backend's internal metadata URL, and require exact issuer consistency.
- Keep pgAdmin development-only unless the user explicitly approves a protected non-local deployment. Do not publish a pgAdmin host port; put it behind Nginx, configure its subpath/script name, persist its state in a named local volume, pre-register the Compose `db` service without embedding the database password, and source pgAdmin login credentials from local environment variables.
- Cache content-hashed static assets for a long duration and keep `index.html` short-lived/no-cache so deployments are discoverable.
- Configure forwarded headers and trusted proxies consistently across Nginx, optional YARP, and ASP.NET Core. Preserve scheme, host, correlation, and safe client-IP semantics without trusting arbitrary forwarded headers from the public Internet.
- In Kubernetes, route to stable Service DNS/ports rather than Pod IPs. Multiple replicas remain behind one service endpoint; readiness controls traffic eligibility.
- Do not add YARP when Nginx or Kubernetes routing fully satisfies the requirement. When YARP is confirmed, keep it free of domain logic and databases.

## Health And Observability

- Wire a deployable backend's liveness and readiness endpoints into the actual container/orchestrator configuration. Liveness proves the process can run; readiness includes only dependencies required to accept traffic.
- Do not use an expensive or externally fragile readiness check as liveness. Do not report readiness before required startup work and dependency checks complete.
- Configure OTLP endpoint, service name, service version/environment, sampling, and authentication through environment variables or the repository configuration system. Never hardcode a collector/vendor endpoint or telemetry credential.
- Inside containers, resolve the collector by the deployed network/service name. Do not assume `localhost` reaches another container.
- Use console telemetry export only for local development. When production telemetry export is enabled, use the confirmed OTLP/collector path. Never emit sensitive bodies, tokens, claims, connection strings, or secrets.
- Keep health endpoints minimally exposed. If detailed dependency health reveals internals, restrict it to the private deployment network and return only the status needed by the orchestrator.
- **OpenTelemetry must never block application availability in any environment.** Missing or blank OTLP configuration disables export; deployment, migrations, startup, liveness/readiness, and business requests must continue without a Collector or Grafana account. Do not require OTEL variables through Compose `${VAR:?}`, CI secrets, startup validation, Collector health dependencies, or successful first export.
- Invalid telemetry configuration (endpoint, protocol, headers, or sampler) and telemetry initialization failures must disable the affected telemetry pipeline with a sanitized warning rather than throw an application startup error. Isolate telemetry failures only; do not suppress failures in required application dependencies or security configuration.
- Collector outages, DNS/TLS/authentication errors, timeouts, and export failures must remain isolated from business operations. Use bounded background export/retries and bounded shutdown flushing; never await successful telemetry delivery on the startup or request path, or turn telemetry failure into an unhealthy application probe. Do not add a fail-closed telemetry mode.
- For Kubernetes microservices, keep liveness process-only. Put only startup state and direct dependencies required for the component's primary role into readiness; do not include every downstream HTTP service. Use a startup probe for slow initialization.

## Production Database Migrations

- Give each service sole ownership of its logical database, database user, migration project, and migration history. Sharing one physical database server does not permit shared schemas, credentials, or cross-service queries.
- Do not let an API apply production migrations at startup. For Docker Compose, build and run a dedicated migration image/service before API rollout; for Kubernetes, use a dedicated migration Job. Never hide production schema mutation inside an ordinary API replica.
- Let the migration container run EF Core migrations and then invoke application-owned C# seeders through the established EF seeding hook or an explicit migration runner. Keep seeders idempotent, configuration-driven, free of environment-specific hardcoded identities/secrets, and in the same transaction where partial migration/seed completion would violate invariants. Do not replace application seeding with ad-hoc SQL when domain/application construction rules are required.
- Pass the migration container the same database configuration and required non-secret seed selectors as the API, with secret values supplied only by the environment secret source. Do not make unrelated API runtime dependencies mandatory merely to migrate or seed.
- Make deployment order explicit: start/verify the database, run the one-shot migration container, stop rollout on any non-zero exit, and only then start or update API replicas. Do not use `--no-deps` or another shortcut for the migration step when it can omit the database/network dependency.
- Use expand-migrate-contract for rolling deployments so old and new replicas can overlap safely. Remove old columns/contracts only after all application consumers are migrated and confirmed.
- Do not automatically execute destructive down migrations when rolling back application code.

## Default Verification

Do not start containers, deploy, start applications, hit health endpoints, open a browser, or run runtime smoke tests unless the user explicitly requests it. Limit default execution to the relevant build and static configuration/syntax validation; perform the remaining checks by inspecting artifacts and report them for the user to run.

- Diff the environment-variable block across all `docker-compose*.yml` files side by side; flag any variable templated in one file and hardcoded in another.
- Verify `.env.example` covers every variable referenced by all compose/workflow artifacts, actual `.env.*` files are ignored, and no production secret/default is tracked.
- Verify the confirmed configuration precedence, external-provider identity/access, startup validation, rotation path, and absence of secret values from artifacts/logs.
- Confirm the exact file(s) referenced by `staging.yml`/`production.yml`'s deploy step match the file(s) just edited.
- After touching any compose/env file, grep the result for `ASPNETCORE_ENVIRONMENT` / `NODE_ENV` and trace it through the application's discovered environment-gated security and operational behavior. Confirm local resolves as intended and staging/production resolve to production-safe behavior or fail closed.
- If the change touches `nginx.proxy.conf` or a Dockerfile's exposed ports, confirm TLS termination is either configured there or explicitly documented as happening upstream.
- Verify only Nginx is publicly reachable among application components, with the confirmed standalone identity-provider endpoint as the sole exception. Confirm Angular uses relative `/api`, SPA fallback excludes `/api` and `/pgadmin`, PostgreSQL has no published host port, pgAdmin has no published host port, and every private upstream resolves through Compose/service DNS.
- Inspect whether the configured health URL and collector address use resolvable deployed service names, intended ports/protocols, and matching GitHub Actions environment/secret wiring; leave deployed-network resolution to the user unless explicitly requested.
- Confirm liveness does not fail solely because an external dependency is unavailable, and readiness changes state when a dependency required to serve traffic is unavailable.
- Verify local/staging/production define the intended one-shot migration service, API startup does not mutate schema, the deployment workflow builds and runs the matching migration image before API rollout, and failure blocks rollout. Confirm configured C# seeders are idempotent and are invoked by the migration path rather than by every API replica.
- Validate the edited YAML/config with available repository tooling and report exact commands, results, unresolved environment assumptions, and checks not run.
- Verify the optional telemetry path: unset/blank OTEL variables pass Compose/CI configuration; invalid telemetry settings disable only telemetry; unavailable or unauthorized Collectors cannot block startup, probes, or requests. Review failure paths statically and run focused checks only when execution is authorized; never mask missing configuration with a dummy required Collector endpoint.
- Do not declare completion while the production path resolves to development behavior or required config validation fails.
