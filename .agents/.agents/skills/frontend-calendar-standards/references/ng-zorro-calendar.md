# NG-ZORRO Calendar Reference

Use this reference after inspecting the installed package. Treat local public types as authoritative when they differ from documentation or this repository baseline.

## Contents

- Version and import baseline
- Capability boundary
- Component contract
- Event and state semantics
- Visible-range loading
- Date and time mapping
- Template selection
- Verification matrix

## Version and Import Baseline

The CRM repository currently declares Angular `^21.2.x` and `ng-zorro-antd` `^21.3.2`; its lockfile resolves NG-ZORRO 21.3.2. Re-check both files before editing because the skill is version-adaptive.

For the current baseline, Calendar is available from:

```ts
import { NzCalendarModule } from 'ng-zorro-antd/calendar';
```

NG-ZORRO 21 also exposes its Calendar declarations as standalone-compatible components/directives while retaining `NzCalendarModule`. Match the repository's established import style and the installed type declarations. Do not copy a future-version example into an older application.

Register the Angular locale with `registerLocaleData(...)` and provide the matching NG-ZORRO locale with `provideNzI18n(...)`. This CRM currently registers Vietnamese locale data and provides `vi_VN`.

Official component documentation: <https://ng.ant.design/components/calendar/en>

## Capability Boundary

`NzCalendarComponent` is a month/year calendar for displaying date-oriented data and selecting a date. It is not a full scheduling engine.

Use another user-approved capability when requirements include:

- week or day time-axis views;
- overlapping appointment layout;
- resources, rooms, people, or lane scheduling;
- drag-to-create, resize, or drag-reschedule;
- recurrence-rule editing or complex availability visualization.

Do not approximate these requirements by absolutely positioning events in Calendar cells or attaching native document-level pointer listeners.

At this boundary, explain the missing capability and request the user's dependency decision. A concise comparison of compatible scheduler products is allowed, but do not select, recommend as decided, assume a commercial license, or write package-specific component architecture until the user approves a choice.

## Component Contract

Verify the installed declarations before use. The v21.3.2 baseline exposes:

| API | Baseline type | Meaning |
|---|---|---|
| `nzValue` / `ngModel` | `Date` | Current selected/active calendar value |
| `nzMode` | `'month' \| 'year'` | Current display mode |
| `nzDisabledDate` | `(date: Date) => boolean` | Determines whether a date is disabled |
| `nzFullscreen` | `boolean` | Switches full-size and compact presentation |
| `nzCustomHeader` | `string \| TemplateRef<void>` | Replaces the built-in header |
| `nzDateCell` | `TemplateRef<{ $implicit: Date }>` | Appends date-cell content |
| `nzDateFullCell` | `TemplateRef<{ $implicit: Date }>` | Replaces full date-cell content |
| `nzMonthCell` | `TemplateRef<{ $implicit: Date }>` | Appends month-cell content |
| `nzMonthFullCell` | `TemplateRef<{ $implicit: Date }>` | Replaces full month-cell content |
| `nzModeChange` | `NzCalendarMode` | Reports display-mode changes |
| `nzPanelChange` | `{ date: Date; mode: NzCalendarMode }` | Reports panel changes; verify the installed emission path |
| `nzSelectChange` | `Date` | Reports the Calendar select/update path; verify whether header updates also emit it |
| `nzValueChange` | `Date` | Reports component value changes |

Calendar implements `ControlValueAccessor`. Choose one canonical value ownership path and prevent duplicate feature actions when combining two-way binding with change outputs. Public output names do not prove which internal interactions emit them; inspect the installed implementation or capture an output trace.

## Event and State Semantics

Model these separately even if the component internally coordinates them:

```ts
type CalendarMode = 'month' | 'year';

interface CalendarViewState {
  readonly selectedDate: Date;
  readonly panelDate: Date;
  readonly mode: CalendarMode;
  readonly visibleStart: string;
  readonly visibleEndExclusive: string;
}
```

The string boundary example is illustrative, not a required application type. Use the API's proven date/range contract.

