# Seattle by Night

A web-based MUSH client for a Shadowrun 5e game set in Seattle. It combines
text-based multiplayer roleplay, persistent locations, room-scoped chat, and
server-authoritative movement through a directed room graph.

## Architecture

The backend is a modular monolith built on ASP.NET Core (.NET 10). It separates
concerns into projects with strict dependency direction.

```text
backEnd/
  src/
    SeattleByNight.Api/            Composition root: endpoints, SignalR hubs, host config
    SeattleByNight.Application/    Use cases (commands, queries, MediatR handlers)
    SeattleByNight.Domain/         Entities and domain enums (no dependencies)
    SeattleByNight.Infrastructure/ EF Core, PostgreSQL, migrations, seeding, stores
  tests/
    SeattleByNight.Domain.Tests/          Domain unit tests
    SeattleByNight.Infrastructure.Tests/  PostgreSQL integration tests
    SeattleByNight.Api.Tests/             API + SignalR integration tests

frontEnd/                          Vite + React + TypeScript single-page app
```

Dependency direction:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application -> Domain
                    -> Domain
```

`Domain` depends on nothing. `Infrastructure` implements the feature-specific
persistence interfaces declared by `Application`. `Api` wires everything together.

## Prerequisites

- .NET 10 SDK
- Node.js 24+ (LTS)
- Docker Desktop (with WSL 2 on Windows)

Verify locally:

```powershell
dotnet --version
node --version
docker --version
```

## Setup

### 1. Configure and start PostgreSQL

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
docker compose ps
```

The database is available at `localhost:5432` with the credentials in `.env`
(defaults: user `seattlebynight`, password `localdevpassword`, database
`seattlebynight`). `.env` is gitignored; never commit real credentials.

### 2. Configure the connection string

The backend reads `ConnectionStrings:SeattleByNight`. In the Development
environment it falls back to a localhost connection string matching the
container above. For other environments, provide it explicitly:

```powershell
$env:ConnectionStrings__SeattleByNight = "Host=localhost;Port=5432;Database=seattlebynight;Username=seattlebynight;Password=localdevpassword"
```

### 3. Apply migrations

```powershell
dotnet tool restore
dotnet ef database update --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
```

In Development, the API also applies migrations and seeds deterministic sample
data on startup (Downtown Street, Coffee Shop, Alley, their exits, and a
development `devuser` / `DevPassword1!` account with the `Dev Runner` character
in Downtown Street). In Development, `devuser` is also granted the
`Administrator` role as the bootstrap administrator. No elevated role is ever
assigned automatically outside Development.

## Roles and the first administrator

Role definitions are created idempotently by database migration in every
environment. In production, the first administrator must be granted explicitly
by an operator; there is no default administrative credential or silent
elevation.

Bootstrap procedure (run once against the target database):

```powershell
# 1. Apply migrations to create the role definitions (idempotent).
dotnet ef database update --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api

# 2. Create (or choose) the operator account through the normal registration flow,
#    then grant Administrator using psql. Replace the placeholder IDs.
```

```sql
-- Find the target user's id and the Administrator role's id, then:
INSERT INTO asp_net_user_roles (user_id, role_id)
SELECT u.id, r.id
FROM asp_net_users u, asp_net_roles r
WHERE u.normalized_user_name = 'OPERATOR_USERNAME'
  AND r.normalized_name = 'ADMINISTRATOR'
ON CONFLICT DO NOTHING;
```

`WorldBuilder` grants access to the world editor without granting user-role or
audit-log administration. `Moderator` is reserved for later moderation work.

## Running the applications

Backend (defaults to `http://localhost:5263`):

```powershell
dotnet run --project backEnd/src/SeattleByNight.Api
```

Frontend (defaults to `http://localhost:5173`):

```powershell
npm --prefix frontEnd install
npm --prefix frontEnd run dev
```

The frontend dev server proxies `/api` and `/hubs` (with WebSocket support) to
`http://localhost:5263`; see `frontEnd/vite.config.ts`. All frontend fetch and
SignalR code uses relative URLs.

Health endpoints:

- Liveness: `GET /health/live` (does not require PostgreSQL)
- Readiness: `GET /health/ready` (checks PostgreSQL connectivity)

