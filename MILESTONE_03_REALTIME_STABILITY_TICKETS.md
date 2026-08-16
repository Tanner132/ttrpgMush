# Milestone 03: Realtime Presence and Consistency

## Milestone Goal

Stabilize the existing SignalR gameplay loop before adding slash commands. Make chat, movement, expiration, room-group membership, durable occupants, and ephemeral online presence agree under reconnects and concurrent operations.

Complete tickets in order. Verify each ticket before proceeding.

## Build Mode Contract

- Read `AGENTS.md`, `PROJECT_CONTEXT.md`, `MILESTONE_02_TICKETS.md`, and this file before editing.
- Implement the explicit ticket scope without changing the modular-monolith architecture.
- Keep SignalR hubs and HTTP endpoints thin. Application owns use-case orchestration; Infrastructure owns EF Core and PostgreSQL details.
- Preserve server-authoritative identity, session, room, and movement decisions.
- Do not add Redis, a SignalR backplane, a message broker, or distributed presence. This milestone targets the documented single-instance deployment.
- Do not begin slash commands, room administration, or full character creation in this milestone.
- Do not combine durable room occupancy with online presence. They have different lifetimes and authorities.
- Do not add a database table or migration for online presence.

## Scope Decisions

- `RoomSession.occupants` remains the durable list of characters whose `CurrentRoomId` matches the room, whether connected or disconnected.
- Online presence is an in-memory projection of joined SignalR connections and is lost on application restart by design.
- Presence is deduplicated by character ID. Multiple connections for the same active play session must display one online character, and the character goes offline only after the final joined connection leaves.
- Existing `CharacterArrived` and `CharacterDeparted` events continue to describe durable movement between rooms. They are not connection or presence events.
- Add the affected room ID to arrival and departure payloads so a client can reject a delayed event from a room it has left.
- Presence synchronization uses an explicit room snapshot contract instead of interpreting movement events as online status.
- PostgreSQL serializes gameplay mutations by locking the active play-session row before reading or changing its character room, expiry, active room visit, or chat destination.
- Expiration must conditionally end a session only if it is still expired when the write occurs. A stale expiration scan must not end a session renewed after the scan.
- Remove the unused `SessionExpiring` SignalR event. The frontend already derives its warning from the server-provided expiry timestamp; this milestone keeps that timestamp synchronized after activity instead of adding a second warning scheduler.

## Shared Contracts

Add a transport contract for ephemeral room presence:

```text
RoomPresence
  roomId: UUID
  revision: integer
  onlineCharacters: CharacterSummary[]

RoomCharacterEvent
  roomId: UUID
  character: CharacterSummary
```

Contract rules:

- `onlineCharacters` contains distinct character IDs and has deterministic ordering by character name, then character ID.
- `revision` increases monotonically for each room during the current application process whenever that room's distinct online-character set changes. It is not persisted.
- `JoinCurrentRoom()` returns the authoritative `RoomPresence` snapshot for the joined room.
- `RoomPresenceChanged(presence)` is sent to joined clients when the distinct online-character set for a room changes.
- `CharacterArrived` and `CharacterDeparted` carry `RoomCharacterEvent` rather than an unscoped `CharacterSummary`.
- The frontend applies a presence snapshot only when `presence.roomId` equals its current room ID and its revision is not older than the latest applied revision.
- Connection count changes that do not change the distinct character set do not require a broadcast.

## M3-001: Make Idle Expiration Conditional and Atomic

**Goal:** Prevent a stale background scan from ending a session that was renewed concurrently.

**Dependencies:** Milestone 02 complete.

**Tasks:**

- Replace the expiration service's `ListExpiredAsync` followed by unconditional `EndAsync` flow with a store operation that conditionally ends one candidate only when `EndedAtUtc` is still null and `ExpiresAtUtc <= now` at write time.
- In one database transaction, lock or conditionally update the play session, close its one open room visit at the same server timestamp, and report whether this call actually ended it.
- Preserve idempotency when expiration, logout, or another cleanup path has already ended the session.
- Have `PlaySessionExpirationService` call `IRoomChatConnectionManager.EndSessionAsync` only when the conditional expiration operation reports that it ended the session.
- Do not disconnect a session merely because it appeared in an earlier stale candidate query.
- Keep explicit logout behavior intact.
- Add an integration test that pauses expiration after candidate discovery, renews the session, resumes expiration, and proves the renewed session and open visit remain active.
- Add an integration test proving two concurrent expiration attempts close the session and visit exactly once.

