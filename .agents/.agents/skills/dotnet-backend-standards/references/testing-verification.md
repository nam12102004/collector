# Testing And Verification

## Test Selection

- For CRUD and application changes, add focused tests around the service/use-case logic.
- Derive failure cases from exceptions, validation outcomes, and branches that the implemented logic actually contains. Do not invent a generic failure checklist unrelated to the code.
- Add integration-test code for serialization, authentication/authorization, middleware, or route wiring only when the user requests it or the established repository requires that coverage. Do not create real-provider integration tests without an explicit user request.
- Use Moq when the service/use case depends on repository interfaces. Use EF Core InMemory only when it directly depends on DbContext and the tested behavior is non-relational. Never use InMemory as proof of mappings, constraints, transactions, migrations, foreign keys, uniqueness, concurrency, or provider SQL.
- Cover success, validation failure, not found, unauthorized/forbidden, conflict/concurrency, and cancellation paths that the change makes relevant.
- Assert observable behavior and important side effects; avoid tests coupled only to implementation details.
- Add one AutoMapper configuration test that scans every registered mapping assembly and calls `AssertConfigurationIsValid()`. Service tests should reuse the real configured `IMapper`, not mock mapping behavior.
- When validation runs at the HTTP/application boundary, test validators directly and cover boundary wiring at the appropriate integration level; do not keep unit tests that expect a service method to invoke `IValidator<T>`.

## Contact Validation And Duplicate Coverage

When email, phone, or contact-identity behavior changes, add focused tests for the applicable contract:

- valid and invalid mailbox input, including rejection of display-name forms when only a mailbox is accepted;
- valid regional phone input, parse failure, invalid numbering-plan values, and any confirmed presentation restriction;
- Create and Update duplicate conflicts for each supported contact field and participating entity type;
- exclusion of the current record during Update;
- consistent normalization between preflight and write-time checks;
- stable 409 error code and safe metadata required by the client;
- transactional or database uniqueness behavior when correctness depends on provider concurrency semantics.

## Verification Order

1. Add or update focused tests for touched behavior.
2. Add broader tests when shared contracts, middleware, persistence abstractions, or startup wiring make that coverage necessary.
3. Build the smallest affected project or solution.
4. Do not apply migrations to a database, execute tests, lint/format, start the application, run runtime smoke checks, or use browser tooling unless the user explicitly requests it in the current task. For EF Core model/schema changes, migration scaffolding is required and allowed; scaffolding does not apply the migration.
5. Inspect generated migrations, DI, contracts, and configuration; hand the relevant test/runtime commands to the user.

## EF Core Migration Generation

- When a change alters an EF Core model or relational schema, first use the repository's migration script if one exists. Otherwise generate the migration with `dotnet ef migrations add <MigrationName> --project <DataProject.csproj> --startup-project <StartupProject.csproj> --context <FullyQualifiedDbContext> --output-dir <MigrationDirectory>`.
- Identify the owning DbContext and migration assembly; keep module migrations in their owning module. Never hand-create migration `.cs`, `.Designer.cs`, or model-snapshot files. Let EF Core scaffold them, then inspect `Up`, `Down`, and the updated snapshot for correctness, duplicate operations, and unrelated changes.
- After scaffolding, run `dotnet ef migrations has-pending-model-changes` for that context when supported, or the repository's equivalent drift check. Inspect generated SQL with `dotnet ef migrations script` when it helps review provider-specific or data-preserving operations.
- If the generated result is wrong, fix the model/configuration and regenerate the migration rather than hand-authoring migration or snapshot files. Do not suppress `PendingModelChangesWarning` to bypass model drift.
- Do not apply the migration to a database as verification. Applying it requires explicit user authorization or an established task workflow that authorizes the target environment.

## Handoff Evidence

Report the exact build command and result, plus unexecuted test/runtime commands and residual checks. Do not claim an unexecuted check passed. A pre-existing build failure must be demonstrated from unchanged code or a baseline build; do not label it pre-existing from assumption.
