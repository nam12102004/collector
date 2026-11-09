---
name: refactor-angular-code
description: Behavior-preserving workflow for refactoring Angular code toward repository standards, including component surfaces, tab boundaries, facade ownership, declarative templates, typing, accessibility, and tests.
---

# Refactor Angular Code

Apply `angular-frontend-standards` first. Preserve observable behavior unless the user explicitly requests a behavior change.

## 1. Establish The Boundary

- Read the complete target, callers, tests, API contracts, nearest compliant feature, and all relevant Angular topic references.
- Run the convention audit on the target and inventory route/page, stateful toolbar, data table, tabs, editors, details, imports, modals, drawers, and asynchronous workflows.
- Record current inputs, outputs, routes, requests, permissions, visible states, and test coverage before moving code.
- Keep unrelated legacy debt outside scope. A touched surface must satisfy the current standard.

## 2. Refactor By Responsibility

Work in reviewable, buildable increments:

1. Extract every tab body into a feature-local component.
2. Extract stateful toolbar/filter, data table, editor, detail, import, modal, and drawer surfaces from route pages.
3. Classify repeated collections semantically; use the repository's native table or installed Angular Material/CDK pattern with its accessibility and containment contract.
4. Introduce a feature facade when shared state or asynchronous orchestration meets the facade gate.
5. Move non-trivial derivation out of templates into computed state, selectors, pure pipes, or facade view models.
6. Replace custom library-equivalent widgets with a suitable installed Angular Material/CDK or shared capability.
7. Promote an abstraction to `shared` only when at least two real consumers share a stable contract.

Do not split arbitrary line ranges merely to satisfy a threshold. Preserve exact typed inputs/outputs and update every caller in the same increment.

## 3. Preserve Behavior And Error Ownership

Keep request shapes, pagination, cancellation, validation, permissions, i18n, focus, responsive behavior, and loading/error/empty/data states stable. Each async failure remains on its owning surface; failed overlays stay open and retain actionable errors.

## 4. Verify

After each increment, run focused tests and typecheck. At completion run the changed-file convention audit with `--strict`, format check, configured lint, CI unit tests, production build, and browser/Playwright checks required by DOM/layout/interaction behavior.

Re-run the surface inventory and explain any oversized file that remains cohesive. Do not call the refactor complete while independent surface implementations or inline tab bodies remain in a page/container.