**Acceptance Criteria:**

- Renewal winning the database race prevents stale expiration.
- Expiration winning the race prevents later renewal and closes the open visit once.
- Repeated expiration is harmless.
- Realtime session cleanup occurs only for a session that was actually ended.
- No schema migration is introduced.

**Verification:**

```powershell
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
dotnet ef migrations has-pending-model-changes --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
```

## M3-002: Make Chat Persistence Use Authoritative Locked State

**Goal:** Ensure a message is persisted to the character's authoritative room and renews the same active session atomically.

**Dependencies:** M3-001.

**Tasks:**

- Keep blank and 4,000-character validation in the Application handler.
- Replace the separate active-session read, activity renewal, and message insert with one feature-specific chat-store operation.
- In one transaction, lock the authenticated user's active play-session row, reject ended or expired sessions, resolve its selected character and current room after acquiring the lock, renew activity, and insert the message into that room.
- Return the persisted `RoomMessage` and renewed `ExpiresAtUtc` from the operation.
- Do not trust connection-local character or room IDs as persistence authority.
- Keep broadcasting after the transaction commits and target the room ID returned with the persisted message.
- Use the same play-session locking order required by movement so send-versus-move operations serialize without deadlocks.
- Add integration tests that coordinate a send and move in both lock orders. The message must be wholly before or wholly after movement: it belongs to the room selected while holding the lock, its timestamp fits the corresponding room visit, and it remains visible under existing visit-history rules.
- Add a send-versus-expiration test proving no message can be persisted for a session that expiration ended first.

**Acceptance Criteria:**

- Message insert and activity renewal either both commit or both fail.
- A concurrent move cannot cause a message to be persisted with stale room state.
- A concurrent expiration cannot leave a message attached to an ended session's post-expiry activity.
- Existing content validation, room isolation, and persistence-before-broadcast guarantees remain intact.
- No schema migration is introduced.

**Verification:**

```powershell
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
dotnet ef migrations has-pending-model-changes --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
```

## M3-003: Make Movement Validate the Locked Active Session

**Goal:** Prevent movement from committing after session expiration or from using stale pre-lock room state.

**Dependencies:** M3-001 and M3-002.

**Tasks:**

- Preserve the `MoveCharacterCommand` API: the caller supplies an exit ID, never a destination room ID.
- Move the final active-session, character-location, exit-source, hidden, locked, and destination-access validation into the movement transaction using state read after the play-session lock is acquired.
- Lock the same play-session row first and in the same order as chat persistence.
- In that transaction, reject an ended or expired session, verify the selected character still occupies the exit source, close exactly one open room visit, create the destination visit, update `Character.CurrentRoomId`, and renew session activity.
- Require each expected state transition to affect exactly one row. Roll back and return the existing appropriate movement failure when it does not.
- Do not return a successful durable move when the play-session renewal or room-visit transition did not occur.
- Keep SignalR group changes after commit.
- Add move-versus-expiration tests for both lock orders.
- Preserve and rerun simultaneous-movement, stale-exit, hidden-exit, locked-exit, and visit-boundary coverage.

**Acceptance Criteria:**

- Expiration winning the lock prevents movement.
- Movement winning the lock renews the session, preventing a stale expiration candidate from ending it.
- Character location, room visits, and session activity cannot partially commit.
- Existing directed-exit and server-authority rules remain unchanged.
- No schema migration is introduced.

**Verification:**

```powershell
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
dotnet ef migrations has-pending-model-changes --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
```

## M3-004: Make Room Joining Idempotent and Self-Healing

**Goal:** Ensure one connection cannot retain stale membership in a previously joined room.

**Dependencies:** M3-003.

**Tasks:**

- Extend the connection registry so the hub can retrieve the current registration for a connection ID before replacing it.
- On `JoinCurrentRoom`, resolve the current active session and database room every time.
- If the connection was registered to a different room, remove it from the old SignalR group before adding it to the authoritative group.
- If it is already registered to the same play session, character, and room, treat the join as an idempotent success without a leave/rejoin transition.
- Replace connection-local state and registry state only with the newly validated authoritative session data.
- If group synchronization fails, avoid leaving registry and `Context.Items` claiming a state that did not complete. Surface a hub error so the client reconnects and re-bootstrap fetches authoritative state.
- Keep disconnect cleanup idempotent.
- Add a SignalR integration test that joins, changes the durable room through a controlled test setup, invokes `JoinCurrentRoom` again on the same connection, and proves it receives new-room traffic but not old-room traffic.
- Add tests for repeated same-room joins, join after the original session ended, and cleanup after a failed rejoin.