## HTTP API

Authentication uses an HTTP-only, same-site cookie issued by ASP.NET Core
Identity. State-changing cookie-authenticated endpoints require an antiforgery
header `X-XSRF-TOKEN` obtained from `GET /api/antiforgery/token`.

### Account

- `POST /api/account/register` — `{ email, username, password }`
- `POST /api/account/login` — `{ login, password }` (email or username)
- `POST /api/account/logout` — ends the active play session, then signs out
- `GET /api/account/me` — `{ id, email, userName, roles }`

### Characters

- `GET /api/characters` — lists the current user's characters
- `POST /api/characters` — `{ name }`; creates a character in the configured
  New Character Room (maximum two characters per user)

### Play session

- `POST /api/play-session/start` — `{ characterId }`; starts or resumes a session
- `GET /api/play-session/current` — current `RoomSession` (optional `?cursor=` for older messages)
- `POST /api/play-session/activity` — renews the idle timeout (throttled)

The `RoomSession` response contains the play-session ID and expiry, the current
character, room, visible exits, durable occupants, the latest message page, and
an older-messages cursor.

### Administration

Administrative endpoints use named authorization policies rather than inline
role checks. Roles are `Administrator`, `WorldBuilder`, and `Moderator`. New
registrations receive no roles.

- `GET /api/admin/users?query=` — user lookup for role management (role-management policy)
- `POST /api/admin/users/{userId}/roles` — `{ roleName }`; assigns a role (role-management policy)
- `DELETE /api/admin/users/{userId}/roles/{roleName}` — removes a role; the last administrator cannot be removed
- `GET /api/admin/audit` — cursor-paginated audit log (audit-reader policy);
  filters: `actor`, `action`, `targetType`, `targetId`, `from`, `to`, `cursor`
- `GET /api/admin/world` — bounded complete room graph for world builders,
  including hidden and locked directed exits
- `GET /api/admin/world/rooms/{roomId}` — room editor details with distinct
  incoming and outgoing exits
- `POST /api/admin/world/rooms`, `PUT /api/admin/world/rooms/{roomId}` — create
  and update rooms
- `POST /api/admin/world/exits`, `PUT /api/admin/world/exits/{exitId}` — create
  and update one directed exit at a time

Every administrative mutation appends an append-only audit record in the same
transaction as the mutation. Audit records cannot be updated or deleted through
the API.

### World editor

`/admin/world` is available to `Administrator` and `WorldBuilder`. It provides a
layered coordinate grid, an accessible room list, empty-cell room creation, room
metadata editing, and explicit incoming/outgoing exit editing.

Coordinates are required, unique within a layer, and immutable. Selecting an
empty grid cell opens the room metadata form. Saving creates both directed paths
to each occupied same-layer compass neighbor. Those exits are defaults only:
persisted directed exits remain authoritative and builders can rewire them or
change hidden and locked state. Vertical `up` and `down` exits are added
explicitly.

Exits have no separate names. Their direction is the player-visible label, and a
room can have at most one exit for each approved compass or vertical direction.
A manually created reverse path remains a separate confirmed operation.

Rooms and exits carry opaque version tokens. An update based on a stale version
returns `409 Conflict` and preserves the newer committed value. Reload before
retrying the edit.

There are no room or exit deletion commands, endpoints, or editor controls.
Incorrect world data is recovered by editing the record or by locking/hiding an
exit while it is repaired.

## SignalR hub

Route: `/hubs/room-chat` (authenticated).

Client-to-server methods:

- `JoinCurrentRoom()` — joins the group for the active session's database room
  and returns the authoritative `RoomPresence` snapshot for that room
- `RecordActivity()` — throttled meaningful browser activity; returns the
  authoritative renewed `expiresAtUtc`
- `SendMessage(content, type)` — persists and broadcasts a room message of the
  given type (`Say` or `Emote`); returns the authoritative renewed `expiresAtUtc`
- `RollDice(expression)` — parses, rolls, and persists a server-rendered `Roll`
  message; returns the authoritative renewed `expiresAtUtc`
- `MoveThroughExit(exitId)` — moves through a valid exit from the current room
- `GetOnlineCharacters()` — returns the distinct online characters across all
  rooms (id and name only; never their rooms)

