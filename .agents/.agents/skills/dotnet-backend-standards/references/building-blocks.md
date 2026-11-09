# Reusable .NET Building Blocks

Use a building block for a cohesive cross-project capability, not as a default destination for code that has no obvious owner.

## Discovery Gate

Before writing cross-cutting code:

1. Inspect solution projects and their SDK/output type.
2. Trace `ProjectReference`, package references, DI extensions, interfaces, options, and current consumers.
3. Search for equivalent behavior and vocabulary, including typed application exceptions, permission resolution, authorization, current-user access, email, clocks, storage, telemetry, resilience, and messaging.
4. Prefer the existing owner when its contract and dependencies fit.
5. If it violates a mandatory skill rule, report the exact conflict, affected consumers, and proposed correction; ask before changing, replacing, or bypassing it. Do not create a parallel implementation silently.

## Creation Gate

Create a new building block only when no suitable owner exists and either at least two projects can reasonably consume the capability or the user explicitly requests reusable ownership.

- Name it `src/<Product>.<Capability>/`; keep reusable capability projects flat under `src/` instead of adding a `BuildingBlocks` folder or name segment.
- Reference it using `ProjectReference` in the same solution.
- Do not add packing metadata, a package feed, or private NuGet publication.
- Create one project per cohesive capability, not one project per interface or class.
- Keep the contract, default implementation, options, and DI registration together by default.
- Split abstraction and provider implementation only for multiple providers, required dependency direction, or a heavy/provider-specific dependency.

## Cross-Repository Distribution Gate

Use `ProjectReference` when all consumers live in one repository. When the user explicitly requires reuse across repositories, confirm distribution before implementation:

- Prefer a versioned NuGet package for a stable capability that must receive fixes independently across repositories.
- Confirm package ID/owner, public or private visibility, feed, target frameworks, semantic-version policy, release trigger, consumer authentication, and compatibility support.
- Use a product-neutral package name such as `<Organization>.Authorization`; do not bind reusable ownership to one consuming product.
- Keep framework-independent contracts separate from ASP.NET Core, EF Core, or provider adapters when dependency direction requires it. Do not make a Clean Application/Domain project reference a package that imports outer-framework dependencies.
- Publish private packages through the confirmed feed's CI identity. Never commit personal access tokens, feed credentials, or generated packages.
- Use source copying only for an explicitly confirmed template or independent fork. Do not present manual copying as shared-library distribution because security and compatibility fixes will drift.
- Use Git subtree or submodule only when source-level consumption is explicitly preferred over package versioning and the repository workflow accepts its update cost.

## Boundary Rules

- Expose a small stable public contract and one clear DI registration entrypoint.
- Bind configuration with typed options and validate required configuration at startup when failure should prevent readiness.
- Propagate `CancellationToken` through asynchronous I/O and use framework clients/factories appropriately.
- Keep feature entities, DbContexts, migrations, controller response types, deployable startup code, and feature-specific business rules out.
- A reusable exception package may own framework-independent exception types, safe metadata, and generic error codes. Keep HTTP mapping in the API and product-specific error codes/business rules in the consuming product.
- Do not let permission or authorization helpers move claims reading into controllers. Resolve current identity and resource access behind the reusable contract or policy layer.
- Keep email/storage/provider credentials in configuration and secret stores, never constants or logs.
- Use approved catalog dependencies and existing resilience/telemetry capabilities; do not hand-roll equivalent infrastructure.
- Avoid catch-all `Common`, `Helpers` or `Utilities` projects, extension-method dumping grounds, and unrelated constants. Focused supporting classes belong in their owning project's/layer's `Helpers/` folder under the [helper conventions](csharp-conventions.md#helpers); that folder is not a shared capability project.
- In a microservice monorepo, never reference one service's Domain/Application/Infrastructure project from another service. A stable technical capability project may be referenced by multiple service solutions. Producer-owned integration contracts stay in a focused flat contract project, are included in each consuming service solution, and use `ProjectReference` under the microservice/event-driven profile instead of a global shared-contract dump or private package.
- Across separate repositories, consume the confirmed package version from the configured feed instead of referencing or copying another product repository's source tree.

## Typical Shape

Adapt filenames to repository convention; this is a responsibility map, not a required folder template:

```text
<Product>.<Capability>/
├── public contract
├── default implementation
├── typed options
└── dependency-injection registration
```

Add provider-specific subprojects only when the creation gate for a split is satisfied.

## Verification

- Add focused tests for contract behavior, configuration validation, failure mapping, cancellation, and provider interaction relevant to the implementation.
- Build every direct consumer after changing the public contract or DI registration.
- Verify no reverse reference from the building block to a feature/deployable was introduced.
- For packaged capabilities, inspect the produced `.nupkg`, build direct consumers, and verify CI uses a repository/workload token rather than a committed secret.
- Report discovered alternatives, why the selected owner fits, consumers changed, commands, and checks not run.
