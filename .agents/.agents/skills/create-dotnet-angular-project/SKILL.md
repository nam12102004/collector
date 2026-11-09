---
name: create-dotnet-angular-project
description: Strict confirmation-first workflow for scaffolding a new full-stack solution with a .NET backend and Angular frontend. Use when Codex needs to create a new repository, solution, backend API, Angular application, initial tests, shared building blocks, or baseline deployment wiring. Ask and record all material choices before scaffolding; do not use for an ordinary feature inside an established application.
---

# Create .NET Angular Project

Apply `dotnet-backend-standards`, `angular-frontend-standards`, and `deployment-config-standards` when deployment artifacts are in scope.

## Confirmation Gate

1. Read `references/new-project-questionnaire.md` completely.
2. Inspect any files already present; do not overwrite an existing architecture or scaffold a second application beside one.
3. Ask for every unresolved material decision. Preserve libraries already confirmed by the user; do not assume a component library or state/form/test package for a new repository.
4. Present a concise decision ledger covering product name, project names, architecture, database, API/auth contracts, application layout/theme, tests, observability, and deployment.
5. Do not scaffold until the user confirms the ledger. Treat code organization, deployment topology, domain modeling, and integration style as separate decisions. Do not infer modular monolith, Clean Architecture, CQRS/Vertical Slice, DDD, microservices, event-driven messaging, PostgreSQL, authentication provider, application-shell behavior, state library, chart library, gRPC, gateway, or deployment topology. Select an application shell from the confirmed design system; do not default to NG-ZORRO.

## Repository Shape

Create the confirmed single-API solution with this outer structure:

```text
<ProjectRoot>/
├── src/
│   ├── <Product>.API/
│   └── <Product>.WebApp/
├── <Product>.slnx or the confirmed solution format
└── repository-level configuration
```

- Derive `<Product>` from the confirmed product/domain name.
- Do not create root-level `backend`, `frontend`, `client`, `web`, `ui`, or `app` folders.
- Use Angular's required identifier format internally while keeping the outer folder `<Product>.WebApp`.
- Add flat `src/<Product>.<Capability>/` projects only when a confirmed cross-project capability needs them. Reference them with `ProjectReference` inside the allowed solution/service boundaries.
- Place tests according to the confirmed convention; do not invent a `tests/` layout before that decision.

For a confirmed microservice monorepo, use this outer responsibility map instead:

```text
<ProjectRoot>/
├── src/
│   ├── <ServiceA>/<ServiceA>.sln and service projects
│   ├── <ServiceB>/<ServiceB>.sln and service projects
│   ├── <Product>.<Capability>/
│   └── <Product>.<Producer>.Contracts/
├── tests/<ServiceA>/ and tests/<ServiceB>/
└── deploy/
```

The same microservice repository also keeps these product deployables under `src/`:

```text
src/<Product>.WebApp/
src/<Product>.Gateway/  # only when a YARP gateway or BFF is confirmed; give it its own solution/projects when independently deployable
```

Do not add intermediate `Services` or `BuildingBlocks` folders. Keep the Angular application and any confirmed gateway under `src/` with product-derived names; never replace them with generic `frontend`, `web`, `client`, `gateway`, or `api-gateway` folders. Select one-project N-Layer, multi-project Clean Architecture, or confirmed large Vertical Slice/CQRS independently for each service; do not impose Clean Architecture or Vertical Slice on a small service.

## Scaffold Order

1. Create the repository/solution entrypoint and `src/` structure.
2. Scaffold each backend in the confirmed architecture profile(s). Apply thin controllers, declarative permission, typed custom exceptions, Problem Details, enum/constant conventions, validation, OpenAPI, health checks, and OpenTelemetry rules from the backend standards.
3. Add persistence only after the database/provider is confirmed. Never default to PostgreSQL or another provider.
4. Add focused standalone capability projects only after searching for existing class libraries and applying `references/building-blocks.md` from `dotnet-backend-standards`.
5. Scaffold Angular with feature-based architecture and the confirmed design system, styling, state, auth, i18n, and test choices. Reuse installed packages and add no library without confirmation.
6. Add deployment and GitHub Actions artifacts only when confirmed. For production full-stack deployment, make Nginx the sole public edge, serve the Angular build directly, route relative `/api` traffic internally, and wire environment-safe health/OpenTelemetry settings.
7. Keep every scaffolded project buildable before adding the next optional capability.

## Contract And Boundary Gates

- Make the backend contract authoritative. Complete and verify routes, DTO serialization, validation, permissions, statuses, Problem Details extensions, and OpenAPI before implementing a frontend consumer.
- Keep controller actions to binding, one service/use-case call, and returning its result.
- Keep Angular feature folders isolated and use installed high-level library components instead of recreating them.
- Do not create shared abstractions speculatively. A building block needs a real consumer and a cohesive capability.
- Report an existing non-compliant structure and ask before migration instead of scaffolding a parallel replacement.

## Verification

- Restore/install dependencies using the repository package managers.
- Build the backend solution and Angular application.
- Do not run tests, lint, application/dev-server/container startup, runtime smoke checks, or browser/browser-automation verification unless explicitly requested.
- Inspect OpenAPI generation wiring, health endpoints, Problem Details, responsive shell usage, and environment configuration statically.
- Report the confirmed ledger, created structure, build commands/results, and the test/lint/runtime/browser commands left for the user.
