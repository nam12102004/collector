---
name: feature-from-spec
description: Strict workflow for implementing (or extending) a feature from a written requirement — an SRS/BRD screen, a use-case, or a ticket that references one. Use BEFORE writing any code for such work. It forces requirement → spec-table → contract → vertical slices → per-slice traceability so that every specified field, business rule, and cross-cutting requirement maps to code and is verified, preventing the coverage gaps that occur when a large spec is coded module-by-module from memory.
---

# Feature From Spec

The orchestration layer that sits above the build skills. It does not replace them: implement each slice with the repository's chosen backend standard (`*-dotnet-standards` + `create-dotnet-*`) and `angular-frontend-standards` + `create-angular-feature`, and audit with `review-dotnet-code` / `review-angular-code`. This skill's job is **coverage and correctness against the written spec**, which those skills do not guarantee.

## Why this exists

Complete docs still produce incomplete code when a large spec is generated piece-by-piece: long field lists get dropped (attention dilution), cross-cutting rules fall between modules, DRY shortcuts silently violate per-screen specs, and FE↔BE contracts drift into runtime bugs. The fix is not a stricter convention skill — it is a **traceability process**: extract every requirement into a checklist, derive one contract, build in vertical slices, and tick each requirement against running code.

## Completion Conditions

Do not declare a feature done until every row of its spec-table is implemented and ticked, the build agrees, and cross-cutting requirements are explicitly accounted for. Report tests, migrations, and browser behavior as user-run checks (this sandbox blocks `dotnet ef`, `dotnet test` execution, and app runtime — see the project's migration/test memory).

## 1. Extract the spec-table (do this first, before code)

For each screen/use-case, read the WHOLE requirement (including every sub-section and business-rule table) and produce a machine-checkable table — this is the coverage contract, and it is far cheaper to review than code. Do not skim; long field lists in a Phân khu / §5.x sub-table are exactly what gets missed.

Produce four tables per feature:

**A. Fields** (one row per field, per popup/drawer/form — do NOT merge two distinct dialogs into one list):

| Popup/Form | Field | Type | Required | Validation (BR) | Entity+Migration | DTO | Validator | FE control | Done |
|---|---|---|---|---|:--:|:--:|:--:|:--:|:--:|

**B. Business rules** (one row per BR-xx): `BR | Rule | Where enforced (service/validator/FE) | Done`.

**C. Endpoints**: `Method | Route | Request DTO | Response DTO | Auth policy | Done`.

**D. Cross-cutting touched** (tick which apply, then create an explicit slice for each — §5): notifications/role-matrix · audit log · system-generated activity/interaction logs · RBAC data-scoping (owner filter) · i18n (BOTH locales) · pagination default · import/export/template · enum-set completeness.

Review A–D against the source requirement line by line before writing code. Missing rows here are the gaps you are trying to prevent.

## 2. Define the contract once (single source of truth)

Before splitting work, fix the API contract: DTOs (request + response), routes, validation rules, enum values, error/success codes. Backend and frontend are both derived from this one contract — this is what prevents FE↔BE drift bugs. Match C# PascalCase to serialized camelCase. Mirror every backend validation rule in the FE form. If the contract needs a new enum value, entity column, lookup category, or permission, list it now (and flag any migration).

## 3. Slice vertically, never horizontally

Split the feature into slices where each slice is ONE use-case end-to-end:

`entity/column → migration → DTO → validator → service/business-rule → endpoint → FE model → FE service → FE form/list field → i18n → test`

Do not slice horizontally (all entities, then all services, then all UI) — that reintroduces dilution and drift. A slice must be small enough to hold entirely in working memory. Build green after each slice before starting the next.

## 4. Implement each slice against its checklist

For every slice, apply the repository build skills, then tick the matching spec-table rows against the running code. A slice is done only when its fields render, its BRs are enforced on the authoritative side (backend), the FE mirrors them, and it builds.

## 5. Cross-cutting requirements are their own slices

Anything not owned by a single screen — role-matrix notifications, audit logging, system-generated logs on state change, RBAC owner-scoping, i18n parity, pagination defaults — gets an explicit slice with its own acceptance list. Never leave these implicit; they are the most-missed class of requirement.

## 6. Verify per slice

- Build backend and frontend; keep both green per slice.
- Tick the spec-table. An unticked row is unfinished work, not a nice-to-have.
- Write integration tests for state-change flows (create, status transitions, two-way syncs) — these catch the FE↔BE contract bugs that a passing build cannot.
- Trace the full request → validation → response → FE-consumer path for each endpoint; confirm the FE sends values the backend accepts and queries with filters that match backend scoping.
- Report migrations and test execution as user-run steps.

## 7. Regression guardrails (verify every feature against these)

These are real defect classes observed in this codebase. Check each before declaring done:

1. **Field parity** — every field in the requirement's §5.x sub-tables exists end-to-end (entity → migration → DTO → validator → FE). Long lists (e.g. parent company, source, industry, size, revenue, bank) are the ones that get dropped.
2. **No form reuse across distinct specced dialogs** — each popup/drawer renders exactly the fields its own spec lists. Sharing one form between, say, "log interaction" and "assign task" leaks fields (priority/occurred-time) into the wrong dialog.
3. **List query ↔ backend scoping** — an untyped list query may be scoped by the backend (e.g. activities without an `activityType` exclude tasks). Query with the filter the backend expects; verify the returned set.
4. **Client timestamps** — for "must not be in the future" fields, default to the SERVER clock (send null → server stamps) and add a small skew tolerance; never send exact `new Date()` from the browser.
5. **Create-status contract** — the FE must send an initial status the backend accepts for that entity type.
6. **Dynamic titles/labels** — drawer/popup titles bind to record data (e.g. company name), not a hardcoded string, when the spec says so.
7. **Enum completeness** — enum sets match the spec (e.g. activity types Call/Email/Meeting/Demo/Note/Other), not a subset.
8. **i18n parity** — every user-facing key added to BOTH locale files; validate JSON.
9. **Import/export/template consistency** — when the spec asks for spreadsheet import, treat it as an end-to-end contract rather than a toolbar button:
   - obtain and inspect the official template artifact when available; if its link/artifact is missing, record the evidence gap and derive a provisional column table from the governing SRS instead of copying stale entity/form fields;
   - trace every template column through parser → lookup/enum conversion → request DTO → authoritative validation/persistence, and reject missing, renamed, unsupported, or removed columns with a stable file-level error;
   - verify every specified file extension, size/count limit, choose and drag/drop path, pre-import content preview, template download cache behavior, and permission gate;
   - preserve the physical spreadsheet row number, return all safe field failures for that row, render them on the FE, and count failed rows by distinct row number rather than validation-message count;
   - inspect the generated workbook contents in a focused test; do not certify template parity from the writer's source array alone.
10. **RBAC data-scoping + owner filters** — list/detail respect owner scoping; Manager/Admin owner filters present where specced.
