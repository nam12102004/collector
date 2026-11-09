# C# Style

## Contents

- Code Shape
- Repository And Unit Of Work
- Naming
- Aubot WCS/RCS Naming Profile
- Type Patterns
- Exceptions And Codes

## Code Shape

- Use `sealed` for services, validators, handlers, and controllers unless extensibility is intentional.
- Use primary constructors for dependency injection when the target language version supports them.
- Use `record` or `sealed record` for request/response DTOs.
- Domain entities inherit the repo base entity, typically `BaseEntity<TKey>`.
- Required reference properties default to `null!`; collections default to `[]`.
- Use `DateTime.UtcNow` or an injected clock. Do not use `DateTime.Now`.
- Use structured logging: `_logger.LogInformation("Order {OrderId} created", orderId);`.
- Use FluentValidation for request validation.
- Default to the approved AutoMapper 16.x baseline. Keep profiles outside controllers, validate configuration in tests, and use `ProjectTo<TDto>()` for EF read projections. Do not scatter manual mapping through service methods or use blind `ReverseMap()`.

## Repository And Unit Of Work

- Controllers/endpoints bind transport input, call one use-case/query service, and form the HTTP response around its result. They may construct the `SuccessResponse<T>` transport envelope, but never read claims, evaluate permissions imperatively, map DTOs, construct application payloads, execute business rules, or access persistence.
- Application services do not inject DbContext under any circumstances.
- Application services inject `IUnitOfWork` or typed custom repositories.
- Call `SaveChangesAsync` (or `CompleteAsync` / `SaveChangesAsync` on UoW) after writes.
- Repositories encapsulate filtering, sorting, paging, includes, and no-tracking behavior.
- Generic repositories do not expose `IQueryable<T>`.
- **Custom Repositories Pattern:** For entities requiring complex query logic (joins, includes, projections, custom paging) or range operations, declare the custom interface in Domain (e.g. `IUserRepository : IGenericRepository<User>`) and its implementation in Infrastructure (e.g. `UserRepository : GenericRepository<User>, IUserRepository`). Move all EF Core logic (`Include`, `Skip`, `Take`, `AnyAsync`, `RemoveRange`, `AddRangeAsync`) into repository methods. Expose this custom repository on `IUnitOfWork`.

Example generic repository:

```csharp
public interface IRepository<T, TKey> where T : BaseEntity<TKey>
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
}
```

Example Unit of Work:

```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
```

Use `SaveChangesAsync` alone for a single logical write. For one atomic business operation with multiple writes, persistence calls or a nested service that persists, use `BeginTransactionAsync`/`CommitTransactionAsync` with rollback on failure. A long-running external execution workflow can have explicit durable checkpoints: keep each checkpoint's related writes atomic, preserve its ownership/idempotency guards, and keep database transactions out of the completion-polling loop. Do not turn one atomic update into accidental partial commits or merge distinct committed execution phases merely to reduce the number of persistence calls.

## Naming

- Types, methods, and properties: PascalCase.
- Locals and parameters: camelCase.
- Private fields: `_camelCase`.
- Interfaces: `I{Name}`.
- Files match primary type names.
- Namespaces match folder structure.
- Supporting classes follow [helper naming, placement and lifetime](../../dotnet-backend-standards/references/csharp-conventions.md#helpers): `{Capability}Helper.cs` under the owning layer's `Helpers/` folder.
- Follow [concise method names based on actual behavior](../../dotnet-backend-standards/references/csharp-conventions.md#method-names): remove redundant wording, retain meaningful domain distinctions, distinguish validation from state changes, and preserve framework-defined names.
- Async methods end in `Async` except framework-defined names.
- Booleans read like questions: `IsActive`, `HasPermission`, `CanRetry`.
- Collections are plural nouns.

Avoid abbreviation names:

| Avoid | Use |
|---|---|
| `repo` | `repository` |
| `ctx` | `context` |
| `req` | `request` |
| `res` | `response` |
| `svc`, `srv` | `service` |
| `cfg`, `conf` | `configuration`, `options` |
| `msg` | `message` |
| `conn` | `connection` |
| `cmd` | `command` |
| `val` | `value` |
| `tmp`, `temp` | `temporary` or descriptive name |
| `idx` | `index` |
| `len` | `length` |
| `src` | `source` |
| `dest`, `dst` | `destination` |
| `err` | `error` |
| `mgr` | `manager` |
| `impl` | `implementation` or omit |
| `db` | `database`, except framework names such as `DbContext` |
| `ct` | `cancellationToken` |

Allowed short forms: `Id`, `Dto`, `Api`, `Url`, `Json`, `Jwt`, `Http`, `Uri`, lambda `x`, loop `i/j/k`, catch `ex`, AutoMapper callback names, DI lambda `sp`.

## Aubot WCS/RCS Naming Profile

Apply these confirmed local choices only in the Aubot WCS/RCS repository, unless the user changes them:

- Keep the `Service` suffix for both application orchestration and technical execution adapters. Distinguish their ownership through capability names, interfaces, layer placement and dependencies.
- Prefer `CraneCommandService`, `ConveyorCommandService`, `CraneTagConstant` and `ConveyorTagConstant` for the supported contracts. Avoid reintroducing `Plc` or the single model name `Sc1000` in internal type/method names. Retain accurate protocol names such as OPC UA and genuinely distinct contract/version identifiers where needed.
- Preserve existing API fields, serialized values and database names during internal renames, including compatibility fields such as `PlcCommandId` and `IsSimulated`, unless a contract/schema change is explicitly requested.
- RCS uses the same execution flow for the mock OPC UA server and physical endpoints. A retained `IsSimulated` metadata value must not select a separate tag set or bypass command/safety/trigger/ACK behavior.

## Type Patterns

- Request DTO: `{Action}{Entity}Request`
- Response DTO: `{Entity}Dto`
- Service interface: `I{Entity}Service`
- Service implementation: `{Entity}Service`
- Supporting helper: `{Capability}Helper` in the owning layer's `Helpers/` folder
- Validator: `{RequestName}Validator`
- Mapper profile: `{Entity}Profile`
- Error/success code constant class: `{Area}ErrorCodeConstant`, `{Area}SuccessCodeConstant`
- Permission/business/audit constant class: `{Area}PermissionConstant`, `{Area}ThresholdConstant`, `{Area}AuditActionConstant`
- Enum: `{Concept}Enum` with explicit `10, 20, 30...` values, string API serialization, and string EF persistence. `[Flags]` is ask-first and uses powers of two.
- Controller: `{Entity}Controller`
- Minimal API endpoint class: `{Entity}Endpoints` or `{Entity}Api`, following repo style
- DbContext: `{ModuleName}DbContext`
- Test class: `{ClassUnderTest}Tests`
- Test method: `{Method}_Should{Expected}_When{Condition}`

## Exceptions And Codes

- Business exceptions inherit the repo base exception and carry an error code.
- Use the shared typed exception mapping from `dotnet-backend-standards/references/api-reliability.md`: bad request 400, forbidden 403, not found 404, conflict 409, business rule 422, and service-owned quota 429. Keep HTTP response objects out of Domain/Application exceptions.
- Client-facing error/success codes live in constants.
- Keep truly local, internal, non-contractual one-off strings inline when that is clearer. Use named constants for reused values and shared business/contract vocabularies such as audit action/entity names, role or permission names, statuses, error codes, and business thresholds.
- Prefer `?? throw new NotFoundException(...)` for simple lookup failures.
