# Seattle by Night

A browser-based MUSH for a Shadowrun Fifth Edition game set in Seattle. Players
build full SR5 characters, walk a persistent room graph, roleplay in room-scoped
chat, and run automated jobs against a server-authoritative rules engine. Staff
build the world and its missions from inside the running game.

- Product and engineering context: [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md)
- What is left for v1: [`roadmap/V1_ROADMAP.md`](roadmap/V1_ROADMAP.md)
- SR5 rules decisions, source manifest, and catalog ledgers: [`roadmap/`](roadmap/)

## What works today

| Area | Capability |
| --- | --- |
| Accounts | Register, log in, log out (ASP.NET Core Identity, HTTP-only cookie, antiforgery). Roles: `Administrator`, `WorldBuilder`, `Moderator`. |
| Character creation | Complete SR5 creator (Standard Priority and Sum-to-Ten): identity, priorities, metatype and metavariants, attributes, qualities, magic/resonance, augmentations, skills, knowledge, contacts, martial arts, gear with attachments and vehicle mods, lifestyle, review and atomic finalization. Two character slots per account. |
| Character sheet | Career sheet at `/characters/:id/sheet` and in play via `/character`. Karma advancement for attributes, Edge/Magic/Resonance, active skills, skill groups, knowledge skills, languages, and specializations. |
| World | Persistent rooms on a layered coordinate grid joined by directed exits (hidden/locked supported). Server-authoritative movement. |
| Play | Realtime room chat (speech, emotes, dice), presence, arrivals/departures, session-scoped transcript, idle expiry. |
| Rules engine | SR5 tests (success, threshold, opposed) built from the real sheet, limits, wound modifiers, Edge (Push the Limit, Second Chance), active effects, seeded and audited dice. |
| NPCs and combat | Placed NPCs with awareness states, room interactables with discovery, and structured-time combat: initiative passes, action economy, ranged and melee attacks, defense, soak, condition monitors, NPC turns, turn timers. |
| Missions | Private instanced encounters, objectives, mission items, Johnson dialogue with negotiation, and Karma/nuyen rewards granted exactly once through the career ledger. One player per instance. |
| World Forge | Staff content builder at `/admin/forge`: missions, encounters, NPC templates and placements, scenes/dialogue, triggers, and test definitions with a draft → validate → publish → retire lifecycle. |
| Administration | User role management, append-only audit log, coordinate world editor at `/admin/world`. |

Not built yet: spellcasting, Matrix actions, rigging, group missions, non-public
rooms, quality/magic career advancement, nuyen purchases, moderation tools, and
email confirmation/password reset. See the roadmap.

## Repository layout

```text
backEnd/
  src/
    SeattleByNight.Api/            Composition root: endpoints, SignalR hub, background services
    SeattleByNight.Application/    Use cases, SR5 catalog and evaluators, game engine
    SeattleByNight.Domain/         Entities and enums (no dependencies)
    SeattleByNight.Infrastructure/ EF Core, PostgreSQL, migrations, seeding, stores
  tests/                           Domain, Application, Infrastructure, and Api test projects
frontEnd/                          Vite + React + TypeScript single-page app
roadmap/                           v1 roadmap and SR5 rules/catalog reference
compose.yaml                       Local PostgreSQL
compose.production.yaml            Full production stack
```

## Prerequisites

- .NET 10 SDK
- Node.js 24+
- Docker Desktop (WSL 2 on Windows) — required for local PostgreSQL and for the
  Infrastructure and Api test projects

## Local setup

### 1. Start PostgreSQL

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
```

The database listens on `localhost:5432` with the credentials in `.env`
(defaults: user `seattlebynight`, password `localdevpassword`, database
`seattlebynight`). `.env` is gitignored.

### 2. Run the backend (`http://localhost:5263`)

```powershell
dotnet run --project backEnd/src/SeattleByNight.Api
```

In Development the API falls back to a localhost connection string, applies
migrations on startup, and seeds sample data: a few rooms, the `devuser` /
`DevPassword1!` account with the `Administrator` role, and a finalized
`Dev Runner` character. It also imports the embedded starter game content (the
gang-warehouse mission) as the first published content set.

Outside Development, set the connection string explicitly:

```powershell
$env:ConnectionStrings__SeattleByNight = "Host=localhost;Port=5432;Database=seattlebynight;Username=seattlebynight;Password=localdevpassword"
```

To apply migrations by hand:

```powershell
dotnet tool restore
dotnet ef database update --project backEnd/src/SeattleByNight.Infrastructure --startup-project backEnd/src/SeattleByNight.Api
```

### 3. Run the frontend (`http://localhost:5173`)

```powershell
npm --prefix frontEnd install
npm --prefix frontEnd run dev
```

The Vite dev server proxies `/api` and `/hubs` (including WebSockets) to the
backend. All frontend requests use relative URLs.

## Checks

```powershell
dotnet test backEnd/SeattleByNight.slnx
npm --prefix frontEnd run test -- --run
npm --prefix frontEnd run lint
npm --prefix frontEnd run build
```

- Domain and Application tests need no external services.
- Infrastructure and Api tests use Testcontainers, so Docker must be running.
- Building the Api project regenerates `backEnd/src/SeattleByNight.Api/openapi/SeattleByNight.Api.json`.
  After a contract change, run `npm --prefix frontEnd run gen:api` and commit
  both the OpenAPI document and `frontEnd/src/api/generated`. CI fails if either
  is stale.

## Roles and the first administrator

Role definitions are created by migration in every environment. New
registrations receive no role, and nothing outside Development grants one
automatically.

