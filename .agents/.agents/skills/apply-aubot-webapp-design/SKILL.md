---
name: apply-aubot-webapp-design
description: Apply or migrate the Aubot enterprise WebApp design language to an existing web application, including the navy-and-emerald theme, authenticated application shell, responsive navigation, login or sign-in gateway, authentication callback and error screens, dashboards, CRUD pages, role-permission matrices, tables, forms, and light/dark behavior. Use when Codex is asked to make another repository look and behave like the Aubot KPI WebApp, reuse its layout or screen composition, standardize Aubot product UI, or adapt Aubot visual patterns to Angular or another established frontend stack without copying KPI-specific business logic.
---

# Apply Aubot WebApp Design

Reproduce the Aubot visual system and screen composition while adapting implementation details to the target repository's framework, architecture, authentication flow, component library, i18n, and product identity.

## Workflow

1. Inspect the target repository before editing:
   - Detect framework and version, component library, utility CSS, routing, application shell, shared components, tokens, i18n, auth ownership, state, and build command.
   - Locate the nearest canonical implementations for a public page, authenticated page, form, table, modal, loading state, and error state.
   - Identify any established design system that conflicts with Aubot UI.
2. Establish the migration contract:
   - Preserve product routes, permissions, copy, logo ownership, domain statuses, auth protocol, and API behavior.
   - Ask before replacing a conflicting design system, adding or replacing dependencies, changing auth ownership, or performing a broad legacy refactor.
   - If authentication is externally hosted, do not invent an app-owned password form. Brand the app's sign-in gateway and configure the identity provider separately when that system is in scope.
3. Read [design-system.md](references/design-system.md) completely for tokens, hierarchy, component mapping, and visual constraints.
4. Read only the relevant sections of [screen-blueprints.md](references/screen-blueprints.md) for the screens in scope. Read its authentication sections completely whenever login, callback, logout, 401/403, session expiry, or auth errors are involved.
5. Read [adaptation-checklist.md](references/adaptation-checklist.md) before implementing in a repository with an existing UI or when migrating more than one screen.
6. Apply the portable theme:
   - Copy `assets/aubot-enterprise-theme.scss` into the target repository's canonical global style location when SCSS is supported. It bundles tokens, a minimal reset, component-library overrides, and the portable utility classes (`.surface`, `.toolbar`, `.public-auth-*`, `.status-*`) in one file; split it across the repository's existing files (e.g. tokens/overrides in a theme partial, reset/utilities in the main global stylesheet) when that better matches local convention.
   - Otherwise translate the same semantic custom properties and utility classes into the established global styling mechanism.
   - Load the theme after the component library's base stylesheet and before feature overrides.
7. Implement screen composition using the target repository's installed components and wrappers. Preserve the blueprint's hierarchy, dimensions, density, responsive behavior, states, and accessibility rather than copying source markup literally.
8. Remove KPI-specific names, routes, permissions, service calls, status labels, and report semantics. Map target-domain concepts to semantic tokens in feature code.
9. Verify static behavior and run only the repository-permitted smallest build or compile command. Do not claim tests, browser checks, accessibility automation, or runtime flows passed unless explicitly requested and executed.

## Non-Negotiable Visual Contract

- Use a calm enterprise interface: navy navigation, emerald accent, neutral canvas, compact controls, clear borders, restrained shadows, 6-8px radii, and 64px shell alignment.
- Build hierarchy with spacing, alignment, typography, and borders before color.
- Keep public authentication screens visually related to the authenticated shell through the same logo treatment, tokens, typography, control heights, and focus states.
- Keep public auth routes outside the authenticated shell.
- Represent loading, error, empty, and data states for every asynchronous surface.
- Preserve visible keyboard focus, labels, WCAG AA contrast, safe text wrapping, and usable layouts around 375px mobile width.
- Do not use gradients, glass effects, decorative blobs, oversized marketing typography, excessive rounding, heavy shadows, or cards nested inside cards.
- Do not hardcode brand or status hex values in feature styles when semantic tokens exist.

## Framework Adaptation

- Prefer the target repository's established component library and shared wrappers. Map Aubot structures to equivalent layout, menu, alert, form, table, modal, skeleton, and feedback components.
- In Angular repositories using NG-ZORRO, use `nz-layout`, `nz-sider`, `nz-header`, `nz-content`, `nz-menu`, `nz-dropdown`, `nz-avatar`, `nz-table`, `nz-form`, `nz-alert`, `nz-modal`, and `nz-skeleton` where suitable.
- Preserve standalone versus NgModule style, state management, route conventions, i18n, and responsive primitives.
- Keep API calls and orchestration in route or smart components. Keep portable presentational components input/output driven.
- Use installed chart wrappers and derive chart colors from theme tokens; update charts when theme changes.
- Do not add a package merely to match the KPI implementation. Ask first when no suitable installed capability exists.

## Reference Source

The design was distilled from these canonical KPI WebApp artifacts:

- `src/KPI.WebApp/src/styles/_aubot-enterprise-theme.scss`
- `src/KPI.WebApp/src/app/layout/main-layout/`
- `src/KPI.WebApp/src/app/features/auth-error/`
- `src/KPI.WebApp/src/app/features/auth-callback/`
- `src/KPI.WebApp/src/app/features/dashboard/`
- `src/KPI.WebApp/src/app/shared/components/admin-crud-page/`

Use them as evidence when working in the KPI repository. Use this skill's portable assets and blueprints elsewhere.