- Initialize `panelDate` and load its range when the screen starts.
- In the v21.3.2 baseline, built-in header year/month changes and date-cell selections all call the same internal date-update path, which emits both `nzSelectChange` and `nzValueChange`. `nzPanelChange` is emitted by the mode-change path, not by built-in header year/month changes.
- For the v21.3.2 built-in header, use one canonical value-change output to update active value and derive any required range load; do not wait for `nzPanelChange` to load the new month.
- If the product must distinguish navigation from date selection, replace the header with accessible explicit navigation controls and keep their typed action separate from the Calendar's canonical value-change output.
- Do not subscribe `nzSelectChange`, `nzValueChange`, two-way value binding, and the ControlValueAccessor callback to equivalent feature actions.
- Keep mode changes distinct from data selection and decide whether year mode loads aggregate month data or suppresses event details.

## Visible-Range Loading

A month grid can display leading and trailing days outside the named month. Query every date that can render domain content.

1. Derive the named month from the panel anchor in the confirmed display zone.
2. Expand the range to locale-aligned week boundaries used by the rendered grid.
3. Express the backend boundary with its documented inclusive/exclusive semantics.
4. Key the request by range plus every active filter.
5. Cancel the previous request with `switchMap` or guard responses with a monotonically increasing request identity.
6. Group the winning response once by calendar-day key.

Do not hardcode a month-only query or assume that every NG-ZORRO version renders the same count of adjacent weeks.

## Date and Time Mapping

Use three explicit categories:

| Category | Contract expectation | Day-key rule |
|---|---|---|
| Date-only | Calendar date without an instant | Preserve the contract date fields |
| All-day interval | Calendar-date interval with documented end semantics | Expand dates in the business calendar/time zone |
| Timed event | UTC/offset instant or interval | Convert into the selected display zone before grouping |

Never use `toISOString().slice(0, 10)` as a local day key. It first converts to UTC and can move an event to the previous or next calendar day.

Locale and time zone are independent decisions. Vietnamese labels, a developer machine in Asia, a server region, or the browser default do not prove the application's business/display zone. Obtain the zone from a documented API contract, application configuration, user preference, or record policy. When daylight-saving or historical civil-time rules matter, use a confirmed IANA zone; do not substitute a current fixed offset.

If that source of truth does not exist, leave the requirement unresolved and write examples against a `displayTimeZone` parameter. Do not choose a likely city zone from locale, convert a fixed GMT/UTC offset into an assumed IANA zone, or label the zone as confirmed. Request the product/API decision before finalizing grouping or range semantics.

When no repository helper exists, derive a Gregorian day key without adding a package:

```ts
const formatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: displayTimeZone,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});
```

Read `formatToParts()` by part type and assemble the key explicitly; do not depend on punctuation or output order. Reuse the formatter rather than allocating it per event or cell. If the business calendar is not Gregorian, follow the confirmed calendar contract instead.

## Template Selection

- Choose `nzDateCell` for badges, event summaries, counts, or secondary content while preserving the built-in date label.
- Choose `nzMonthCell` for year-mode aggregates.
- Choose a full-cell template only when the complete visual structure must change.
- Choose a custom header only when built-in year/month/mode controls cannot meet confirmed requirements.

Full replacements increase ownership. Verify selected, today, disabled, adjacent-period, hover, focus, keyboard, accessible-name, RTL, locale, and theme behavior. Keep event actions interactive without turning the entire cell into nested conflicting controls.

## Verification Matrix

| Area | Minimum scenarios |
|---|---|
| Navigation | Initial panel, previous/next month, year change, rapid navigation, mode switch, exact output trace |
| Requests | Correct visible boundary, filters in key, cancellation/stale response, retry |
| Dates | Date-only, UTC instant, offset instant, cross-midnight, week boundary, DST when applicable |
| Cells | Adjacent month, today, selected, disabled, zero events, overflow, long text |
| Interaction | Date select, event action, `+N more`, custom header/full-cell keyboard behavior |
| Presentation | Compact/fullscreen, mobile agenda fallback, light/dark theme, localized text |
| Accessibility | Visible focus, accessible names, non-color status, touch targets, focus return |

Run browser verification whenever the change replaces Calendar-owned markup or depends on responsive, focus, keyboard, or overlay behavior. Static inspection and compilation cannot prove these interactions.
