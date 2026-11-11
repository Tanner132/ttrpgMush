# Seattle by Night — v1 Roadmap

The single list of work remaining before v1. Scope was set by the project owner
on 2026-11-10. Current capabilities are described in
[`../README.md`](../README.md) and [`../PROJECT_CONTEXT.md`](../PROJECT_CONTEXT.md);
this file lists only what is not done.

v1 is seven workstreams:

| # | Workstream | State |
| --- | --- | --- |
| 1 | [World Forge fixes and gaps](#1-world-forge-fixes-and-gaps) | Backend done; redesign phase 1 built, Jobs (phase 2) next |
| 2 | [Career advancement completion](#2-career-advancement-completion) | Defined; ready to build |
| 3 | [Group missions](#3-group-missions) | Needs design decisions |
| 4 | [Magic in play](#4-magic-in-play) | Needs rules contract and design |
| 5 | [Matrix in play](#5-matrix-in-play) | Needs rules clarification from the owner |
| 6 | [Auth hardening](#6-auth-hardening) | Needs a delivery decision; prerequisite of 7 |
| 7 | [Private and owned rooms](#7-private-and-owned-rooms) | Needs design decisions; blocked on 6 |

Workstreams 1 and 2 are specified well enough to build now. Workstreams 3–7 each
open with questions only the project owner can answer; nothing in them should be
built until those are answered and the answers are recorded (rules in
`SR5_RULE_DECISIONS.md`, architecture in `PROJECT_CONTEXT.md`).

## Delivery rules

- Do not implement a Shadowrun rule from assumption. Cite the approved PDF and
  page, or record an owner decision first.
- Keep the dependency direction and keep endpoints and the hub thin.
- Persist before broadcasting.
- New engine behavior goes through the existing action → queue → state change →
  event pipeline. Do not add a parallel path.
- New content vocabulary (event, effect, condition, objective kinds) is an
  additive code change with tests, exposed through the World Forge palette.
- A work item is done when its focused tests and the full checks in the README
  pass, and the README/context are updated if the surface changed.

---

## 1. World Forge fixes and gaps

Carried over from the World Forge implementation review (second pass,
2026-09-02). Re-verified against the code on 2026-11-10; the backend items were
fixed that day (see 1.1). The remaining frontend defects (WF-1, WF-2, WF-9,
WF-10) were left for the World Forge redesign, which replaced the screens they
lived in; its first phase (2026-11-11, see 1.3c) rules them out.

### 1.1 Defects

| ID | Area | Problem | Fix shape |
| --- | --- | --- | --- |
| WF-1 | Frontend | **Designed out 2026-11-11.** Every reference is a GUID (WF-27) and the redesign mints ids once, so a rename only changes a display name. Original problem: **Rename corrupts references through transient key collisions.** `EncounterEditor.renameRoom` and `SceneEditor.renameNode` retarget references by current string value on every keystroke. Renaming `storage` toward `entry-hall` passes through `entry` and then rewrites everything pointing at the real `entry` room. The result passes the publish gate, so the corruption is silent. | Track row identity instead of the current string: rename on blur/commit, or suspend retargeting while the new key duplicates another row's. Add a test with colliding names. |
| WF-2 | Frontend | **Designed out 2026-11-11.** The Forge loads through `useLoad`, which drops any answer that arrives after the screen moved on (test in `forge/fields.test.tsx`). Original problem: **Kind-switch race in `useContentDraft.open`.** An in-flight fetch that resolves after a kind switch lands the old kind's fragment in the controller, so "Save draft" can write an encounter fragment as a Mission. | Capture a request token in `open` and drop stale responses. Add a test that resolves the fetch after the switch. |
| WF-3 | Backend | **Done 2026-11-10.** Idle-scope reclaim could break per-scope serialization in `GameCommandQueue`, and a reclaimed write could even run twice (once on the orphaned consumer, once on the retry). | The idle check + close and the count-in + write now happen under one per-scope lock; a caller that finds the scope reclaimed retries on a fresh one without having written anything. New scopes start "just used". Tests: reclaimed-scope reuse runs each command once. |
| WF-4 | Backend | **Done 2026-11-10.** Damage did not stack across a trigger's reactions. | `SceneDamageTally` is created once per `TriggerEngine.RunAsync` and threaded through every reaction. Playthrough test (3P branch + 2P applyEffects = +5) fails on the old code. |
| WF-5 | Backend | **Done 2026-11-10.** `giveItem` neither raised `ItemPickedUp` nor advanced an `AcquireItem` objective. | Already fixed before this pass: `SceneEffectResolver` completes the objective in the same commit and `EventsFor` raises `ItemPickedUp`. |
| WF-6 | Backend | **Done 2026-11-10.** Stale-event drop lost fire-once triggers. | **Owner decision:** keep progress, drop flavor. A stale `playerEnteredRoom` is still dropped; any other stale event runs its trigger against the room it happened in, applying effects but skipping narration, NPC lines, scene opens, `startCombat`, and `advanceScene`. Playthrough test covers a defeat consumed after the player left. |
| WF-7 | Backend | **Done 2026-11-10.** Scene NPC effects validated too leniently. | **Owner decision:** match where it can play. A named NPC in a scene effect must exist in every encounter that can open the scene (placement binding, template binding, or trigger); `pacifyNpc`/`startCombat` must also be in the room it opens in when that is known. A scene never opened inside an encounter can only target its own NPC. Loader tests added. |
| WF-8 | Backend | **Done 2026-11-10.** A trigger with two scene-opening reactions let the second silently replace the first. | Publish refuses more than one scene-opening reaction per trigger (a `runTest` whose two branches each open a scene counts once). Loader tests added. |
| WF-9 | Frontend | **Designed out 2026-11-11.** The Forge's `NumberInput` keeps the typed text until it is a valid number, says what is wrong when out of range, and lets optional fields clear (tests in `forge/fields.test.tsx`). Original problem: `NumberField` writes `0` on clear rather than the field minimum; a leading `-` is eaten after a clear. | Use the raw string or `validity.badInput`. |
| WF-10 | Frontend | **Mostly designed out 2026-11-11.** Tabs carry `aria-current`; a refused delete disarms the confirm and shows the reason; leaving unsaved edits asks first. The trigger-picker item moves to the Jobs screen (phase 2), whose pickers come only from the references endpoint. Original problem: The mission-owner trigger picker falls back to the global pool while references load or when `encounterId` dangles; the dashboard inventory row lacks `aria-current`; a refused dashboard delete leaves the confirm state armed; kind/module switches discard unsaved drafts with no dirty check. | Fix each; add a dirty-check prompt. |
| WF-11 | Tests | **Done 2026-11-10.** The queue's fault logging had no test. | `GameCommandQueueTests` asserts the error record carries the exception, action id, request id, scope and user. |

Remaining: WF-10's trigger picker, which belongs to the Jobs screen. With 1.2
and WF-25 done, every non-UI World Forge item is finished.

### 1.2 Authoring gaps

| ID | Gap | Resolution |
| --- | --- | --- |
| WF-20 | **Done 2026-11-10 (backend).** World-room triggers could not be authored. | **Owner decision:** a new draft/publish content kind, `Place`, keyed by the public room's content key (every map room has one since WF-25). A place's triggers watch the room itself (no room filter), name world NPCs by name (checked against the room at publish), and may use the room events below plus `npcSpokenTo`/`npcDefeated`/`npcPacified`/`missionAccepted`. Effects that need a live run (`giveItem`, pay negotiation, turn-in) are refused. Superseded in part by WF-26: a room's own events fire every time and work in any map room. Publish refuses a place on a missing room or naming an NPC not standing there; a room with published events cannot be deleted. UI: Places screen in the redesign. |
| WF-21 | **Done 2026-11-10 (backend).** Only `interactableInspected` existed for placements. | **Owner decision:** four new events. `sneakAttempted` (names the NPC; optional `outcome` success/failure filter), `combatStarted` (names the NPC the fight started with, or the room), `lockedExitTried` (the room the door is in), `playerLeftRoom` (the room left; runs where the player arrives, so NPC lines and face-to-face effects are refused). |
| WF-22 | **Done 2026-11-10.** Publish and delete were check-then-write. | Publish (validate + promote), delete (reference check + delete) and draft saves now run in one transaction under a Postgres advisory lock on the corpus; the cache reloads after commit. Tests: a draft save waits while another storyteller holds the corpus; the lock is re-entrant. |
| WF-23 | **Done 2026-11-10 (backend).** Pickers made one request per definition. | **Owner decision:** references endpoint only, no dashboard cache. `GET /api/admin/content/references` returns every encounter (rooms, NPCs, items, inspectables), mission (objectives), scene (nodes), test, stat block, place and map room (key, access, NPCs) in one response. The redesign's pickers use it. |
| WF-24 | **Done 2026-11-10.** Test coverage holes. | Playthroughs added for `encounterEntered`, `itemPickedUp`, `missionAccepted` through the Johnson, a condition holding a trigger back without spending it, a repeatable place trigger, two copies of one event racing on different queues (a job's listener fires once), and a trigger's scene not replacing an open conversation. `npcDefeated` is covered by the offstage playthrough (event raised directly, not from a won fight). |

### 1.3 Job sites are map rooms (WF-25)

**Owner decision, 2026-11-10.** A job site is made of ordinary map rooms, not
rooms that exist only inside an encounter definition. Verified against the code
the same day: public rooms are database rows with map coordinates and layers
(the world editor already builds on multiple layers), but job sites are not.
An encounter definition declares its own rooms (key, name, description,
environment modifier) with no map position, its exits carry no hidden/locked
flags, and the only link to the map is the mission's entry room.

The requirement:

- **One kind of room.** Every room is authored on the map in Places, with
  coordinates and a layer. Adding a room while building a job adds it to the
  shared map like any other room.
- **A job's site is a set of map rooms** plus an entry room. The job reuses
  those room records; it does not keep its own copies of name, description or
  exits.
- **Room access.** A room is either *public* (anyone can enter) or *private to
  the run* (only runners on the associated run can enter). For everyone else a
  private room's way in behaves as locked.
- **A private copy per run.** When a run starts, its private rooms are copied
  for that run (today's instancing): name, description, environment modifier,
  map position, exits including hidden and locked flags, and the job's NPCs,
  items, inspectables and events placed in them. Runners on the run who walk
  into a private room enter their run's copy. Edits to the map room affect new
  runs only.
- **Hidden and locked exits work inside sites** exactly as they do on public
  rooms.
- **Job content lives in private rooms.** NPCs, items, inspectables and events
  that belong to a job are placed only in the job's private rooms; public rooms
  on a job are paths (entry and return), not containers for run content.
  The one exception is the job's Johnson, who stands in a public room so
  runners can be offered the job and get paid.
- **Design for intervention later.** Not required now, but runners should
  eventually be able to join or intervene in someone else's run. Access checks
  should therefore ask "may this character enter run X's copy" (today: is a
  participant) rather than hard-coding participation, and copies stay keyed by
  run so another character can later be admitted to a specific one.
- **Room images are out of scope** until there is file storage; the UI keeps a
  placeholder.
- **Migration.** The shipped Gang Warehouse encounter is converted to map rooms
  (positions assigned on an unused part of the map) with its run content
  attached to those rooms, and the warehouse playthrough tests keep passing.

**Backend done 2026-11-10** (uncommitted, for review): content keys and the
Private access type on map rooms, the warehouse moved onto the map,
run copies made from map rooms with their exit flags, walk-in entry for
runners on the run and locked doors for everyone else, publish-time site
checks, and a delete guard for site rooms. Also done the same day:

- **Johnsons in public rooms.** World NPCs are placed, edited and removed from
  Places (`/api/admin/rooms/{roomId}/npcs`), only in Public map rooms, with a
  stat block, name, description, optional conversation (whose bound stat block
  must match) and starting awareness. Audited; removal is refused mid-conversation.
- **Migration verified.** `JobSitesAreMapRooms` is tested against a database
  with existing rooms (duplicate names, reserved keys). That found and fixed a
  key collision (a second "Alley" became `alley-2`, the key of "Alley 2"); it
  now falls back to slug + id fragment. `Down` then `Up` is tested too.
- **Exit list.** A door into a job site's private room shows as locked to
  anyone not on that job.

The World Forge redesign (Places / Jobs / Library) is built on this model, so
WF-25 lands first. The redesign mockups live in the "World Forge Redesign"
canvas.

### 1.3a Events belong to rooms (WF-26)

**Owner decision, 2026-11-10.** Events are defined in one place: a room. There
is no such thing as a roomless event — anything that happens, happens in the
room where the action occurred (a future timed event uses the room its action
lands in). Today triggers live in three places (the job site, the mission, and
the public room's place); that collapses to one shape:

- **A listener is room + event (+ subject) + reactions, owned by either the
  room itself or a job.** World creation comes first: build the rooms, then the
  job. Authoring a job is the Johnson interaction, then "pick a room, pick an
  event in it, say what happens".
- **One job per event.** At most one job may listen to a given event in a given
  room. The event is identified by its room, kind and subject, so each item's
  pick-up, each NPC, each inspectable, and a sneak's success and failure are
  separate events that different jobs may claim. Events about the job itself
  (accepted, entered the site) only ever reach the job they are about. The room
  itself does not count: a room and one job can both react to the same event.
- **Order.** The room's reaction runs first, then the job's.
- **Room reactions fire every time** (a room description on every entry), not
  once per character. Job reactions keep their fire-once-per-run default.
- **Job reactions are private in public rooms.** The player gets the job's
  lines; everyone else in the room sees a cover line ("Tanner reaches for their
  commlink and answers a call.", overridable per listener). Inside a run's
  private copy everyone present is on the run, so nothing is hidden. Later:
  magic or devices that let someone eavesdrop.
- **Room narration in a public room goes to the player who caused it** (it is
  written as "you"); an NPC speaking is heard by the whole room. *(Engineering
  choice, for review.)*
- **Matching.** An event in a run's copy reaches that run's job; an event in a
  public room reaches the player's open jobs. Listeners name the map room; a
  run's copy records the map room it was made from (by id since WF-27).
- **Migration.** Existing job-site and mission triggers move onto the job with
  a room derived from what they watch (the NPC's, item's or inspectable's room;
  the entry room for site entry; the Johnson's room for acceptance). Stored
  content is upgraded at startup. The `Place` kind becomes "the room's own
  events".

**Backend done 2026-11-10** (uncommitted, for review):

- Job sites carry no triggers; a job's listeners live on the mission, each with
  a required room (one of its site rooms, or a public room; `roomId` since
  WF-27). The loader
  checks the subject is in that room (an NPC, inspectable or placed item), and
  refuses a second job on the same event. A sneak listener with no outcome
  hears both outcomes, so it collides with either.
- `Place` is the room's own events: any map room, always repeating (no
  fire-once record), no `othersSee`.
- Publish: a job's public-room listener must name a Public map room and only
  NPCs standing in it; a room's own events need the room and its NPCs. A room
  a job listens in cannot be deleted (its own events go with it since WF-27);
  a room a job listens in from outside its site cannot be made Private.
- Runtime: the room's listeners run first, then the job's. Job listeners in a
  public room reach only their runner, with the cover line (`othersSee`,
  default "reaches for their commlink and answers a call.") shown to the room.
- Admin assignment raises "job accepted" where the character is standing, so
  the shipped listener (in the coffee shop) only fires if they are there.
- The shipped bundle is in the new shape. The `PlaceTriggerFires` migration
  from earlier today is withdrawn (room events need no fire-once record). The
  startup content upgrade written for this was removed by WF-27, which resets
  development databases instead.
- Done 2026-11-11: the old screens that authored triggers on the encounter
  are deleted; room events are authored on Places (1.3c).

Known follow-up: a retired job still holds its events until deleted. (Renaming
or removing a world NPC that a listener names was settled by WF-27: renames are
safe because listeners use the id, and removal is refused while a listener
names them.)

### 1.3b Every authored record is keyed by a GUID (WF-27)

**Owner decision, 2026-11-10.** No more text keys. Every record a storyteller
creates has a randomly generated GUID, and every reference to it uses that
GUID. Names are for display only and can change freely; nothing ever matches
on them, and there is no key-generation scheme to maintain.

- **In scope:** rooms (their database id), job sites, jobs, objectives,
  listeners, conversations and their steps and choices, stat blocks, authored
  tests, a job's NPC placements, items and inspectables, world NPCs (their row
  id), and a room's own events (keyed by the room's id).
- **Engine vocabulary stays named:** skills, attributes, dice pools, effect and
  event kinds, actions, and the built-in tests (`sneak-past`,
  `negotiate-pay`, …) are code, not content. A test reference is therefore
  either a built-in test's name or an authored test's GUID.
- **Run copies point back at what they came from** (room → map room, NPC →
  placement, item → item, inspectable → inspectable), and events carry the
  original's id, so a listener matches every run's copy.
- **Edits flow through automatically**, because nothing points at a name.
- **Deletes are refused while anything points at the record**, and the refusal
  names what does (by display name).
- **Deleting a room deletes everything in it** — its NPCs, items,
  inspectables, its own events and its exits in both directions — and is only
  allowed when no job listens to it or uses it as part of a site. Characters
  standing there are moved, as today.
- **Items can be hidden** like inspectables: a hidden flag and a search
  difficulty, found per character by searching the area; a hidden item cannot
  be taken until found.
- **Existing data is not migrated.** Development databases are recreated.

**Backend done 2026-11-10** (uncommitted, for review):

- One migration, `GuidContentKeys`, replaces the uncommitted
  `JobSitesAreMapRooms`: content-key columns become `uuid`, `rooms.content_key`
  is dropped, run copies gain `source_npc_id` / `source_interactable_id`, items
  gain `item_id`, `is_hidden` and `discovery_threshold`, and the fire ledger
  keys on `trigger_id`. The startup upgrade (`GameContentUpgrades`) and the
  room-key helper (`RoomContentKeys`) are deleted.
- The shipped bundle is regenerated with fixed GUIDs, exposed to the seeder and
  tests as `ShippedContentIds`. The development seeder now places the four
  warehouse rooms on the map itself.
- The loader validates GUID ids, refuses a duplicate id anywhere it could be
  confused (site contents, objectives, listeners, steps, choices), and its
  errors name records by display name (listeners as
  `listener 'kind' (first 8 of id)`).
- Room deletion cascades as above; NPC removal is refused while a room's or a
  job's listener names the NPC. Content-definition deletes still go through
  the loader-based reference check, now worded with display names.
- Hidden items: `DiscoverySubjectType.Item`; observe-area reveals items whose
  difficulty the hits reach; affordances and take-item ignore undiscovered
  hidden items.
- Tests: legacy-shaped fixtures in the loader and playthrough tests are
  rewritten to the GUID shape by `LegacyContentFixtures` (old key → a stable
  GUID; shipped keys → the shipped GUIDs).
- Done 2026-11-11: the old screens that authored string keys are deleted; the
  redesign mints ids with `crypto.randomUUID()` (1.3c).

### 1.3c World Forge redesign (WF-28)

**Owner decision, 2026-11-11.** Build the "World Forge Redesign" canvas in
phases, replacing the old screens, on the existing API only. Anything the API
cannot do yet is stubbed in the UI and listed below.

**Phase 1 done 2026-11-11** (front end only, uncommitted, for review):

- One Forge at `/admin/forge` with three tabs: Places, Jobs, Library. The old
  World editor and World Forge screens, their API modules and stylesheets are
  deleted; `/admin/world` redirects to Places. Code lives in
  `frontEnd/src/forge/`, styles in `styles/components/forge.css`.
- **Places.** A map grid per area and layer (pan, layer up/down, build a room
  in any empty cell). The selected room shows its name, description and
  access; exits (add and edit, both ways, locked, hidden, stairs); what is in
  it — world NPCs and inspectables in a public room, or the job site's NPCs
  (with stat overrides), items (hidden or handed over by an event) and
  inspectables in a private room; the room's own events; and which jobs touch
  it, with delete (relocating anyone standing there).
- **Events editor** (shared with Jobs later): event, NPC, sneak outcome,
  conditions, and steps — narrate, NPC says/does, roll (pass and fail
  branches), effects, open a conversation. Room events always repeat and have
  no cover line, as the loader requires.
- **Library.** Stat blocks and dice tests: list, search, new (blank or copy
  of), edit, save draft, go live, retire, delete. New records get
  `crypto.randomUUID()` ids. Built-in tests are listed read-only.
- **Home.** Counts per tab, drafts and records that cannot go live, and recent
  edits linking to their editors.
- Shared: a draft bar (status, undo edits, save, go live, retire, delete with
  confirm); server refusals are shown verbatim, including field-level
  validation messages (`api/client.ts` now reads `errors`); modals render in a
  portal so panels no longer clip them; leaving with unsaved edits asks first.
- WF-1, WF-2, WF-9 and most of WF-10 are designed out (see 1.1).
- Tests: Places (14), Library and Home (8), fields/hooks (5); full suite, type
  check, lint and build pass. Checked end to end against the real API on a
  fresh database, including at phone width.
- The Forge uses hand-written types in `forge/api.ts` and `forge/model.ts`;
  `npm run gen:api` was not rerun.

**Phase 2: Jobs.** Today a read-only list. To build: job and objective
editing, Johnson and pay, job site rooms, the job's listeners (fire-once and
cover lines allowed here), and conversations (scene nodes, choices, tests an
NPC rolls against). Close out WF-10's picker item there.

**Backend gaps found by phase 1** (the UI shows each as unavailable):

| Gap | Where it shows |
| --- | --- |
| No endpoint to delete an exit. | Exits: "Removing an exit isn't available yet; deleting a room removes its exits both ways." |
| Inspectables in a public room can be added but not edited or removed. | Places, public room contents. |
| Items cannot be placed in a public room (only on job sites). | Places, public room contents. |
| A stat block's "used by" does not count world NPCs placed from it. | Library, stat block usage. |
| Rooms have no image. | Places, room section. |
| Conversations (Ask steps) need the Jobs screen. | Phase 2. |

### 1.4 Acceptance run

The original World Forge acceptance scenario has never been executed end to end
through the UI. v1 requires it:

> In a running game, with no code or database access, a staff member authors a
> second complete mission — a new Johnson with a negotiation test, a new
> job site built from map rooms, a placed NPC from an existing template, a retrievable
> item, objectives, rewards, and at least one trigger-driven scene that exists
> nowhere in code. They publish it, play it through with a normal character, get
> paid once, retire it, confirm it is gone from play while the character's
> ledger and history are intact, and hard-delete an unpublished draft.

---

## 2. Career advancement completion

Attributes, Edge, Magic, Resonance, skills, skill groups, knowledge skills,
languages, specializations, the routed sheet, and `/character` are done. The
rules contract for everything below is frozen in
[`SHEET_901_CAREER_RULES_BASELINE.md`](SHEET_901_CAREER_RULES_BASELINE.md) and
the "Milestone 9 Career Decisions" section of
[`SR5_RULE_DECISIONS.md`](SR5_RULE_DECISIONS.md).

Constraints that apply to all of it:

- The finalized sheet stays immutable. Changes go into career state.
- The server computes every cost, maximum, price, and resulting balance.
- Every mutation is version-checked, idempotent by request ID, and writes one
  advancement or inventory record plus one matching resource transaction.
- Advancement is immediate: no training time, downtime, or Storyteller approval.
- New controls appear in the shared sheet component, so the route and the modal
  both get them.

### 2.1 Quality advancement

- Acquire career-eligible positive qualities; remove eligible negative ones.
- Add the career metadata the catalog lacks (creation-only flags, career
  eligibility) and validate it in the loader.
- Validate parameters, conflicts, prerequisites, repeatability, and ratings.
- Costs use the approved career multiplier, not the creation cost.
- Endpoint: `POST /api/characters/{characterId}/advancements/qualities`.

### 2.2 Magic and Resonance advancement

- Learn spells, rituals, preparations, and complex forms for Karma. Per the
  approved decisions, the creation-time `Magic × 2` and
  `min(Logic, Resonance × 2)` caps do **not** apply in career.
- Initiation (with metamagics) and Submersion (with echoes), including the
  `6 + grade` maximum, which stacks additively with Exceptional Attribute.
- Adept Power Points. A mystic adept gains no Power Points by direct Karma
  purchase after creation; the only career source is the `power-point`
  metamagic.
- No post-creation awakening: a character who finished creation with Magic 0 and
  Resonance 0 is rejected.
- Replace the flat natural maximum of 6/7 that attribute advancement uses today
  for Magic and Resonance with the grade-aware maximum.
- Add catalog data for the 9 core metamagics and 9 core echoes.
- Endpoint: `POST /api/characters/{characterId}/advancements/magic-resonance`.

### 2.3 Nuyen purchases and inventory

- An eligible-catalog query and a purchase command creating
  `CharacterInventoryItem` rows and nuyen transactions atomically.
- First surface: fully priced ordinary items from gear, weapons, armor, vehicles
  and drones, and cyberdecks. Availability and legality are shown but never
  block a purchase.
- **Prerequisite:** run the SHEET-901 §6.1 eligibility rule mechanically over
  every row of those collections to produce the per-item eligible/excluded list
  (SHEET-901 §6.4). No item is purchasable without a deterministic price and
  supported parameters.
- Searchable, filterable purchase UI with rating and parameter controls and an
  inline confirmation showing the resulting balance.
- Purchased ammunition and consumables are inert inventory with a quantity.
- Endpoint: `POST /api/characters/{characterId}/purchases`.

Not purchasable in this pass, because each mutates more than a balance:
augmentation installation and Essence changes; weapon accessories and mounts;
armor modifications; device, cyberlimb, and vehicle attachments; focus purchase
and bonding. Workstream 4 needs focus bonding; decide there whether to pull it
in.

### 2.4 Contacts

- Add a new contact in career at zero Karma, starting at Connection 1 /
  Loyalty 1 (approved decision `career.contact-free-addition-pending-st-gate`).
- Raising an existing contact's ratings stays out: the rules give no formula.

### 2.5 Release gate

- Reconcile every included advancement and purchasable entry against the frozen
  contract.
- PostgreSQL integration tests for concurrency, idempotency, and ownership on
  each new mutation.
- Accessibility and narrow-viewport review of the sheet, modal, confirmations,
  and purchase search.
- The baseline sheet is byte-for-byte unchanged by any advancement or purchase.

---

## 3. Group missions

**Outcome:** more than one player character can take part in the same mission
encounter instance and the same combat.

What exists: `EncounterInstance` holds a participant collection
(`EncounterParticipant`), the command queue serializes per instance, combat
tracks participants generically, and presence and chat are already room-scoped.
Everything else assumes one player.

Decisions needed from the owner before design:

1. **Forming a group.** How do characters join the same run — a party the
   leader forms before taking the job, an invitation after accepting, or
   everyone talking to the Johnson together?
2. **Mission ownership.** Is there one mission instance shared by the group, or
   one per character pointing at a shared encounter? Who can turn it in?
3. **Rewards.** Does each participant receive the full Karma and nuyen, or is
   nuyen split? Is the negotiated pay one roll for the group? Rewards must stay
   grant-once per character through the ledger either way.
4. **Objectives and items.** Is an objective complete when any participant
   satisfies it? Who holds the mission item, and can it be handed over?
5. **Joining and leaving.** Can someone join a run already in progress? What
   happens to the run when one member disconnects, is knocked out, or leaves —
   and when the last one does?
6. **Decisions and timers.** Combat already has per-turn timers. Do scene
   prompts go to one player (who?) or to everyone, and how is a split answer
   resolved?
7. **Size limit** per instance.

Known engineering consequences once those are answered:

- NPC combat behavior has only ever faced one player; its target selection needs
  a defined rule for several.
- `SceneSession` is one row per character; a shared prompt needs a defined owner.
- `TriggerFire` records fire-once per character and mission instance; group
  semantics must be defined.
- The defeat path (wake at the entry point, mission fails) must be redefined for
  a partial wipe.
- WF-22 (atomic publish/delete) should land first. Done 2026-11-10.

---

## 4. Magic in play

**Outcome:** awakened characters use magic through the engine during play, not
only as selections on their sheet.

What exists: creation-time traditions, spells, rituals, adept powers, mentor
spirits, spirit types, and foci in the catalog; Magic as an attribute the
engine can read; the active-effect framework with durations and stacking; the
decision-pause framework. The architecture plan (§44) anticipates sustained
spells, active spell effects, drain, summoned and bound spirits with remaining
services, and temporary magical modifiers. None of it is built.

Decisions needed from the owner before a rules contract can be written:

1. **Scope.** Which of these are v1: spellcasting, drain, sustaining, counter-
   spelling, summoning, binding, banishing, adept powers in play, alchemy and
   preparations, ritual spellcasting, astral perception and projection, foci,
   mentor spirit modifiers.
2. **Spell coverage.** The catalog holds spells as descriptive data. Does every
   cataloged spell become mechanically usable, or a defined subset (for example
   direct and indirect combat spells, plus a few health and illusion spells)?
3. **Spirits.** Are spirits full actors in combat (NPC-like, on the `IActor`
   abstraction) or a simplified service counter?
4. **Astral space.** In or out. If in, how it relates to the room graph.
5. **Adept powers.** Which powers become enforced modifiers rather than display
   text.
6. **Foci.** Bonding requires a Karma spend and a purchase flow that does not
   exist (`focus.pricing-added-no-purchase-flow`). Is that part of this
   workstream?

Once scoped, the first deliverable is a cited rules contract in the same form
as `SHEET_901_CAREER_RULES_BASELINE.md`, with every ambiguity recorded as an
owner decision. Implementation follows the existing pipeline: spell and power
definitions as actions and tests, drain as a resisted test producing damage
State Changes, sustained spells as Active Effects.

Dependency: workstream 2.2, so that learned spells and Initiation exist on the
career sheet.

---

## 5. Matrix in play

**Outcome:** deckers and technomancers act in the Matrix through the engine.

What exists: commlinks, cyberdecks (device rating, attribute array, program
slots as data), software, complex forms, and sprite types in the catalog. The
architecture plan (§43) states the intent: SR5 statistics and dice combined with
"a streamlined access system inspired by Shadowrun 6e", with conceptual access
levels Outsider / User / Admin, and anticipates AR / cold-sim / hot-sim,
persona, current host, Matrix condition monitor, and Overwatch Score. It also
says full rule clarification will be provided before implementation. That
clarification has not been written.

**This workstream is blocked on the owner's Matrix rules document.** It needs to
settle at least:

1. The access model: how a character moves between Outsider, User, and Admin,
   and what replaces SR5 marks.
2. Which Matrix actions exist and the test for each.
3. Whether hosts are places (rooms in the graph, an overlay on a physical room,
   or their own instance type) and how IC behaves.
4. Overwatch Score and convergence: in or out, and the consequence.
5. Matrix damage, biofeedback, link-locking, and dumpshock.
6. Technomancers: Living Persona, fading, threading, sprites — v1 or later.
7. Cyberdeck configuration: attribute swapping and program slots, which are
   uncataloged gaps today.
8. How Matrix actions interact with a mission encounter — for example whether a
   decker can affect devices, doors, and NPC gear in the physical rooms.
9. How World Forge authors hosts, devices, and Matrix objectives.

Because this deliberately departs from SR5 as written, every departure is an
owner decision recorded in `SR5_RULE_DECISIONS.md`.

---

## 6. Auth hardening

**Outcome:** an account is tied to an email address its owner controls, and a
lost password can be recovered without database access. Added to v1 on
2026-11-10 as a prerequisite of workstream 7: non-public rooms must not ship
before it.

What exists: ASP.NET Core Identity with a required, unique email; cookie
authentication, lockout, login rate limiting, and generic login errors. No email
is ever sent, addresses are unconfirmed, and there is no reset flow.

Required behavior:

- Email confirmation for new registrations.
- Password reset by emailed, single-use, expiring token.
- Responses that never disclose whether an account or address exists.
- Rate limiting on the confirmation-resend and reset-request endpoints.
- A successful reset rotates the security stamp, ending existing sessions.

Decisions needed from the owner:

1. **Delivery.** How email is sent from the single self-hosted instance — an
   SMTP relay or a transactional email provider — and which sender domain. This
   is a new external dependency and a new deployment secret.
2. **Enforcement.** What an unconfirmed account may do: nothing until confirmed,
   or everything except entering non-public rooms.
3. **Existing accounts.** Whether accounts registered before this ships must
   confirm, and how the bootstrap administrator and `devuser` are treated.
4. **Changing email.** Whether a user can change their address in v1 (it needs
   its own confirmation flow).
5. **Failure handling.** What a player sees, and what the operator is told, when
   delivery fails.

---

## 7. Private and owned rooms

**Outcome:** rooms that are not open to everyone.

**Prerequisite:** workstream 6.

What exists: `RoomAccessType` with the single value `Public`; exits with hidden
and locked flags; server-authoritative movement that already validates the exit.
No ownership or access checks exist.

Decisions needed from the owner:

1. **Access types.** Which exist — owned, invite-only, private, claimable,
   staff-only?
2. **Ownership.** Is a room owned by a character or by an account? Can it be
   transferred or shared?
3. **How a player gets one.** Staff assignment only, a claim action in play, or
   a purchase tied to lifestyle or nuyen?
4. **Invitations.** Per character or per account, permanent or per visit, and
   who can revoke.
5. **Occupants on a change.** What happens to characters inside when a room
   becomes private or an invitation is revoked.
6. **Visibility.** Do non-members see the exit, the room's name, or `/who`
   presence for people inside?
7. **Chat privacy.** All chat is retained for future moderation. Does that hold
   for private rooms, and are players told?
8. **Staff access.** Can staff enter any room, and is that audited?

---

## Not in v1

Considered and deliberately left out. Each needs its own scoping before it is
picked up.

| Item | Note |
| --- | --- |
| Moderation tools | The `Moderator` role and `ModerationAccess` policy exist and are unused. |
| Staff Karma/nuyen awards | The ledger supports other sources; only mission rewards grant today. |
| SR5 catalog schema lock and digest enforcement | The catalog stays in its mutable pre-alpha phase. Lock it before real players create characters they expect to keep. |
| Further sourcebook imports | Chrome Flesh, Data Trails, Forbidden Arcana, and Street Lethal are registered and unimported. |
| Fuller combat | Grenades, called shots, suppressive fire, multiple attacks, interception, knockdown, full environmental tables, healing rules, PC death. |
| Rigging, vehicles, and drones in play | Catalog data only. |
| Enforced quality and martial-arts effects | Display text today. |
| Career: augmentation installation, attachments, selling/refunds/transfers, lifestyle billing, Availability tests, reputation (Street Cred, Notoriety, Public Awareness) | Lifetime Karma is tracked so Street Cred can be derived later. |
| Storyteller approval queues and training time | Zero-Karma contact acquisition joins the gated list if this is ever built. |
| Player-facing city map | |
| PvP, mission spectators, persistent world consequences, factions, procedural missions | |
| Content version history and multi-admin review workflows | |
| Database backup automation | Operational, not a feature — but nothing backs up `postgres_data` today. |
| Caching, SignalR backplane, horizontal scaling | Only on measured need. |

## Reference documents in this folder

| File | What it is |
| --- | --- |
| `GAME_ENGINE_FULL_ARCHITECTURE_PLAN.md` | Target design for the game engine. Sections on Matrix, magic, and vehicles describe intent, not built behavior. |
| `SHEET_901_CAREER_RULES_BASELINE.md` | Frozen, cited rules contract for career advancement (workstream 2). |
| `SR5_RULE_DECISIONS.md` | Register of owner-approved rule interpretations. |
| `SR5_RULESET_MANIFEST.md` | Approved source PDFs, citation convention, catalog schema lifecycle, scope. |
| `SR5_RULESET_BASELINE.md` | Original creation rules baseline and test contract. |
| `SR5_CATALOG_LEDGER.md`, `SR5_CATALOG_DEFERRED_WORK.md`, `sr5-catalog/` | Per-category and per-book catalog completeness ledgers with citations. |

These were written during the work they describe and refer to ticket IDs
(`CHAR-8xx`, `SHEET-9xx`) and milestone files that no longer exist; read those
as historical markers. The manifest and ledger also lag the catalog: they state
that nothing has been imported from `rigger-5`, `street-grimoire`, or
`hard-targets`, while the runtime catalog carries entries from all three.
