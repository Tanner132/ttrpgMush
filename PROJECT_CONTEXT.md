# Seattle by Night Project Context

## Product

Seattle by Night is a browser-based MUSH for a Shadowrun Fifth Edition tabletop roleplaying game set in Seattle. It should combine text-based multiplayer roleplay, persistent locations, room-scoped chat, character movement, and eventually Shadowrun-specific gameplay systems.

MUSH behavior is the primary product model:

- Players connect through a web client and act through characters.
- Characters occupy persistent rooms in a shared world.
- Rooms form a navigable graph connected by directed exits.
- Chat, presence, arrivals, and departures are scoped to the current room.
- Public rooms are the initial use case.
- Future rooms may be owned, claimed, private, invite-only, or otherwise access controlled.
- Future features may include character sheets, dice, combat, initiative, equipment, Matrix activity, and a graphical city map.

Do not implement Shadowrun gameplay rules from assumptions. Record unclear rule interpretations as product decisions and confirm them before implementation.

## Engineering Priorities

This is a medium-scale personal project maintained primarily by one developer. Use industry-standard engineering practices where they improve correctness, maintainability, security, testing, or deployment, but avoid infrastructure and abstractions whose operational cost is not justified.

Prefer:

- A modular monolith over microservices.
- One application deployment and one PostgreSQL database initially.
- Explicit module boundaries and dependency direction.
- Vertical slices that work end to end.
- Server-authoritative state and authorization.
- Simple, measurable solutions before distributed infrastructure.
- Small changes that preserve a clear path to future growth.

Do not introduce Redis, message brokers, Kubernetes, microservices, multiple replicas, or a dedicated load balancer without a measured need or an approved architectural change.

## Technology Stack

### Backend

- C# and ASP.NET Core on .NET 10.
- Modular-monolith solution in `backEnd/SeattleByNight.slnx`.
- EF Core 10.0.4 with Npgsql 10.0.3.
- PostgreSQL 17.
- ASP.NET Core Identity provides local email, username, and password authentication.
- MediatR provides application commands and queries.
- SignalR provides realtime messaging and movement events.
- OpenAPI is enabled in Development.
- `dotnet-ef` 10.0.4 is pinned in `dotnet-tools.json`.

### Frontend

- React 19 with TypeScript in strict mode.
- Vite 8.
- Oxlint using the scaffolded configuration.
- Responsive CSS without a component framework.
- No routing, API client, or global state library; `@microsoft/signalr` powers the realtime client.

### Testing and Local Development

- xUnit for backend tests.
- Testcontainers.PostgreSql for isolated PostgreSQL integration tests.
- Docker Compose runs the local PostgreSQL service.
- Infrastructure tests require a working Docker runtime.
- Local defaults are API `http://localhost:5263`, frontend `http://localhost:5173`, and PostgreSQL `localhost:5432`.

Use the versions actually pinned in project manifests and lockfiles as the source of truth when this summary becomes stale.

## Backend Architecture

The backend projects and dependency direction are:

```text
SeattleByNight.Api -> SeattleByNight.Application -> SeattleByNight.Domain
SeattleByNight.Api -> SeattleByNight.Infrastructure -> SeattleByNight.Application
                                                -> SeattleByNight.Domain
```

### `SeattleByNight.Domain`

- Contains persistence-ignorant domain entities and enums.
- Has no project dependencies.
- Must not reference EF Core, ASP.NET Core, SignalR, or Infrastructure.

### `SeattleByNight.Application`

- Owns use cases, commands, queries, authorization decisions, and application orchestration.
- References Domain only.
- Is currently an empty boundary awaiting the first feature slices.
- Future SignalR hubs and HTTP endpoints should delegate behavior to this layer rather than contain business logic.

### `SeattleByNight.Infrastructure`

- Owns EF Core, Npgsql, entity mappings, migrations, and development seeding.
- Implements persistence and external adapters required by Application.
- References Application and Domain.

### `SeattleByNight.Api`

- Is the composition root.
- Owns host configuration, dependency registration, transport endpoints, health checks, and Development initialization.
- References Application and Infrastructure.
- Must keep controllers, minimal endpoints, and SignalR hubs thin.

