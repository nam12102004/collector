# TypeScript, Naming, Types, And Imports

## MUST-AUTO

- Do not weaken enabled TypeScript or Angular compiler checks to land a feature. Project-wide `strict` and `strictTemplates` are a separate migration until they are enabled in `tsconfig.json`; do not claim the current typecheck verifies them.
- Do not introduce explicit `any`. Use a concrete contract or `unknown` plus narrowing at an untrusted boundary.
- Follow the nearest ASRS feature's Angular DI convention; use `inject()` for new or materially changed code when it fits that established pattern.
- Remove unused imports and variables. Import only the component-library modules actually used.
- Use `readonly` for immutable component dependencies, signal handles, inputs, outputs, and constant collections.

## MUST-REVIEW

- Name files after their primary exported symbol using repository casing: kebab-case files, PascalCase types/classes, camelCase values and methods.
- Use business and UI intent in names. Avoid generic state names such as `data`, `items`, `flag`, or `handle` when the owning scope contains more than one concept.
- Prefer the shortest clear function or method name for the concrete operation and its subject, such as `openAisleDrawer`, `loadLiveWarehouseMap`, or `getOrCreateCraneGroup`. Read it with the owning type/module, parameters, return value, and body; omit context already obvious at the call site. Do not introduce `Ensure`/`ensure` prefixes for authored functions; validation or throwing an error alone does not require that prefix.
- Avoid filler qualifiers such as `Core`, `Internal`, `Detailed`, or `Safe` when they convey no functional distinction. Names must match actual behavior, side effects, and frequency; use `loadSnapshot` for a reloadable operation rather than claiming `fetchOnce`.
- Keep domain qualifiers needed to distinguish behavior. Do not list every implementation step in the name, shorten through cryptic abbreviations, or impose an arbitrary character limit.
- Use `validate...` for rule checks, action verbs for state changes or I/O, and `is`/`has`/`can` for boolean queries. Use `try...` for the established boolean-success pattern. Do not mechanically rename every `ensure...` to `validate...`; preserve framework/third-party API names and required interface implementations.
- When renaming an owned function, update imports, callers, template bindings, and tests together while preserving guards, exceptions, and observable behavior.
- Keep functions cohesive. Extract a named operation when a method mixes query construction, state transitions, mapping, and UI side effects.
- Do not use casts to silence a known contract mismatch. A boundary cast must sit beside runtime narrowing or an explicit invariant.
- Feature folders may import core/shared and their own feature. Cross-feature imports require an intentional public contract; relative-path convenience is not a boundary.
- Use configured aliases when they exist. Do not introduce aliases as part of an unrelated feature.

## SHOULD

- Prefer discriminated unions to coupled booleans for mutually exclusive UI state.
- Prefer small pure mapping functions for DTO-to-view-model conversion.
- Keep exports narrow; do not add barrels that expose feature internals.
