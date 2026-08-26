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
- React Router 7 for client-side routing; no API client or global state library.
- `@microsoft/signalr` powers the realtime client.
- A project-owned retro-future neon-noir design system: semantic CSS custom
  properties in `frontEnd/src/styles/tokens.css`, accessible UI primitives in
  `frontEnd/src/components/ui`, and self-hosted Rajdhani and Source Code Pro
  (SIL OFL 1.1) via `@fontsource`.

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

- Persistent location containing a name, description, public access type, required immutable map coordinates, and creation timestamp.
- Coordinates are unique within a layer. A room is created directly in an empty editor grid cell and cannot be repositioned.
- `RoomAccessType` currently contains only `Public`.
- Ownership and non-public access rules are deferred.

### RoomExit

- Directed edge from `SourceRoomId` to `DestinationRoomId`.
- Contains one of the approved directions plus hidden and locked flags; exits have no separate names.
- Approved directions are `north`, `northeast`, `east`, `southeast`, `south`, `southwest`, `west`, `northwest`, `up`, and `down`.
- A source room has at most one exit per direction.
- Creating a room generates separate forward and reverse exits for occupied same-layer cells in its eight-cell neighborhood. Other reverse paths require a separate `RoomExit`.
- Movement must be requested through an exit from the character's current room, not by accepting an arbitrary destination room ID.

### Character

- Player-facing identity in the game world.
- Has a durable `CurrentRoomId`.
- Has a required owner user ID and an explicit `Draft` or `Finalized` lifecycle.
- A user may own at most two characters, counting both incomplete creation drafts
  and finalized playable characters.
- Character names are globally unique after normalization; drafts reserve their
  names until discarded or finalized.
- Every newly created character starts in the configured `New Character Room`.
- Drafts are not playable and are excluded from character selection, room
  occupants, play sessions, movement, chat, and presence.
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
- Required coordinates determine editor placement and seed default adjacency only. Persisted exits remain authoritative after room creation.
- One-way exits, loops, branches, hidden paths, and locked paths must remain possible.
- Rooms cannot move. Manual exit editing may diverge from visual adjacency, so the map is not the ongoing source of truth for connectivity.

An eventual movement operation should validate the character's current room, the selected exit, destination access, and any exit restrictions before updating `CurrentRoomId`.

## Realtime Model

SignalR is implemented. The design is:

- One authenticated SignalR connection per browser session, not one connection per room.
- SignalR groups named by room ID route room-scoped events.
- Joining a SignalR group is not an authorization boundary; the server must validate every join, movement, and send operation.
- Character location in PostgreSQL is durable state.
- Active connections and online presence are ephemeral state.
- A SignalR disconnect immediately removes that connection from realtime delivery, but does not by itself end the durable play session.
- Moving removes every connection registered to the play session from its previous room group and adds it to the destination group only after the durable location update succeeds.
- Messages are persisted before they are broadcast.
- Clients deduplicate messages by server-generated message ID and rejoin the authoritative room after reconnecting.

Online presence is an in-memory projection of joined SignalR connections:

- A connection registration carries the play-session ID, character summary, and room ID.
- Presence is deduplicated by character ID, so multiple connections for one active play session show a single online character; the character goes offline only after the final connection leaves.
- `JoinCurrentRoom()` returns the authoritative `RoomPresence` snapshot and is idempotent: re-joining the same session/character/room is a no-op, and re-joining after the durable room changed self-heals by leaving the stale group before joining the authoritative one. The server revalidates the session after registration and rolls back stale membership if the session ended or changed concurrently.
- Movement, session replacement, logout, and expiration reconcile every connection registered to the affected play session. Hub mutations revalidate the connection registration against the authoritative active session rather than trusting connection-local state or group membership.
- `RoomPresenceChanged(presence)` is broadcast to a room only when its distinct online-character set changes, and carries a monotonic in-process `revision` that clients use to reject stale snapshots.
- Presence is lost on application restart by design and never changes durable occupancy.

The initial deployment has one backend instance, so SignalR's in-process connection and group state is sufficient. Before adding multiple backend replicas, introduce an appropriate SignalR backplane and distributed presence strategy.

## Typed Messages And Dice

