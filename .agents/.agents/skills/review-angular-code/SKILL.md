---
name: review-angular-code
description: Evidence-driven review workflow for Angular diffs, branches, pull requests, and local changes covering correctness, component boundaries, state ownership, NG-ZORRO usage, declarative templates, accessibility, contracts, and tests.
---

# Review Angular Code

Apply `angular-frontend-standards` and read every routed topic reference relevant to the diff. Review only; do not refactor or redesign unless separately requested.

## Establish Evidence

1. Read the complete diff and enough caller/backend context to trace behavior.
2. Read Angular, TypeScript, lint, test, design-system, and repository-profile configuration.
3. Identify the governing requirement when one exists; use `feature-from-spec` for spec coverage rather than mixing product traceability into coding convention.
4. Identify the nearest compliant surface and shared wrapper.
5. Run the convention audit against changed files and validate each heuristic finding against semantics.

## Review Gates

Review, in priority order:

- Correctness, security, permissions, API contracts, validation, nullability, async races, and stale state.
- Component ownership: pages compose; every tab and independent surface has a component; facades own qualifying shared/async orchestration.
- Semantic UI: tabular collections use `nz-table` plus its containment contract; library-supported widgets are not redrawn with custom HTML/CSS.
- Template quality: declarative bindings, prepared view models, no business transformation or side-effectful computation.
- State and errors: signals/RxJS fit the behavior, subscriptions are bounded, and errors render on the owning surface.
- Forms/i18n: typed forms, canonical Zod adapter, authoritative backend errors, complete locale parity, and single mutation feedback.
- Styling/accessibility/responsive behavior: tokens, NG-ZORRO/Tailwind ownership, keyboard/focus, overflow, and mobile/desktop usability.
- Tests and tooling: changed behavior, failure states, interaction-sensitive browser coverage, lint, typecheck, and build evidence.
- Touched-code ratchet: new or materially changed code meets the current standards; unrelated legacy debt becomes a separate finding/backlog item, not an unsolicited migration.

## Finding Standard

Report only actionable findings supported by code evidence. For each finding include severity `P0` through `P3`, file and tight line range, trigger, impact, violated topic/rule, and the smallest safe correction direction.

Do not report personal preferences or line count alone. A file above 300 TypeScript or 250 template lines requires an ownership review; report it only when the responsibilities are actually mixed or the rationale is missing.

## Output

Lead with findings ordered by severity, then open questions, a short summary, audit/tooling results, and residual browser or contract risks. If there are no findings, say so explicitly.
