# Form Feedback And Error Mapping

Read this reference when implementing or refactoring submit behavior, field validation, backend error mapping, or form layout. Apply it to create/edit forms in pages, modals, and drawers.

## Error ownership

- Keep page/table loading and mutation errors separate from modal, drawer, or editor form errors. The form surface owns its own feedback state.
- Expose two distinct channels from the feature facade: `fieldErrors` keyed by canonical control path and `formError` for failures that cannot be attached to one control.
- Render a field error next to the associated control. Use a form-level alert only for permission, conflict, resource, unknown, or otherwise non-field-specific failures.
- Do not show the same backend event both beside a field and in the form-level alert.

## Submit behavior

- On submit, mark every control as touched and run the complete local validation before emitting a typed mutation request.
- Do not rely on a single alert at the top of the form to communicate missing or invalid fields.
- When the backend returns field validation errors, mark the matching control invalid/touched and render the translated message through the installed design system's normal feedback mechanism.
- Keep the submit button and mutation flow independent from presentation details: the facade maps the response, while the template renders the appropriate channel.

## Problem Details mapping

- Parse the repository's stable `errorCode`, `field`, and `errors[]` shape rather than depending on a free-form backend message.
- Allow-list and normalize field paths before assigning an error to a control. Map stable codes and parameters to localized messages.
- An unknown error code, unknown field, or malformed field path stays form-level; never invent a control mapping or expose raw technical details as the primary UX.
- Reuse the shared form-error mapper when the repository provides one, such as `shared/forms/form-errors.ts`, instead of duplicating parsing logic in each editor.

## Remote error lifecycle

- Clear remote field errors when the affected value changes, when a new submit starts, and when the form is opened, reset, or cancelled for a new workflow.
- Preserve a remote error while the user is correcting the value; clear it only after the corresponding edit or a fresh validation result.
- Do not clear an unrelated field error because another control changed.
- Keep form-level errors visible until the next meaningful submit, reset, close, or workflow transition.

## Stable field layout

- Give every field one container that owns its label, control, description, and validation feedback.
- Reserve a consistent feedback slot or minimum height so an error appearing in one column does not shift neighboring controls or misalign grid rows.
- Keep composite controls, such as country selector plus phone number, inside the same field layout and feedback boundary.
- Use the design system's control and dropdown primitives first. Give selectors a predictable width and alignment; do not fix focus or overlay issues with broad global overrides.

## Schema-based validation

- When a schema library is already installed and canonical in the affected feature, keep it as the source for client-side validation and map its issue paths to form controls.
- Do not install Zod or another schema library by default. When the repository uses Angular validators, follow those patterns and avoid duplicating the same rule in a second layer.
- During editing, show realtime issues only after the field is dirty/touched; after submit, show all relevant field issues.
- Client validation handles shape and rules known to the client; the backend remains authoritative for permissions, uniqueness, conflicts, and server-only rules.

## International phone input

- When collecting international phone numbers, pair a country selector with a local-number input so the selected country determines the calling/trunk-prefix context.
- Reuse an installed `libphonenumber` integration for parsing and normalization when the product collects international phone numbers; do not add a phone library to a feature that does not need it.
- Keep country and number changes in the same field-validation lifecycle so stale phone errors are removed when either part changes.

## Verification

- Test local submit errors, one and multiple backend field errors, form-level failures, stale-error clearing after edits, and stable feedback layout.
- Include a browser check for focus, dropdown overlay, error placement, and responsive multi-column alignment when those behaviors are changed.