Do not add a generic repository over EF Core by default. Add application-facing persistence abstractions only when they represent a meaningful use-case boundary or improve testability.

## Domain Model

All persisted identifiers are UUIDs. Persisted timestamps use UTC `DateTimeOffset` values and PostgreSQL `timestamp with time zone`.

### Room

- Persistent location containing a name, description, public access type, optional map coordinates, and creation timestamp.
- `RoomAccessType` currently contains only `Public`.
- Ownership and non-public access rules are deferred.

### RoomExit

- Directed edge from `SourceRoomId` to `DestinationRoomId`.
- Contains a name, direction, hidden flag, and locked flag.
- A reverse path requires a separate `RoomExit`; never infer bidirectionality.
- Movement must be requested through an exit from the character's current room, not by accepting an arbitrary destination room ID.

### Character

- Player-facing identity in the game world.
- Has a durable `CurrentRoomId`.
- Must have a required owner user ID once the next authentication migration is applied.
- A user may own at most two characters.
- Character names are globally unique after normalization and initially support creation by name only.
- Every newly created character starts in the configured `New Character Room`.
- Location, chat, and gameplay behavior operates on the selected character while authorization is derived from the authenticated owner.

### ChatMessage

- Durable room-scoped message.
- Identifies its room and sending character.
- The server must determine sender identity and timestamps; clients must not be trusted to supply authoritative values.

Entities currently expose scalar foreign keys without navigation properties. EF mappings are explicit in Infrastructure, use snake_case tables and columns, create relevant indexes, and restrict cascading deletes.

## Room Graph and Movement

Treat the world as a directed graph, not a linked list or grid:

- Rooms are nodes.
- Room exits are directed edges.
- Graph connectivity determines allowed movement.
- Optional coordinates and map layers determine presentation only.
- One-way exits, loops, branches, hidden paths, and locked paths must remain possible.
- Visual map layout must not become the source of truth for connectivity.

An eventual movement operation should validate the character's current room, the selected exit, destination access, and any exit restrictions before updating `CurrentRoomId`.

## Realtime Model

SignalR is implemented. The design is:

- One authenticated SignalR connection per browser session, not one connection per room.
- SignalR groups named by room ID route room-scoped events.
- Joining a SignalR group is not an authorization boundary; the server must validate every join, movement, and send operation.
- Character location in PostgreSQL is durable state.
- Active connections and online presence are ephemeral state.
- A SignalR disconnect immediately removes that connection from realtime delivery, but does not by itself end the durable play session.
- Moving removes the connection from the previous room group and adds it to the destination group only after the durable location update succeeds.
- Messages are persisted before they are broadcast.
- Clients deduplicate messages by server-generated message ID and rejoin the authoritative room after reconnecting.

Online presence is an in-memory projection of joined SignalR connections:

- A connection registration carries the play-session ID, character summary, and room ID.
- Presence is deduplicated by character ID, so multiple connections for one active play session show a single online character; the character goes offline only after the final connection leaves.
- `JoinCurrentRoom()` returns the authoritative `RoomPresence` snapshot and is idempotent: re-joining the same session/character/room is a no-op, and re-joining after the durable room changed self-heals by leaving the stale group before joining the authoritative one.
- `RoomPresenceChanged(presence)` is broadcast to a room only when its distinct online-character set changes, and carries a monotonic in-process `revision` that clients use to reject stale snapshots.
- Presence is lost on application restart by design and never changes durable occupancy.

The initial deployment has one backend instance, so SignalR's in-process connection and group state is sufficient. Before adding multiple backend replicas, introduce an appropriate SignalR backplane and distributed presence strategy.

## Transactional Gameplay Ordering

PostgreSQL serializes gameplay mutations by locking the active play-session row
(`SELECT ... FOR UPDATE`) before reading or changing its character room, expiry,
active room visit, or chat destination. Chat send, movement, and expiration all
acquire this lock first and in the same order, so they serialize without
deadlocks:

- Chat send locks the user's active session, rejects ended/expired sessions,
  resolves the selected character's authoritative room, renews activity, and
  inserts the message in one transaction.