**Acceptance Criteria:**

- Repeated same-room joins do not duplicate or corrupt registration.
- A same-connection rejoin cannot retain old-room delivery.
- Registry state, connection-local state, and SignalR group membership converge on the database room.
- Failed validation does not grant new group membership.
- No schema migration is introduced.

**Verification:**

```powershell
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
```

## M3-005: Add Ephemeral Room Presence Snapshots

**Goal:** Expose who is currently connected without changing the durable occupant model.

**Dependencies:** M3-004.

**Tasks:**

- Extend each connection registration with play-session ID, character ID, character summary, and room ID.
- Add registry operations that return a room's distinct, deterministically ordered online characters, a monotonic in-process room revision, and whether an add, move, or removal changed that distinct set.
- Keep registry operations thread-safe across simultaneous joins, disconnects, movement, timeout cleanup, and repeated joins.
- Change `JoinCurrentRoom()` to return the joined room's `RoomPresence` snapshot.
- Broadcast `RoomPresenceChanged` after a successful first join when the distinct online set changes.
- On disconnect or session cleanup, remove the registration and broadcast the affected room's updated snapshot only when the distinct online set changes.
- After durable movement commits and group membership succeeds, update the registry and publish updated snapshots to the old and new room groups.
- Preserve `CharacterArrived` and `CharacterDeparted` as durable movement notifications. Do not emit them for connect or disconnect.
- Change arrival and departure payloads to `RoomCharacterEvent` containing the affected old or new room ID and character summary.
- Ensure multiple connections for one character produce one online entry and no false offline event until the final connection leaves.
- Ensure a failed move or failed join does not alter presence.
- Add SignalR integration coverage for first join, second character join, disconnect, movement, timeout cleanup, duplicate connections, repeated joins, and room isolation.

**Acceptance Criteria:**

- Presence reflects joined live connections, not `Character.CurrentRoomId` alone.
- Durable occupants continue to include disconnected characters in the room-session query.
- Online characters are unique and deterministically ordered.
- Older presence broadcasts cannot overwrite a newer snapshot for the same room.
- All remaining clients receive the correct room snapshot after presence changes.
- Application restart may clear presence without changing durable occupancy.
- No database entity or migration is added.

**Verification:**

```powershell
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
dotnet ef migrations has-pending-model-changes --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
```

## M3-006: Synchronize Frontend Occupants and Online Presence

**Goal:** Keep the current-room sidebar accurate as characters move, connect, disconnect, and reconnect.

**Dependencies:** M3-005.

**Tasks:**

- Add typed frontend contracts and handlers for room-scoped `CharacterArrived`, room-scoped `CharacterDeparted`, and revisioned `RoomPresenceChanged`.
- Have the realtime hook consume the `RoomPresence` returned by `JoinCurrentRoom` on initial connection and reconnect.
- Update durable `roomSession.occupants` by character ID only when a `CharacterArrived` or `CharacterDeparted` payload identifies the current room.
- Make occupant updates idempotent so duplicate events do not duplicate or remove the wrong character.
- Replace occupants from authoritative `RoomChanged` and HTTP refresh responses; do not merge stale occupants across rooms.
- Store online presence separately from `RoomSession.occupants` and apply a snapshot only when its room ID matches the currently rendered room and its revision is not older than the latest applied revision.
- Clear presence while disconnected, when the play session ends, and before switching rooms. Repopulate it from join or destination-room presence.
- Render durable occupants and online state distinctly. A disconnected character remains listed as present in the room but is not marked online.
- Use accessible text, not color alone, to identify online status.
- Add hook tests for all three events, reconnect join results, stale-room event rejection, stale-room and stale-revision snapshot rejection, and cleanup.
- Add component tests for arrival, departure, disconnected occupants, online indicators, movement, duplicate events, and reconnect.

**Acceptance Criteria:**