Room communication carries an explicit `ChatMessageType` persisted with every
`ChatMessage`. The approved types are `Say`, `Emote`, and `Roll`. Only room
communication is persisted: speech, emotes, and the server-rendered result of a
public roll. `/help`, `/look`, `/who`, parser output, and usage errors remain
local-only, and `/go` persists movement rather than command text. Movement
arrivals and departures remain ephemeral presence events, not durable typed
messages.

Dice rolls are server-authoritative. The client submits only a dice expression
(`/roll <expression>`), never an outcome. The server parses and enforces the
approved grammar, generates the result with an unbiased system random source,
and persists one canonical server-rendered `Roll` chat message containing the
normalized expression and result. There is no separate `DiceRoll` entity or
structured roll-result columns; the durable chat entry is the room transcript
record for the roll. Future mechanics that need rolls as gameplay inputs must
introduce a separately approved structured model.

Approved dice grammar and limits:

- Expression form: `NdS` with an optional signed integer modifier, e.g. `2d6+3`.
  The count must be explicit; `d6` is not accepted (write `1d6`).
- Maximum 100 dice and maximum 1,000 sides.
- No exploding dice, pools, limits, glitches, or opposed tests.
- The default application-owned limits are configurable through the `Dice`
  options section (`MaxDice`, `MaxSides`, `MaxExpressionLength`,
  `MaxModifierMagnitude`).

Dice library decision: no third-party package is used. Evaluated candidates
(`DiceRoller`, `D20Tek.DiceNotation.Standard`, `RoguelikeToolkit.Dice`, and the
dormant `EdCanHack.DiceNotation`/`DiceNotation.CoreClass`) all target older
.NET Standard, add expression complexity (keep/drop/reroll) far beyond the
approved `NdS ± modifier` grammar, and still require adapter code for
server-controlled randomness and deterministic tests. A minimal internal parser
is implemented behind the `IDiceEngine` application boundary so the grammar and
limits stay explicit and a package can be swapped in later without changing
transport or domain code.

## Shadowrun Character Creation

Initial Shadowrun Fifth Edition character creation uses only the two approved
local PDFs at the repository root: the SR5 core rulebook and Run Faster. It
supports Standard Priority and the Sum-to-Ten method from Run Faster. Run Faster
is used for Sum-to-Ten allocation, its priority-grant clarification that
magician and mystic-adept spell grants may be selected as spells, rituals, and/or
alchemical preparations, and (CHAR-813, approved by the project owner
2026-08-26) its 17 metavariants of the five core metatypes. A metavariant is a
parameterized sub-choice of picking its parent metatype at a Standard Priority
or Sum-to-Ten Metatype level: it replaces the parent's natural attribute
ranges and racial-trait text outright and adds a flat Karma surcharge from
Run Faster's Extended Priority Charts. See
`roadmap/sr5-catalog/RUN_FASTER_METATYPES.md` for the full citation ledger.
Run Faster's qualities are also in scope (CHAR-814, approved by the project
owner 2026-08-26): the single "Rank" quality and the full "Qualities for
Good or Ill" chapter, 84 new catalog entries published as `sr5-core` catalog
version `1.3.0` (an overlay on `1.0.0` republishing every earlier overlay's
additive content) alongside the 60 `sr5-core`/CHAR-807 qualities and the
`poor-self-control-vindictive` quality CHAR-813 already added. None of these
qualities needed bespoke evaluator logic; like most `sr5-core` qualities,
their mechanical prose is documentation only (recorded in
`roadmap/sr5-catalog/RUN_FASTER_QUALITIES.md`), not code-enforced, except for
the one new bidirectional conflict between `erased` and `records-on-file`.
Run Faster's Point Buy and Life Modules creation methods, its metasapients
(Centaur, Naga, Pixie, Sasquatch), its shapeshifters, and its Changelings/
SURGE system remain excluded; all other selectable character options remain
core-rulebook-only. Do not use external rules summaries, implementations,
catalogs, errata documents, or other books as rules or completeness references
unless the project owner explicitly adds them to the approved source set.
Seattle by Night must implement its own typed, server-authoritative rules and
validation behavior within the existing modular-monolith boundaries. Every
implemented rule and catalog option must cite the approved PDF and page.
Unclear interpretations must still be recorded as product decisions and
confirmed before implementation.

