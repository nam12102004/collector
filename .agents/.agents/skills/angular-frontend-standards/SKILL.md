---
name: angular-frontend-standards
description: Strict, repository-adaptive Angular frontend conventions for TypeScript, component boundaries, state, forms, installed UI libraries, styling, accessibility, and verification. Use whenever Codex writes, reviews, or refactors Angular frontend code.
---

# Angular Frontend Standards

Use this skill as a router. Load only the references required by the task, then enforce their rules against the repository's installed Angular version, architecture, and design system.

## Rule Strength

- `MUST-AUTO` is release-blocking and has a deterministic lint, typecheck, test, build, or audit check.
- `MUST-REVIEW` is release-blocking and requires concrete code evidence against a stated ownership or semantic criterion.
- `SHOULD` may be changed only for a documented repository or product reason.
- A rule that cannot name its evidence is not a `MUST`.

Current user intent and verified product/API contracts outrank repository conventions. Repository conventions outrank generic examples. Do not use cleanliness rules to change behavior or expand scope.

## First Move

Before editing:

1. Read `package.json`, TypeScript/Angular config, routes, lint/test/build config, shared UI, theme, and the nearest compliant feature.
2. Read [repository-profile.md](references/repository-profile.md) and keep its Angular version, installed packages, routes, component patterns, test runner, and quality commands authoritative for this repository.
3. Inventory every requested UI surface and map it before writing markup:

   `requirement -> owning component -> installed library/shared capability -> state owner -> verification`

4. Record the exact commands that will verify the change. Do not postpone the choice of component boundaries or UI primitives until after the template is written.

Ask before adding, removing, replacing, or first-adopting a dependency or high-level library capability that is not already canonical. Preserve unrelated legacy code; code newly written or materially touched MUST satisfy these standards.

## Topic Routing

| Task touches                                          | Read completely                                                                       |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------- |
| TypeScript, naming, types, imports, DI                | [typescript-code.md](references/typescript-code.md)                                   |
| Pages, components, tabs, surfaces, boundaries, reuse  | [component-architecture.md](references/component-architecture.md)                     |
| Signals, RxJS, searches, paging, async state, facades | [state-rxjs-facades.md](references/state-rxjs-facades.md)                             |
| Forms, API contracts, errors, i18n                     | [forms-api-i18n.md](references/forms-api-i18n.md); for submit/field feedback and form layout also read [form-feedback-patterns.md](references/form-feedback-patterns.md) |
| Templates, installed components, tables, accessibility | [templates-ng-zorro-accessibility.md](references/templates-ng-zorro-accessibility.md) |
| Tailwind, SCSS, tokens, responsive behavior           | [styling-responsive.md](references/styling-responsive.md)                             |
| Tests, browser checks, lint, build, completion        | [testing-tooling.md](references/testing-tooling.md)                                   |

Additional focused references are routed from those topic files. In particular, do not treat a collection as a non-table before reading the template topic's semantic table gate. Library-specific requirements activate only when the package is installed and used by the affected application. ASRS uses NG-ZORRO 22 for widgets and Angular CDK for interaction infrastructure; do not reintroduce Angular Material or Material Icons. Do not introduce Zod, NgRx, or Jest by default.

## Global Completion Gate

- Run configured format and lint checks, typecheck, focused tests, and production build unless the user explicitly narrows verification. Report unconfigured scripts clearly.
- Run browser/Playwright verification when behavior depends on viewport, DOM layout, overlays, focus, drag/drop, scrolling, or table containment.
- Run `scripts/audit-frontend-conventions.mjs --changed --strict` for changed Angular work. The pull-request workflow runs the same gate against the base branch. It fails on deterministic page/table/tab/overlay boundaries and clear page-owned async orchestration.
- Inspect remaining `MUST-REVIEW` findings as semantic evidence; a passing automatic gate does not certify component ownership, accessibility, or browser behavior.
- Report commands and results. Do not declare completion with an unexplained failed gate.
