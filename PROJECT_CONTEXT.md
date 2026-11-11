# Seattle by Night — Project Context

Product and engineering context for anyone (human or agent) working in this
repository. Setup and operator instructions live in [`README.md`](README.md);
remaining work lives in [`roadmap/V1_ROADMAP.md`](roadmap/V1_ROADMAP.md).

Last reconciled against the code: 2026-11-10.

## Product

Seattle by Night is a browser-based MUSH for a Shadowrun Fifth Edition game set
in Seattle.

- Players connect through a web client and act through characters.
- Characters occupy persistent rooms in a shared world. Rooms form a directed
  graph joined by exits.
- Chat, presence, arrivals, and departures are scoped to the current room.
- Characters are full SR5 characters. Their real sheet drives automated tests,
  combat, and missions resolved by the server.
- Staff author rooms, NPCs, missions, dialogue, and triggers from inside the
  running game. No deployment is needed to add content.

Do not implement Shadowrun rules from assumptions. Every implemented rule and
catalog entry cites an approved source PDF and page. An unclear rule is recorded
as a decision in `roadmap/SR5_RULE_DECISIONS.md` and confirmed by the project
owner before it is built.

## Engineering priorities

A medium-scale personal project maintained mainly by one developer. Use
industry-standard practice where it buys correctness, security, or
maintainability; avoid infrastructure whose operating cost is not justified.

- Modular monolith. One deployment, one PostgreSQL database.
- Explicit module boundaries and dependency direction.
- Vertical slices that work end to end.
- Server-authoritative state and authorization. The client sends intent only.
- Generalize on the second consumer of an abstraction, not the first.

Do not introduce Redis, a message broker, Kubernetes, microservices, multiple
replicas, or a load balancer without a measured need and an approved
architectural change.

## Technology

