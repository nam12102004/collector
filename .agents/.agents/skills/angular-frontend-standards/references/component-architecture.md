# Component Architecture And Ownership

## Surface Inventory

Before editing a screen, inventory its route/page, stateful toolbar/filter, data table, each tab, editor, detail surface, import workflow, modal, and drawer. Assign one owner to each surface.

## MUST-REVIEW Boundaries

- A route page owns route state, permission checks, surface composition, and cross-surface coordination. It MUST NOT contain the implementation of a stateful toolbar, data table, editor, detail view, import workflow, modal/drawer body, or tab body.
- Every tab body MUST be a feature-local component. The parent tabset may own the title, permission gate, selected index, and lazy template only. Apply library-specific syntax such as `nz-tab` only when that library is installed and used by the feature.
- A tab/surface component consumes typed `input()` values or a typed view model and emits typed `output()` user intents. Shared feature state and API orchestration belong to a facade when the facade gate applies.
- A workflow component owns one cohesive form or interaction lifecycle: open/reset/validate/submit/cancel and surface-local errors. It does not absorb unrelated workflows merely because they share a drawer.
- A presentational component does not call APIs, navigate, or mutate shared state.
- Define and verify exact input/output names at every parent binding. Within a feature, use one close-event convention, preferably `closed`.

## Facade Gate

A feature-local facade is required when any of these is true:

- state is shared by two or more sibling surfaces;
- a page coordinates two or more independent asynchronous workflows;
- the workflow owns cancellable search plus paging/load-more or multiple loading/error lifecycles;
- orchestration must be reused or tested independently from its view.

The facade owns feature state and orchestration. API services own transport. Components own view-local interaction. Do not accumulate request IDs, pagination cursors, search subjects, and unrelated error signals in a route page when the facade gate is met.

## Reuse

- Keep a component feature-local for its first real consumer.
- Promote it to `shared` only after at least two real consumers demonstrate a stable feature-agnostic API.
- Do not predict reuse by creating generic wrappers before the common contract exists.
- Reuse an existing shared wrapper or canonical surface when it already satisfies the requirement.

## Responsibility Review

TypeScript over 300 lines or a template over 250 lines triggers a mandatory ownership review, not a mechanical split. Completion is blocked by mixed independent responsibilities, not by line count alone. Record why an oversized file remains cohesive.
