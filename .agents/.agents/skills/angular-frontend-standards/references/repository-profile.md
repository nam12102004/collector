# ASRS Angular Repository Profile

This file records repository facts. Topic references own the conventions.

## Runtime And Libraries

- Application root: `src/Aubot.WCS.WebApp`.
- Angular 22 standalone components. The current `tsconfig.json` enables selected compiler checks but does not yet enable project-wide TypeScript `strict` or Angular `strictTemplates`; do not report either as verified.
- NG-ZORRO 22, Angular CDK 22, Tailwind CSS 3.4, Less themes, and scoped SCSS are installed. Angular Material and Material Icons were removed.
- Signals own local UI state; RxJS owns HTTP and event streams. SignalR 10 provides warehouse realtime updates.
- Pixi.js 8 and Three.js 0.185 support warehouse visualization features.
- `TranslationService`, `I18N_KEYS`, and the shared `TranslatePipe` own localization.
- Vitest 4 runs unit tests through Angular's unit-test builder.

## Repository Shape

- `core`: app-wide API configuration, auth, guards, models, interceptors, and utilities.
- `shared`: feature-agnostic components and directives.
- `features`: feature-local pages, components, schemas, state, and workflows.
- `layout`: authenticated application shell.
- Feature API services own transport; use the nearest existing OMC/RCS service and facade patterns.
- Protected routes are lazy-loaded and permission guarded.

## UI And Canonical Evidence

- `src/styles/zorro-light.less`, `src/styles/zorro-dark.less`, `src/styles.scss`, and component `.scss` files own the theme and presentation; Tailwind 3 utilities are available for layout.
- Use NG-ZORRO for buttons, controls, tables, overlays, feedback, navigation, and status widgets. Keep Angular CDK for infrastructure and keep the 2D/3D warehouse canvas and charts.
- Each validated field reserves at least one line for `nzErrorTip`; map backend validation messages to the matching control and show unmatched messages at form level.
- Do not reintroduce Angular Material or Material Icons. Do not add Zod, NgRx, or Jest by default. No frontend ESLint/lint script is configured.

## Commands

Run from `src/Aubot.WCS.WebApp`:

- `npx.cmd prettier --check <changed-files>`
- `npm.cmd run typecheck`
- `npm.cmd test -- --watch=false`
- `npm.cmd run build -- --configuration=production`

Run the convention-audit test and the ASRS frontend audit from the repository root. Review heuristic findings; they are not configured lint rules. If Angular cache produces a native crash or locked output, retry with `CI=true` and report the locking process rather than changing source code.
