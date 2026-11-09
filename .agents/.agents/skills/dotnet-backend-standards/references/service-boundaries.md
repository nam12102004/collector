# Service Ownership And Readable Workflows

Apply these criteria within the repository's confirmed architecture and the user's authorized scope. A request to clean up a service permits the necessary callers, DI and fixture updates; it does not authorize unrelated API/schema migrations or replacement of the module architecture.

## Keep The Business Sequence Visible

- Keep the use case's eligibility checks, ownership decisions, state transitions and persistence order readable in its public operation. Avoid making the reader traverse a chain of private `Get...`, `Validate...` and `Execute...` methods to understand one sequential workflow.
- Extract when there is a meaningful owner, a reused rule, a complex technical algorithm, or an invariant that becomes easier to understand and maintain in one place. Shorter methods/files alone do not establish a responsibility boundary. For a standalone supporting class, apply [helper naming, placement and lifetime](csharp-conventions.md#helpers).
- Inline private methods that only forward a call or conceal a one-use lookup/check. Private methods remain appropriate when they contain cohesive complexity or genuinely shared logic; this is not a blanket ban.
- Keep an operation used only by one service, such as resetting its command payload, in that service unless it has an independent responsibility. Other callers should use the owning capability interface. Do not create a separate `Reset`, `Helper`, `Store` or facade merely to relocate lines.
- Judge the resulting call chain as a whole: the reader should be able to see why an action is allowed, where it commits, and which failures permit retry without reconstructing the workflow from scattered fragments.

## API Calls And Service Ownership

- Route each API operation directly to the application service that owns its use case or query, through the established interface and module contracts. A controller's name does not require one matching facade: it may inject multiple owning services while each request path calls one service. Keep persistence access, business decisions and transaction logic out of controllers.
- Do not add or retain a service/interface/DI binding whose operation only calls another service and returns its result. Forwarding arguments, normalizing an action or obtaining the current operator alone does not justify an intermediate business service. Bind endpoint intent at the API boundary; keep trusted identity and business validation with the owning service.
- Service-to-service collaboration remains valid when the caller owns a cohesive workflow, policy, result aggregation or transaction boundary. Preserve genuine orchestration, resolver selection and capability ports; do not flatten these merely to remove a call, and do not bypass module boundaries.
- Let the service that owns a snapshot/read model load its inputs through the established repositories or Unit of Work. Do not place a query in an execution service solely so the snapshot service can call it. Share pure classification/calculation in the owning helper; each service loads its own inputs rather than calling another use-case service just to reuse that calculation.
- When removing an intermediate service, update interfaces, callers, DI and focused tests together. Preserve routes, endpoint permissions, response contracts, trusted operator identity, bound action intent, cancellation, supported legacy dispatch, lease order and transaction ownership. Do not move those business safeguards into controllers.

## Technical Services And Business Ownership

Apply this split where Application and Infrastructure boundaries already exist; preserve a focused technical library or standalone tool's established layout.

- Application owns use-case eligibility, state transitions, which audit facts to record and when, event-selection policy, and transaction/commit boundaries. Query services that validate inputs and shape application results also belong here; calling a repository or technical port alone does not make them Infrastructure.
- Keep application-facing technical interfaces in the established inward location, usually `Application/Interfaces`. Put their implementations in the owning module's `Infrastructure/Services` or the matching capability folder. Examples include audit/history persistence, connection-health reporting, credential protection, file/cache/broker adapters, and database sequence reservation. Match namespaces to folders and preserve dedicated service/provider/generator naming; use `Helpers` only for classes that meet the helper convention.
- Classify the implementation by its decisions. An audit writer that serializes payloads and stages history records is technical even when it uses domain entities and the module's Unit of Work. Application decides whether an action requires an audit entry; Infrastructure implements recording it. Durable command identity ownership, reuse/exclusion rules, and execution checkpoints stay with the application workflow; atomic counter/reservation storage belongs to persistence.
- Keep audit/history data inside its owning module. Preserve the same scoped Unit of Work and ambient transaction; recording a side effect inside a use case must not introduce an independent `SaveChanges`, commit, context, or best-effort background log that can diverge from the business change. Preserve any explicitly required external-I/O checkpoints.
- A service that selects recovery behavior, permission policy, temporary-pallet routing, or other business outcomes stays in Application. For mixed responsibilities, extract a cohesive technical operation behind a port while retaining policy and orchestration; do not move the whole service merely because it performs I/O or has a technical name.
- Before moving a technical class, check whether it needs to exist. If it only forwards to an existing repository/Unit of Work method or technical query port and adds no policy, algorithm, or independent capability, use that existing port and remove the redundant wrapper/interface/DI binding. Application services continue owning business operations; keep necessary validation at the owning boundary rather than duplicating it in another layer.
- A read-only technical health/status endpoint may use an existing inward status-query operation directly when it already returns the required payload. Keep declarative authorization, versioning, and response-envelope handling at the API boundary. This permits a single status query; it does not move protocol execution, persistence access, or business orchestration into controllers.
- Update DI bindings, lifetimes, imports, concrete-implementation tests, and live documentation links together when moving an implementation. Callers continue depending on its inward interface; do not add cross-module persistence access or invent a new interface solely to move a class.

## Separate Policy From Protocol Mechanics

Where Application and Infrastructure boundaries exist, assign behavior by ownership:

| Application owns | Infrastructure owns |
|---|---|
| Step eligibility, safety policy, execution ownership, stop/cancel intent | Endpoint/credential resolution, connection reuse, node addressing and wire payload conversion |
| Durable command identity policy, acceptance/progress milestones, recovery decisions | Trigger writes, status-bit reads, identity echoes, polling, handshake, ACK and payload reset |
| Transaction/lease scope, idempotency policy and externally observable workflow milestones | Transport facts and errors reported through typed application contracts |

- Put application-facing execution interfaces/results in the established inward layer; implement protocol adapters in Infrastructure. Application orchestration should consume operations or meaningful observations rather than know tag identifiers or construct wire payloads.
- Protocol constants and node maps belong beside their adapter. A fixed contract may use constants directly; introduce a runtime tag store only when runtime configuration, multiple implemented contracts or another demonstrated requirement needs it.
- Prefer capability names over vendor/model names when only one contract is supported. Keep version-specific names where actual distinct implementations require them. Preserve exact protocol identifiers and compatibility fields during an internal rename.
- Keep durable workflow writes and ID allocation out of protocol adapters. Await application milestone callbacks when persistence must precede the next protocol action. Use typed phase operations or a small observer appropriate to the actual workflow, without building a generic callback framework.
- A compiling refactor can still violate ownership. Check constructor dependencies, call sites, constants and global imports; moving a class to another folder is insufficient if Application still performs its protocol mechanics.

## Repository Retrieval Has A Data Boundary

Use this section when the architecture already uses repositories; do not introduce repositories into an unrelated architecture just for these examples.

- When callers repeatedly fetch an entity by ID and immediately filter its subtype, use an intent-named repository query such as `GetCraneByIdAsync` or `GetByIdAndTypeAsync`. Apply ID/type criteria in the database.
- When a caller needs only the type, project a nullable scalar such as `GetEquipmentTypeByIdAsync`; avoid materializing the entity just to inspect its discriminator. An absent entity must remain distinguishable from a valid enum value.
- A typed lookup returning null may mean missing ID or wrong subtype. Preserve the established not-found versus wrong-type error behavior; use a scalar fallback only when that distinction is required.
- Repositories own retrieval, projection and persisted filtering. Step eligibility, safety, ownership, protocol readiness and recovery policy remain with their owning service/adapter; do not move these decisions into a repository to eliminate a private method.

## Preserve External Execution Invariants During Extraction

- Preserve the established lease/transaction boundaries and I/O order. Keep the final ownership/stop checks and trigger write within the same command lease; do not accidentally extend that lease across the whole completion-polling loop.
- Keep intentional durable checkpoints around external execution explicit. Commit each checkpoint's related writes atomically; do not stretch a database transaction across a long device polling loop or collapse separately committed execution phases during extraction.
- Record whether a trigger write was attempted before awaiting it. A failed transport write can still have reached the endpoint. Application may release an untriggered reservation; an uncertain triggered command needs the established reconciliation policy rather than automatic replay or a new identity.
- Preserve durable milestones around acceptance, payload clearing, pickup/handoff and ACK. Check both device command identity and current application ownership wherever the existing workflow requires them.
- Keep cleanup order and cancellation/error semantics intact, including writes that throw, lost ownership after acceptance, residual results and ACK retries. Compare these paths before and after extraction instead of relying only on a successful build.

## Simulators Using The Same Protocol

- A mock server implementing the same protocol is another configured endpoint. Use the same adapter, payload contract, handshake, safety and ownership behavior; a simulation flag alone is not a reason to skip reads, validation, trigger writes or ACK.
- Keep simulation controls and scenario generation in the simulator. Distinct production behavior needs a demonstrated capability/contract difference or an explicit requirement, not a guessed distinction between mock and physical equipment.
- Remove obsolete legacy/fallback paths when their removal is authorized and the supported contract replaces them. Do not preserve them speculatively or restore them through a new flag.
- When API/database fields must remain compatible, preserve their serialized names and persistence mappings. A retained simulation metadata field need not participate in connection equality or runtime behavior. Deleting it from API/schema is a separate contract change.

## Verification

Update the nearest meaningful tests around the affected boundary: application policy can mock the execution interfaces, while existing protocol fixtures continue checking payload/trigger order, identity, cleanup and ACK behavior. Follow the repository's test execution permissions. Report build results separately from tests or runtime checks that were not executed.