Server-to-client events:

- `MessageReceived(message)`
- `CharacterDeparted(event)` — `{ roomId, character }` to other clients in the old room
- `CharacterArrived(event)` — `{ roomId, character }` to other clients in the new room
- `RoomChanged(roomSession)` — to the moving client
- `RoomPresenceChanged(presence)` — `{ roomId, revision, onlineCharacters }`
  broadcast to a room when its distinct online-character set changes
- `SessionExpired()`

Movement is server-authoritative: the client supplies only an exit ID, the
server validates the active session and exit, commits the durable move, and only
then changes SignalR group membership.

The frontend derives its idle warning from the authoritative `expiresAtUtc`
timestamps carried by `RoomSession` (from `JoinCurrentRoom` and `RoomChanged`)
and by the results of `RecordActivity` and `SendMessage`; there is no separate
warning event.

## Gameplay commands

The composer accepts plain text (speech) and a small command set parsed entirely
on the client:

- `/say <text>` — speak; identical to typing plain text
- `/emote <action>` — act in the current room; rendered as `Name <action>`
- `/roll <NdS[+/-M]>` — roll dice (e.g. `2d6+3`); the expression is submitted to
  the server, which parses, rolls, and persists the authoritative result
- `/help` — list commands
- `/look` — describe the current room, its visible exits, and its occupants
- `/go <direction|exit>` — move through a visible exit resolved locally, then
  submitted to the server as the authoritative exit ID
- `/who` — list characters online right now (no locations)

`/help`, `/look`, `/who`, and parser/usage errors are private local transcript
output and are never persisted or broadcast. `/go` persists only the resulting
movement; the command text is not a chat message.

## Tests

```powershell
dotnet test backEnd/SeattleByNight.slnx
npm --prefix frontEnd run test -- --run
```

- Domain tests require no external services.
- Infrastructure and API integration tests use Testcontainers to run an
  isolated PostgreSQL container, so Docker must be running.

## Build and lint checks

```powershell
dotnet build backEnd/SeattleByNight.slnx
npm --prefix frontEnd run lint
npm --prefix frontEnd run build
```

## Manual multi-browser verification

1. Start PostgreSQL, the backend, and the frontend (see above).
2. Open two browser windows to `http://localhost:5173` and register two
   different accounts (or register one and log in as `devuser` /
   `DevPassword1!` in the other).
3. In each window, create a character and enter the world.
4. Move both characters into the same room and send messages; each window shows
   new messages without refreshing.
5. Move one character through an exit and confirm the other window shows the
   departure/arrival, while the mover stops receiving old-room chat and starts
   receiving new-room chat.
6. Reload a window and confirm the same play session and eligible transcript are
   restored. Log out and back in to confirm a fresh session starts with empty
   history.
7. Confirm presence in the occupants sidebar: two online characters show as
   "online"; disconnect one tab and confirm its character becomes "offline" but
   remains listed as a durable occupant; open a second tab for one character and
   confirm it still shows a single online entry.

## Deployment target

The current deployment target is a single application instance serving both the
REST API and the built React frontend, backed by one PostgreSQL database. No
Redis, load balancer, message broker, or multiple replicas are required yet.

## Security and hardening

- Passwords are stored only through ASP.NET Core Identity's salted, one-way
  password hasher.
- Authentication is cookie-based and HTTP-only; the `Secure` flag is required
  outside local HTTP Development.
- Character selection and all gameplay operations are authorized against the
  authenticated user's ownership; room, character, and session IDs are never
  accepted as credentials.
- Administrative endpoints are guarded by named authorization policies, and
  every administrative mutation writes an append-only audit record.

Authentication hardening (email confirmation and password-reset delivery) is
deferred and must be completed before enabling non-public rooms or production
deployment. Moderation APIs remain a later milestone.

## Deferred work

- Room ownership and access policies beyond public rooms
- Email confirmation and password-reset delivery
- Moderation APIs (role and policy scaffolding already exists)
- Gameplay systems (character sheets, dice, combat, initiative, equipment, Matrix)
- In-memory caching and invalidation
- Graphical map rendering