Character-creation navigation is non-linear. A player may revisit and change
any earlier step at any time. Saving an upstream change must not silently delete
or normalize downstream selections. Instead, the server re-evaluates the full
typed draft against the new budgets, caps, prerequisites, and provenance, and
returns field-level diagnostics for every downstream value that is now invalid
(for example, skill points or nuyen overspent after lowering a priority grant).
The UI must preserve those selections, mark the affected future steps as
attention-required, and explain the amount or requirement that must be fixed.
Finalization is blocked until the complete draft is valid. This applies to all
budgeted or constrained sections, including attributes, skills, resources,
nuyen, special points, and future downstream systems.

## Transactional Gameplay Ordering

PostgreSQL serializes gameplay mutations by locking the active play-session row
(`SELECT ... FOR UPDATE`) before reading or changing its character room, expiry,
active room visit, or chat destination. The authoritative operation timestamp is
obtained after lock acquisition so persisted time follows serialization order.
Chat send, movement, activity renewal, explicit ending, session replacement, and
expiration use the same lock discipline:

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
- Explicit ending and session replacement return the affected session identity so
  the transport layer can reconcile its realtime connections after commit.

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

Email confirmation, password reset delivery, and moderation UI are later hardening milestones. Their absence must be documented before public deployment.

Player transcript queries should use cursor pagination over the current session's eligible messages. Do not copy one row per message recipient. Visibility is derived from room-visit intervals, which avoids the largest source of storage amplification.

All chat messages remain available to future authorized moderation queries independently of player transcript visibility. Administrative chat access must require a role, be auditable, and bypass room-visit filtering only through an explicit moderation use case. No moderation endpoint should be exposed before authentication and roles exist.

PostgreSQL text storage is expected to be sufficient at the project's initial scale. A million typical short messages is generally measured in hundreds of megabytes before indexes, not four gigabytes merely because the maximum content length is 4,000 characters. Monitor actual growth before adding complexity. If needed later, add a documented retention period, time-based partitions, and compressed archival exports; do not sacrifice moderation records or player privacy semantics for premature optimization.

## Roles, Policies, and Administrative Auditing

Authorization uses named roles and named policies rather than inline role-name
checks. The centralized role names in `ApplicationRoles` are `Administrator`,
`WorldBuilder`, and `Moderator`. ASP.NET Core authorization policies are defined
in the Api composition root and map to least-privilege role sets:

- `RoleManagement` — `Administrator` only.
- `WorldEditing` — `Administrator` or `WorldBuilder`.
- `ModerationAccess` — `Administrator` or `Moderator`.
- `AuditLogReading` — `Administrator` only.

Policies are applied at endpoint boundaries. New registrations receive no role.
Role definitions are created idempotently by database migration in every
environment, and the deterministic Development `devuser` is granted
`Administrator` there only. Production startup contains no default
administrative credential or silent elevation; the first administrator is
granted through a documented operator bootstrap procedure (see `README.md`).

Every administrative mutation appends an append-only `AuditRecord` in the same
database transaction as the mutation. Records contain the server-assigned UTC
timestamp, authenticated actor user ID, action, target type, target ID, and
bounded structured JSON details. There are no update or delete operations for
audit records; failed or rolled-back mutations leave no audit record. The audit
query is cursor-paginated (newest first, ID tie-breaker) with bounded filters
for actor, action, target type/ID, and UTC time range.

Role administration removes a role transactionally and refuses to remove the
last effective `Administrator`. Concurrent role mutations serialize on a
PostgreSQL advisory transaction lock so check-then-act remains atomic. Successful
role assignment or removal rotates the target user's security stamp; authentication
principals are validated on every request so stale role claims are rejected.

World editing is available to `Administrator` and `WorldBuilder`. Room and exit
creates and updates append audit records atomically. Both entities expose an
opaque UUID version token; updates use it as an EF concurrency token, rotate it
on success, and return a conflict without an audit success record when stale.
Coordinates are required, immutable, and unique within a layer. Creating a room
in an empty cell atomically creates both directed paths to every occupied
same-layer compass neighbor. A PostgreSQL advisory transaction lock serializes
topology creation so concurrent editors cannot miss adjacency. After creation,
persisted exits remain authoritative and can be manually rewired or assigned
hidden/locked state. Vertical `up` and `down` exits are always explicit.
There is no room or exit deletion use case. Recovery is performed through edits
or by locking/hiding an exit.

## Caching

No application cache is implemented. When caching is justified:

