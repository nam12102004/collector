---
name: frontend-calendar-standards
description: Strict, version-adaptive standards for implementing, reviewing, or refactoring Angular calendar screens with NG-ZORRO Calendar. Use for NzCalendarComponent, calendar cell templates, month/year navigation, date-range loading, event rendering, date selection, disabled dates, custom headers, locale and time-zone behavior, responsive calendar layouts, accessibility, performance, or calendar tests.
---

# Frontend Calendar Standards

Apply `angular-frontend-standards` with these calendar-specific rules. Preserve the repository's architecture, component-library conventions, i18n, design tokens, state approach, and verification policy instead of repeating or replacing them here.

## Rule Strength

- Treat `MUST` and `MUST NOT` as release-blocking.
- Treat `SHOULD` as the default unless repository evidence or confirmed requirements justify a deviation.
- Let the installed Angular and NG-ZORRO versions, public contracts, locale, and established date utilities outrank examples.

## First Move

1. Read the frontend `package.json`, lockfile, Angular configuration, locale providers, shared date utilities, and nearest canonical screen.
2. Inspect the installed `ng-zorro-antd/calendar` public types and the relevant implementation, tests, or observed browser behavior before choosing imports, inputs, outputs, or templates. Do not infer event semantics from output names alone.
3. Read [NG-ZORRO calendar reference](references/ng-zorro-calendar.md) completely.
4. Confirm the calendar's purpose, supported views, selection behavior, event density, date/time contract, display time zone, permissions, and API range-filter contract before editing.
5. Record the initial panel date, locale week start, visible query range, loading/error ownership, and required mobile behavior.

## Capability Gate

- Use `NzCalendarComponent` for month/year date-data display or prominent calendar selection.
- Use the repository-approved date, range, and time picker components for ordinary form fields instead of embedding a full calendar.
- MUST NOT imply that NG-ZORRO Calendar provides week/day time grids, overlapping appointment layout, resource lanes, recurrence editing, drag-to-create, or drag-rescheduling.
- When any unsupported scheduler capability is required, stop after explaining the capability gap and request a user decision before choosing a package or writing package-specific architecture. If comparison helps the decision, present compatible choices without designating one as selected, assuming licensing or budget, or building the implementation around it. Do not handcraft a scheduler with DOM listeners or CSS positioning.

## State and Data Flow

- Keep selected date, panel anchor date, calendar mode, visible query range, filters, and load state as distinct typed state.
- Use the installed version's verified active-value or navigation path to calculate and load the visible range. Load the initial range explicitly; do not wait for the first user event. MUST NOT assume `nzPanelChange` fires for built-in header month/year navigation; NG-ZORRO 21.3.2 emits it only for mode changes.
- Include adjacent-month days needed by the rendered grid. Derive boundaries from the panel month and locale week rules; do not assume an API query for the named month covers every visible cell.
- Cancel superseded range requests or ignore stale responses. A slower prior panel request MUST NOT overwrite the current panel.
- Keep server-side range, permission, owner, and status filtering authoritative for growing event collections. Do not fetch an unbounded history for client-side filtering.
- Index loaded items once by a stable calendar-day key. MUST NOT filter the full event collection from every cell template evaluation.
- Keep display cells presentational. Let the page or feature facade own range loading and filters; expose typed selection, event activation, retry, and overflow actions.
- Represent loading, error, empty, and data states without shifting the grid. Keep navigation usable when safe, render a visible retry action for failed range loads, and never mask stale data as current data.

## Date and Time Semantics