- Movement locks the active session, validates the exit and destination against
  the locked room, updates `Character.CurrentRoomId`, closes exactly one open
  room visit, opens the destination visit, and renews activity in one transaction.
- Expiration conditionally ends a session only if it is still expired at write
  time; a stale scan cannot end a session that was renewed concurrently. It closes
  the open visit at the same server timestamp and only notifies the connection
  manager when it actually ended the session.

## Play Sessions and Chat Visibility

Player-visible chat history is scoped to a durable play session, not to a SignalR connection and not to all messages in a room.

The intended model is:

- `PlaySession` identifies one continuous period of play for a character.
- `RoomVisit` records half-open time intervals during which that play session occupied a room: `[EnteredAtUtc, LeftAtUtc)`.
- A player may read a message only when it belongs to the current play session and its room and timestamp fall within one of that session's room visits.
- Leaving and later re-entering a room creates separate visits.
- Starting a new play session produces an empty transcript even when old messages remain in PostgreSQL.
- Reloading, reconnecting SignalR, a short network interruption, laptop sleep, or an application restart does not start a new play session.
- Explicit logout ends the play session and closes its active room visit.
- Inactivity also ends the session. The default idle timeout is 60 minutes and must be configurable.
- The client warns shortly before expiry and may renew the lease only after meaningful user activity.
- SignalR transport heartbeats and a background browser tab do not count as meaningful activity.
- Chat sends, movement, and a throttled activity signal produced by real keyboard, pointer, or focused interaction renew the lease.
- When the lease expires, the server closes the session and room visit, removes associated connections from room groups, emits a session-expired event, and rejects further hub commands.

ASP.NET Core Identity's HTTP-only, same-site authentication cookie identifies the user, with the `Secure` flag required outside local HTTP Development. Selecting one of the user's owned characters starts or resumes the user's active `PlaySession`. The server resolves play sessions through authenticated user ownership rather than a client-supplied user, character, or room identity. Limit the initial design to one active play session and selected character per user.

## Authentication and Character Ownership

Use ASP.NET Core Identity rather than custom password storage:

- User IDs are UUIDs.
- Email and username are required, normalized, and unique.
- Passwords are stored only through Identity's versioned, salted, one-way password hasher; never encrypt passwords or implement custom password cryptography.
- Use cookie authentication, login rate limiting, lockout, antiforgery protection for state-changing HTTP endpoints, and generic login errors that do not disclose whether an account exists.
- The default authentication inactivity window aligns with the configurable 60-minute play-session timeout.
- Meaningful activity may renew authentication and play-session expiry through an explicit throttled HTTP request; SignalR heartbeats do not renew either.
- Idle expiry removes SignalR group membership and causes subsequent HTTP and hub operations to be rejected even if a stale browser cookie remains.
- Explicit logout ends the active play session before signing out the Identity cookie.

Registration creates only the user account. Character creation is a separate authenticated operation. Character creation must validate ownership count transactionally so concurrent requests cannot create a third character. It sets `CurrentRoomId` to the configured `New Character Room`; clients cannot choose an arbitrary starting room.

Email confirmation, password reset delivery, administrator roles, and moderation UI are later hardening milestones. Their absence must be documented before public deployment.

Player transcript queries should use cursor pagination over the current session's eligible messages. Do not copy one row per message recipient. Visibility is derived from room-visit intervals, which avoids the largest source of storage amplification.

All chat messages remain available to future authorized moderation queries independently of player transcript visibility. Administrative chat access must require a role, be auditable, and bypass room-visit filtering only through an explicit moderation use case. No moderation endpoint should be exposed before authentication and roles exist.

PostgreSQL text storage is expected to be sufficient at the project's initial scale. A million typical short messages is generally measured in hundreds of megabytes before indexes, not four gigabytes merely because the maximum content length is 4,000 characters. Monitor actual growth before adding complexity. If needed later, add a documented retention period, time-based partitions, and compressed archival exports; do not sacrifice moderation records or player privacy semantics for premature optimization.

## Caching

No application cache is implemented. When caching is justified:

- Use `IMemoryCache` initially for stable room metadata or similarly read-heavy reference data.
- Do not use the database cache as the authority for live SignalR presence.
- Do not cache message history or rapidly changing occupant lists without evidence that it is necessary.
- Invalidate or update specific room entries when durable room metadata changes.
- Revisit distributed caching only before horizontal backend scaling.

## Database and Development Data

The current schema contains `rooms`, `room_exits`, `characters`, and `chat_messages`. The initial migration is `InitialWorldSchema`.

Development startup applies migrations and attempts deterministic, idempotent seeding. Initialization failures are logged as warnings so the process can still expose liveness while PostgreSQL is unavailable.

Seed data contains:

- Downtown Street.
- Coffee Shop.
- Alley.
- Directed Downtown Street to Coffee Shop and Coffee Shop to Downtown Street exits.
- Directed Downtown Street to Alley exit.
- Dev Runner located in Downtown Street.

The next authentication migration will add a deterministic `New Character Room` used as the configured starting location for all newly created characters. It does not need to replace Dev Runner's Downtown location.

No credentials or chat messages are seeded.

## Current Implemented Surface

Backend endpoints:

- `GET /health/live`: process liveness without a PostgreSQL dependency.
- `GET /health/ready`: PostgreSQL readiness through the EF Core context.
- `GET /api/antiforgery/token`: returns the antiforgery token used by
  state-changing cookie-authenticated requests.
- `POST /api/account/register`, `POST /api/account/login`, `POST /api/account/logout`,
  `GET /api/account/me`: ASP.NET Core Identity account management.
- `GET /api/characters`, `POST /api/characters`: list and create owned characters.
- `POST /api/play-session/start`, `GET /api/play-session/current`,
  `POST /api/play-session/activity`: start/resume, read, and renew play sessions.

SignalR hub (`/hubs/room-chat`):

- `JoinCurrentRoom()`, `RecordActivity()`, `SendMessage(content)`,
  `MoveThroughExit(exitId)`.
- Events: `MessageReceived`, `CharacterDeparted` (`{ roomId, character }`),
  `CharacterArrived` (`{ roomId, character }`), `RoomChanged`,
  `RoomPresenceChanged` (`{ roomId, revision, onlineCharacters }`), `SessionExpired`.
- `JoinCurrentRoom()` returns the joined room's `RoomPresence`; `RecordActivity()`
  and `SendMessage()` return the authoritative renewed `ExpiresAtUtc`.

The frontend implements registration, login, character creation/selection, room
session rendering, realtime room chat, idle-expiry handling, and movement
through visible unlocked exits.

Exit and send controls are enabled only while connected and joined. Deployment
does not yet serve the built React application from ASP.NET Core (static-file and
SPA fallback hosting are not configured).

## Security and Consistency Rules

- Treat every client payload as untrusted.
- Authorize room access, movement, and message sending on the server.
- Never use SignalR group membership as proof of authorization.
- Persist state-changing operations before broadcasting resulting events.
- Avoid exposing connection strings, credentials, or internal exception details.
- Keep secrets in environment variables, user secrets, or deployment secret storage.
- Preserve restrictive foreign-key delete behavior unless an explicit domain lifecycle requires otherwise.

## Testing Expectations

- Domain tests cover domain behavior and invariants without external services.
- Infrastructure tests use real PostgreSQL semantics through Testcontainers, never EF Core's in-memory provider.
- Future HTTP and SignalR features should include integration tests for authorization, room isolation, persistence-before-broadcast behavior, reconnect handling, and invalid movement.
- Frontend behavior should gain tests when interactive state and server integration are introduced.

## Deferred Features

- Email confirmation and password-reset delivery.
- Administrator roles, moderation APIs, and moderation audit logs.
- Room ownership and access policies beyond public access.
- Room metadata caching.
- Graphical map rendering.
- Shadowrun character sheets, dice, combat, initiative, equipment, and Matrix systems.
- Redis, brokers, load balancing, and horizontal scaling.

## Change Discipline

- Preserve the modular-monolith dependency direction.
- Keep transport concerns out of Domain and Application.
- Do not change architecture during Build mode; propose architecture changes in Plan mode first.
- Update this file when an accepted architectural decision, technology choice, or major implemented capability changes.
- Use `README.md` for setup and operator instructions; use this file for product and engineering context.
