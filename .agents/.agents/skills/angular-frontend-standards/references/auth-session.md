# Angular Auth Session

Apply these rules whenever an Angular application handles access tokens (AT), refresh tokens (RT), login callbacks, logout, protected routes, or HTTP 401/403 responses.

## NgStore Ownership

- Use the repository-confirmed `NgStore` abstraction as the single source of truth for AT, RT, token expiry, authentication status, and the authenticated-user snapshot.
- Treat names such as `NgStore` as project-owned, not package guesses. Before implementation, locate the actual provider, installed package, public API, persistence adapter, and canonical feature. Do not assume or install NgRx or another state library when it is absent.
- Keep token mutations inside auth actions/effects/store methods. Components, guards, feature services, and layout code consume derived auth state only.
- Let the HTTP auth interceptor obtain the current AT through the auth facade/store contract. Do not expose RT to components or ordinary API services.
- Perform refresh as one coordinated store effect/operation. Concurrent 401 responses must share one refresh attempt; atomically replace AT/RT on success and clear the complete auth state on failure.

## OIDC Persistence Adapter

- When the installed OIDC client requires a storage interface, implement one adapter backed by NgStore and register it through the client's supported storage-provider hook.
- Do not allow the OIDC client and NgStore to maintain independent token copies. NgStore is authoritative; the adapter is the only persistence boundary.
- A full-page authorization-code redirect destroys in-memory NgStore state. Persist only the minimum tab-scoped OIDC transaction metadata required to validate the callback (state, nonce, PKCE verifier, flow-in-progress marker, authorization request parameters, and discovery endpoints) through the adapter's private `sessionStorage` namespace. Apply a short expiry, restore it into NgStore before callback processing, and delete it after validation, logout, or expiry.
- Whitelist transaction fields instead of copying the OIDC client's full serialized storage object. Never persist AT, RT, ID token, token response, user profile, token expiry, or reusable refresh-token fields in `localStorage` or `sessionStorage`.
- Do not access browser storage from components, guards, interceptors, effects, or ordinary auth services. If NgStore itself has another confirmed persistence mechanism, document whether it survives reload, tab close, browser restart, and cross-tab use without weakening the token rule.
- Never log AT, RT, authorization codes, PKCE verifiers, token payloads, or storage snapshots.

## Redirect And Failure Semantics

- Preserve a sanitized internal `returnUrl` through a callback URI parameter or signed OIDC state that survives the redirect, then restore it into the auth store. Reject absolute URLs, protocol-relative URLs, and external schemes.
- An expired AT triggers coordinated refresh without navigation while RT remains valid.
- A failed/expired/revoked RT clears NgStore auth state and redirects once to the localized session-expired login flow.
- A 401 may trigger at most one coordinated refresh and one request retry. A 403 never triggers refresh or logout; route to the established forbidden experience.
- Explicit logout revokes tokens through the identity provider when supported, clears NgStore atomically, and redirects to the confirmed post-logout route.

## Verification

- Verify startup rehydration/callback ordering so transient state and the PKCE verifier are restored before the OIDC callback check and guards do not redirect first.
- Verify AT renewal, rotated RT replacement, concurrent 401 single-flight behavior, refresh failure, logout, reload, deep links, 403 handling, and absence of token values in logs.
- Build the Angular application. Run tests or browser checks only when explicitly requested by the user.
