# Aubot WebApp Design System

## Contents

- Visual direction
- Semantic tokens
- Portable utility classes
- Logo variants
- Typography and spacing
- Surfaces and controls
- Component mapping
- Light and dark themes
- Accessibility and motion

## Visual Direction

Use a calm, operational enterprise design. The authenticated product is anchored by a navy navigation shell and emerald selection or positive emphasis. Content lives on a neutral canvas with white or dark surfaces, crisp borders, compact controls, and minimal shadows.

Do not carry KPI terminology into another product. Preserve the visual grammar, not the business vocabulary.

## Semantic Tokens

Use `assets/aubot-enterprise-theme.scss` as the portable source. The essential light tokens are:

| Role | Token | Value |
|---|---|---:|
| Primary navy | `--primary-color` | `#1e3a8a` |
| Primary hover | `--primary-color-hover` | `#2563eb` |
| Primary active | `--primary-color-active` | `#1d4ed8` |
| Primary tint | `--primary-color-light` | `#eff6ff` |
| Text on primary | `--on-primary-color` | `#ffffff` |
| Accent emerald | `--accent-color` | `#10b981` |
| Accent hover | `--accent-color-hover` | `#059669` |
| Accent tint | `--accent-color-light` | `#ecfdf5` |
| Text on accent | `--on-accent-color` | `#052e2b` |
| Canvas | `--bg-color` | `#f9fafb` |
| Surface | `--surface-color` | `#ffffff` |
| Surface hover | `--surface-hover` | `#f3f4f6` |
| Text | `--text-color` | `#111827` |
| Muted text | `--muted-text-color` | `#6b7280` |
| Border | `--border-color` | `#e5e7eb` |
| Sidebar brand row | `--sidebar-brand-bg` | `#172554` |

`--sidebar-brand-bg` is fixed navy in both themes (not redefined under `[data-theme='dark']`) so the brand row never shifts tone. `--on-primary-color` and `--on-accent-color` exist so text/icons on filled primary or accent surfaces (buttons, selected menu items, avatars) don't have to hardcode white.

Use semantic status tokens rather than domain-specific colors:

| Meaning | Token prefix | Foreground | Background | Border |
|---|---|---:|---:|---:|
| Success | `--success-*` | `#059669` | `#ecfdf5` | `#a7f3d0` |
| Information | `--info-*` | `#2563eb` | `#eff6ff` | `#bfdbfe` |
| Warning | `--warning-*` | `#d97706` | `#fffbeb` | `#fde68a` |
| Destructive | `--danger-*` | `#dc2626` | `#fef2f2` | `#fca5a5` |

Never name general tokens after KPI ranks, departments, or reports — the tokens above are `--success/--info/--warning/--danger`, not `--excellent/--on-track/--poor`. Map target-domain labels to these meanings in feature code instead (e.g. a KPI-flavored consumer can alias "excellent" -> `--success-*` in its own component styles without renaming the shared token).

## Portable Utility Classes

`assets/aubot-enterprise-theme.scss` ships a small set of framework-agnostic utility classes so feature code composes layout instead of repeating raw CSS:

| Class | Purpose |
|---|---|
| `.page-title` / `.page-subtitle` | Operational page header text |
| `.surface` | Bordered, radius-8 panel background |
| `.toolbar` / `.toolbar-filters` | Responsive header row with left filter group and right action(s) |
| `.money` | Tabular-nums, weight-600 numeric values |
| `.muted` | Secondary/supporting text color |
| `.status-success` / `.status-info` / `.status-warning` / `.status-danger` | Chip/tag backgrounds mapped to the semantic status tokens |
| `.public-auth-page` | Full-height centered canvas for sign-in, callback, error, and forbidden screens |
| `.public-auth-panel` | The centered max-440px panel inside the canvas |
| `.public-auth-logo` / `.public-auth-header` / `.public-auth-product` / `.public-auth-title` / `.public-auth-copy` | Logo and heading block inside the panel |
| `.public-auth-action` | Primary action button, left-aligned, min-width 136px |

Prefer these over reproducing the blueprint markup ad hoc — see [screen-blueprints.md](screen-blueprints.md) for how they compose per screen.

## Logo Variants

Ship two logo files and pick by the surface's own background, not by the active theme:

- A light-colored/white mark for dark surfaces (the navy sidebar brand row, which stays navy regardless of the active theme) — e.g. `aubot-logo-dark.png` (named for the *dark background* it targets).
- A dark-colored mark for light surfaces (the public-auth canvas in light mode) — e.g. `aubot-logo-light.png`.