- Use `IMemoryCache` initially for stable room metadata or similarly read-heavy reference data.
- Do not use the database cache as the authority for live SignalR presence.
- Do not cache message history or rapidly changing occupant lists without evidence that it is necessary.
- Invalidate or update specific room entries when durable room metadata changes.
- Revisit distributed caching only before horizontal backend scaling.

## Database and Development Data

The current schema includes Identity, rooms, directed exits, characters,
character-creation drafts, immutable character sheets, typed chat messages, play
sessions, room visits, and audit records. The initial migration is
`InitialWorldSchema`; later migrations add ownership, sessions, authorization,
auditing, world-editing concurrency, topology constraints, transcript indexes,
character lifecycle, and additional data constraints.

Development startup applies migrations and attempts deterministic, idempotent seeding. Initialization failures are logged as warnings so the process can still expose liveness while PostgreSQL is unavailable.

The configured New Character Room is required operational world data and is
created by migration in every environment. Development seeding enriches that
baseline with sample rooms, exits, users, and characters but is not required for
character creation.

Seed data contains:

- Downtown Street.
- Coffee Shop.
- Alley.
- Downtown Street at `(0, 0, layer 0)`, Coffee Shop at `(1, 0, layer 0)`,
  Alley at `(0, 1, layer 0)`, and New Character Room at `(0, 0, layer -1)`.
- Direction-only east/west exits between Downtown and Coffee, north/south exits
  between Downtown and Alley, and down/up exits between Downtown and the New
  Character Room.
- Dev Runner located in Downtown Street.

The deterministic `New Character Room` is the configured starting location for
all newly created characters. Dev Runner remains in Downtown Street.

No credentials or chat messages are seeded.

## Current Implemented Surface

The Application layer includes the immutable SR5 catalog foundation for
`sr5-core`, pinned by semantic SHA-256 digests. All catalog
option facts — qualities, skills, skill groups, knowledge categories, creation
paths, aspected values, traditions, spells, rituals, adept powers, mentor
spirits, complex forms, spirit/sprite types, foci, and the per-priority skill
and Magic/Resonance grants — live in append-only pinned JSON resources, so the semantic
digest changes whenever any catalog fact changes. The loader validates unique
IDs, bounded display names, citations, and cross-references, plus all 25
priority cells, at startup. Its pure evaluator returns structured diagnostics and
canonical previews for Standard Priority and Sum-to-Ten assignments. The catalog
also contains the five core metatypes, natural attribute ranges, metatype
priority availability and special-point grants, normal attribute definitions,
and normal-attribute priority point grants. Draft documents can carry typed
metatype, normal-attribute, and special-attribute allocations; server evaluation
validates priority compatibility, point totals, natural maxima, and metatype
ranges. CHAR-807 adds typed immutable catalog surfaces for 31 positive and 28
  negative qualities, 75 active skills, 15 skill groups, and 4 open Knowledge
  categories, plus additive draft selections, bounded text validation, quality
  caps/conflicts, the Aptitude rating-7 cap, Bilingual's second native language,
  free Knowledge/Language points derived from natural Intuition and Logic,
  specialization prerequisites, skill/group budgets and overlap, and
  native-language `N` semantics. The creator UI now exposes Qualities, Active
  Skills & Groups, and Knowledge & Languages. The Knowledge step keeps subjects
  and languages open-authored while offering cited core-book examples as catalog
  prefills, including category and specialization suggestions; its dossier view
  shows linked attributes and live base/specialized dice pools. CHAR-808 adds typed immutable
  catalog surfaces for the six creation paths (Mundane, Magician, Mystic Adept,
  Adept, Aspected Magician, Technomancer), 3 aspected values, 2 traditions, 84
  spells, 9 rituals, 25 adept powers, 16 mentor spirits, 20 complex forms, 6
  spirit types, 5 sprite types, and 16 foci, plus per-priority path grants
  (attribute rating, skill/group grants, formula grants, complex-form grants).
  Server evaluation enforces path availability, Magic/Resonance mutual
  exclusivity, natural maxima (7 with Exceptional Attribute), tradition and
  aspect requirements, skill-grant count/domain/distinctness, formula and
  complex-form caps, the improved-reflexes irregular Power Point cost, mystic
  adept Power Point purchases at 2 Karma each, mentor-spirit prerequisites, and
  the shared 25 Karma creation pool. Priority-granted skill ratings are free and
  count toward the final natural-rating cap without consuming the skill budget.
  The creator UI now exposes the Awakening / Emergence step.

