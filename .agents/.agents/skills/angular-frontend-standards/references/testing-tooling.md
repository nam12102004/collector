# Tests, Browser Checks, Tooling, And Completion

## Required Gate

Unless the user explicitly narrows verification, run the configured gates in this order:

1. format check;
2. ESLint, if an ESLint command is configured;
3. TypeScript application typecheck;
4. focused unit tests for changed behavior;
5. the repository's CI unit-test command;
6. production build.

Run browser/Playwright verification when the change depends on viewport, DOM geometry, overlays, table containment, scrolling, focus, keyboard interaction, drag/drop, or another browser-only behavior.

## Test Standard

- Test observable behavior, ownership boundaries, requests, state transitions, error states, and user intents rather than private implementation details.
- Cover success, authoritative failure, stale/cancelled async responses, empty data, and retry where relevant.
- Component tests verify typed inputs/outputs and meaningful rendering states.
- Facade tests verify query semantics, cancellation/paging, state ownership, and API coordination without requiring a page fixture.
- Browser tests verify behavior that compilation and jsdom cannot prove.

## Convention Audit

Run:

```powershell
node .agents/skills/angular-frontend-standards/scripts/audit-frontend-conventions.mjs src/Aubot.WCS.WebApp/src/app
```

Use `--changed --strict` to fail automatic findings in changed files. In a pull request, pass `--base origin/<target-branch>`; the `Frontend conventions` GitHub workflow does this and also runs the audit tests, typecheck, frontend tests, and production build. Configure this workflow as a required status check in branch protection to block merges. Existing untouched findings remain visible in a full audit but do not block unrelated pull requests.

Automatic audit findings cover inline tables, tabs, and overlays in route pages, plus pages with at least five subscriptions and two async state signals. The audit identifies route pages by a component file matching its immediate directory under `pages`. Other findings remain review items because their meaning depends on the screen; do not promote a line-count or raw-control heuristic to a failure without a reliable semantic rule.

## Completion Evidence

- Compare every changed component output with all parent bindings.
- Confirm every tab body follows the installed tabs implementation's component boundary and tabular collections use the current repository's accessible table pattern.
- Inventory changed files above the 300-line TypeScript or 250-line template review thresholds and record why any remains cohesive.
- Report every command and result. Do not claim browser behavior passed unless it was exercised.

ASRS does not configure ESLint or a dedicated `format:check` script. Use the installed Prettier and the configured `typecheck`, audit, test, and build commands; report any missing lint script as unconfigured rather than PASS.
