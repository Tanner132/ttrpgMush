# Seattle by Night — v1 Roadmap

The single list of work remaining before v1. Scope was set by the project owner
on 2026-11-10; workstreams 8 and 9 were added on 2026-11-12. Current
capabilities are described in [`../README.md`](../README.md) and
[`../PROJECT_CONTEXT.md`](../PROJECT_CONTEXT.md).

Last checked against the code: 2026-11-12, at commit `6d0c4b5`. The
workstream 1 gap work of 2026-11-12 (end of 1.3c) is built but not yet
committed. Workstream
1 keeps the record of what was built and decided, because workstream 8 changes
much of it.

v1 is nine workstreams:

| # | Workstream | State |
| --- | --- | --- |
| 1 | [World Forge fixes and gaps](#1-world-forge-fixes-and-gaps) | Built. Left: the acceptance run (1.4, after 8), and the 1.3c gaps that belong to 8, 9 or playtesting |
| 2 | [Career advancement completion](#2-career-advancement-completion) | Defined; ready to build |
| 3 | [Group missions](#3-group-missions) | Mostly answered by 8 (crews); what is left is group combat, in the combat milestone |
| 4 | [Magic in play](#4-magic-in-play) | Needs rules contract and design |
| 5 | [Matrix in play](#5-matrix-in-play) | Needs rules clarification from the owner |
| 6 | [Auth hardening](#6-auth-hardening) | Needs a delivery decision; prerequisite of 7 |
| 7 | [Private and owned rooms](#7-private-and-owned-rooms) | Needs design decisions; blocked on 6. 8 fixes how a private room is marked |
| 8 | [One shared world](#8-one-shared-world) | Phase 1 (exit states) built 2026-11-16, not committed; phases 2–5 next, in the order given in 8.4 |
| 9 | [Room images](#9-room-images) | Decided 2026-11-12; ready to build, independent of the rest |

Workstream 8 replaces how jobs run today (1.3) and changes the questions in 3
and 7; read it before picking either of those up.

Workstreams 2, 8 and 9 are specified well enough to build now. Workstreams 4–7
each open with questions only the project owner can answer; nothing in them
should be built until those are answered and the answers are recorded (rules in
`SR5_RULE_DECISIONS.md`, architecture in `PROJECT_CONTEXT.md`).

**Suggested order.** 9 and phase 1 of 8 (exit states) are independent and
small. The rest of 8 comes before the acceptance run in 1.4, because 8 changes
what a job is.

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
lived in; its first phase (2026-11-11, see 1.3c) rules them out, and its second
(2026-11-12) closes WF-10.

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
| WF-10 | Frontend | **Designed out 2026-11-12.** Tabs carry `aria-current`; a refused delete disarms the confirm and shows the reason; leaving unsaved edits asks first. The Jobs screen's pickers are built only from data that has loaded (the job's own drafts plus the references endpoint), and the job editor shows nothing until all of it has. Original problem: The mission-owner trigger picker falls back to the global pool while references load or when `encounterId` dangles; the dashboard inventory row lacks `aria-current`; a refused dashboard delete leaves the confirm state armed; kind/module switches discard unsaved drafts with no dirty check. | Fix each; add a dirty-check prompt. |
| WF-11 | Tests | **Done 2026-11-10.** The queue's fault logging had no test. | `GameCommandQueueTests` asserts the error record carries the exception, action id, request id, scope and user. |

Nothing in this table remains open.

### 1.2 Authoring gaps

| ID | Gap | Resolution |
| --- | --- | --- |
| WF-20 | **Done 2026-11-10 (backend).** World-room triggers could not be authored. | **Owner decision:** a new draft/publish content kind, `Place`, keyed by the room's id (a text content key at first; a GUID since WF-27). A place's triggers watch the room itself (no room filter), name world NPCs by id (by name before WF-27; checked against the room at publish), and may use the room events below plus `npcSpokenTo`/`npcDefeated`/`npcPacified`/`missionAccepted`. Effects that need a live run (`giveItem`, pay negotiation, turn-in) are refused. Superseded in part by WF-26: a room's own events fire every time and work in any map room. Publish refuses a place on a missing room or naming an NPC not standing there; a room with published events cannot be deleted. UI: Places screen in the redesign. |
| WF-21 | **Done 2026-11-10 (backend).** Only `interactableInspected` existed for placements. | **Owner decision:** four new events. `sneakAttempted` (names the NPC; optional `outcome` success/failure filter), `combatStarted` (names the NPC the fight started with, or the room), `lockedExitTried` (the room the door is in), `playerLeftRoom` (the room left; runs where the player arrives, so NPC lines and face-to-face effects are refused). |
| WF-22 | **Done 2026-11-10.** Publish and delete were check-then-write. | Publish (validate + promote), delete (reference check + delete) and draft saves now run in one transaction under a Postgres advisory lock on the corpus; the cache reloads after commit. Tests: a draft save waits while another storyteller holds the corpus; the lock is re-entrant. |
| WF-23 | **Done 2026-11-10 (backend).** Pickers made one request per definition. | **Owner decision:** references endpoint only, no dashboard cache. `GET /api/admin/content/references` returns every encounter (rooms, NPCs, items, inspectables), mission (objectives), scene (nodes), test, stat block, place and map room (id, access, NPCs with their stat block and conversation) in one response. The redesign's pickers use it. |
| WF-24 | **Done 2026-11-10.** Test coverage holes. | Playthroughs added for `encounterEntered`, `itemPickedUp`, `missionAccepted` through the Johnson, a condition holding a trigger back without spending it, a repeatable place trigger, two copies of one event racing on different queues (a job's listener fires once), and a trigger's scene not replacing an open conversation. `npcDefeated` is covered by the offstage playthrough (event raised directly, not from a won fight). |

### 1.3 Job sites are map rooms (WF-25)

*Built, committed, and how the game works today. Workstream 8 replaces the
private copy per run, the `Private` access type and "job content lives in
private rooms" with one shared map.*

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
  placeholder. Now planned as workstream 9.
- **Migration.** The shipped Gang Warehouse encounter is converted to map rooms
  (positions assigned on an unused part of the map) with its run content
  attached to those rooms, and the warehouse playthrough tests keep passing.

**Backend done 2026-11-10** (committed): the Private access type on map rooms
(content keys too, until WF-27 replaced them with ids), the warehouse moved onto the map,
run copies made from map rooms with their exit flags, walk-in entry for
runners on the run and locked doors for everyone else, publish-time site
checks, and a delete guard for site rooms. Also done the same day:

- **Johnsons in public rooms.** World NPCs are placed, edited and removed from
  Places (`/api/admin/world/rooms/{roomId}/npcs`), only in Public map rooms, with a
  stat block, name, description, optional conversation (whose bound stat block
  must match) and starting awareness. Audited; removal is refused mid-conversation.
- **Migration.** The `JobSitesAreMapRooms` migration written that day never
  shipped: WF-27's `GuidContentKeys` replaced it before it was committed, and
  development databases are recreated instead of migrated (1.3b).
- **Exit list.** A door into a job site's private room shows as locked to
  anyone not on that job.

The World Forge redesign (Places / Jobs / Library) is built on this model, so
WF-25 lands first. The redesign mockups live in the "World Forge Redesign"
canvas.

### 1.3a Events belong to rooms (WF-26)

*Built and committed. Workstream 8 keeps this model; only "an event in a run's
copy" goes away, leaving the public-room rule for every room.*

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

**Backend done 2026-11-10** (committed):

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

Done 2026-11-12: a retired job no longer holds its events (see the end of
1.3c). (Renaming
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

**Backend done 2026-11-10** (committed):

- One migration, `GuidContentKeys`, replaces the never-committed
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

**Phase 1 done 2026-11-11** (front end only; committed in `750fbc5`):

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

**Phase 2 done 2026-11-12** (front end and API; committed in `750fbc5` and
`6d0c4b5`):

- **Jobs builder** at `/admin/forge/jobs/:id/:step`. A new job is saved at
  once as three drafts — the job, its site, and the Johnson's conversation —
  and edited in six steps that share those drafts, so moving between steps
  keeps unsaved edits and one Save writes them all:
  1. *Johnson* — job name, summary, repeat; place a new world NPC or give the
     job to one already standing in a public room.
  2. *Pitch* — the Johnson's conversation as a script of parts and replies
     (conditions, an optional roll with pass/fail branches, effects, where it
     goes next).
  3. *Site* — pick Private map rooms on the map (or build them), the entry
     room and the public way in; reachability and doors are shown.
  4. *Events* — the job's listeners, each in a site room or a public room
     (fire-once and cover lines here), and the prompts they open.
  5. *Objectives · Pay* — ordered objectives, karma and nuyen, and the job's
     items.
  6. *Go live* — the builder's own checks with one-click fixes (an offer not
     limited to "while available"; a deliver objective with no hand-over
     reply), then the server's check, then publish, retire and delete.
  The mockup has five steps; *Events* is added because it had nowhere to write
  a job's listeners. Replies have no separate "answer" line (the content model
  has none: a reply leads to a part whose text is the answer). Playtest and
  the graph view are inert buttons; the mockup's "Editors" presence panel is
  left out rather than shown with invented data.
- **Which conversation is a job's pitch** is not stored: it is the one with an
  `acceptMission` effect naming the job. Its Johnson is any world NPC bound to
  that conversation.
- **API added.**
  - `POST /api/admin/content/validate`, `/publish` and `/delete` take a set of
    definitions. A job, its site and its Johnson's conversation point at each
    other, so none can pass the publish gate — or, once live, the delete gate
    — alone. A set is checked as one candidate corpus and promoted or deleted
    together, under the corpus lock.
  - `DELETE /api/admin/world/exits/{id}`, audited (`RoomExitDeleted`). Refused
    when it would leave a live job site failing the map check.
  - `PUT` / `DELETE /api/admin/world/rooms/{roomId}/interactables/{id}`,
    audited; removing one also removes who had discovered it.
  - The references endpoint lists each world NPC's stat block and
    conversation, which is how the Library counts world NPCs under "Used by"
    and how Jobs finds a job's Johnson.
  - A world NPC may be bound to a conversation that exists only as a draft
    (a Johnson is placed while their job is still being written). They have
    nothing to say until it is live.
- **Fixed from phase 1:** building a room beside another failed with a 409.
  The server already joins a new room to its same-layer neighbours both ways,
  and the screen then created those exits again. `forge/places/exits.ts` now
  reads the map after the build and corrects it to what was asked (flags, or
  removing the way back for a one-way door).
- Tests: backend 970 passing and 3 skipped (set publish/delete, exit delete
  and its site guard, inspectable edit/remove, draft-conversation binding);
  front end 492 in 46 files, of which Jobs 14, Places 14, exits 4, map content
  and library usage 5. Front end re-run 2026-11-12 after the last commit; the
  backend figure is from the full run on the same code.
- Checked end to end against the real API: a job created, a Johnson placed,
  a site built from two new rooms, an item and four objectives added, the
  missing hand-over reply caught and fixed, the job put live as one set,
  retired, and deleted with its conversation and site; an exit and the rooms
  then removed from Places.

**Gaps closed 2026-11-12** (front end and API; not yet committed):

- **A Johnson is edited and moved from Jobs.** The Johnson step has an Edit
  button per Johnson: name, description, stat block, starting awareness and
  location (any Public room). Their conversation stays the job's pitch, and a
  changed stat block is written onto the pitch as when they were first given
  the job. Places' NPC dialog is the same component and can move NPCs too.
  - API: `POST /api/admin/world/rooms/{roomId}/npcs/{npcId}/move`
    (`{ toRoomId }`), audited (`WorldNpcMoved`). The NPC keeps their id,
    conversation, damage and awareness. Refused into a room that is not a
    Public map room, mid-conversation, while their room has a fight, and while
    a live listener names them (listeners name an NPC in the room they stand
    in, as removal already required).
  - The dialog saves the edit first and then moves, so a refused move leaves
    the edit saved and can be retried.
- **Deleting a job takes its prompts and deals with its Johnson.** The
  confirm in *Go live* lists the prompts the job's events open. Those nothing
  else uses (another job's or a room's events, an NPC on a site or the map;
  draft and live copies both count) are deleted with the job unless unticked;
  the others are named with what still uses them. The Johnson is removed from
  the map (default) or left standing with no conversation. That runs after the
  job's set delete, because the job's own listeners may name the Johnson; if
  it fails, the job is still gone and the Jobs list says what was left.
- **A retired job holds no events.** The one-job-per-event rule (WF-26) skips
  retired jobs and jobs whose site is retired, so a live job may take an event
  over. The retired job's listeners stay in the document and still reach runs
  already under way. Publishing it again is refused if the event has been
  taken. Retirement is store metadata, so the loader is now given the store's
  retired ids by the provider, the publish gate (minus what is being
  published) and the delete gate. A runner with an open run on the retired job
  and one on the job that took its event over gets both reactions.
- **API types.** `npm run gen:api` is rerun, and `forge/api.contract.ts`
  checks every Forge request type against the generated schema at compile
  time (verified to fail on a mismatch). The OpenAPI document declares no
  response bodies (the endpoints return untyped results), so response types
  and the content model in `forge/model.ts` remain hand-written.
- Tests: front end 497 in 46 files (new: Johnson edit and move, delete with
  prompts and Johnson both ways, the Johnson failure notice, a refused move in
  Places); backend Application 593 passing and 2 skipped (new: a retired job
  holds no events). New Infrastructure tests — moving a Johnson, moves refused
  into a site, mid-conversation and while a listener names them, and a
  retired job's event taken over and not given back — are written but were
  not run: Docker was not answering, so the Testcontainers suites (and the
  Api tests) have not been run on this change.

**Still not offered** (the UI says so where it matters):

| Gap | Why |
| --- | --- |
| Items cannot be placed in a public room (only on job sites). | Decided 2026-11-12 in workstream 8: a job item is one object in the shared world, taken by whoever gets there first, and it returns when the job resets. Built with 8. |
| Rooms have no image. | Planned as workstream 9. |
| Playtesting a conversation or a job; the conversation graph view. | Not built; shown as disabled buttons. Left out of the 2026-11-12 gap work by the owner (no playtesting). |

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

State 2026-11-12: the authoring half was done once through the UI against the
real API (a job with a Johnson, a two-room site, an item, four objectives; put
live, retired, deleted). The job was never played with a character, so pay,
the ledger and history are unchecked. Events, prompts and editing a pitch
reply were covered by component tests only.

Workstream 8 changes what a job is (no site, shared rooms, a deadline, a
reset), so this scenario should be rewritten for that model and run after 8,
not before.

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

**Outcome:** more than one player character can take part in the same job and
the same combat.

**State 2026-11-12:** the owner answered most of this workstream while
designing workstream 8. Taking part in the same job is crews (8.1), built in
8.4 phase 5. Taking part in the same combat is postponed to the combat
milestone. Nothing here is built.

What exists: `EncounterInstance` holds a participant collection
(`EncounterParticipant`), the command queue serializes per instance, combat
tracks participants generically but admits one player
(`combat.single-player-encounters`), and presence and chat are room-scoped.
Workstream 8 removes the encounter instance.

The original questions, and where each now stands:

| Question | Answer |
| --- | --- |
| Forming a group | A crew: a crew id on the character, at most 5, joined by slash command (8.1). |
| Mission ownership | One acceptance per character, stamped together for a crew. Only the character holding what the Johnson asked for can turn in; a job with nothing to hand over completes when any member speaks to the Johnson (8.1). |
| Rewards | Full Karma to each member; nuyen split between members still in the crew at turn-in (8.1). **Open:** whether negotiated pay is one roll for the crew. |
| Objectives and items | An objective counts for the crew whichever member does it. The item is one object and can be handed to another character (8.1). |
| Joining and leaving | Leaving is allowed during a job, joining is not. A downed member keeps their run (8.1). |
| Decisions and timers | **Open:** who answers a prompt a job opens when several crew members are present. |
| Size limit | 5. |

Left for the combat milestone:

- Several players in one fight, and how an NPC picks a target among them.
- The defeat path for a partial wipe.

Engineering notes that still apply:

- `SceneSession` is one row per character; a shared prompt needs a defined owner.
- `TriggerFire` records fire-once per character and mission instance, which
  stays correct with one acceptance per character.

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

What exists: `RoomAccessType` has `Public`, `Instanced` (a run's copy) and
`Private` (a job site's room); the last two belong to job sites, not to
ownership, and workstream 8 removes them. Exits have hidden and locked flags;
movement is server-authoritative and validates the exit. No ownership or
invitation checks exist.

Settled by workstream 8 (2026-11-12): a player's room is marked by a `private`
state on the exits into it, with the owner recorded on the room. Until this
workstream is built, a `private` exit simply refuses entry.

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

## 8. One shared world

**Outcome:** one map that jobs happen in. No room is copied for a run, and
runners compete for the same job in the same rooms.

**State:** owner decisions recorded 2026-11-12 from a design discussion (8.1).
Phase 1 (exit states) is built (2026-11-16, not yet committed; see 8.4). The
rest of the build plan is in 8.4 and nothing blocks it. Room images, first planned
here, are workstream 9.

What it replaces: today a job site is `Private` map rooms nobody can stand in,
and accepting a job copies its rooms, exits, NPCs and items into a private
encounter instance (1.3, WF-25).

### 8.1 Decisions

**The map**

- Rooms are only ever map rooms. The run copies, the `Private` access type and
  the `mission-enter` step go away. A character's location is always a real
  room, and everyone in a room sees each other.
- A job references rooms and NPCs that already exist on the map. It does not
  own a site.

**Exits**

- Each exit carries a state array. Values: `locked`, `hidden`, `private`,
  `restricted`. An empty array means open (there is no stored `unlocked`).
  This replaces the exit's separate hidden and locked flags.
- Door state is shared world state. There is no per-run door state.
- A character is stopped from entering only by `locked`, `hidden` or
  `restricted`.
- `restricted` stops entry and never stops leaving. Exits are one-way rows, so
  it is set on the exits leading into a room. A world admin can set it by hand,
  and combat sets it on the room when it starts and clears it when it ends.
  Leaving stays possible so chases work.
- `private` means the room beyond is owned by a player (a future player home,
  workstream 7). The owner is recorded on the room. Not built now.

**NPCs**

- NPCs are world NPCs. Jobs do not own NPCs.
- Each has one of three dispositions (decided 2026-11-12, replacing the
  earlier two-way flag):
  - **Passive:** cannot be fought.
  - **Hostile:** can be fought, and fights back once alerted, but does not
    start it. A security guard.
  - **Aggressive:** all of that, and attacks a character as soon as it notices
    them. An insect spirit.
- A defeated NPC stays down until the job that uses it resets. One that no job
  uses returns 24 hours after it was defeated.

**A runner going down** (decided 2026-11-12)

- Stun damage that fills its track rolls over into physical. A runner whose
  physical track is full is **incapacitated**; physical damage beyond that
  fills the overflow track, and a runner who exceeds it is **dead**.
- An incapacitated runner can be healed, is still paid if their job is turned
  in, and can be carried by other characters, drones or vehicles.
- A dead runner is paid nothing and is archived on the player's persona page
  (see "Permanent character death" under Not in v1). The body can still be
  carried and moved; it disappears after 24 hours.
- **The death threshold follows the core rules:** the physical overflow track.
  Owner decision 2026-11-12. When it is built, add the entry to
  `SR5_RULE_DECISIONS.md` with the PDF page; it has not been looked up yet.
- None of this is built in workstream 8. See "The combat milestone" below.

**The combat milestone** (owner, 2026-11-12)

Combat needs refining as a whole and gets its own milestone. Workstream 8 does
not change the combat system. Everything here is decided and waits for it:

- the three NPC dispositions, including limiting attacks to Hostile and
  Aggressive NPCs and Aggressive NPCs attacking on sight;
- incapacitation, the overflow track and death;
- carrying a downed or dead character;
- several players in one fight.

Until then combat behaves as it does today: any NPC can be attacked, `hostile`
stays a true/false on the stat block, and a fight has one player.

**Fights** (decided 2026-11-12)

- Several players in one fight is postponed; combat mechanics need refining
  first. Until then a fight has one player and anyone else in the room,
  crewmates included, looks on.

**Jobs**

- Several runners can accept the same job. Only the first to finish completes
  it and is paid; the others are told someone got there first.
- A runner's own state is only their objective progress.
- A job has a deadline in real hours, set by its author (expected 2–4, longer
  for harder jobs). It is one clock per job: it starts when the first runner
  accepts, and everyone who accepts later works to the same time.
- Job states: available → open (accepted, deadline running, others may still
  join) → completed or expired.
  - **Completed:** the Johnson stops offering it. Aftermath runs for 24 hours,
    then NPCs, doors and the item return and the job is available again.
  - **Expired** (deadline passed, nobody finished): every run fails and the job
    resets immediately, with no aftermath.
- **Aftermath text.** The author writes stages on the job as "from hour X, show
  this", shown when a character enters the room and chosen by the time since
  completion. The owner's example: 0–2 h bodies and a wrecked room; 2–12 h
  security teams investigating; 12–20 h cleaned up; 20–24 h the room's plain
  description; reset at 24 h. This needs no timers: it derives from the
  completion time.
- **The job item** is one world object. Whoever takes it has it; whoever
  delivers it is paid. A reset recalls it from wherever it is, including a
  character's inventory.

**Crews** (decided 2026-11-12; answers workstream 3's "forming a group")

- A character can belong to one crew, recorded as a crew id on the character.
- When a member accepts a job, every character with that crew id is on the job
  too, without accepting it themselves.
- A crew holds at most 5 characters.
- **Pay.** On a successful turn-in each crew member receives the full Karma;
  the nuyen is split between them.
- **Turn-in.** Only the character actually holding what the Johnson asked for
  can turn the job in. Being in the crew is not enough. This is deliberate: a
  member can take the item, hand it to a rival who has accepted the same job,
  and leave the crew.
- **A job with nothing to hand over** completes when its objectives are done
  and any member of the crew speaks to the Johnson again. Objectives count for
  the crew whichever member does them.
- **A member who has left is not paid.** The nuyen is split between the members
  still in the crew when the job is turned in.
- **Failure.** When the job expires the whole crew takes the penalty, and each
  member takes it in full. It is not divided, so a crew is not a way to dilute
  failure.
- **Membership during a job.** A character can leave a crew while its job is
  open. Nobody can join one until the job is over.
- **Acceptance belongs to the character, not the crew.** When a member accepts,
  every member gets their own acceptance with the same timestamp. Leaving the
  crew does not remove it: the character is still on the run.
- **Who is penalised.** When a job expires, the penalty goes to the characters
  holding the earliest acceptance, whether or not they are still in a crew.
  Leaving before the deadline therefore escapes nothing. For the build: store
  on each acceptance which accepting group it came from, so the earliest group
  is found by that and not by comparing timestamps.

**Handing an item to another character** (part of this workstream)

- A character can give an item they hold to another character in the same
  room. The betrayal above depends on it.
- It does not exist. Checked 2026-11-12: `giveItem` is only a conversation
  effect that hands an item from a job site to the runner; there is no
  player-to-player action.
- Giving nuyen to another character is not part of this. The owner expects it
  later, as how a betrayer is paid in advance.

**Intended play.** A loud team starts combat, which restricts the room and
keeps rivals out, at the price of the fight. A quiet runner is slower but need
not fight. An ambush has to be in the room before combat starts.

**Job supply** is a game-design matter, not architecture. The owner's working
figure is jobs numbering at least 80–90% of active players, so there is
competition.

### 8.2 Decided, deferred

- **Failure penalty.** When a job expires, the runner who accepted it first —
  and everyone who accepted with them as a crew, each in full — takes a
  penalty: the Johnson likes them less, or they lose nuyen. Runners who
  accepted later take none. The exact penalty is undecided.
- **Taking the item from another player**, by the Pickpocket skill or by
  defeating and looting them. Neither exists yet.
- **Player homes** (`private`, workstream 7).

### 8.3 Open points for the plan

- **Farming through expiry: not a concern.** An immediate reset on expiry
  brings defeated NPCs back after 2–4 hours instead of 24. Owner, 2026-11-12:
  defeating NPCs gives no reward; Karma and nuyen come only from turning a
  finished run in to the Johnson. Keep it that way, or this reopens.
- **Absent crew members** (a consequence, not a question). A member who is
  offline when the job is accepted is on it like the rest: paid on success,
  penalised in full on failure.
- **Events and conversations.** Answered by the plan (8.4): a job's listener
  in any room fires for a runner who has that job open, privately, with a
  cover line for everyone else; fire-once stays per character and acceptance.

### 8.4 Build plan

Written 2026-11-12 from a read of the code. It names what changes and in what
order; it is not a line-by-line design. Section 8.5 lists what the owner has
answered and the working rules it rests on.

**What the code does today** (the parts this replaces)

- `StateChangeApplier` handles `EnterEncounterChange` by copying the site's map
  rooms, exits, NPCs, items and inspectables into rows tagged with an
  `EncounterInstance`; copies carry `SourceRoomId` / `SourceNpcId` /
  `SourceInteractableId` so events can be matched back to what was authored.
- `MovementStore` sends a runner who walks into a `Private` room to the
  `mission-enter` action (via `IRunAccessPolicy`); everyone else is told the
  door is locked. `mission-exit` and `mission-defeat` move them back out.
- `GameScopeResolver` gives every room of one run a single command queue.
- `EncounterLifecycleStore` and `EncounterExpirationService` abandon a run
  nobody has touched for 15 minutes.
- `MissionInstance` is already one row per character, with its own objectives.
  That is the "acceptance" in 8.1 and it stays.
- `TriggerEngine` already has the public-room path this design needs: a job's
  listener in a public room fires for a runner who has that job open, reaches
  them privately, and shows everyone else a cover line (`OthersSee`).
- Combat (`CombatEngine`, `InMemoryCombatTracker`) is one fight per room, held
  in memory, with exactly one player in it
  (`combat.single-player-encounters`).
- `NpcTemplate.Hostile` exists and means "starts a fight when alerted". The
  attack affordance is not limited by it.
- Doors never change in play: there is no unlock or lockpick action.

**Target data model**

| Thing | Change |
| --- | --- |
| `Room` | Drop `EncounterInstanceId` and `SourceRoomId`. `RoomAccessType` keeps only `Public` until workstream 7. |
| `RoomExit` | Replace `IsHidden` / `IsLocked` with one `States` array column (Postgres `text[]`), values from a closed list. Migration maps the two flags into it. |
| `NpcInstance` | Drop `SourceNpcId`. Every NPC is a map row. Add `DefeatedAtUtc`. |
| `RoomInteractable` | Drop `SourceInteractableId`. |
| `WorldItemInstance` | Drop `EncounterInstanceId` and `MissionInstanceId`; add `MissionId` (the job that owns it). One row per job item, made when the job goes live and put back on reset. |
| `EncounterInstance`, `EncounterParticipant` | Removed. |
| `MissionInstance` | Add `AcceptGroupId` (shared by everyone who accepted together) and the crew id at acceptance. |
| New `JobState` | One row per job: status (available, open, aftermath), opened / deadline / completed times, the first accept group, and a version for optimistic concurrency. |
| `Character` | Add nullable `CrewId`. New `Crew` table. |
| Content: `Encounter` kind | Retired. Its room list and items move onto the job; its NPCs and inspectables become map rows. A job then goes live as itself plus its conversations. |
| Content: `Mission` | Gains room ids, items, the ids of the NPCs it resets, deadline hours and aftermath stages. Loses `EncounterId` and `EntryLinkRoomId`. |
| Objective kinds | `EnterEncounter` / `ExitEncounter` become "reach room". `PickUpItem` and `DeliverItem` stay. |
| Trigger events | `EncounterEntered` is removed; `PlayerEnteredRoom` covers it. |

Two design points the model depends on:

- **Combat's `restricted` is computed, not stored.** Combat lives in memory, so
  a stored flag would stay set for ever after a server restart mid-fight. An
  exit is restricted if its array says so (an admin set it) or its destination
  room has a fight in the tracker.
- **"First to finish" is decided by the database.** Runners in different rooms
  are on different command queues, so completing, expiring and resetting a job
  all take the `JobState` row under its version check. The loser of a race is
  refused cleanly.

**Phases**

Each phase ends with the full checks in the README passing. Phases 2 to 4 are
not playable apart (after 2 alone, a defeated guard never returns), so they are
built in order on one branch and released together.

1. **Exit states.** Independent. Column and migration; `MovementStore` reads
   the array (hidden, locked, restricted refuse entry; `private` refuses until
   workstream 7); world editor commands, endpoints and `JobSiteMap` models take
   states in place of the two booleans; Forge `places/ExitsSection.tsx` and
   `places/exits.ts`; play `RoomDetails.tsx` and `api/roomSession.ts`.
   `LockedExitTried` keeps firing. Waits on nothing.

   **Done 2026-11-16** (not yet committed):
   - `RoomExit.States` (`text[]`, check constraint on the four values, default
     empty); migration `ExitStates` copies the old flags into it before
     dropping them (and maps back on rollback, with restricted and private
     going back as locked). Values and their stored order live in
     `SeattleByNight.Domain.ExitStates`.
   - Movement: hidden → `ExitHidden`; locked → `ExitLocked` (still raises
     `lockedExitTried`); restricted → new `ExitRestricted` ("You can't get in
     that way right now."); private → new `ExitPrivate`. A restricted door into
     a room leaves the way out untouched, because the way out is its own exit.
     Combat setting restricted is phase 4.
   - Room view: `RoomExitSummary.States` replaces `IsLocked`; hidden doors are
     still left out, and a door into a job site the runner is not on still
     shows as locked (until phase 2 removes sites).
   - World editor: create/update exit requests take `states` instead of
     `isHidden`/`isLocked`; unknown values are refused with the list of valid
     ones; states are stored de-duplicated in a fixed order; the audit record
     carries them. `JobSiteMap` keeps its two booleans, read from the array
     (it goes in phase 2).
   - Front end: Forge exit dialog has a checkbox per state with what each
     means, chips per state, and the map draws restricted and private doors as
     locked. Play: exits are disabled with their state in the label, and `/go`
     refuses a shut door locally in the server's words. Helpers live in
     `src/api/exitStates.ts`. `gen:api` rerun; the request contract check
     caught the old schema until it was.
   - Tests: Domain 11; Api 34 in the movement and world-editor classes (new:
     restricted refuses entry but not leaving, private refuses, unknown state
     refused); Infrastructure 43 across movement, job-site doors, room events,
     the world editor and room deletion; front end 501 in 47 files. Full
     backend run afterwards: Domain 11, Application 593 (2 skipped), Api 158,
     Infrastructure 217 (1 skipped).
   - Checked in the running app on the dev database (migration applied on
     start-up): a door set to restricted in Places shows its chip and a shut
     link on the map; in play the exit is disabled as "(restricted)" and
     `/go east` answers "You can't get in that way right now." The door was
     put back to open afterwards.
2. **Remove the copies.** The large one.
   - Content: fold the site into the job (models, `GameContentLoader`,
     `ContentReferences`, publisher set rules); replace `JobSiteMapValidator`
     with a check that a job's rooms, NPCs and item rooms exist on the map
     and that no item belongs to two jobs;
     rewrite the shipped warehouse job and `GameContentSeeder`.
   - Engine: delete the materialising branch of `StateChangeApplier`, the
     enter / leave changes, `mission-enter`, `mission-exit`, `IRunAccessPolicy`,
     the lifecycle store and expiration service, and the `Private` branches in
     `MovementStore` and `RoomSessionReader`; `GameScopeResolver` returns the
     room id; `TriggerEngine` keeps only its public-room path; `RoomContentStore`
     and the world editor lose their copy / instance guards; items are taken
     and granted against the job's single row.
   - Forge: the Site step becomes "Rooms" (pick rooms, pick NPCs already on the
     map, place items); `places/SiteContents.tsx`, the site-room colours and
     the reachability check go; the set that goes live shrinks to job and
     conversations.
3. **The job clock.** `JobState`, written by accept, turn-in and a background
   sweep that replaces the expiration service.
   - Accept opens the job and starts the deadline; later accepts join it. The
     Johnson's offer conditions read job state as well as the character's
     history.
   - Turn-in completes it for the winner, fails every other open run on that
     job with no penalty, and starts aftermath.
   - The sweep expires a job at its deadline (all runs fail, immediate reset)
     and resets one 24 hours after completion: NPC damage and awareness
     cleared, the item row put back, discoveries of the job's hidden things
     forgotten.
   - Aftermath text is added to the room view from the completion time; no
     timer.
   - Forge: deadline and aftermath stages on the job; play: the deadline in the
     job tracker.
4. **Fights in shared rooms, without changing combat.** Two things only:
   - `MovementStore` treats a room's inbound exits as restricted while the
     combat tracker holds a fight there. It reads the tracker; combat code is
     untouched.
   - `CombatEngine` today queues `mission-defeat` when the player goes down
     inside a run copy. Phase 2 removes run copies, so that one hook is
     deleted with them. A downed runner then stays in the room, as a downed
     character in a public room does today, and their run is not failed.
   Dispositions, death and group fights are the combat milestone (8.1).
5. **Crews.** Crew table and membership endpoints (leave always; join refused
   while any member has an open job); accepting stamps every member with the
   same accept group; objective progress is mirrored across the group; turn-in
   requires holding the item, or any member for a job with none; Karma to each
   member, nuyen split between members still in the crew; a new give-item
   action between characters in the same room. The failure penalty reads the
   job's first accept group when it is built.

**Tests that are rewritten, not just adjusted:** `WarehousePlaythroughTests`,
`JobSiteMapRoomTests`, `JobSiteMapValidatorTests`, `MissionEngineTests`,
`MissionPersistenceTests`, `MovementTests` / `MovementPersistenceTests`, the
trigger playthroughs, and on the front end `JobsPage.test.tsx`,
`MapContent.test.tsx` and `exits.test.ts`. New: two runners racing one job, an
expiry, a reset, aftermath by hour, a restricted door during a fight, and each
crew rule in 8.1.

**Docs to update when it lands:** `PROJECT_CONTEXT.md` (instances, WF-25, the
"only materialized content is isolated" divergence, scopes), 1.3 and 1.3c
here, and workstream 3.

### 8.5 Decisions the plan needs

Answered 2026-11-12 (recorded in 8.1 unless noted):

- **Disposition** is three values, not the one `Hostile` flag. Deferred to the
  combat milestone. Build then: replace the stat block's `hostile` boolean
  with a disposition (true maps to Hostile, false to Passive); Aggressive
  opens combat when a character it notices enters.
- **No combat changes in workstream 8.** Death follows the core rules'
  overflow track, also in the combat milestone.
- **NPCs no job uses** return 24 hours after defeat.
- **A defeated runner** is incapacitated, stays down until healed, and keeps
  their run.
- **Group fights** are postponed; one fighter, others look on.
- **Joining a crew** is a slash command.
- **Existing job content** is not converted: rewrite the shipped warehouse job
  and discard drafts.

Nothing blocks the build. Two working rules follow from sharing (below) and
are the owner's to overrule.

Answered later on 2026-11-12:

- **Jobs may share rooms and NPCs, never items.** Publishing refuses an item
  used by two jobs.
- **Crew invitations** are a slash command aimed at a character in the same
  room, who joins with a slash command. Leaving is one command.

Working rules that sharing makes necessary (proposed, not yet confirmed):

- **A shared NPC** comes back when any job that uses it resets, or 24 hours
  after it was defeated, whichever is first.
- **No NPC returns to a room while an online character is in it, or while it
  has a fight in progress** (owner, 2026-11-12). Its return is put back an
  hour and tried again. This holds for every kind of return: a job's reset,
  an expiry, and the 24-hour timer. The rest of a reset (doors, the item,
  other rooms' NPCs) goes ahead on time. Build: the sweep in phase 3 checks,
  per NPC, for a live play session in the room and for a fight in the combat
  tracker, and stores the NPC's own next-return time.
  - "Online" means a play session that has not ended or timed out, so an idle
    character stops counting once their session expires.
  - **At most two delays for a watching character.** After the second, the
    NPC returns even with someone in the room, so nobody can hold a job site
    by standing in it.
  - **A fight always delays.** While the room has a fight in progress the NPC
    never returns, however many delays there have been; it is tried again
    each hour until the fight is over. A fight ends by its own rules (and its
    turn timers), so this cannot be held open indefinitely.
- **A shared room in aftermath** shows the text of the job completed most
  recently.

For the combat milestone, not this workstream:

- **What "notices" means for an Aggressive NPC.** Working assumption: a
  character entering its room, unless a sneak attempt succeeds.
- **Who an Aggressive NPC attacks** when several characters are in the room.

---

## 9. Room images

**Outcome:** a room can have a picture, set by a world admin and shown to
players in the room.

**State:** decided 2026-11-12; nothing built. Independent of every other
workstream. Room images only: character portraits are later, and need
moderation that this does not.

What exists: nothing. There is no file storage, and the Forge shows a
placeholder where the image would go.

### 9.1 Decisions

- Hosting is the owner's SteamOS server in a Docker container, with 25–50 GB of
  hard-drive space to spare.
- Images are files in a host folder bind-mounted into the container. The folder
  goes under `/home`, which SteamOS updates leave alone.
- Upload is admin-only and audited. The server re-encodes to WebP at about
  1280 px wide, strips metadata, caps the upload at about 5 MB, and never
  serves the bytes that were uploaded.
- Files are named by content hash and served as static files with a long cache
  lifetime. The room row stores only the key.
- Storage sits behind an interface in Application so it can move later.
- Back the folder up alongside the database dump.

### 9.2 Build

- `ImageKey` column on `Room`, carried in the room session and world graph
  models.
- `IRoomImageStore` in Application with a disk implementation in
  Infrastructure.
- Admin upload and remove endpoints, audited, with static serving of the
  stored files.
- Forge: upload and remove in `places/RoomSection.tsx`, replacing the
  placeholder.
- Play: the image shown in `RoomDetails.tsx`.
- Docker: the bind mount and the config key naming the folder.

### 9.3 To settle before building

- **The image library's licence.** ImageSharp has a split licence; SkiaSharp
  is MIT. Check before choosing.
- **Removing a room that has an image**, and replacing an image: whether the
  old file is deleted or left for the backup to carry.

---

## Not in v1

Considered and deliberately left out. Each needs its own scoping before it is
picked up.

| Item | Note |
| --- | --- |
| Combat milestone | Owner decision 2026-11-12: combat is reworked as its own milestone, not piecemeal. What is already decided for it is listed in 8.1 ("The combat milestone"): NPC dispositions, incapacitation and death, carrying, and several players in one fight. |
| Permanent character death | Owner decision 2026-11-12, for later; part of the combat milestone. A player character who dies is archived for good: it stays on the owner's persona page, greyed out, viewable but not playable, and its character slot is freed. Dying is defined in 8.1 ("A runner going down"): damage past the physical overflow track, per the core rules. A dead runner is paid nothing; the body can be carried and disappears after 24 hours. Still to define: what happens to a job item the character was holding, and their place in a crew. Carrying an incapacitated or dead character (by characters, drones or vehicles) is part of this and not built. |
| Moderation tools | The `Moderator` role and `ModerationAccess` policy exist and are unused. |
| Staff Karma/nuyen awards | The ledger supports other sources; only mission rewards grant today. |
| SR5 catalog schema lock and digest enforcement | The catalog stays in its mutable pre-alpha phase. Lock it before real players create characters they expect to keep. |
| Further sourcebook imports | Chrome Flesh, Data Trails, Forbidden Arcana, and Street Lethal are registered and unimported. |
| Fuller combat | Grenades, called shots, suppressive fire, multiple attacks, interception, knockdown, full environmental tables, healing rules. Belongs with the combat milestone above. |
| Rigging, vehicles, and drones in play | Catalog data only. |
| Enforced quality and martial-arts effects | Display text today. |
| Career: augmentation installation, attachments, selling/refunds/transfers, lifestyle billing, Availability tests, reputation (Street Cred, Notoriety, Public Awareness) | Lifetime Karma is tracked so Street Cred can be derived later. |
| Storyteller approval queues and training time | Zero-Karma contact acquisition joins the gated list if this is ever built. |
| Player-facing city map | |
| PvP, mission spectators, persistent world consequences, factions, procedural missions | |
| Content version history and multi-admin review workflows | |
| Database backup automation | Operational, not a feature — but nothing backs up `postgres_data` today. Workstream 9 adds an image folder that needs backing up with it. |
| World snapshots and an owner tier above builder | Raised 2026-11-12 and not decided. Anyone with Forge access can delete map rooms, and the audit log cannot undo it. Proposed: an owner-only snapshot of the world (rooms, exits, NPCs, inspectables, content) that can be restored by id, an owner role for restore and destructive deletes, and a copy of the baseline in the repository. |
| Giving nuyen to another character | Expected by the owner (2026-11-12) as how a crew betrayal is paid for in advance. Handing over an item is in workstream 8; nuyen is not. |
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