CHAR-809 (Resources and Essence) is substantially complete. The
`ResourcesEssenceEvaluator` generically resolves and prices any purchasable
catalog item — gear, weapons, armor, augmentations, vehicles, and cyberdecks —
against the priority-derived nuyen budget plus an optional Karma-to-nuyen
conversion (up to 10 Karma at 2,000 nuyen each), tracks cumulative Essence
loss against the starting 6 Essence, applies per-item availability and
creation-availability-12 limits, and resolves augmentation grade
cost/Essence/availability multipliers. The catalog contains 77 weapons and 11
armor entries (substantially reconciled against the core inventory), 91
augmentations across 5 grades (standard, alphaware, betaware, deltaware,
used), 114 general-gear/electronics/magical-supplies entries spanning
commlinks, electronics accessories, RFID tags, communications and
countermeasures, software and skillsofts, credsticks, tools, fixed-capacity
optical devices, security devices and restraints, breaking-and-entering gear,
industrial chemicals, survival gear, biotech, DocWagon contracts, slap
patches, and magical supplies (reagents and lodge materials), a new typed
`cyberdecks` catalog (9 named decks with device rating, attribute array, and
program slots), and 40 vehicles/drones covering the full core
groundcraft/watercraft/aircraft/drone tables. Capacity-scaled host devices
(optical/audio/sensor hosts and their vision/audio enhancements), spell
formulae's linkage to a specific known spell, and vehicle modifications
(rigger interface, weapon mounts) remain out of scope for CHAR-809 and are
deferred to CHAR-809A alongside armor/weapon attachments. The creator UI
exposes both the Augmentations & Essence and Resources & Vehicles steps
across all new categories. Street samurai, decker, rigger, and
magical-equipment golden builds all pass; a full CHAR-812-style line-by-line
reconciliation against the core PDF has not yet been run.