- Classify every value as a date-only value, an all-day interval, or a timed instant before mapping it to `Date`.
- Preserve date-only and all-day business dates as contract-defined calendar dates. Do not convert them through UTC midnight when that can change the displayed day.
- Treat timed values as instants and convert them into the confirmed display time zone before deriving their calendar day, label, or range membership.
- MUST NOT build a local day key with `date.toISOString().slice(0, 10)`. Use an established zone-aware utility or `Intl.DateTimeFormat(...).formatToParts()` with the confirmed calendar and time zone.
- Confirm interval inclusivity, all-day end-date semantics, daylight-saving transitions, and cross-midnight behavior from the API contract. Do not infer them from sample data.
- MUST NOT infer the business or display time zone from language or locale, browser or workstation defaults, server or deployment region, or a bare UTC offset. Require an explicit contract, configuration, or user/record setting; when civil-time rules matter, use a confirmed IANA time-zone policy rather than treating a fixed offset as equivalent.
- When no authoritative zone is proven, report it as an unresolved requirement and keep designs and code examples parameterized as `displayTimeZone`. MUST NOT fill the gap with a plausible city/region zone or promote an existing fixed-offset utility into the product contract.
- Avoid mutating shared `Date` instances. Clone before date arithmetic and compare normalized values with explicit semantics.
- Register matching Angular and NG-ZORRO locale data. Localize weekday/month labels, event text, overflow labels, empty/error messages, and accessible names.

## Rendering and Interaction

- Prefer `nzDateCell` or `nzMonthCell` to append domain content while retaining the library's date structure and selection behavior.
- Use `nzDateFullCell`, `nzMonthFullCell`, or `nzCustomHeader` only when requirements need complete replacement. Recreate every displaced label, navigation control, selected/disabled/today state, focus affordance, and accessible name, then verify interaction in a browser.
- Bind `nzValue`, `nzMode`, `nzPanelChange`, `nzSelectChange`, and `nzValueChange` according to the installed version's verified emission contracts. Do not label every Calendar update as a product date selection or emit duplicate feature actions from equivalent value-change paths.
- When the installed Calendar version conflates built-in header changes and date-cell selections but the product requires them to remain distinct, use a custom header with explicit typed navigation actions and one canonical Calendar value-change path. Do not guess the interaction source from the emitted `Date`.
- Render only a bounded number of event summaries per day and expose the remainder through a localized `+N more` action. Make the complete list keyboard and touch accessible; do not rely on hover.
- Use stable event ordering based on confirmed product rules. Do not let server arrival order decide display priority.
- Prevent titles, times, counters, and badges from overflowing their cells. Use deliberate wrapping or ellipsis and provide an accessible full value.
- Distinguish adjacent-month dates without hiding them. Preserve their selection behavior unless the product explicitly disables it.
- Encode status with text or icon semantics as well as color. Keep contrast and selected/today/focus states legible in every supported theme.
- Use `nzDisabledDate` only for confirmed business restrictions and enforce the same rule in the authoritative mutation path.

## Responsive and Accessible Behavior

- Design the desktop grid and mobile experience together. Do not squeeze an event-dense month grid into unreadable narrow cells.
- Use compact non-fullscreen Calendar for simple selection or summary use. For event-dense mobile screens, provide a repository-consistent agenda/list for the selected day or visible range while preserving navigation context.
- Keep date cells, event actions, overflow controls, and custom header controls keyboard reachable with visible focus.
- Preserve or restore semantic labels when templates replace visible date text. Announce selected date, event count, disabled state, and overflow purpose without duplicating noisy content.
- Use buttons or links for actions, meet touch-target conventions, and return focus predictably after closing an event detail surface.

## Performance

- Compute zone-aware day keys, visible range boundaries, grouped events, and overflow counts outside the template using stable memoized or signal-derived state.
- Track repeated events by stable identifier. Avoid allocating arrays, formatters, or new `Date` objects from cell-template expressions.
- Cache only by complete range and filter identity, define invalidation after mutations, and bound cache growth.

## Verification

- Verify installed Calendar APIs against local types and run the repository's required TypeScript/build checks from `angular-frontend-standards`.
- Test initial loading, rapid panel navigation, stale-response protection, built-in or custom header year/month changes, the exact emitted output trace, mode changes, selection, disabled dates, adjacent-month cells, and failed-range retry.
- Test date-only, UTC instant, cross-midnight, locale week-boundary, and applicable daylight-saving scenarios in the confirmed display zone.
- Test zero, one, maximum-visible, and overflowing event counts; stable ordering; long localized text; status semantics; and activation of the `+N more` path.
- Statically audit mobile layout, theme contrast, focus visibility, accessible names, and template ownership. Require browser verification for custom full cells, custom headers, keyboard behavior, responsive agenda transitions, or drag behavior.
- Report any unsupported scheduler requirement and the user decision still needed. Do not claim NG-ZORRO Calendar behavior that was not verified.