| Role | Access |
| --- | --- |
| `Administrator` | Everything: role management, audit log, world editor, World Forge. |
| `WorldBuilder` | World editor and World Forge only. |
| `Moderator` | Reserved. The policy exists; no moderation feature uses it yet. |

To bootstrap a deployment, set `Bootstrap__AdministratorEmail`
(`BOOTSTRAP_ADMINISTRATOR_EMAIL` in the Compose environment), register that
account through the normal flow, then restart the API. Startup grants the role
and logs it; until the account exists it logs a warning and continues.

Manual alternative with `psql`:

```sql
INSERT INTO asp_net_user_roles (user_id, role_id)
SELECT u.id, r.id
FROM asp_net_users u, asp_net_roles r
WHERE u.normalized_user_name = 'OPERATOR_USERNAME'
  AND r.normalized_name = 'ADMINISTRATOR'
ON CONFLICT DO NOTHING;
```

## Playing

Type plain text to speak. Commands:

| Command | Effect |
| --- | --- |
| `/say <text>`, `/emote <action>` | Speak or act in the room. |
| `/roll <NdS[+/-M]>` | Free-form dice, rolled and recorded by the server. |
| `/look`, `/who`, `/help` | Local output only; never broadcast or stored. |
| `/go <direction>` | Move through a visible exit. |
| `/character` | Open your character sheet without leaving the room. |
| `/test [name] [edge]` | Perform a game test, or list the available ones. `edge` spends Edge to Push the Limit. |
| `/do [name] [edge]` | Perform any action currently available to you (for example `/do sneak past razor`), or list them. |
| `/run` | Toggle running. |
| `/edge <yes\|no>`, `/defend <standard\|full>` | Answer a pending Edge or defense decision. |
| `/missions` | Show your missions and objectives. |
| `<number>` | Pick a numbered dialogue or scene choice. |

`/surge` is a development-only test effect.

The server computes which actions each player can take right now; typed
commands are validated against that same list.

## HTTP and realtime surface

The committed OpenAPI document
(`backEnd/src/SeattleByNight.Api/openapi/SeattleByNight.Api.json`) is the
authoritative contract. In summary:

| Prefix | Purpose | Access |
| --- | --- | --- |
| `/health/live`, `/health/ready` | Liveness and PostgreSQL readiness | Anonymous |
| `/api/antiforgery/token` | Token for the `X-XSRF-TOKEN` header | Anonymous |
| `/api/account` | Register, login, logout, current user | Mixed |
| `/api/character-creation` | Catalog, drafts, change preview, finalize | Authenticated owner |
| `/api/characters` | List, delete, career sheet, advancements | Authenticated owner |
| `/api/play-session` | Start/resume, current room session, activity renewal | Authenticated |
| `/api/game/actions`, `/api/game/decisions` | List/perform game actions, answer decisions | Authenticated |
| `/api/game/missions` | The caller's missions | Authenticated |
| `/api/admin/users`, `/api/admin/audit` | Role management, audit log | `Administrator` |
| `/api/admin/world` | Rooms, exits, room deletion, NPC/interactable placement, room environment | `Administrator`, `WorldBuilder` |
| `/api/admin/content` | World Forge content lifecycle | `Administrator`, `WorldBuilder` |
| `/api/admin/missions` | List mission definitions, assign a mission to a character | `Administrator`, `WorldBuilder` |

Every state-changing request requires the antiforgery header. IDs the caller
does not own return `404`; stale optimistic-concurrency versions return `409`.

SignalR hub `/hubs/room-chat` (authenticated):

- Client → server: `JoinCurrentRoom`, `RecordActivity`, `SendMessage`,
  `RollDice`, `MoveThroughExit`, `GetOnlineCharacters`.
- Server → client: `MessageReceived`, `CombatUpdated`, `DecisionRequested`,
  `CharacterArrived`, `CharacterDeparted`, `RoomChanged`, `RoomPresenceChanged`,
  `SessionExpired`.

## Deployment

One application instance and one PostgreSQL database. No Redis, broker, or
replicas.

```text
Cloudflare -> Nginx Proxy Manager -> (Tailscale) -> frontend nginx :8080
                                                      |- /        static React build
                                                      |- /api/    backend:8080
                                                      '- /hubs/   backend:8080 (WebSockets)
                                                                     |
                                                                  postgres:5432
```

```bash
cp .env.production.example .env    # then fill in real values
docker compose -f compose.production.yaml up -d --build
```

Pushing to `main` runs CI (`.github/workflows/ci.yml`): backend tests, frontend
typecheck/lint/tests, generated-contract checks, then a deploy job on the
self-hosted `mush` runner that rebuilds the stack from
`/opt/seattlebynight/.env`, waits for the backend health check, and verifies the
path through nginx.

Operational notes:

- The API applies migrations at startup. Outside Development a migration failure
  stops the app.
- PostgreSQL is on an internal network and publishes no port. The frontend binds
  only to the host's Tailscale address.
- The data protection key ring lives in the `dataprotection_keys` volume. Losing
  it signs out every user.
- The rate limiter trusts `CF-Connecting-IP` (converted to `X-Forwarded-For` by
  nginx). That is only safe while the origin cannot be reached directly.
- `postgres_data` has **no backup automation**. Schedule an off-box `pg_dump`.

## Known limitations

- No email confirmation or password reset. An account with a lost password
  cannot be recovered without database access.
- No moderation tooling.
- All rooms are public.
- The SR5 catalog is a single mutable pre-alpha schema; a content change can
  invalidate existing drafts and sheets. See
  [`roadmap/SR5_RULESET_MANIFEST.md`](roadmap/SR5_RULESET_MANIFEST.md).
