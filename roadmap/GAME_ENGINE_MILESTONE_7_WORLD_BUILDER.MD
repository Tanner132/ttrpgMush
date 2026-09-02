# Milestone 7 — Content Pipeline + World Builder

## Goal

Replace hand-authored, compiled-in JSON with a database-backed content pipeline and an
admin-facing World Builder UI, so that new missions — NPCs, dialogue, encounters, rooms,
items, objectives, and rewards — can be created, published, retired, and removed in a
running production game without code deployments or manual database surgery.

The Milestone 5–6 warehouse content proved the data model (§50: "hand-authoring the
gang-warehouse JSON **is** the admin-tool MVP"). This milestone builds the tool that
model was designed for. The builder is the **second consumer** of the content schema,
which is exactly when the roadmap's development principle says to generalize.

---

## Guiding Constraint — Compose, Don't Script

The builder lets admins **compose game content from an engine-defined palette**. It does
not let them define new engine primitives. Concretely:

* Each event **kind**, effect **kind**, condition **kind**, and objective **kind** —
  and the test-resolution rules — remain **code**. Adding a new kind is a small,
  additive engine change with tests that immediately becomes available to every
  author in the builder.
* Everything built *from* those kinds is **authorable content**: which NPCs exist,
  what they say, which triggers fire on which events, what scenes and prompts they
  open, which tests gate which choices (pools, opposed pools, limits, thresholds),
  what the objectives and rewards are, and how rooms, exits, items, and NPC
  placements fit together.

The flexibility bar: **most basic events and responses must be creatable without any
code change** (see the Trigger + Scene System below). Code is involved only when a
mission needs a *new palette entry* — a new way of mutating the world — and then the
change is one engine primitive, never per-mission code. Effects are the only things
that touch game state, and keeping them a closed, tested vocabulary is what preserves
the §23 invariant and the ledger/audit guarantees underneath everything.

---

## Primary Systems

### 1. Database-Backed Content Store (replaces embedded resources)

* Content moves from embedded assembly resources to a database-backed store behind the
  existing `IGameContentProvider` abstraction. Engines and appliers do not change.
* **Draft → Validate → Publish lifecycle.** Drafts are freely editable and invisible to
  the game. Publishing runs the full `GameContentLoader` validation suite (id
  uniqueness, reference integrity, exit directions, test references, dialogue graph
  reachability, effect mission ids) as a hard gate — invalid content cannot be
  published, and errors are surfaced in the builder with enough detail to fix them.
* **Published edits affect new instances only.** Encounter instances materialize real
  room/NPC/item rows at instantiation, so running missions are naturally isolated from
  content changes; they finish (or expire via the Milestone 5 grace-window sweep) on
  the definitions they started with.
* **Migration:** the current embedded `game-content-1.0.0` bundle is imported as the
  first published content set on startup (idempotent seed). The warehouse playthrough
  CI tests are switched to consume content through the database-backed provider,
  proving parity between the old path and the new one.
* **Authorization:** all builder endpoints are gated behind an admin/builder role.
  Players never see draft content, and content endpoints are absent from the player
  API surface.

### 2. Creation — Full Content Coverage (Requirement 1)

The builder can author every content type the vertical slice consumes:

* **Missions** — display name/description, repeatability rules (one-time / cooldown),
  entry link room, and the reward block (karma + base nuyen). Rewards continue to flow
  through the grant-once career ledger path unchanged.
  * *Corrected during implementation:* the **return room is not authored**. The engine
    records where the runner entered from at encounter entry
    (`StateChangeApplier.InstantiateEncounter`), which is strictly better than an
    authored constant — a mission taken from two places puts you back where you came
    from either way.
  * *Corrected during implementation:* **negotiable pay is not a mission flag**. Pay is
    negotiable exactly when a scene offers a `setNegotiatedPay` choice, which puts the
    negotiation where the conversation is and needs no second switch to keep in sync.
* **Objectives** — ordered objective list with kind (`ReachRoom` / `AcquireItem` /
  `LeaveEncounter` / `DeliverItem` — the current engine vocabulary), display text, and
  the item/room they bind to.
* **Encounters** — instanced room layouts: rooms with descriptions, exits (validated
  directions, connectivity check so no room is unreachable), item placements, and NPC
  placements.
* **Mission items** — the objective/delivery items (key, name, description) referenced
  by `AcquireItem` / `DeliverItem` objectives. *Corrected during implementation:* items
  are declared **inside the encounter fragment**, not as standalone content, so they are
  authored on the encounter screen alongside the rooms they lie in. An item with no room
  is declared but not placed — that is how a scene or trigger hands one over.
* **NPCs and their dialogue** — see the template system below for the NPC stat side.
  Dialogue graphs are fully authorable: nodes with spoken text, choices with labels,
  conditions (mission available/open/ready-to-turn-in, carrying item, not-yet-
  negotiated), optional skill tests with success/failure branches, and effects
  (accept mission, set negotiated pay, turn in, pacify, alert). The graph editor
  is the largest single UI lift in this milestone and warrants its own design pass
  (node-graph view with validation of dangling branches).
* **Interactions/actions** — what a player can *do* to an NPC or the environment is
  configured per placement from the engine's action palette: talk (bind a dialogue),
  attack, sneak-past-style test-gated interactions, item pickup. The builder also
  authors **test definitions** (dice pool composition from attributes/skills, opposed
  pool or threshold, applicable limit) so that dialogue choices, gated interactions,
  and trigger reactions reference admin-defined tests instead of the current
  code-defined development tests.

### 3. Trigger + Scene System — Event-Driven Content Without Code

The heart of the flexibility requirement: admins attach **triggers** to encounters,
rooms, and placements — `(event, conditions) → reaction` — so new interactions do not
require new engine actions.

* **Event palette (initial):** player enters room, player interacts with a placed
  object, item picked up, NPC spoken to, NPC defeated/pacified, mission accepted,
  encounter entered. These surface the engine's existing internal events (§24) to
  content authors; new event kinds are additive engine changes.
* **Reactions**, composable in sequence:
  * broadcast narration, NPC speech, or an emote to the room
  * open a **scene node** (below)
  * run an authored test with success/failure branches
  * apply effects from the palette — deal damage, start combat, alert/pacify an NPC,
    give or remove an item, complete or fail an objective, advance a dialogue
* **Scene nodes** — the dialogue node graph **generalized**: the same
  text-plus-numbered-choices structure (conditions, per-choice tests, branches,
  effects) detached from NPC conversation, so a trigger can present a prompt directly.
  Canonical example: entering the back hallway fires a trigger — *"A man steps out
  from around the corner and fires his gun. 1. Dodge  2. Block"* — where Dodge rolls
  an authored `Intuition + Reaction` test and Block rolls `Strength + Unarmed`, each
  with authored success/failure branches (take damage, combat starts, he flees).
  Dialogue graphs become a special case of scenes (a scene bound to an NPC), reusing
  one engine, one editor, and the existing numbered-choice UI and decision machinery.
* **Trigger discipline:** per-trigger fire-once vs. repeatable; per-trigger
  conditions (mission state, carried items) reusing the dialogue condition evaluator;
  everything flows through the universal action pipeline and lands in the audit log
  like all other actions; publish validation checks trigger references (events,
  scenes, tests, effect targets) like every other cross-reference.

The litmus test for this system: a "poison gas storeroom" (enter room → scene prompt →
Body + Willpower test → damage on failure), an ambush, a locked crate with a Logic test,
and a rival runner who talks or attacks — all creatable in the builder with zero code.

### 4. NPC Template System (Requirement 3)

Two-layer model, matching SR5 grunt design:

* **Base templates** — the mechanical stat block: attributes, skills/pools, initiative,
  armor/AP, weapons, condition monitors, hostility defaults. Authored once, reused
  everywhere. The current code templates (street ganger, Mr. Johnson) migrate to
  content as the first base templates.
* **Placed NPCs** — a mission/encounter NPC references a base template and overrides
  only **player-visible identity**: name, short/long description, dialogue binding,
  starting awareness/hostility, and placement room. Stats come from the template
  unless explicitly overridden.

Editing a base template affects future instantiations of every NPC built on it (new
instances only, per the publish rule above) — that is the time-saving point of the
system, and the builder makes the blast radius visible by listing dependent NPCs.

### 5. Retire + Delete Lifecycle (Requirement 2)

Requirement 2 is "get old jobs, NPCs, and missions out of the game without database
surgery." Two operations deliver that, and the distinction protects the ledger:

* **Retire (the workhorse).** Any published content can be retired at any time.
  Retired missions are no longer offered, assignable, or instantiable; retired NPCs
  stop appearing; retired dialogue choices stop being offered by the affordance gate.
  Running instances finish or expire naturally. Historical data — mission instances,
  career-ledger entries, grant-once receipts, audit records — is untouched, so a
  character's history never dangles. Retiring is instant, reversible (re-publish),
  and is what the "Delete" button does for anything that has ever been live.
* **Hard delete (drafts and the never-used).** Content that was never published, or
  that has zero historical references, can be truly deleted. The builder checks
  references before allowing it and says why when it refuses.
* **Rooms.** Encounter rooms are owned by their encounter definition — retiring or
  deleting the encounter handles them, and per-instance materialized rows are already
  archived (not deleted) by the Milestone 5 teardown. Deleting a **public world room**
  from the builder requires the referential check to pass: no exits pointing at it, no
  mission entry/return links, no characters currently located there (offer a
  relocation target when there are).

The invariant: **nothing an admin can click ever breaks a character's ledger, receipts,
or audit history.** "Gone from the game" and "erased from the record" are different
operations, and only the first is routine.

---

## Builder UI

Screens (Deck OS design mockups first, unwired, per the established design workflow;
then ported and wired screen by screen):

1. **Content dashboard** — all content by type and status (draft / published /
   retired), with publish validation results.
2. **Mission editor** — mission fields, objective list, reward block, encounter and
   room links.
3. **Encounter editor** — room grid/map with exits, item placement, NPC placement.
4. **NPC template library + placed-NPC editor** — base stat blocks, and the
   override-only view for placed NPCs.
5. **Scene/dialogue graph editor** — node graph with choices, conditions, tests,
   effects; covers NPC dialogue and trigger-opened scenes with one editor; inline
   validation for unreachable nodes and dangling branches.
6. **Trigger editor** — per-room/per-placement trigger list: event, conditions,
   reaction sequence, fire-once vs. repeatable.
7. **Test definition editor** — pools, opposed pools, thresholds, limits.

Server-side publish validation is the source of truth; the UI mirrors the same checks
inline where cheap, but passing the UI never substitutes for the publish gate.

---

## Suggested Build Order

The trigger/scene system is engine work and must exist before the builder can author
it; the content pipeline must exist before anything is editable at all.

1. **Content pipeline** — DB-backed store, draft/publish/validate, embedded-bundle
   migration, CI playthroughs on the DB provider.
2. **Trigger + scene engine** — generalize dialogue nodes to scenes, expose the event
   palette, add the new reaction effects (damage, start combat, give item), authored
   test definitions. Proven first in JSON content (extending the warehouse), the same
   way Milestones 5–6 proved their schemas.
3. **Builder UI** — design mockups first (unwired), then port and wire screen by
   screen against the live pipeline.
4. **Retire/delete lifecycle**, then the second-mission acceptance run.

---

## Explicitly Out of Scope

* New engine primitives authored as content — event/effect/condition kinds and test
  resolution stay a code-owned palette; no free-form scripting language
* Non-sequential / branching objectives (additive later if missions need them)
* Procedural or template-generated missions
* Collaborative multi-admin editing, locking, or review workflows
* Content version history / rollback UI beyond draft-vs-published
* Player-facing content discovery changes (job boards etc.)

---

## Example Demonstration

An admin opens the World Builder in a running game and, without touching code or the
database directly, authors **a second complete mission**: a new Johnson (built from the
Johnson base template with a new name, description, and dialogue graph including a
negotiation test), a new instanced encounter with rooms, a lookout NPC from the ganger
template, a retrievable mission item, four objectives, ledgered rewards — and at least
one **trigger-driven scene** that exists nowhere in code (e.g. an enter-room ambush
prompt with two test-gated responses and damage on failure). They publish it, play it
end to end with a normal player character — accept, run, hit the ambush, turn in, get
paid once — then **retire** it and confirm it vanishes from the game while the
character's ledger entries and mission history remain intact. Finally they hard-delete
a draft mission that was never published.

## Milestone 7 Definition of Done

> An admin can create, edit, publish, retire, and (where nothing references it) delete
> every content type the vertical slice consumes — missions, objectives, rewards,
> encounters, rooms, exits, mission items, NPC base templates, placed NPCs,
> scene/dialogue graphs, triggers, and test definitions — entirely through the World
> Builder UI in a running game; basic event-driven interactions (a triggered prompt
> with test-gated responses and consequences) are creatable with zero code changes;
> publish-time validation refuses broken content; published edits affect only new
> instances; retirement removes content from play without disturbing any character's
> ledger, receipts, or audit history; and the existing CI playthroughs pass against
> content served from the database-backed store.

*Corrected during implementation:* **"published edits affect only new instances" holds
for what is materialized, which is not everything.** Rooms, exits, items, NPC placements
and mission objectives are copied into the instance at creation and are genuinely
isolated. Triggers, scenes, NPC base stat blocks and mission definitions resolve LIVE
from the served document, so publishing an edit to those does reach runs already in
flight. That is deliberate for NPC templates (a template fix is meant to propagate
wherever a placement has not pinned the value) and it is what makes trigger and scene
authoring iterative rather than needing a fresh run to see a change. Making them
materialize instead is a real design decision with a real cost, not a bug fix.

*Corrected during implementation:* **retired content stays in the served document.**
Retirement is store metadata stamped onto the composed definitions as an `IsRetired`
flag, so an instance already running keeps resolving what it was built from; the flag is
what stops the content being OFFERED to anyone new. Retiring an encounter also retires
every mission that runs in it.

---

## Known gaps after the implementation review

Recorded rather than built, because each is a design decision rather than a defect:

* **World-room triggers are unauthorable.** Triggers hang off encounters and missions,
  and `playerEnteredRoom` needs a room KEY — which only encounter rooms have. Public
  world rooms are database rows with no content key, so an ambience trigger on a shared
  street ("Downtown, narrate") cannot be authored at all. Fixing it means giving public
  rooms stable content keys and giving world triggers a content home to live in.
* **"Object interact" is inspect-only.** `interactableInspected` is raised by the
  inspect action alone; sneak-past and other placement interactions raise nothing, so a
  trigger cannot react to them.
* **Publish and delete are check-then-write, not atomic.** A publish validates the
  corpus and then writes it, and a delete's reference check races a concurrent publish
  that adds a reference. Harmless at one admin; a real problem at several.
* **The dashboard runs the content loader once per pending-edit definition** plus once
  for the corpus, so listing the inventory is O(N) loader runs. Fine at this corpus
  size, worth a cache when it is not.
* **The builder's reference pickers make one request per encounter, mission and scene.**
  Same trade, same answer: a server-side references endpoint when the corpus outgrows
  it, not a smaller picker.
