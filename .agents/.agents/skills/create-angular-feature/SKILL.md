---
name: create-angular-feature
description: Strict workflow for building or extending an Angular feature from a verified API or product contract while applying the repository's frontend standards, component boundaries, NG-ZORRO capabilities, tests, and quality gates.
---

# Create Angular Feature

Apply `angular-frontend-standards` first. This skill owns the build workflow; the routed topic references own coding conventions.

## 1. Establish Evidence

- Read the governing requirement or ticket, real backend endpoint/DTO/validator/permission, Angular version/config, and nearest compliant feature.
- If a written SRS/BRD governs the task, apply `feature-from-spec` before coding.
- Read the Angular topic references required by the surfaces and behavior in scope.
- Do not invent a backend contract, permission, route, dependency, state library, or shared abstraction.

## 2. Design The Feature Before Markup

Create a short implementation map:

`use case -> API contract -> facade/state owner -> page/surface components -> NG-ZORRO/shared capabilities -> tests`

Inventory the route page, stateful toolbar/filter, data table, every tab, editor, detail, import, modal, and drawer. Apply the component and facade gates before creating files. Classify every repeated collection with the semantic table gate before choosing list, cards, or `nz-table`.

## 3. Build In Cohesive Slices

For each use case:

1. Add or extend exact typed models and transport methods.
2. Add the feature facade when the facade gate applies.
3. Build each tab and independent UI surface as its own feature-local component.
4. Keep the page focused on route state, permissions, composition, and cross-surface coordination.
5. Add forms, validation/error mapping, i18n, responsive states, and accessibility through the routed topic standards.
6. Promote code to `shared` only when two real consumers prove a stable feature-agnostic API.
7. Add focused tests before starting the next slice.

Keep the application buildable between slices. Do not expand into unrelated legacy cleanup; materially touched code must meet the current standards.

## 4. Verify

Run the convention audit for changed files, format check, strict changed-file lint, typecheck, focused tests, CI unit tests, and production build. Run browser/Playwright checks for DOM/layout/interaction-sensitive work.

Before completion, verify exact component input/output bindings, tab-component boundaries, semantic table decisions, facade ownership, loading/error/empty/data states, both locale files, and any oversized-file responsibility review. Report every command and result.