- Other players' movement updates the occupant list without an HTTP refresh.
- Connecting and disconnecting changes online indicators without changing durable occupancy.
- Reconnect obtains a complete authoritative presence snapshot.
- Events or snapshots from the prior room cannot corrupt the current-room sidebar.
- Frontend tests, lint, and production build pass.

**Verification:**

```powershell
npm --prefix frontEnd run test -- --run
npm --prefix frontEnd run lint
npm --prefix frontEnd run build
```

## M3-007: Synchronize Session Expiry After Activity

**Goal:** Prevent the idle-warning UI from using an expiry timestamp that successful activity already extended.

**Dependencies:** M3-002, M3-003, and M3-006.

**Tasks:**

- Change activity renewal results to return the authoritative `ExpiresAtUtc` for the active session, including a throttled renewal that succeeds without writing.
- Return the authoritative expiry timestamp from `RecordActivity` and update frontend expiry state from the invocation result.
- Return or otherwise deliver the committed renewed expiry after `SendMessage`; do not make the client guess by adding the configured timeout locally.
- Continue obtaining movement expiry from the authoritative `RoomChanged` session.
- Ensure older asynchronous activity responses cannot replace a newer known expiry timestamp.
- Remove `SessionExpiring` from `IRoomChatClient`, frontend SignalR registrations, tests, README, and project context. Keep the frontend timer-based warning driven by authoritative expiry timestamps.
- Keep the warning threshold behavior consistent with current configuration. If the frontend retains a threshold constant, document that it must match `PlaySession:ExpiryWarning`; do not add a new configuration delivery system in this ticket.
- Add backend tests for throttled and unthrottled renewal results.
- Add frontend tests proving activity and chat move the warning deadline forward, out-of-order expiry results cannot move it backward, and expiration still clears the session.

**Acceptance Criteria:**

- Successful activity, chat, and movement leave the frontend with the latest server expiry known to that operation.
- The client never computes a renewed expiry from its own clock and timeout assumptions.
- The warning does not appear based on an obsolete pre-renewal timestamp.
- No declared SignalR event remains permanently unimplemented.
- Backend and frontend checks pass without a schema migration.

**Verification:**

```powershell
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
npm --prefix frontEnd run test -- --run
npm --prefix frontEnd run lint
npm --prefix frontEnd run build
```

## M3-008: Verify and Document Realtime Stability

**Goal:** Leave the stabilized realtime model reproducible and accurately documented before slash-command work starts.

**Dependencies:** M3-001 through M3-007.

**Tasks:**

- Update `PROJECT_CONTEXT.md` to remove stale Application/schema statements and document transactional gameplay mutation ordering, conditional expiration, durable occupants, ephemeral presence, idempotent joins, and expiry synchronization.
- Update `README.md` with the final SignalR methods/events and manual presence verification steps.
- Keep `MILESTONE_02_TICKETS.md` as historical planning; do not rewrite its completed ticket contracts.
- Run all backend and frontend checks with Docker available.
- Manually verify two characters in one room, durable arrival/departure updates, online join/disconnect updates, duplicate tabs for one character, movement between rooms, reconnect, same-connection rejoin behavior where test tooling permits it, chat during movement, idle renewal, and final timeout.
- Record any test that cannot deterministically force a concurrency ordering and the seam used to make the integration test deterministic. Do not accept timing-only `Task.Delay` race tests as proof.

**Acceptance Criteria:**

- Documentation matches the implemented contracts and current schema.
- Automated tests cover both outcomes of send/move/expiration races through deterministic coordination.
- Backend build and tests pass.
- Frontend test, lint, and production build pass.
- EF Core reports no pending model changes.
- No slash-command code is included.

**Verification:**

```powershell
docker compose up -d postgres
dotnet build backEnd/SeattleByNight.slnx
dotnet test backEnd/SeattleByNight.slnx
dotnet ef migrations has-pending-model-changes --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
npm --prefix frontEnd run test -- --run
npm --prefix frontEnd run lint
npm --prefix frontEnd run build
```

## Completion Rule

For each ticket:

1. Implement only the active ticket and necessary prerequisites.
2. Preserve unrelated worktree changes.
3. Run every verification command listed for the ticket.
4. Fix failures introduced by the ticket before proceeding.
5. Report changed files, behavior, tests, migration status, and assumptions.
6. Mark the ticket complete only after its acceptance criteria are verified.
