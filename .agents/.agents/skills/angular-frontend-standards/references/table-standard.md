# Data Table Standard

Apply this standard to every data table, including collections inside tabs, drawers, modals, import results, and admin panels. Classify the collection before choosing markup: when users compare multiple records across stable fields, use `nz-table`. ASRS uses NG-ZORRO 22; for server-backed tables set `[nzFrontPagination]="false"` and keep the caller's paging index convention in a shared adapter.

Timeline, feed, navigation, and genuinely independent cards may remain lists because cross-column comparison is not their purpose. Record that semantic reason when a repeated multi-field collection is intentionally not a table.

The goal is to prevent text or rendered controls from crossing into a neighboring cell while keeping important data readable on desktop and mobile. A table component alone is insufficient; the layout and containment rules below still apply.

## Content policy

| Content type                                                             | Required rendering                                                                                                                    | Typical use                                          |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| Primary entity users need to identify                                    | Wrap to at most two lines. Use `min-w-0 break-words line-clamp-2`; provide `title`, a tooltip, or a detail action for the full value. | Customer, lead, product, activity subject            |
| Long secondary text or metadata                                          | One-line ellipsis plus the full value on hover. Use `min-w-0 truncate` with `title` or `nz-tooltip`.                                  | Email, related entity, description, category         |
| Codes, phone numbers, dates, amounts, statuses, ratings, and row actions | Never wrap. Use `whitespace-nowrap`; right-align amounts.                                                                             | Tax code, execution time, money, status tag, buttons |
| Long tokens with no reliable whitespace                                  | Permit word breaks. Use `min-w-0 break-words`.                                                                                        | URL, long identifier, exceptional tax code           |

Do not place `truncate` directly on a `td` unless table layout has a real, fixed width constraint. Otherwise ellipsis is not guaranteed and text can overlap an adjacent column.

For a clickable primary label with secondary text in the same cell, reuse the repository's plain clickable-anchor pattern with `table-primary-link` instead of `nz-button nzType="link"`: the library button padding misaligns the label with its metadata. Give the anchor `role="button"`, `tabindex="0"`, Enter/Space handlers, a visible focus style, and a fixed two-line label area when row heights must match.

## Editable and interactive tables

When an `nz-table` contains editable or interactive controls, apply the containment rules regardless of whether those controls use Reactive Forms, `FormArray`, or standalone `ngModel`. If the table belongs to a form, place it inside the repository's form container so NG-ZORRO validation ownership and the shared control-containment rules apply. Otherwise use an established equivalent wrapper that provides the same rendered-widget width normalization:

```html
<nz-form-item class="mb-0 min-w-0">
  <nz-form-control [nzValidateStatus]="form.controls.items">
    <nz-table nzTableLayout="fixed" ...> ... </nz-table>
  </nz-form-control>
</nz-form-item>
```

- Do not place an editable table directly under a section, modal body, or drawer body when its cell controls depend on `.ant-form-item` width normalization, including standalone `ngModel` controls.
- Keep the table container, each editable cell, `nz-form-control`, and the rendered NG-ZORRO widget shrinkable with `min-width: 0`; constrain the actual `.ant-input-number`, `.ant-select`, picker, or group wrapper to the cell width. A `w-full` class on the Angular host is insufficient when the component renders a nested `.ant-*` wrapper.
- Compare the column's usable content width after cell padding with the widget's rendered/default width. Either widen the declared column and update `nzScroll.x`, or constrain the real inner widget to `width`/`max-width: 100%`; never let it paint into the neighboring column.
- Reuse the nearest canonical editable table and shared form CSS before adding a page-local override. Do not conceal an overflowing control with clipping as a substitute for correct containment.

## Horizontal scrolling

Use horizontal scrolling when the sum of essential column widths exceeds the available content area, or when a business table has six or more meaningful columns that include fixed-width data such as dates, status, or actions. Long text alone is not a reason to widen a table: apply the content policy instead.

Every horizontally scrollable NG-ZORRO table must follow this shape:

```html
<nz-table nzTableLayout="fixed" [nzScroll]="{ x: '1200px' }">
  <thead>
    <tr>
      <th nzWidth="72px">...</th>
      <th nzWidth="240px">...</th>
      <!-- every column has nzWidth -->
    </tr>
  </thead>
</nz-table>
```

- Give every header an explicit `nzWidth`.
- Set `nzScroll.x` to the sum of every column width or a larger value; never use a smaller estimate.
- Always set `nzTableLayout="fixed"` so the declared widths and text containment take effect.
- For a long table, pin the index and primary entity on the left with `nzLeft`, and pin actions on the right with `nzRight`.
- Use the repository `appHorizontalDragScroll` directive only when an already-required horizontal scrollbar would benefit from mouse drag-panning. Do not replace the native scrollbar.

## Non-scrolling tables

Omit horizontal scrolling only if all columns remain usable in the desktop container with translated labels. Still define width or min-width for code, date/time, status, and action columns; do not make essential columns unreadably narrow merely to avoid a scrollbar.

## Verification

Before handoff, statically check:

1. Long subjects, entity names, emails, and descriptions cannot overlap a neighboring cell.
2. Any ellipsized value has a way to reveal the full value.
3. For a horizontally scrollable table, all headers have width, the width sum matches `nzScroll.x`, and pinned cells remain usable.
4. No page-local CSS or DOM drag handler reimplements NG-ZORRO table scrolling.
5. For every editable or interactive table, the table has canonical containment and the rendered widgets—not only their Angular hosts—fit inside the narrowest cell in normal, disabled, error, and suffix/addon states.

When browser testing is authorized, verify these behaviors at desktop and narrow mobile widths.
