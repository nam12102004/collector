# Aubot Screen Blueprints

## Contents

- Authenticated application shell
- Authentication ownership decision
- Sign-in gateway
- App-owned login form
- Authentication callback
- Authentication error and session screens
- Dashboard
- CRUD and administration pages
- Role-permission matrix
- Responsive rules

## Authenticated Application Shell

Use this composition:

```text
full-height application
|- sidebar: 260px expanded, navy, collapsible
|  |- 64px brand row
|  `- permission-filtered primary navigation
`- main column
   |- header: 64px surface row
   |  |- navigation trigger
   |  `- theme action, user avatar/name, user menu
   `- scrollable content: neutral canvas, 24px desktop padding
```

- Align the sidebar brand row and header to 64px.
- Use the product's own logo and name. The expanded logo target is about 110x32px, collapsing to a 32px mark. Ship a dark-surface logo variant (light/white mark) for the brand row and a light-surface variant (dark mark) for public-auth panels — see [design-system.md](design-system.md#logo-variants). Give the brand row its own fixed background token (e.g. `--sidebar-brand-bg`) independent of `--primary-color` so it doesn't shift when the primary token is retuned.
- Highlight the selected dark-menu item with emerald. Omit inaccessible navigation entries unless the product contract explicitly requires disabled visibility.
- Collapse or overlay navigation below the component library's standard "large" breakpoint (NG-ZORRO's `lg` is ~992px). Below it, keep the sidebar `position: fixed`, drive its open/closed state off the same collapsed signal as the desktop trigger, and add a full-screen backdrop button (`opacity`/`pointer-events` toggled by the collapsed state, not `display: none`, so the transition animates) that closes the sidebar on click. Never leave a permanent 260px sidebar beside 375px content.
- Header actions read left to right: menu trigger, theme toggle, language switcher, user menu. Give the theme toggle, language switcher, and any similar chip controls a shared style (~32px height, ~12px horizontal padding, bordered, icon+label) so they read as one control family; hide their text labels below ~767px and keep only the icon. The user menu trigger pairs an avatar with the display name (hide the name below ~767px) and opens a dropdown whose only required item is logout.
- Keep theme and user controls keyboard accessible and localized.
- Keep the content canvas scrollable while the shell stays full height.

## Authentication Ownership Decision

Determine ownership before creating any login UI:

1. If an external OIDC/OAuth identity provider owns credentials, use the sign-in gateway blueprint. Do not add username/password fields to the application.
2. If the application backend explicitly owns credential authentication, use the app-owned login blueprint and match its verified contract, validation, MFA, password-reset, and error behavior.
3. If ownership cannot be proven, stop and ask. A visual migration does not authorize changing the security model.

Public auth routes must render outside the authenticated application shell.

## Sign-In Gateway

Use for external identity providers:

```text
main.public-auth-page (neutral full-height canvas)
`- section.public-auth-panel (max 440px, 32px padding desktop, 24px mobile)
   |- img.public-auth-logo (light-surface logo variant)
   |- div.public-auth-header
   |  |- p.public-auth-product (eyebrow/product name)
   |  |- h1.public-auth-title (concise title)
   |  `- p.public-auth-copy (optional explanation; omit the element when there's nothing to say)
   |- optional semantic alert for a non-sensitive environment/organization note or a recovered reason (see below)
   `- button.public-auth-action (primary "Continue to sign in")
```

- Use the portable classes from [design-system.md](design-system.md#portable-utility-classes) (`public-auth-page`, `public-auth-panel`, `public-auth-logo`, `public-auth-header`, `public-auth-product`, `public-auth-title`, `public-auth-copy`, `public-auth-action`) rather than reproducing the layout ad hoc.
- Use the global tokens, 1px border, 8px radius, and no decorative artwork.
- Explain that the user will continue to the trusted identity provider when useful.
- When the route carries a reason (`session-expired`, `logged-out`, etc.), swap the title/copy and add a semantic alert (e.g. `nz-alert nzType="warning"`) instead of the plain explanation, and change the action label accordingly (e.g. "Sign in again" instead of "Continue to sign in").
- Preserve a sanitized internal return URL through the repository's established OIDC mechanism.
- Configure branding of the provider-hosted login page in that provider; app SCSS cannot style a cross-origin hosted page.

## App-Owned Login Form

Use only when the repository proves the app owns credentials:

```text
main.public-auth-page (neutral full-height canvas)
`- section.public-auth-panel (max 440px)
   |- img.public-auth-logo
   |- div.public-auth-header (product eyebrow, title, supporting copy)
   |- form
   |  |- email/username label and control
   |  |- password label, control, reveal action
   |  |- optional remember-device control
   |  `- inline field errors and form-level alert
   |- button.public-auth-action, full-width variant, primary submit
   `- verified recovery/help actions
```

Reuse the same `public-auth-*` classes as the sign-in gateway; only the panel contents differ.

- Never add recovery, registration, social sign-in, MFA, or remember-me behavior without a verified contract.
- Disable or mark the submit action busy during submission without changing layout.
- Keep server errors non-enumerating and localized by stable codes when the backend supports them.
- On mobile, use 16px viewport padding and 24px panel padding.

## Authentication Callback

Replace a bare text callback with a stable branded state:

- Use the same `public-auth-page`/`public-auth-panel`/`public-auth-header` family as the sign-in gateway.
- Show logo, `public-auth-title` such as "Signing you in", `public-auth-copy` with one line explaining the redirect, and a compact spinner (e.g. `nz-spin`) below the header block. Mark the panel `aria-live="polite"` so the status is announced.
- Trigger the completion call once from the component's init lifecycle hook, not from the template, to avoid re-firing on change detection.
- Prevent duplicate submission or navigation.
- Restore required OIDC transaction state before callback processing and before guards redirect.
- On failure, replace history and route to the auth-error screen with a stable reason code and sanitized return URL.