`data-theme` is set on the document root (not scoped to the authenticated shell), so public-auth screens inherit whichever theme the user last chose even though they expose no theme toggle of their own — `.public-auth-page`/`.public-auth-panel` follow `--bg-color`/`--surface-color` like any other surface. Read the saved/preferred mode and apply it to the root before first paint (constructor-time, not after a route resolves) so there's no light-to-dark flash on the login screen.

## Typography And Spacing

- Use Inter when licensed and already available; otherwise use the repository's established sans-serif stack.
- Use 24px, weight 600 for operational page titles; public auth titles may use 28px desktop and 24px mobile.
- Use 16px, weight 600 for panel titles; use 13-14px for compact supporting text and control labels.
- Use 24px for major desktop page padding, 16px inside ordinary surfaces, 8-12px between related controls, and 4px between compact icon actions.
- Keep controls predominantly 32-40px high according to the selected component library.
- Keep radii at 6px for controls and 8px for panels.

## Surfaces And Controls

- Prefer a 1px neutral border over a shadow. Use a small shell/header shadow only to communicate layering.
- Do not wrap the entire page in a card. Use panels for bounded charts, metrics, forms, tables, and public auth tasks.
- Buttons must have a clear primary/secondary/destructive hierarchy, stable height, visible focus, disabled state, and localized accessible name for icon-only actions.
- Put form labels above controls. Keep validation beside the affected field. Never use placeholder text as the only label.
- Keep tables dense but readable, with stable columns, server pagination for growing data, explicit loading and empty states, and horizontal overflow at narrow widths.

## Component Mapping

Map each role to the target repository's established component:

| Role | NG-ZORRO reference | Required behavior |
|---|---|---|
| App shell | Layout, Sider, Header, Content | 64px alignment, responsive navigation |
| Navigation | Menu | permission-filtered, selected state in emerald |
| User actions | Dropdown, Avatar | keyboard accessible, explicit logout |
| Page feedback | Alert, Message, Notification | semantic severity, concise copy |
| Loading | Skeleton, Spin | stable layout, meaningful status text on public auth pages |
| Data | Table, Pagination | server metadata, loading/empty/error/data states |
| Forms | Form, Input, Select, DatePicker | labels, inline validation, error summary when needed |
| Bounded task | Modal | one purpose, primary action, cancel path |
| Status | Tag, Badge | semantic token mapping, not decorative |

## Light And Dark Themes

- Set `data-theme="dark"` on the document root (`document.documentElement`, not a shell-scoped wrapper) — this makes public-auth routes theme-aware for free since they render under the same root, provided the service below is actually running on those routes.
- Back the mode with a signal/observable a root-provided (`providedIn: 'root'`) service owns: read `localStorage` first, fall back to `prefers-color-scheme`, and expose the current mode read-only. Persist through the target repository's approved application service or store rather than copying a storage strategy blindly.
- A `providedIn: 'root'` service only runs its constructor on first injection — Angular does not instantiate it at bootstrap for free. If only the authenticated shell injects it (e.g. only the sidebar's theme-toggle button does), a cold load of a public-auth route never applies the stored theme and silently falls back to light. Inject it somewhere that resolves on every route (the root app component, or a bootstrap-time provider factory) so public-auth pages are theme-correct on first paint too, not only after the user has passed through the authenticated shell once in the same session.
- Apply the theme in that service's constructor (not in a component's `ngOnInit` or an app-init hook that runs after the shell resolves) so there's no flash before first paint.
- Keep component-library overrides centralized in the theme asset. Do not scatter dark-mode patches across feature SCSS files.
- Recompute chart colors when the theme changes: read tokens via `getComputedStyle(document.documentElement).getPropertyValue('--token-name')` inside an `effect()` (or equivalent reactive subscription) keyed on the theme signal, then destroy and re-render the chart. Do not hardcode chart hex values that would desync from a manual token edit.

## Accessibility And Motion

- Meet WCAG AA text contrast in both themes.
- Use a restrained focus ring derived from the primary color.
- Keep all triggers as real buttons or library controls, not clickable spans or divs.
- Keep motion between 100-200ms for micro-interactions and respect `prefers-reduced-motion`.
- Ensure labels, errors, table headers, dialog titles, and status changes are exposed to assistive technology.
