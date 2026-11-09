# Repository Adaptation Checklist

Use this checklist before changing more than one screen or applying Aubot UI to an established application.

## Inventory

- Confirm framework/version and standalone/module/component conventions.
- Confirm component library, utility CSS, chart library, icons, i18n, state, and auth client.
- Locate global tokens, root styles, app shell, public layout, shared form/table/modal wrappers, theme service, and build command.
- Confirm whether login is app-owned or identity-provider-owned.
- Confirm logo files, accessible product name, supported themes, and localization ownership.
- Record target routes, permissions, API contracts, status meanings, and responsive conventions.

## Conflict Gate

Ask before proceeding when:

- the repository has a materially different established design system;
- applying Aubot UI would replace or mix component libraries;
- a required capability is missing and needs a dependency;
- login ownership, MFA, recovery, registration, or token persistence is unclear;
- the requested migration implies moving established architecture or broadly refactoring legacy screens.

## Mapping

Create an explicit source-to-target map:

| Aubot role | Target artifact | Decision |
|---|---|---|
| Semantic tokens | global theme/tokens | copy, translate, or extend |
| Portable utility classes (`.surface`, `.toolbar`, `.public-auth-*`, `.status-*`) | global stylesheet | copy as-is, or translate to the repository's utility-CSS mechanism |
| Logo variants (dark-surface vs. light-surface mark) | static asset directory | supply both, wired to the correct surface, not the active theme |
| Authenticated shell | layout component | adapt structure and breakpoints |
| Public auth layout | public layout or auth feature | create or reuse |
| Sign-in/callback/error | auth routes | map verified reason codes and actions |
| Dashboard metrics/charts | canonical dashboard | preserve data contract, change composition only |
| Chart theme sync | chart wrapper/effect | recompute colors from tokens on theme change, don't hardcode hex |
| CRUD toolbar/table/modal | shared wrapper or nearest page | reuse before creating |

Do not begin frontend models or auth behavior from visual assumptions.

## Implementation Order

1. Add or translate tokens and global component overrides.
2. Wire light/dark theme through the established service or store.
3. Adapt the authenticated shell and responsive navigation.
4. Adapt public auth layout, sign-in, callback, error, logout, and forbidden experiences.
5. Adapt shared operational patterns such as page headers, toolbars, tables, forms, and modals.
6. Adapt dashboard composition and chart token integration.
7. Migrate feature pages incrementally, keeping product behavior unchanged.

## Completion Evidence

- Theme loads in the correct global order.
- Feature styles consume tokens rather than repeat brand hex values.
- Public auth pages and authenticated shell share one visual language.
- External IdP login remains external; app-owned login remains contract-accurate.
- Routes, permissions, return URLs, logout, 401, 403, and session-expired behavior remain correct by static inspection.
- Loading, error, empty, and data states are present.
- Sidebar and toolbars adapt around 375px without covering content.
- Focus, labels, contrast, overflow, modal cancel behavior, and destructive confirmation are present.
- The permitted build succeeds.
- Report tests, lint, runtime auth flows, mobile/desktop browser checks, and accessibility automation as unexecuted unless explicitly requested.