| Layer | Stack |
| --- | --- |
| Backend | C# / ASP.NET Core on .NET 10, EF Core 10 + Npgsql, PostgreSQL 17, ASP.NET Core Identity, MediatR, SignalR, OpenAPI |
| Frontend | React 19, TypeScript (strict), Vite 8, React Router 7, `@microsoft/signalr`, Oxlint, Vitest. No component framework or global state library. API types are generated from the committed OpenAPI document (`npm run gen:api`). |
| Tests | xUnit; Testcontainers.PostgreSql for Infrastructure and Api tests (never EF's in-memory provider) |
| Delivery | Docker Compose; GitHub Actions CI with a self-hosted deploy runner |

Manifests and lockfiles are the source of truth for versions.

## Backend architecture

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application -> Domain
```

- **Domain** — persistence-ignorant entities and enums. References nothing.
- **Application** — use cases (MediatR commands/queries), authorization
  decisions, the SR5 catalog and evaluators, and the game engine. References
  Domain only. Declares the persistence interfaces it needs.
- **Infrastructure** — EF Core mappings, migrations, seeding, and the stores
  that implement Application's interfaces.
- **Api** — composition root: host configuration, minimal-API endpoints, the
  SignalR hub, background services, authorization policies.

Rules:

- Endpoints and the hub stay thin. Gameplay and authorization decisions belong
  in Application.
- No generic repository over EF Core. Add a persistence abstraction only where
  it is a real use-case boundary.
- All IDs are UUIDs. Timestamps are UTC `DateTimeOffset` / `timestamptz`.
- EF mappings are explicit, snake_case, indexed, and restrict cascading deletes.
- Persist a mutation before broadcasting it or reporting success.

Application is organized by feature: `CharacterCreation` (Catalog, Drafts,
Evaluation, Sheets), `CharacterCareer`, `Characters`, `PlaySessions`,
`RoomSessions`, `RoomChat`, `Movement`, `Dice`, `WorldEditing`, `RoleAdmin`,
`Auditing`, and `GameEngine` (Actions, Actors, Characters, Combat, Decisions,
Dice, Effects, Missions, Modifiers, Npcs, Resolution, Rooms, Runtime, Scenes,
StateChanges, Tests).

## World model

- **Room** — name, description, access type, and required immutable coordinates
  `(x, y, layer)` unique within a layer. `RoomAccessType` contains only `Public`.
- **RoomExit** — a directed edge with one of ten directions (eight compass
  points, `up`, `down`) and hidden/locked flags. At most one exit per direction
  per source room. Exits have no names.
- Coordinates place a room in the editor and seed default adjacency only.
  Creating a room creates both directed exits to each occupied same-layer
  neighbor; after that, persisted exits are authoritative and may diverge from
  the grid. Vertical exits are always explicit. A manual reverse exit is a
  separate create.
- Movement names an exit from the character's current room, never a destination
  room ID.
- A public room can be deleted only through a referential check (exits, mission
  links, occupants with a relocation target); its chat and visit history go with
  it in the same transaction and the counts are audited. There is no exit
  deletion.
- Rooms and exits carry opaque version tokens; a stale update returns `409`.
- The `New Character Room` is required world data created by migration.
  Development seeding adds sample rooms, `devuser`, and `Dev Runner`.

## Accounts, sessions, and chat

- ASP.NET Core Identity with email, username, and password. Cookie
  authentication (HTTP-only, same-site, `Secure` outside local HTTP), login rate
  limiting, lockout, generic login errors, and antiforgery on every
  state-changing request.
- A user owns at most two characters, counting drafts and finalized characters.
  Names are globally unique after normalization.
- One active **play session** and selected character per user. Reload,
  reconnect, or an app restart does not start a new session. Logout or idle
  expiry (60 minutes by default, configurable) ends it.
- Only meaningful user activity renews the lease. SignalR heartbeats do not.
- **Transcript visibility** derives from `RoomVisit` intervals: a player can
  read a message only if it was sent in a room while their current play session
  was there. A new play session starts with an empty transcript. There is no
  per-recipient message copy.
- Chat messages are typed (`Say`, `Emote`, `Roll`) and persisted before
  broadcast. Local command output is never stored.
- Gameplay mutations serialize on the play-session row (`SELECT … FOR UPDATE`);
  the operation timestamp is taken after the lock.

## Realtime

- One authenticated SignalR connection per browser session; groups are named by
  room ID.
- Group membership is never an authorization boundary. Every hub call
  revalidates the connection against the authoritative active session.
- Presence is an in-memory projection of joined connections, deduplicated by
  character and versioned by a monotonic revision. It is lost on restart by
  design and never changes durable occupancy.
- Group membership changes only after the durable move commits.
- A single backend instance is assumed. Add a SignalR backplane and distributed
  presence before running more than one.

## Roles, policies, and auditing

Roles: `Administrator`, `WorldBuilder`, `Moderator`. Policies are named, defined
in the Api composition root, and applied at endpoint boundaries:

| Policy | Roles |
| --- | --- |
| `RoleManagement`, `AuditLogReading` | `Administrator` |
| `WorldEditing` (world editor and World Forge) | `Administrator`, `WorldBuilder` |
| `ModerationAccess` | `Administrator`, `Moderator` — defined, not yet used |

- Every administrative mutation writes an append-only `AuditRecord` in the same
  transaction. Audit records are never updated or deleted.
- The last `Administrator` cannot be removed. Role changes serialize on an
  advisory lock and rotate the target's security stamp.
- No default administrative credential exists outside Development.

## SR5 character creation

- Methods: Standard Priority and Sum-to-Ten.
- The catalog is embedded JSON, one file per collection, under
  `Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0/`. It currently
  carries content from eight books: `sr5-core`, `run-faster`, `run-gun`,
  `rigger-5`, `street-grimoire`, `hard-targets`, `gun-heaven-3`, and
  `better-than-bad`.
- **The catalog is in a pre-alpha mutable phase.** There is one version
  (`1.0.0`), content changes are written straight into it, and digest/version-pin
  enforcement is deliberately disabled. Existing drafts and sheets are considered
  discardable until the schema is locked. The lifecycle is defined in
  `roadmap/SR5_RULESET_MANIFEST.md`.
- A draft is a typed JSONB document with optimistic concurrency. The server
  re-evaluates the whole draft on every save and returns field-level
  diagnostics.
- Navigation is non-linear. An upstream change never silently deletes or
  normalizes downstream selections; affected steps are flagged and finalization
  is blocked until the whole draft is valid.
- Finalization is atomic. It writes an immutable evaluated `CharacterSheet`,
  rolls starting cash once server-side, and seeds the career state.
- Most quality and martial-arts technique effects are catalog display text, not
  code-enforced mechanics.
- Excluded creation content (Point Buy, Life Modules, metasapients,
  shapeshifters, SURGE, and the per-book exclusions) is listed in the manifest
  and catalog ledgers.

## Career sheet

- The finalized `CharacterSheet` is never rewritten. Current values are composed
  on read from that baseline plus a mutable `CharacterCareerState` (Karma, nuyen,
  lifetime Karma, a typed progression document).
- `CharacterResourceTransaction` and `CharacterAdvancement` are append-only.
  Corrections are compensating records.
- Every career mutation is version-checked and idempotent by a client-generated
  request ID (`CharacterActionReceipt`).
- The server computes every cost, maximum, and eligibility. The composed sheet
  returns `nextActions` with exact costs and blocking reasons; the frontend never
  calculates them.
- Derived statistics are recomputed on read, never stored.
- Product interpretations: advancement is immediate, ignores training time and
  downtime, and needs no Storyteller approval.
- Implemented: attributes, Edge, Magic, Resonance, active skills, skill groups
  (including break and rebuild), knowledge skills, languages, specializations.
  Not implemented: qualities, spells/forms, Initiation/Submersion, nuyen
  purchases. The rules contract for those is
  `roadmap/SHEET_901_CAREER_RULES_BASELINE.md`.

## Game engine

The target design is `roadmap/GAME_ENGINE_FULL_ARCHITECTURE_PLAN.md`. What is
built follows it; these are the load-bearing rules.

- **Rules adapter.** The engine reads characters through a read-only adapter
  over the composed sheet. It never parses sheet JSON directly and never writes
  to the sheet.
- **Store facts, derive consequences.** Runtime state stores damage, current
  Edge, and active effects. Wound modifiers, limits, and effective attributes are
  derived.
- **One actor abstraction.** Players and NPCs are both `IActor`. The engine
  never branches on which one it has.
- **Pure, seeded resolution.** Test composition, the modifier engine, and the
  dice roller are pure with an injected roller. Every resolution records its
  seed and a full pool breakdown; a pool never reaches the client as an
  unexplained number. Every test is audited.
- **Command queue.** Actions run strictly one at a time per scope (room or
  encounter instance) with idempotent request IDs. No reentrancy: anything an
  action causes is enqueued as a new command.
- **Decisions.** A resolution can pause on a `DecisionRequest` (Edge, defense).
  Every request has a default and a timeout, so an absent player can never
  freeze anything.
- **State changes and events.** Actions produce State Changes applied
  atomically. Events have three consumers: synchronous consequences in the same
  commit, enqueued reactions, and post-commit notifications over SignalR. An
  *Active Effect* is an ongoing condition; a *State Change* is a one-shot
  mutation.
- **Affordances.** The server computes each viewer's available actions. Hidden
  interactables stay invisible until discovered.
- **Time.** Encounters are `Freeform` until combat starts, then
  `StructuredTime`, driven by a background service that runs NPC turns and
  enforces player turn timers.
- **Combat scope.** Initiative and passes, the SR5 action economy, ranged
  (single shot, semi-auto, burst) and basic melee/unarmed attacks, reload, cover,
  Full Defense, delay, soak, condition monitors, and deterministic NPC behavior.
  A defeated player character is knocked out, not killed, and wakes at the
  encounter entry point with damage persisting. Recoil and environmental
  modifiers are simplified; both simplifications are recorded in the rule
  decisions register.
- **Instances.** A mission runs in a private encounter instance that
  materializes its own room, item, and NPC rows. Instances survive disconnects
  within a grace window (15 minutes by default) and are swept when abandoned.
  One player per instance today; the model holds a participant collection.
- **Every authored record is keyed by a GUID (WF-27).** Rooms, job sites,
  jobs, objectives, listeners, conversations (and their steps and choices),
  stat blocks, authored tests, NPC placements, items and inspectables are all
  referenced by id; names are display text only. Engine vocabulary (skills,
  pools, effect and event kinds, actions, built-in tests) keeps its names, so
  a test reference is a built-in name or an authored test's GUID. The shipped
  bundle's ids are in `ShippedContentIds`.
- **Job sites are map rooms (WF-25).** Every map room has an access type:
  `Public`, or `Private` (part of a job site). An encounter names its site
  rooms by map room id; a run's copy is made from the map rows (name,
  description, environment, position, and the exits between them with
  hidden/locked flags) and each copy records its `SourceRoomId` — as copied
  NPCs and inspectables record `SourceNpcId` / `SourceInteractableId` — so
  events in a copy carry the original's id. Nobody stands in a `Private` map
  room: walking into one enters the runner's run copy through the
  `mission-enter` action (`IRunAccessPolicy` decides who may), and everyone
  else finds the door locked. The publish gate checks sites against the map
  (`JobSiteMapValidator`: rooms on the map, Private, reachable from the entry).
- **Events belong to rooms (WF-20, WF-26).** Every event happens in a room.
  A room's own reactions are a `Place` definition keyed by the room's id; they
  fire every time. A job's listeners live on the mission, each naming a room
  (its site, or a public room); only one job may listen to a given event
  (room + kind + subject), and they fire once per run. The room reacts first,
  then the job. In a public room, narration goes to the player who caused it
  and a job's lines reach only its runner while the room sees a cover line
  (`othersSee`). Johnsons are world NPCs placed in Public rooms
  (`WorldNpcPlacement`). Room events beyond entry (WF-21): `sneakAttempted`
  (with outcome), `combatStarted`, `lockedExitTried`, `playerLeftRoom`.
- **Deleting (WF-27).** Deleting a room deletes its NPCs, items, inspectables,
  its own events and its exits both ways; it is refused while a job listens to
  the room or builds a site from it. A world NPC a listener names cannot be
  removed. Refusals name what still points at the record.
- **Hidden items (WF-27).** Items, like inspectables, can be hidden with a
  search difficulty; observe-area reveals them per character
  (`DiscoverySubjectType.Item`), and an undiscovered item cannot be taken.
- **Content writes are serialized (WF-22).** Publish, delete and draft saves
  run under one advisory lock (`IGameContentStore.RunExclusiveAsync`).
- **Rewards.** Karma and nuyen are granted through the career ledger, keyed by
  mission instance, exactly once.
- The action and test catalogs are still partly code-defined
  (`DevelopmentGameActions`, `DevelopmentGameTests`); `/surge` is a development
  effect.

`/roll` is separate from the engine: a free-form `NdS±M` roller behind
`IDiceEngine` that persists one server-rendered `Roll` chat message.

## Game content and World Forge

- Content kinds: `Encounter`, `Mission`, `Scene`, `Test`, `NpcTemplate`. Mission
  items, rooms, exits, interactables, NPC placements, and encounter triggers are
  authored inside an encounter fragment.
- **Compose, don't script.** Event, effect, condition, and objective *kinds* are
  code. Everything built from them is content. A new kind is a small additive
  engine change with tests; there is no scripting language.
- Lifecycle: draft → validate → publish → retire. One validation path
  (`GameContentLoader`) serves as loader, publish gate, and delete gate. Drafts
  are invisible to players.
- The embedded `game-content-1.0.0` bundle (the gang-warehouse mission) is
  imported once as the first published set. Content is served from the database.
- A **scene** is a node graph of text and numbered choices with conditions,
  optional tests, and effects. Bound to an NPC it is dialogue; unbound it is a
  prompt a trigger opens.
- A **trigger** is `(event, conditions) → reactions`, fire-once or repeatable.
- NPCs are two-layer: a base template (stat block) plus a placement that stores
  only its overrides.

Deliberate decisions that look like bugs but are not:

1. **Only materialized content is isolated from edits.** Rooms, exits, items,
   NPC placements, and objectives are copied into an instance. Triggers, scenes,
   NPC base stat blocks, and mission definitions resolve live, so publishing an
   edit reaches runs already in flight.
2. **Retired content stays in the served document** so running instances keep
   resolving it. The `IsRetired` flag stops it being offered to anyone new.
   Retiring an encounter retires its missions.
3. **`alertNpc` resolves encounter-wide; `pacifyNpc` and `startCombat` are
   room-scoped.** The publish gate enforces the split.
4. **The return room is not authored.** The engine records where the runner
   entered from.
5. **Pay is negotiable when a scene offers a `setNegotiatedPay` choice**, not by
   a mission flag.

Nothing an admin can click may break a character's ledger, receipts, or audit
history. Retiring removes content from play; hard delete is only for drafts and
content with no historical references.

## Frontend

- Routes: `/login`, `/characters`, `/characters/create`,
  `/characters/create/:characterId`, `/characters/:characterId/sheet`, `/play`,
  `/admin/users`, `/admin/audit`, `/admin/forge` (Places, Jobs, Library;
  `/admin/world` redirects to Places). Guards redirect by authentication, active
  session, and role; the server remains authoritative.
- Creator and administration routes are separate production chunks.
- The command composer parses commands on the client. Local-only commands never
  reach the server; everything else submits intent.
- The realtime client rejoins before reporting a reconnect as ready and uses
  lifecycle generations to reject stale responses.
- Visual language is the project-owned "Deck OS" neon-noir system: semantic CSS
  tokens in `frontEnd/src/styles/tokens.css`, primitives in
  `frontEnd/src/components/ui`, self-hosted Rajdhani and Source Code Pro. See
  [`frontEnd/README.md`](frontEnd/README.md).
- The route sheet and the `/character` modal render the same component.

## Security and consistency rules

- Treat every client payload as untrusted, including command text, dice
  expressions, identifiers, and editor payloads.
- Authorize on the server from authenticated ownership. Room, character, and
  session IDs are never credentials.
- Never use SignalR group membership as proof of authorization.
- Do not expose connection strings, credentials, or internal exception details.
- Keep secrets in environment variables or deployment secret storage.
- No application cache exists. If one is justified, start with `IMemoryCache`
  for stable reference data; never cache presence or message history.

## Testing expectations

- Domain and Application tests run without external services. The pure rules
  core (evaluators, test resolution, modifiers) carries the heaviest coverage.
- Infrastructure and Api tests run against real PostgreSQL through
  Testcontainers.
- A headless, seeded playthrough of the warehouse mission runs in CI against
  database-served content.
- New HTTP, hub, and engine features need tests for authorization, isolation,
  idempotency, concurrency, and persist-before-broadcast.
- A change is complete only when its tests and the build/lint checks in
  `README.md` pass.

## Change discipline

- Preserve the dependency direction and keep transport concerns out of Domain
  and Application.
- Follow `AGENTS.md`: architecture changes are proposed in Plan mode, not made
  during Build mode.
- Update this file when an architectural decision, technology choice, or major
  capability changes. Update `README.md` for setup and operator changes. Update
  `roadmap/V1_ROADMAP.md` when work is finished or rescoped.
