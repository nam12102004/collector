# Typed Forms, API Contracts, And I18n

Read [api-contracts.md](api-contracts.md) completely when consuming response envelopes, Problem Details, pagination, SignalR, tenant context, or backend response codes. Read [auth-session.md](auth-session.md) completely when authentication or tokens are in scope. Read [form-feedback-patterns.md](form-feedback-patterns.md) when submit behavior, field-level feedback, remote-error mapping, or form layout is in scope.

## Forms

- Use typed reactive forms for feature forms unless the repository has an explicit canonical exception.
- Use the repository's current Angular Forms and validator patterns. If a feature already has an installed schema adapter, keep its schema authoritative; do not add Zod or duplicate rules in a second validation layer.
- Validate the complete request before emitting a command. Backend validation remains authoritative for authorization, uniqueness, conflicts, and other server-only rules.
- Backend validation remains authoritative. Map stable field codes and parameters into the canonical field-error path; unknown fields become a localized form-level error.
- Clear stale remote field errors when the corresponding value changes.
- Keep feedback height stable in repeated and multi-column forms. Do not insert ad hoc errors inside the control row.

## API Boundary

- Read the real endpoint, DTO, validator, enum, permission, and response wrapper before implementing the client.
- API services own URLs, serialization, and typed transport. They do not own view state or component orchestration.
- Match serialized field names and nullability exactly. Use backend pagination/filter/sort for growing collections.
- Do not expose secrets, refresh tokens, privileged keys, provider credentials, or connection strings to Angular runtime configuration.

## I18n And Feedback

- User-facing strings belong to every configured locale file.
- Resolve success, error, conflict, and validation display through stable codes and safe parameters. Backend message/detail is an unknown-code fallback only.
- Every user-initiated successful mutation receives one immediate localized success message. Reads and failed requests do not.
- Centralized feedback and surface-level feedback MUST NOT duplicate the same event.
