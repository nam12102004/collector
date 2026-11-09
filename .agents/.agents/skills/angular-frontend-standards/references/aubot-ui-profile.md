# Aubot UI Profile

Apply this profile only when the user asks for Aubot UI, an interface like the KPI application, the navy-and-emerald theme, or consistent UI across Aubot projects. It does not authorize replacing a conflicting established design system. ASRS uses NG-ZORRO widgets with the Aubot navy-and-emerald theme and keeps Angular CDK for interaction infrastructure.

## Source of Truth

Prefer the target repository's existing Aubot tokens and shared components. In the KPI repository, use these canonical artifacts:

- `src/KPI.WebApp/src/styles/_aubot-enterprise-theme.scss` for portable tokens, light/dark behavior, and NG-ZORRO overrides.
- `src/KPI.WebApp/src/app/layout/main-layout/` for the shell composition.
- `src/KPI.WebApp/src/app/shared/components/admin-crud-page/` for repeated administration pages.

Copy or adapt the portable theme rather than reproducing its values across component styles. Keep product-specific routes, permissions, copy, logos, and domain status names outside the shared theme.

## Visual Contract

Use a calm enterprise interface with a navy navigation shell, emerald accent, neutral content canvas, compact controls, clear borders, and restrained shadows. Do not use gradients, decorative blobs, glass effects, oversized marketing typography, excessive rounding, or cards nested inside cards.

Use these light-theme tokens when the target has no existing Aubot token file:

```css
:root {
  --primary-color: #1e3a8a;
  --primary-color-hover: #2563eb;
  --primary-color-active: #1d4ed8;
  --primary-color-light: #eff6ff;
  --accent-color: #10b981;
  --accent-color-hover: #059669;
  --accent-color-light: #ecfdf5;
  --bg-color: #f9fafb;
  --surface-color: #ffffff;
  --text-color: #111827;
  --muted-text-color: #6b7280;
  --border-color: #e5e7eb;
}
```

Use the portable theme's dark-token values. Activate dark mode with `data-theme="dark"` on the document root. Persist the user's preference in an application service; keep the storage key configurable or repository-consistent. Ensure the theme is applied before first paint when the application already supports startup initialization.

Use semantic tokens instead of feature-local status hex values:

| Meaning | Foreground | Background | Border |
|---|---:|---:|---:|
| Excellent / success | `#059669` | `#ecfdf5` | `#a7f3d0` |
| On track / informational | `#2563eb` | `#eff6ff` | `#bfdbfe` |
| Needs attention / warning | `#d97706` | `#fffbeb` | `#fde68a` |
| Poor / destructive | `#dc2626` | `#fef2f2` | `#fca5a5` |

Map domain labels to these meanings in feature code. Do not put KPI-specific terminology into the general theme.

## Application Shell

Build the authenticated shell with NG-ZORRO `NzLayoutModule` and `NzMenuModule`:

```text
nz-layout
├── nz-sider: navy, 260px expanded, collapsible, product logo and permission-filtered menu
└── nz-layout
    ├── nz-header: 64px, surface background, menu trigger, theme control, user menu
    └── nz-content: scrollable neutral canvas, 24px desktop padding
```

- Use `nz-sider`, `nz-header`, `nz-content`, `nz-menu`, `nz-dropdown`, `nz-avatar`, and `nz-icon`; do not recreate them with raw containers.
- Keep the header and sidebar at 64px alignment points.
- Highlight the selected dark-menu item with the emerald accent.
- Use repository permissions to omit inaccessible navigation entries; do not merely disable them unless the product contract requires visibility.
- Use NG-ZORRO breakpoint/collapse APIs for narrow screens. At mobile width, avoid a permanently occupying 260px sider and keep the content usable around 375px.
- Make the menu trigger and theme toggle real keyboard-accessible buttons with localized accessible names.
- Add breadcrumbs only when hierarchy warrants them; do not add a decorative breadcrumb row.

## Page Composition

Use a predictable operational-page order:

1. Page title and concise supporting context when needed.
2. Primary action and relevant filters in one responsive toolbar.
3. Main NG-ZORRO table, form, chart, or content surface.
4. Pagination and secondary actions close to the surface they affect.

Use 24px for major desktop page spacing, 16px inside ordinary surfaces, 8-12px between related controls, and 4px between compact icon actions. Keep radii at 6-8px. Prefer a 1px neutral border and minimal shadow. Avoid translating every section into a card.

Use NG-ZORRO components for supported widgets:

- `nz-table` for growing collections with backend pagination, sorting, filtering, and explicit loading/empty/error states.
- `nz-form` controls with labels above inputs and inline validation.
- `nz-modal` for one bounded task, with a clear primary action and cancel path.
- `nz-tag`, alerts, messages, notifications, progress, and skeletons for their intended states.
- Installed chart wrappers for charts; derive chart colors from theme tokens and update them on theme changes.

Use Tailwind utilities for responsive layout and spacing when available and canonical. Keep reusable visual decisions in tokens or shared wrappers, not repeated arbitrary-value classes.

## Interaction and Quality

- Keep body text readable and compact controls at 14px when density requires it.
- Make focus visible with the primary color and a restrained focus ring.
- Use emerald for positive emphasis and navigation selection, not as decoration on every element.
- Reserve red for destructive or poor states; require confirmation for destructive actions.
- Use motion only to explain state changes, normally 100-200ms, and respect `prefers-reduced-motion`.
- Verify light/dark contrast, keyboard navigation, text overflow, loading/error/empty/data states, and mobile/desktop layout statically.

## Completion Check

Before handing off an Aubot UI implementation, confirm that:

- the portable theme is loaded after the NG-ZORRO base stylesheet;
- component styles consume tokens rather than duplicate brand hex values;
- the shell uses NG-ZORRO Layout/Menu and responds at mobile width;
- product-specific routes, permissions, copy, and domain labels remain local;
- user-facing strings follow the repository's i18n convention;
- asynchronous surfaces include loading, error, empty, and data states;
- the permitted Angular build succeeds.
