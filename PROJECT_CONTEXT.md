# Seattle by Night — Project Context

Product and engineering context for anyone (human or agent) working in this
repository. Setup and operator instructions live in [`README.md`](README.md);
remaining work lives in [`roadmap/V1_ROADMAP.md`](roadmap/V1_ROADMAP.md).

Last reconciled against the code: 2026-11-12.

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
| Frontend | React 19, TypeScript (strict), Vite 8, React Router 7, `@microsoft/signalr`, Oxlint, Vitest. No component framework or global state library. API types are written by hand; `npm run gen:api` generates `src/api/generated/schema.d.ts` from the committed OpenAPI document, and the Forge's request types are checked against it at compile time (`forge/api.contract.ts`). The document declares request bodies only. |
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

- **Map** (`CityMap`, World Forge phase 2) — a city. Every room is on one;
  rooms built before maps existed were moved to Seattle
  (`CityMap.SeattleId`) by the `MapsAndRoomLifecycle` migration.
- **Room** — name, description, access type, its map, and coordinates
  `(x, y, layer)` unique within its map and layer. A room can be moved (WF-46,
  `WorldMapStore.MoveRoomsAsync`; a selection by an offset, optionally to
  another map; exits are kept as authored; audited). Every room is `Public`
  (workstream 8: one shared map, no run copies); the field stays for
  player-owned rooms.
- **Draft and live rooms** (WF-60). `Room.IsLive`; the Forge builds drafts,
  seeded and migrated rooms are live. A draft room, every exit and travel link
  into it, and what is in it are out of play: movement refuses them and the
  room view leaves them out. A job cannot go live while it uses a draft room
  (`JobMapValidator`). Unpublishing is refused while a job uses the room, for
  the starting room, and while someone inside has no exit to a live room
  outside the selection; otherwise they are moved through such an exit.
- **District** (WF-73, World Forge phase 3) — a named group of rooms on one
  map, on any layers. A room's description may be blank; the room view shows
  the district's `DefaultDescription` instead, and publishing refuses a room
  with neither.
- **TravelLink** (WF-70) — a named way from a room to a room on any map.
  Instant for now (phase 8 gives travel a time); taken through the same hub
  call as an exit (`MovementStore` falls back to a link id), with `/travel`.
- **RoomExit** — a directed edge with one of ten directions (eight compass
  points, `up`, `down`) and a `states` array (`ExitStates`: `locked`, `hidden`,
  `restricted`, `private`; empty is an open door, workstream 8). Hidden,
  locked and restricted refuse entry; restricted sits on the doors into a room,
  so leaving is never refused; private refuses entry until player homes exist.
  A room with a fight in progress counts as restricted on every door into it,
  read off the in-memory combat tracker and never stored (workstream 8 phase 4).
  Door state is shared world state. At most one exit per direction per source
  room. Exits have no names.
- Coordinates place a room in the editor and seed default adjacency only.
  Creating a room creates both directed exits to each occupied same-layer
  neighbor on its map whose way back is free, or only to the neighbours named
  in `Joins` (WF-43; refused whole if one of those is taken); after that, persisted exits are authoritative and may diverge from
  the grid. Vertical exits are always explicit. A manual reverse exit is a
  separate create.
- Movement names an exit from the character's current room, never a destination
  room ID.
- A room can be deleted only through a referential check (exits, the jobs
  that use it, occupants with a relocation target); its chat and visit history
  go with it in the same transaction and the counts are audited. A single exit
  can always be deleted.
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
- **Command queue.** Actions run strictly one at a time per room with
  idempotent request IDs. No reentrancy: anything an
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
- **Time.** A room is `Freeform` until combat starts, then
  `StructuredTime`, driven by a background service that runs NPC turns and
  enforces player turn timers.
- **Combat scope.** Initiative and passes, the SR5 action economy, ranged
  (single shot, semi-auto, burst) and basic melee/unarmed attacks, reload, cover,
  Full Defense, delay, soak, condition monitors, and deterministic NPC behavior.
  A defeated player character is knocked out, not killed, and stays where
  they fell with damage persisting; going down does not fail a job. Recoil and environmental
  modifiers are simplified; both simplifications are recorded in the rule
  decisions register.
- **One shared world (workstream 8).** There are no run copies: every runner
  on a job walks the same map rooms, meets the same NPC rows and competes for
  the same item rows. A job resets between runs (phase 3).
- **Every authored record is keyed by a GUID (WF-27).** Rooms, jobs, objectives, listeners, conversations (and their steps and choices),
  stat blocks, authored tests, NPC placements, items and inspectables are all
  referenced by id; names are display text only. Engine vocabulary (skills,
  pools, effect and event kinds, actions, built-in tests) keeps its names, so
  a test reference is a built-in name or an authored test's GUID. The shipped
  bundle's ids are in `ShippedContentIds`.