CHAR-809A (Gear Capacity, Mounts, And Attachments) is complete: firearm
mounts, armor Capacity, device Capacity, augmentation/cyberlimb Capacity, and
vehicle weapon mounts are all implemented. Draft resource
selections (`ResourceSelection`) carry a client-generated, stable per-line
`instanceId` so two purchased copies of the same host track their attachments
independently; a new typed `AttachmentSelection` (`hostInstanceId`,
`accessoryId`, optional `mount`/`rating`) is evaluated by a new, deliberately
independent `GearAttachmentEvaluator` that never shares code with
`ResourcesEssenceEvaluator` — it re-derives remaining nuyen budget from that
evaluator's canonical output, the same pattern `KarmaBudgetEvaluator` already
uses. It enforces firearm mount slots (17 cataloged accessories against a new
`weaponAccessories` catalog; top/barrel/underbarrel availability by weapon
category, `TopOrUnderbarrel` accessories requiring an explicit mount choice,
one accessory per mount, and per-attachment Availability-12/Rating-6 checks)
and armor Capacity pools (7 cataloged modifications against a new
`armorModifications` catalog; fixed or per-Rating Capacity cost against the
host's `ArmorRating`-derived Capacity). The canonical sheet gained
`CanonicalAttachment`/`CanonicalGearAttachments` records. The creator UI
renders attachments as sub-items nested under their host resource line; a
small square "+" control on a purchased host opens a modal (the shared `Modal`
primitive, closed only by its own control or an outside click) that shows the
host's mount slots or Capacity count at the top and a list of eligible,
not-yet-attached options below — selecting one adds it and the modal stays
open. The same evaluator and modal pattern also cover device Capacity
(optical/audio/sensor hosts bought at a chosen Rating-as-Capacity, e.g.
goggles, with vision/audio/sensor enhancements consuming it), augmentation/
cyberlimb Capacity (cybereyes/cyberears/cyberlimbs carry a Capacity pool;
bracketed-Capacity-cost bodyware/cyberguns install in a cyberlimb instead of
costing Essence; cyberlimb Agility/Armor/Strength enhancements are capped at
one per type per limb), and vehicle weapon mounts (`floor(Body / 3)` mount
slots; standard mounts cost 1 slot, heavy mounts cost 2 and are
creation-unavailable; Manual Operation requires an existing mount on the same
host instance). Cyberdeck program slots remain a separately tracked gap
outside CHAR-809A's written scope. `AttachmentSelection.Mount` is a plain
string, not a C# enum,
because the draft document round-trips through the API's default JSON options
(which have no enum-to-string converter — other domain enums like
`RoomAccessType` already serialize as raw integers); only catalog responses
use `CatalogJsonOptions`' `JsonStringEnumConverter`.

Contacts, identities, lifestyles, starting cash, remaining Karma, and final
review/finalization sections (CHAR-810 through CHAR-812) remain unavailable
until their milestones.

The persistence layer supports slot-bearing, name-reserving SR5 drafts with JSONB
typed selections, UUID optimistic concurrency, start/read/update/discard/finalize
application operations, and immutable evaluated sheets. Finalization now writes a
complete canonical evaluated sheet (sheet schema version 3) capturing the resolved
metatype, absolute attribute and special-attribute values, qualities, skills and
groups, knowledge and languages, native languages, and the Awakening/Emergence
selection — each retaining its allocation provenance (priority, special points,
group points, grant, Karma, free points, or native). Version 3 records attachment
Essence and explicit allocated, granted, and total skill-group ratings; it is the
only supported evaluated schema version — there is no `CharacterSheetKind` and no
legacy sheet concept (SHEET-902, 2026-08-25: no real characters existed yet, so
that support was removed rather than built out, and the dev/test seed character
was rebuilt as a real evaluated sheet). A `CharacterCreationBaselineReader`
normalizes a persisted sheet into a typed `CharacterCreationBaseline`, rejecting
an unsupported schema version, malformed JSON, a catalog digest mismatch, or a
sheet missing a mandatory section. Resource-budget evaluation always runs after priority
assignment, including for an empty purchase list, and consolidates direct and
attachment Essence before deriving Magic/Resonance loss, Social Limit, and final
Essence. Quality ratings are restricted to the supported single-selection model;
priority-granted skills and groups participate in canonical ratings, overlap checks,
and point/Karma accounting. Only finalized characters are playable. The
authenticated character-creation
  HTTP surface and the priority, metatype, attribute, quality, skill, knowledge,
  Awakening/Emergence, resources/essence (including gear Capacity, mounts, and
  attachments), contacts, lifestyle, and review/finalize creator UI are all
  implemented; every creator step is available. There is no dedicated Karma
  step — Knowledge/Language points beyond the free pool draw extra Karma
  directly (sr5-core p. 107 Karma Advancement Table) rather than being
  blocked, and the header's running Karma total already covers this. Creator
  autosave tracks whether diagnostics and derived statistics match the latest
  local edit generation, disables finalization while evaluation is stale, and
  reports explicit success/failure for finalize and discard before navigating.
  Failed saves are retryable, dirty browser exits are guarded, and route
  unmounts queue a best-effort save. Draft and catalog loads reject obsolete
  responses. Knowledge skills and languages use explicit add/edit/remove rows,
  including multiple entries in one Knowledge category, ratings, and
  specializations. Immutable catalog transport projections are cached by semantic
  digest and expose ETags; the authenticated API retains its security-middleware
  `no-store` policy, while the SPA deduplicates catalog requests for its lifetime.
  Creator lookups use a catalog-owned `WeakMap` index instead of rebuilding the
  unified resource list and repeatedly scanning catalog arrays. Character creator
  and administration routes are loaded as separate production chunks. Successful
  draft creates and replacements return the already validated document rather than
  deserializing the JSON that was just persisted.

Milestone 9 (Career Character Sheets) adds a mutable career layer on top of the
immutable finalized sheet. `CharacterCareerState` (one per finalized character)
holds current Karma, current nuyen, lifetime Karma earned, and a typed
`CareerProgressionDocument` JSONB envelope for permanent post-creation changes
(SHEET-903); opening Karma/nuyen are seeded once from
`DerivedStatistics.CarryoverKarma`/`CarryoverNuyen` plus starting cash, and
backfillable for pre-existing finalized sheets. Append-only
`CharacterResourceTransaction` and `CharacterAdvancement` tables record every
balance change and mechanical change respectively; `CharacterActionReceipt`
(unique on character + client-generated request id) backs request idempotency
for every career mutation. `GetComposedCharacterSheetQuery` (SHEET-904)
composes the immutable baseline with career progression and acquired inventory
into a `ComposedCharacterSheet` — current attributes/derived statistics,
balances, bounded recent history, and server-derived `NextActions` (exact
cost, eligibility, blocking reasons) — never mutating or reinitializing career
state on read. The frontend's `/characters/:characterId/sheet` route
(SHEET-905) renders this read-only, reusing character-creation catalog
indexing/description helpers but never mounting the creator; finalized
character slots expose a "View Character Sheet" link alongside "Jack in".
SHEET-906 adds the first mutation: raising a Physical/Mental attribute, Edge,
Magic, or Resonance by exactly one rating per request, at `new rating x 5`
Karma (sr5-core p. 106), capped at each attribute's own metatype/metavariant
natural maximum (+1 for the `exceptional-attribute` quality; Edge additionally
+1 for `lucky`; Magic/Resonance use a flat natural maximum of 6/7 since
Initiation isn't implemented yet). A mundane character simply has no
`magic`/`resonance` entry in the composed sheet's special attributes, so
advancing either is rejected as an unknown attribute id with no separate
"no post-creation awakening" gate needed. Every advancement is one atomic
operation: version-checked against the caller's `expectedVersion` (an EF
concurrency token, so a stale write fails at the database level),
deduplicated by client-generated request id, and recorded as one
`CharacterAdvancement` plus one `CharacterResourceTransaction` plus one action
receipt. Derived statistics are never persisted on the mutable state; they're
recomputed from baseline plus progression on every composed-sheet read via the
same pure Inherent Limit/Condition Monitor/Initiative formulas creation uses
(`DerivedStatisticsFormulas`, shared with `DerivedStatisticsEvaluator`). The
routed sheet's Attributes tab is the only interactive section so far — each
row shows the current value and cost, with an inline (non-modal) spend
confirmation before calling the advancement endpoint and reloading.

Backend endpoints:

- `GET /health/live`: process liveness without a PostgreSQL dependency.
- `GET /health/ready`: PostgreSQL readiness through the EF Core context.
- `GET /api/antiforgery/token`: returns the antiforgery token used by
  state-changing cookie-authenticated requests.
- `POST /api/account/register`, `POST /api/account/login`, `POST /api/account/logout`,
  `GET /api/account/me`: ASP.NET Core Identity account management (`/me` includes roles).
- `GET /api/characters`: list the caller's own finalized characters. The
  legacy name-only `POST /api/characters` quick-create path was removed
  (SHEET-902) now that the SR5 creator flow is the only way to create a
  character.
- `GET /api/character-creation/catalogs/current`,
  `GET /api/character-creation/catalogs/{catalogId}/{version}`: current and
  retained immutable SR5 catalog contracts.
- `POST /api/character-creation/drafts`, `GET /api/character-creation/drafts`,
  `GET /api/character-creation/drafts/{characterId}`,
  `PUT /api/character-creation/drafts/{characterId}`: authenticated,
  owner-scoped draft lifecycle with server-derived evaluation and UUID
  optimistic concurrency.
- `POST /api/character-creation/drafts/{characterId}/change-preview`,
  `DELETE /api/character-creation/drafts/{characterId}`,
  `POST /api/character-creation/drafts/{characterId}/finalize`: version-matched
  impact preview, discard, and atomic finalization operations.
- `GET /api/characters/{characterId}/sheet`: owner-scoped immutable finalized
  sheet retrieval.
- `GET /api/characters/{characterId}/career-sheet`: owner-scoped composed
  career sheet (current permanent attributes/derived statistics, current
  balances, bounded recent history, acquired inventory, and next-action
  eligibility); non-enumerating `404` for unowned/nonexistent/unfinalized
  characters, `409` if career state isn't initialized yet.
- `POST /api/characters/{characterId}/advancements/attributes`:
  antiforgery-protected, version-checked, idempotent single-rating attribute/
  Edge/Magic/Resonance advancement.
- `POST /api/play-session/start`, `GET /api/play-session/current`,
  `POST /api/play-session/activity`: start/resume, read, and renew play sessions.
- `GET /api/admin/users`, `POST /api/admin/users/{userId}/roles`,
  `DELETE /api/admin/users/{userId}/roles/{roleName}`: administrator-only user
  lookup and role assignment/removal (role-management policy).
- `GET /api/admin/audit`: cursor-paginated audit log with bounded filters
  (audit-reader policy).
- `GET /api/admin/world`, `GET /api/admin/world/rooms/{roomId}`: complete
  bounded world graph and room editor details, including hidden and locked
  directed exits (world-editing policy).
- `POST /api/admin/world/rooms`, `PUT /api/admin/world/rooms/{roomId}`,
  `POST /api/admin/world/exits`, `PUT /api/admin/world/exits/{exitId}`:
  antiforgery-protected, audited world mutations with optimistic concurrency
  (world-editing policy). No room or exit deletion endpoints exist.

SignalR hub (`/hubs/room-chat`):

- `JoinCurrentRoom()`, `RecordActivity()`, `SendMessage(content, type)`,
  `RollDice(expression)`, `MoveThroughExit(exitId)`, `GetOnlineCharacters()`.
- Events: `MessageReceived`, `CharacterDeparted` (`{ roomId, character }`),
  `CharacterArrived` (`{ roomId, character }`), `RoomChanged`,
  `RoomPresenceChanged` (`{ roomId, revision, onlineCharacters }`), `SessionExpired`.
- `JoinCurrentRoom()` returns the joined room's `RoomPresence`; `RecordActivity()`,
  `SendMessage()`, and `RollDice()` return the authoritative renewed `ExpiresAtUtc`.
- `SendMessage(content, type)` accepts only `Say` and `Emote`; `/roll` goes through
  `RollDice(expression)`, which parses, rolls, and persists the result server-side.
- `GetOnlineCharacters()` returns the distinct online characters across all rooms
  (id and name only) and requires an active, joined play session.

The frontend is a routed SPA. `src/pages` holds route-level pages (`LoginPage`,
`CharactersPage`, `GameplayPage`, `admin/AdminUsersPage`, `admin/AdminAuditPage`,
`admin/WorldEditorPage`),
`src/components` holds reusable UI and the shared `AppShell`, `src/hooks` holds
gameplay lifecycle hooks, and `src/auth` holds account restoration context.
Routes are `/login`, `/characters`, `/play`, `/admin/users`, `/admin/audit`, and
`/admin/world`,
with a not-found fallback; `/` redirects based on authentication and active
session. Guards redirect unauthenticated users to `/login` (preserving the
intended destination), `/play` without an active session to `/characters`, and
unauthorized users from protected administration routes to an access-denied
state. Role management and audit remain administrator-only; the world editor
allows `Administrator` or `WorldBuilder`. Server authorization remains
authoritative.

The frontend implements registration, login, character creation/selection, room
session rendering, realtime room chat with typed rendering (speech, emotes, and
rolls) and a message filter, idle-expiry handling, movement through visible
unlocked exits, and a client-side command composer (`/help`, `/who`, `/look`,
`/say`, `/emote`, `/roll`, `/go`) whose output is rendered as private local
transcript entries. `/go` resolves a selector against the current room's visible
exits and submits only the resolved exit ID; the server remains the movement
authority. `/emote` and plain speech are sent through the typed message path, and
`/roll` submits only an expression for server-authoritative dice.

The realtime client retries failed initial starts, rejoins before reporting a
reconnection as ready, and uses lifecycle generations to reject obsolete joins and
HTTP room-session refreshes. Explicit idle renewal is awaitable and bypasses the
passive activity throttle; logout preserves local authenticated state when the
server cannot confirm that the session was ended.

The world editor provides a responsive layered coordinate grid and an
always-present keyboard-accessible room list. Occupied cells select rooms; empty
cells open an accessible metadata modal and create the room at that immutable
coordinate. Same-layer adjacency seeds both directed paths. The editor also
provides distinct incoming/outgoing direction-only exit editing, including
manual vertical exits. Reverse exits are separate confirmed creates.

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
- Moderation APIs and moderation UI.
- Room ownership and access policies beyond public access.
- Room metadata caching.
- Player-facing graphical city map rendering.
- Shadowrun character sheets, dice, combat, initiative, equipment, and Matrix systems.
- Redis, brokers, load balancing, and horizontal scaling.

## Change Discipline

- Preserve the modular-monolith dependency direction.
- Keep transport concerns out of Domain and Application.
- Do not change architecture during Build mode; propose architecture changes in Plan mode first.
- Update this file when an accepted architectural decision, technology choice, or major implemented capability changes.
- Use `README.md` for setup and operator instructions; use this file for product and engineering context.
