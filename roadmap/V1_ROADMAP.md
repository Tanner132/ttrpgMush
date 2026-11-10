# Seattle by Night — v1 Roadmap

The single list of work remaining before v1. Scope was set by the project owner
on 2026-11-10. Current capabilities are described in
[`../README.md`](../README.md) and [`../PROJECT_CONTEXT.md`](../PROJECT_CONTEXT.md);
this file lists only what is not done.

v1 is seven workstreams:

| # | Workstream | State |
| --- | --- | --- |
| 1 | [World Forge fixes and gaps](#1-world-forge-fixes-and-gaps) | Defined; ready to build |
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
2026-09-02). These were confirmed or suspected at that date and have not been
re-verified since; confirm each against the code before fixing.

### 1.1 Defects

| ID | Area | Problem | Fix shape |
| --- | --- | --- | --- |
| WF-1 | Frontend | **Rename corrupts references through transient key collisions.** `EncounterEditor.renameRoom` and `SceneEditor.renameNode` retarget references by current string value on every keystroke. Renaming `storage` toward `entry-hall` passes through `entry` and then rewrites everything pointing at the real `entry` room. The result passes the publish gate, so the corruption is silent. | Track row identity instead of the current string: rename on blur/commit, or suspend retargeting while the new key duplicates another row's. Add a test with colliding names. |
| WF-2 | Frontend | **Kind-switch race in `useContentDraft.open`.** An in-flight fetch that resolves after a kind switch lands the old kind's fragment in the controller, so "Save draft" can write an encounter fragment as a Mission. | Capture a request token in `open` and drop stale responses. Add a test that resolves the fetch after the switch. |
| WF-3 | Backend | **Idle-scope reclaim can break per-scope serialization** in `GameCommandQueue`: between the sweeper's idle read and completing the writer, an enqueue can run on the orphaned consumer while a second consumer starts for the same scope. A brand-new scope is also instantly idle-eligible because `lastActivityTicks` starts at 0. | Re-check `Reclaimed` after a successful write; initialize `lastActivityTicks` at construction. |
| WF-4 | Backend | **Damage does not stack across a trigger's reactions.** `DamageTally` is created per `ResolveAsync`, and `TriggerEngine` calls that once per reaction against the same snapshot, so a `runTest` branch dealing 3P followed by an `applyEffects` dealing 2P commits +2, not +5. | Hoist the tally to `TriggerEngine.RunAsync` scope. |
| WF-5 | Backend | **`giveItem` neither raises `ItemPickedUp` nor advances an `AcquireItem` objective.** Content must hand-pair it with `completeObjective`, and no trigger can react to the grant. | **Owner decision:** make `giveItem` behave like a pickup (raise the event, advance the objective), or keep it inert and document the pairing as intended. |
| WF-6 | Backend | **Stale-event drop loses fire-once triggers** (suspected). Every stamped event is dropped if its subject has left the room, including progression events (`NpcDefeated`, `MissionAccepted`, `ItemPickedUp`). A player who moves in the gap permanently loses that trigger. | **Owner decision:** drop only room-anchored reactions (scenes, narration) rather than whole events, or re-raise subject-agnostic events in the subject's new room. |
| WF-7 | Backend | **Scene effects validate too leniently** (suspected). A scene's NPC effect validates against all encounters, but at runtime `pacifyNpc`/`startCombat` resolve the current room and `alertNpc` the active encounter, so a Johnson dialogue naming a warehouse ganger publishes and silently does nothing. | **Owner decision:** how tightly the publish gate should scope scene-authored NPC effects. |
| WF-8 | Backend | A trigger with two scene-opening reactions lets the second silently supersede the first. | Refuse more than one scene-opening reaction per trigger at publish. |
| WF-9 | Frontend | `NumberField` writes `0` on clear rather than the field minimum; a leading `-` is eaten after a clear. | Use the raw string or `validity.badInput`. |
| WF-10 | Frontend | The mission-owner trigger picker falls back to the global pool while references load or when `encounterId` dangles; the dashboard inventory row lacks `aria-current`; a refused dashboard delete leaves the confirm state armed; kind/module switches discard unsaved drafts with no dirty check. | Fix each; add a dirty-check prompt. |
| WF-11 | Tests | The queue's fault logging has no test asserting the log record. | Add one. |

Suggested order: WF-1 and WF-2 (silent, publishable data corruption), then WF-3,
then WF-4 and WF-5, then the two decisions (WF-6, WF-7), then the rest.

### 1.2 Authoring gaps

| ID | Gap | Needs |
| --- | --- | --- |
| WF-20 | **World-room triggers cannot be authored.** `playerEnteredRoom` needs a room key, and only encounter rooms have one. Public world rooms are database rows with no content key, so ambience on a shared street is impossible. | **Owner decision:** how public rooms get stable content keys and where world-level triggers live. |
| WF-21 | **"Object interact" is inspect-only.** `interactableInspected` is the only placement event; sneak-past and other placement interactions raise nothing a trigger can react to. | **Owner decision:** which additional interaction events join the palette. |
| WF-22 | **Publish and delete are check-then-write**, so a delete's reference check can race a concurrent publish. Harmless with one admin. | Make them atomic. Becomes necessary when group play brings more staff. |
| WF-23 | The dashboard runs the content loader once per pending-edit definition, and the reference pickers make one request per encounter, mission, and scene. | A cache and a server-side references endpoint. Do when the corpus is large enough to feel it. |
| WF-24 | Test coverage holes: repeatable triggers, trigger conditions, `NpcDefeated`/`EncounterEntered`/`MissionAccepted`/`ItemPickedUp` end to end, the fire-once concurrent race, and a scene replacing a dialogue. | Tests. |

### 1.3 Acceptance run

The original World Forge acceptance scenario has never been executed end to end
through the UI. v1 requires it:

> In a running game, with no code or database access, a staff member authors a
> second complete mission — a new Johnson with a negotiation test, a new
> instanced encounter, a placed NPC from an existing template, a retrievable
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
- WF-22 (atomic publish/delete) should land first.

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