- **A job names map rooms (workstream 8).** A mission lists the `roomIds` it
  happens in, the world `npcIds` it uses (its reset brings them back), and
  its own `items`. Each job item is one `WorldItemInstance` row keyed by
  `ItemId`, carrying the `MissionId`; `IJobItemStore.SyncAsync` makes and
  renames the rows after every publish, retire and delete and at start-up,
  but never moves one — where an item is, is game state. Handing an item in
  puts it out of play (no room, no owner) rather than deleting it, and a
  `giveItem` never takes an item from someone holding it. No item belongs to
  two jobs. The publish gate checks jobs and room events against the map
  (`JobMapValidator`: rooms, NPCs and inspectables on the map, a listener's
  subject and speakers in its room, an alert's NPC anywhere on the map).
  Objectives are `reachRoom` (completes on the arrival room's queue, before
  the room's own events), `pickUpItem` and `deliverItem`.
- **Events belong to rooms (WF-20, WF-26).** Every event happens in a room.
  A room's own reactions are a `Place` definition keyed by the room's id; they
  fire every time. A job's listeners live on the mission, each naming any map
  room; only one job may listen to a given event
  (room + kind + subject), and they fire once per run. A retired job holds no
  events: another job may take one over, and the retired job's listeners
  still reach runs already under way (the loader is given the store's retired
  ids). Publishing the retired job again is refused if its event has been
  taken. The room reacts first,
  then the job. Narration goes to the player who caused it, and a job's lines
  reach only its runner while the room sees a cover line (`othersSee`).
  A Johnson offers the job in whatever room they stand; no room is asked
  for. A Johnson is either a world NPC given the pitch by name (`SceneId`),
  or anyone built on the pitch's stat block who holds no conversation of
  their own (`FindSceneForNpc`); the Forge counts the stat-block route only
  when the pitch is that block's one conversation. Johnsons are placed
  (`WorldNpcPlacement`), and may be bound to a conversation that is still a
  draft: they are placed while the job is written and have nothing to say
  until it is live. A world NPC can be moved between rooms as the same
  row (`WorldNpcPlacement.MoveAsync`), except mid-conversation, during a fight
  in their room, or while a live listener names them. Room events beyond entry (WF-21): `sneakAttempted`
  (with outcome), `combatStarted`, `lockedExitTried`, `playerLeftRoom`,
  `interactableInspected`.
- **Deleting (WF-27).** Deleting a room deletes its NPCs, items, inspectables,
  its own events and its exits both ways; it is refused while any job uses the
  room (happens in it, listens in it, puts an item in it, sends runners to it,
  or uses an NPC standing in it). A world NPC a listener names cannot be
  removed. Refusals name what still points at the record.
- **Hidden items (WF-27).** Items, like inspectables, can be hidden with a
  search difficulty; observe-area reveals them per character
  (`DiscoverySubjectType.Item`), and an undiscovered item cannot be taken.
- **Content writes are serialized (WF-22).** Publish, delete and draft saves
  run under one advisory lock (`IGameContentStore.RunExclusiveAsync`).
- **A job goes live, and leaves, as a set (WF-28).** A job names the
  conversations its events open, and its Johnson's conversation names the
  job, so those definitions cannot pass the publish gate — or the delete gate
  — one at a time. `GameContentPublisher.PublishSetAsync` and
  `GameContentLifecycle.DeleteSetAsync` check the set as one candidate corpus
  and promote or delete all of it, or none. Deleting a job from the Forge also
  deletes the prompts its events open that nothing else uses (the author can
  keep any), and removes its Johnson or leaves them standing with no
  conversation.
- **Rewards.** Karma and nuyen are granted through the career ledger, keyed by
  mission instance, exactly once.
- **The job clock (workstream 8 phase 3).** One `JobState` row per job
  (`Available` → `Open` → `Aftermath` → `Available`), taken under its version
  by accept (`MissionStore.AssignAsync`), turn-in (`StateChangeApplier`) and
  the sweep (`JobClockSweeper`, run every 30 s by `JobClockService`), so
  runners racing one job from different rooms are decided by the database;
  the loser gets `JobRaceLostException`'s sentence and nothing commits. The
  first accept sets the deadline (`deadlineHours`, default 3); turn-in fails
  every other accept group's run; the deadline fails every run and resets at
  once; a completed job resets 24 hours later (item back to its room,
  discoveries of its hidden items forgotten, its NPCs due back). Defeated
  NPCs come back when due, put off an hour while their room has a fight
  (always) or an online character (twice at most). The room view's
  `aftermath` is the stage for the hours since the most recent completion of
  a job that uses the room. Lines to other runners go out after the commit
  through `IJobNotices`.
- **Crews (workstream 8 phase 5).** `Character.CrewId`, up to five members,
  invitations to someone in the same room (`CrewStore`, `/api/game/crew`).
  A member's accept gives every member their own acceptance in one accept
  group (`MissionInstance.AcceptGroupId`, `CrewId`); objectives done by one
  are done for the group; turn-in pays the full Karma to each member still in
  the crew and splits the nuyen. `give-item` hands a carried item to anyone in
  the room.
- The action and test catalogs are still partly code-defined
  (`DevelopmentGameActions`, `DevelopmentGameTests`); `/surge` is a development
  effect.

`/roll` is separate from the engine: a free-form `NdS±M` roller behind
`IDiceEngine` that persists one server-rendered `Roll` chat message.

## Game content and World Forge

- Content kinds: `Mission`, `Scene`, `Test`, `NpcTemplate`, `Place`. Rooms,
  exits, world NPCs and inspectables are map rows edited in Places; a job's
  items are authored on the job. In the Forge a job is built in steps:
  Johnson, Pitch, Rooms, Events, Objectives · Pay, Go live.
- **A reply can roll.** It names a `Test` (a Library dice test or a built-in),
  or builds its own `roll` (`ChoiceRoll`): the runner adds up one or two
  attributes or skills, read off their sheet when they pick it; contested, the
  NPC being talked to adds up one or two of their stat block's pools, Body or
  Willpower (`NpcActor.GetOpposingPool(terms)`); uncontested, it needs a
  threshold. A built roll becomes the same `SkillTestDefinition` a Library
  test loads into, so modifiers, limits and Edge work alike. Contested needs a
  conversation bound to a stat block. Event reactions still roll Library tests.
- **A reply can answer (WF-74).** `answer` on a reply, or on each branch of
  its roll, is said by the NPC (or narrated) before the next part, so most
  replies need no part of their own. A part may carry a `name` for the Forge.
  Conversations live in the Library and can be given to any world NPC built on
  the stat block they are written for (WF-63).
- **Character facts (WF-80–83).** A `Fact` is a Library content kind; a
  character's facts live in `character_facts`, written only by the state
  change applier in the same commit as their cause. A fact with `runOf`
  records the run it was set in and counts only while that run is open. Jobs
  can require or refuse facts before they are offered and set or clear them
  when done; going live refuses anything that tests a fact nothing sets.
- **Things in rooms (WF-84–87).** Exits, NPCs and interactables share one
  hidden / locked model: hidden things are found per character by a search
  scoring their difficulty; locked exits and interactables open for whoever
  carries their key or a runner an `unlockExit` effect opened them for (an
  unlock is per runner, never for everyone). Interactables are inspect, read,
  search (containers) or use (raises `interactableUsed`). Map items
  (`map_items`) are unique or come back after a set time; a deleted
  character's carried items drop in their last room.
- **Objectives (WF-66, WF-88, WF-89).** Besides reach / get / deliver, a goal
  can be talk to, observe, be there for one of the job's events, or plant an
  item; these complete in the trigger engine when the event happens. Optional
  goals are open from the start and pay a bonus. A job can fail when a runner
  is seen, an alarm is raised or a named NPC is defeated, and can end in named
  ways with their own pay and facts, chosen by the hand-over reply.
- **Draft saves are version-checked (WF-61).** A save that sends
  `expectedUpdatedAtUtc` (or `expectNew`) is refused with 409 when the stored
  draft has moved on; the Forge autosaves this way.
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

1. **Content resolves live.** Nothing is copied into a run except its
   objective list. Triggers, scenes, NPC stat blocks and mission definitions
   resolve live, so publishing an edit reaches runs already in flight.
2. **Retired content stays in the served document** so running instances keep
   resolving it. The `IsRetired` flag stops it being offered to anyone new.
3. **`alertNpc` reaches anywhere on the map; `pacifyNpc` and `startCombat`
   are room-scoped.** The publish gate enforces the split.
4. **Pay is negotiable when a scene offers a `setNegotiatedPay` choice**, not by
   a mission flag.

Nothing an admin can click may break a character's ledger, receipts, or audit
history. Retiring removes content from play; hard delete is only for drafts and
content with no historical references.

## Frontend

- Routes: `/login`, `/characters`, `/characters/create`,
  `/characters/create/:characterId`, `/characters/:characterId/sheet`, `/play`,
  `/admin/users`, `/admin/audit`, `/admin/forge` (Places, Jobs, Library;
  `/admin/world` redirects to Places). A job is edited at
  `/admin/forge/jobs/:id/:step`. Guards redirect by authentication, active
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