## Authentication Error And Session Screens

Use one reusable public-auth outcome layout:

```text
main.public-auth-page (neutral full-height canvas)
`- section.public-auth-panel (max 440px, 32px padding, 24px gap)
   |- img.public-auth-logo
   |- div.public-auth-header (p.public-auth-product eyebrow, h1.public-auth-title 28px)
   |- semantic alert (nz-alert) with message and optional description
   `- a/button.public-auth-action, primary recovery action aligned to start
```

Support verified stable reason codes such as:

| Reason | Tone | Recovery |
|---|---|---|
| callback failed | warning/error | retry login |
| profile invalid | warning | retry or contact support |
| account unlinked | warning | retry or verified support path |
| session expired | informational/warning | sign in again |
| logged out | informational/success | sign in again |
| forbidden | warning | return to a permitted page; never refresh tokens |

- Keep diagnostics free of tokens, authorization codes, PKCE values, and raw provider payloads.
- Do not expose stack traces or internal identifiers.
- A 403 must not trigger refresh or logout. A failed refresh clears auth state once and enters the session-expired flow.
- If already authenticated when retrying, navigate to the sanitized return URL instead of starting another login.

## Dashboard

Use this operational order:

1. Page header with title/supporting text and responsive period/export filters.
2. Three to five metric tiles in a grid; use 16px gaps, 90px minimum height, and stable loading skeletons (wrap each tile's content in a skeleton keyed to the page's loading signal, not a separate per-tile fetch state).
3. Chart panels in a 2:1 desktop grid that becomes one column below roughly 1000px.
4. A table panel (or, for a lighter dashboard, an activity/feed panel with an explicit empty state) with local filters near its title and server-driven pagination.

- Use 24px page title, 16px panel titles, and tokenized surfaces/borders.
- Make selectable metrics real buttons with selected/focus states.
- Derive chart series and labels from semantic tokens and refresh them on theme changes — see the concrete `getComputedStyle` + `effect()` pattern in [design-system.md](design-system.md#light-and-dark-themes).
- Reduce the metric grid from its full column count to 3 columns around 1200px, then to a single column around 700px; stack full-width filters at the same ~700px point.

## CRUD And Administration Pages

Use a predictable page hierarchy:

1. Page title and concise context.
2. Responsive toolbar: search/filter/help on the left and primary create action on the right.
3. Bordered data table with explicit loading, empty, error, and data states.
4. Server pagination close to the table.
5. Create/edit modal or route form according to task complexity and repository convention.

- Reuse the target repository's existing CRUD wrapper when suitable; otherwise compose the toolbar from the portable `.toolbar`/`.toolbar-filters` classes (see [design-system.md](design-system.md#portable-utility-classes)).
- Keep desktop search around 280px and full width on mobile.
- Below roughly 640px, stack toolbar groups and make filters full width.
- Keep row actions grouped at 4px gaps with accessible labels and destructive confirmation.
- Use a modal only for one bounded task. Prefer a route page for multi-step or long forms.

## Role-Permission Matrix

Use this composition for role administration with a permission matrix:

```text
bordered operational surface
|- role panel: 220-260px
|  |- compact role list with selected state
|  |- always-visible, labeled edit/delete actions
|  `- full-width dashed create action
`- matrix panel: remaining width
   |- role title and save action
   |- bordered permission table
   `- concise prerequisite/save hint
```

- Use the installed data-table component, not a raw HTML table. In NG-ZORRO, enable bordered, loading, no-pagination, horizontal scroll, and a sticky 220px first column.
- Derive action columns from the target permission contract. Do not copy KPI-only actions such as Export, Import, Unlock, or Grade into another product.
- Render every backend-catalog permission that operators must manage, including non-CRUD rows such as an explicit `data-scope.read-all`; do not infer full-data access from an editable role name or code.
- Auto-select the first available role when no valid selection exists. Keep system roles visible and locked or hidden according to the target contract.
- Keep edit/delete actions visible without hover, stop their clicks from selecting the role, and provide localized tooltip and accessible names for icon-only buttons.
- Represent role-list loading/empty states and matrix loading/error/empty/data states without layout shift.
- Enforce permission prerequisites in UI state only as a reflection of backend rules; for example, disabling dependent actions until Read/View is selected and clearing row dependents when it is revoked.
- On narrow screens, stack the role panel above the matrix and preserve table horizontal scrolling. Do not squeeze permission columns below usable checkbox targets.

When the full-stack bootstrap is in scope, seed one immutable system Administrator role and grant it the complete permission catalog. Provision the initial application user and its user-role join idempotently from environment-owned identity data. Keep production identity values out of EF seed data and source control; never treat a string such as `ADMIN` as an authorization bypass.

## Responsive Rules

- Around 1200px: reduce a wide metric-tile grid to 3 columns.
- Around 991-1000px: stack chart/table-header columns to one, and collapse the sidebar to a fixed overlay with backdrop (component libraries typically expose this as a named breakpoint, e.g. NG-ZORRO's `lg` ≈ 992px — use it rather than a hand-picked pixel value when available).
- Around 767px: reduce shell/content padding, hide header-control and user-menu text labels (keep icons and avatar), reduce the page title size, and stack toolbars/filters to full width.
- Around 700px: collapse metric grids to one column.
- Around 640px: stack CRUD toolbars.
- Around 480px: use 16px public-page padding, 24px auth-panel padding, and 24px auth titles.
- Treat these values as the KPI/CRM design intent. Use the target component library's nearest established breakpoints when they differ, while preserving the same behavior.
