# Signals, RxJS, Async State, And Facades

## Ownership

- Use signals and `computed()` for synchronous local and derived UI state.
- Use RxJS for HTTP, cancellation, debouncing, retries, event streams, and library interop.
- Keep feature-shared state in the feature facade. Do not place feature-only state in a global store.
- Keep page/table errors separate from modal, drawer, tab, and form errors. An error renders on the surface that owns the failed operation.

## MUST-AUTO

- Bound subscriptions with `takeUntilDestroyed`, `AsyncPipe`, or an equivalent repository-approved lifecycle.
- Do not nest subscriptions.
- Do not mutate signals from a computed expression.
- A new search stream MUST cancel or ignore stale responses.

## MUST-REVIEW

- Model search/debounce/cancellation declaratively with operators such as `debounceTime`, `distinctUntilChanged`, and `switchMap`; do not prefer manual request counters when stream cancellation expresses the contract.
- Serialize or reject duplicate mutations deliberately with the appropriate operator or explicit submitting state.
- A paged or load-more query keeps query, page, availability, loading, error, and stale-response semantics in one owner.
- Represent loading, error, empty, and data states for every asynchronous surface.
- Clear stale surface errors on open, close, or a new submission; preserve a failed submission's error while the user can correct it.
- Treat SignalR events as hints unless the contract proves they are authoritative. Reconnect and detected gaps trigger an authoritative resync.

## Facade Shape

A facade exposes readonly state/view models and named intent methods. Keep HTTP details in the API service, mapping in pure helpers where useful, and subscriptions/effects inside the facade. Do not mirror every component method with a pass-through facade method that adds no ownership or policy.
