# Milestone 2: Routed Frontend And Gameplay Extraction

**Outcome:** The SPA has explicit pages and navigation, shared UI is represented by
components, and gameplay no longer lives in `App.tsx`.

**Page boundary:** A page owns a route-level layout and data-loading lifecycle.
A component is reusable UI or a focused section composed by a page. Route pages live
under `src/pages`; shared or page-composed UI lives under `src/components`. API and
realtime code remain under their existing feature folders.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## FE-201: Add React Router And Route Skeleton

**Depends on:** Realtime presence cleanup.

**Scope:**

- Add the current stable `react-router-dom` package and use the version pinned by the lockfile.
- Mount the browser router from `main.tsx`.
- Define routes for `/login`, `/characters`, and `/play` with a not-found fallback.
- Introduce a shared application shell for the site heading, account summary, logout action, loading state, and startup error state.
- Preserve cookie account restoration. Redirect unauthenticated users to `/login`, authenticated users without a selected/active character to `/characters`, and players with an active session to `/play`.
- Preserve the intended destination when an unauthenticated route guard redirects to login.

**Acceptance criteria:**

- Direct navigation and browser refresh work for every route in Vite development.
- Back and forward navigation do not create duplicate SignalR connections.
- Route guards do not briefly render protected page content before account restoration completes.
- Existing logout behavior ends the play session, clears client state, and navigates to `/login`.
- Router and guard behavior have focused frontend tests.

## FE-202: Extract Authentication Page And Forms

**Depends on:** FE-201.

**Scope:**

- Move login/register layout and route-level state into `pages/LoginPage.tsx`.
- Extract reusable form or field UI only where it removes real duplication; do not introduce a form framework.
- Keep account API calls in `api/account.ts`.
- Keep generic login errors and current antiforgery behavior unchanged.

**Acceptance criteria:**

- Login and registration behavior matches the current application.
- Successful authentication navigates to the preserved destination when valid, otherwise `/characters`.
- Authentication tests no longer depend on rendering the gameplay page.

## FE-203: Extract Character Selection Page

**Depends on:** FE-201.

**Scope:**

- Move character listing, creation, two-character limit display, and selection into `pages/CharactersPage.tsx`.
- On successful selection, start or resume the play session and navigate to `/play`.
- Keep character API calls in `api/characters.ts` and play-session calls in `api/playSession.ts`.

**Acceptance criteria:**

- Loading, empty, error, create, limit, and selection states retain current behavior.
- A failed session start stays on `/characters` and shows an actionable error.
- Character page tests cover creation and navigation after selection.

## FE-204: Extract Gameplay Page And Components

**Depends on:** FE-201. Can proceed in parallel with FE-202 and FE-203.

**Scope:**

- Move `PlayingView` into `pages/GameplayPage.tsx`.
- Extract focused components for room details, transcript, composer, exits, occupants, connection status, and idle warning.
- Move cohesive React lifecycle behavior into gameplay hooks. Use the existing `useRoomChat` for SignalR transport, a gameplay-session hook for authoritative room loading/reconnect/session expiry, and a room-presence hook for room scoping, revision rejection, buffering, arrivals, and departures.
- Extract transcript pagination/deduplication into a hook when it remains cohesive; keep DOM-specific scroll behavior with the transcript component that owns the scroll element.
- Keep `GameplayPage` focused on composing hooks and page layout. Do not create a hook for every small state value or scatter lifecycle behavior across presentational components.
- Preserve message deduplication, scroll restoration, reconnect refresh, room movement, and idle renewal behavior.

**Acceptance criteria:**

- `App.tsx` is reduced to application composition rather than page implementation.
- Extracted components receive typed props and do not call APIs directly unless they are explicitly data-owning components.
- Only the gameplay route owns the room-chat SignalR connection.
- Presence revisions, stale-room filtering, buffered snapshots, reconnects, and session expiry have focused hook tests independent of page rendering.
- Existing realtime and interaction tests pass after being relocated or rewritten around the new boundaries.

## FE-205: Restore Active Play Route On Reload

**Depends on:** FE-203 and FE-204.

**Scope:**

- During authenticated startup, query the current play session to determine whether `/play` is valid.
- Treat a missing or expired session as normal and route to `/characters`.
- Avoid storing the selected character as the sole authority; derive it from the authoritative current session when one exists.

**Acceptance criteria:**

- Refreshing `/play` resumes the active character and transcript without selecting the character again.
- Navigating to `/play` without an active session redirects to `/characters`.
- Expiry while routed to `/play` returns the user to `/characters` without a full page reload.

## FE-206: Frontend Extraction Regression Pass

**Depends on:** FE-202 through FE-205.

**Scope:**

- Remove superseded view code and stale styles from `App.tsx` and `index.css` without redesigning the established UI.
- Update tests to target pages, shared components, and route behavior at the appropriate level.
- Document route behavior and frontend folder conventions.

**Acceptance criteria:**

- Frontend tests, lint, and build pass.
- Desktop and narrow mobile layouts retain access to all controls.
- No obsolete duplicate page implementation remains in `App.tsx`.
