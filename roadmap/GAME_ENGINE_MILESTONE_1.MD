# MUSH/MUD Game Engine — Incremental Milestone Roadmap

## Context

The project already has:

* Fully implemented Shadowrun 5e core-rulebook character creation.
* Saved character sheets containing attributes, skills, qualities, equipment, spells, adept powers, ware, and other character data.
* Existing MUSH/MUD rooms.
* Admin-created rooms.
* Live SignalR room connections.
* Existing `RoomEnter` and `RoomExit` events.
* A career advancement ledger (evaluator/composer/store/endpoint pattern) for persistent character changes.

We are **not building the basic MUSH room system from scratch**.

We are now layering automated Shadowrun game mechanics on top of the existing multiplayer room/world infrastructure.

A separate full **Game Engine Architecture / Target Design** document (`GAME_ENGINE_FULL_ARCHITECTURE_PLAN.md`) exists in the project and should be consulted when architectural context is needed. Section references below (§) point into that document.

The following milestones intentionally implement that architecture incrementally.

The priority is:

> Build the smallest reusable foundation, prove it works, then add complexity.

The MVP vertical slice **includes minimal structured-time combat** (architecture doc §42). It does **not** include Matrix, magic, rigging, group play, or full-combat extras. Do not implement those prematurely unless required by the current milestone.

---

# Milestone 1 — Character Runtime + Dice Resolution (Pure Core)

## Goal

Prove that a saved SR5 character can use their actual character sheet to perform automated Shadowrun tests inside the existing MUSH/MUD world.

This milestone establishes the foundational rules-resolution engine that later combat, Matrix, magic, social, stealth, NPC, and mission systems will reuse. The test and modifier engines built here are **pure** (no I/O, injected RNG) — this is deliberate: it makes them the most heavily testable code in the project, and a subtle bug here silently mis-rolls every test in the game.

## Scope

### 1. Character Sheet Rules Adapter

Create a **read-only** abstraction between saved character data and the game engine (§1).

The rest of the engine should not directly understand or repeatedly parse the structure of the saved character JSON.

Conceptually:

```text
Saved Character
      ↓
CharacterRulesAdapter
      ↓
Game Rules Engine
```

The adapter should initially support retrieving values necessary for basic skill tests, including:

```text
GetAttribute()
GetSkill()
GetSpecialization()
GetQuality()
GetPhysicalLimit()
GetMentalLimit()
GetSocialLimit()
```

It should be designed so additional requests such as equipment, spells, adept powers, ware, and other values can be added later.

Do not redesign the existing character-creation system unless absolutely necessary. The adapter never writes; persistent consequences will flow through ledgers in later milestones.

### 2. Basic Character Runtime State

Establish a runtime-state model separate from permanent character creation data (§2), with the state-tier classification from §3 in mind (persistent vs semi-persistent vs ephemeral).

For this milestone, only implement the minimum required live values:

* Current Physical Damage
* Current Stun Damage
* Current Edge

The model should be designed to expand later for equipment state, ammo, active effects, Matrix state, magic state, and encounter/combat state. Do not implement all of those yet.

### 3. Derived Character Values

Implement a small derived-value service (§7).

For the first milestone, support values necessary for basic tests, especially:

* Effective attribute value
* Physical Limit
* Mental Limit
* Social Limit
* Current wound modifier

Follow the principle: **store facts, derive consequences** (§4). Do not independently persist the wound modifier.

### 4. Universal Dice Test Model

Create a reusable test abstraction capable of representing Shadowrun dice tests (§18), supporting:

* **Simple / Success Test** — e.g. `Intuition + Perception`
* **Threshold Test** — e.g. `Agility + Gymnastics, Threshold 2`
* **Opposed Test** — e.g. `Agility + Sneaking` vs a perception pool

Extended tests do not need to be implemented yet; the architecture should allow them later.

Every test carries **test tags** (`Physical`, `Mental`, `Social`, `Stealth`, `Perception`, ...) from day one — tags are how modifiers select which tests they apply to, and retrofitting them later means touching every test site.

### 5. Modifier Engine

Implement structured modifiers (§21). A modifier contains at minimum:

```text
Source
Target        (DicePool for now; the enum includes Limit, Threshold, DV, AP, ... for later)
Operation     (Add for now; Replace/Cap/Floor exist in the model)
Value
AppliesTo     (test-tag predicate)
```

Example:

```text
Agility                    5
Sneaking                   6
Specialization            +2
Wound Modifier            -1
--------------------------------
Final Dice Pool            12
```

The final pool must never be an unexplained integer — the engine always provides the breakdown (explainability invariant).

Suggested initial modifier sources:

* Wound modifier
* Skill specialization
* Manually supplied test modifier for development/testing

Do not attempt to encode every SR5 quality, environmental modifier, item, or piece of cyberware yet.

### 6. Shadowrun Dice Roller (Seeded, Injectable)

Implement SR5 dice resolution behind an injected `IDiceRoller` abstraction (§19):

* **Server-authoritative** — the client only ever sends intent.
* **Seeded** — every resolution records its RNG seed so any roll can be replayed deterministically.
* Supports roll options needed later by Edge (§20): **exploding sixes** (Rule of Six) and **ignore limit**. Implement the options now even though nothing spends Edge yet — they are cheap here and painful to retrofit.

The roller determines: dice rolled, hits, applicable limit, hits after limit, glitch, critical glitch; for opposed tests, actor/opponent/net hits and winner; for threshold tests, whether the threshold was reached.

The dice-rolling component should not need to understand why a character received a particular pool. Its job is only to resolve the supplied test.

### 7. Structured Resolution Result

Every roll returns a structured result (§22):

```text
ResolutionResult

Test Type
Base Components
Modifiers
Final Dice Pool
Limit
RNG Seed
Raw Hits
Limited Hits
Glitch
Critical Glitch
Threshold
Opponent Hits
Net Hits
Success
Status            (Pending | Final — Pending matters once post-roll Edge exists)
```

Usable by UI, SignalR messages, future action systems, logging, and admin debugging.

### 8. Basic Roll History / Audit

Persist or otherwise record enough information to inspect completed automated rolls (§46), **including the seed**:

```text
Character:
Test:
Timestamp:
Seed:

Intuition: 4
Perception: 5
Wounds: -1

Final Pool: 8

Roll:
Hits: 3
Limit: 5

Result:
Success
```

The exact long-term audit architecture does not need to be finalized yet. We simply need a reliable way to determine how and why a roll resolved the way it did.

### 9. Core Test Coverage

Because the core is pure, cover it now (§51):

* Unit tests for hit counting, limits, glitch/critical-glitch classification with deterministic dice sequences.
* Property-style tests for the explainability invariant (`final pool == base + sum of modifiers`), non-negative pools, and limit application.
* Golden tests for a few canonical SR5 examples.

## Milestone 1 Demonstration

Use the existing room system to demonstrate the engine.

A player enters an existing room through the current MUSH/MUD functionality and is offered a temporary interaction:

```text
[Observe Area]     → Intuition + Perception (threshold test)
[Sneaking Test]    → Agility + Sneaking vs a hard-coded development opposing pool
```

using values from the player's real saved character sheet, with the full pool breakdown returned to the client. The temporary opposing pool is acceptable because proper NPC support belongs to Milestone 3.

## Milestone 1 Definition of Done

> A player using an existing saved SR5 character can enter an existing live MUSH/MUD room and perform both a threshold test and an opposed Shadowrun test using real character-sheet attributes and skills, current wound penalties, applicable limits, and structured modifiers, with the result returned to the client, the RNG seed recorded, and an auditable explanation of how the roll was calculated — and the test/modifier core has deterministic unit coverage.

No NPC system, mission system, combat system, Matrix system, or spellcasting system is required yet.

---

# Milestone 2 — Actions, Execution Model + Active Effects

## Goal

Move from manually triggering dice tests to a reusable, safely serialized game-action system capable of pausing for participant decisions and changing character/world state.

This milestone builds the three architectural spines the rest of the engine hangs on: the **command queue** (§15), the **decision pause** (§16), and the **state-change/event split** (§23–24).

## Primary Systems

Implement:

* Universal `GameAction` abstraction (§14)
* Rule Context (§17)
* Action validation
* **Per-instance command queue** — actions execute strictly one at a time per instance/room scope, with idempotent client request IDs (§15). No reentrancy: anything an action causes is enqueued as a new command.
* **Interactive decision framework** (§16) — `DecisionRequest` / `DecisionResponse` with a default option and timeout on every request; resolution enters `AwaitingDecision` and resumes on response or timeout. Nothing can ever deadlock waiting on an absent player.
* **Edge spends as the first real decisions** (§20): pre-roll Push the Limit (declared with the action) and post-roll Second Chance (amends a `Pending` result before it becomes `Final`). This proves the pause mechanism single-player, before combat needs it.
* Active Effect framework — sources, types, payloads, duration, expiration, basic stacking (§9–12)
* **State Changes** (mutations) as the uniform output of resolved actions (§23) — including `SpendEdge`
* Game event system with the **three consumption kinds** (§24): synchronous domain consequences (same commit), enqueued reactions (with cascade depth guard), and post-commit notifications over the existing SignalR hub
* Atomic action resolution (§47)
* Basic persistent/semi-persistent character statuses

Initial statuses/effects may include:

```text
Prone
Running
Temporary Attribute Modifier
Temporary Dice Pool Modifier
```

Do not attempt to model every SR5 status.

Terminology from day one (§8): **Active Effect** = ongoing condition; **State Change** = one-shot mutation. Never call a mutation an "effect."

## Example Demonstration

Provide an action such as:

```text
[Run]
```

which applies `MovementMode = Running`; a subsequent test automatically detects that state and applies a configured modifier. Another action applies `Agility +2, Duration: Until End of Turn`, and subsequent Agility-based tests automatically use the effective value until it expires.

Then demonstrate the decision pause:

```text
Player triggers a test
        ↓
Result: Pending, 2 hits
        ↓
DecisionRequest: Spend Edge (Second Chance)?  [default: No, timeout 30s]
        ↓
Player accepts → non-hits rerolled → result becomes Final
```

Also demonstrate idempotency: submitting the same request ID twice resolves once.

## Milestone 2 Definition of Done

> A player can execute reusable game actions that are serialized through a per-instance command queue with idempotent request IDs, validate requirements, pause for a participant decision with a working default/timeout, spend Edge pre- and post-roll, alter runtime state through structured Active Effects and State Changes, and automatically influence subsequent dice tests — without the UI or individual actions manually recalculating consequences, and with concurrency tests covering duplicate and simultaneous submissions.

---

# Milestone 3 — NPCs, Actor Abstraction + Room Interactions

## Goal

Allow existing MUSH rooms to contain automated NPCs and interactable gameplay objects, with players and NPCs interchangeable to the rules engine.

## Primary Systems

Implement:

* **`IActor` abstraction** (§25) — the test engine, modifier engine, and action framework talk only to `IActor`; implementations:
  * `PlayerActor` — backed by the character adapter + runtime state
  * `NpcActor` — backed by an NPC template + instance
  * `ResolveDecision()` on the interface: players pause the pipeline, NPCs answer synchronously through behavior logic
* NPC templates with simplified dice pools (§26)
* NPC instances (§27)
* NPC Physical/Stun damage and active effects/statuses
* Room-associated NPC placement
* Room interactables
* Character discovery/knowledge state (§33) with **viewer-relative rendering** — room descriptions and contents are composed per viewer through their discovery state from the start
* **Server-computed affordances** (§32) — the server sends each viewer their available actions; typed commands validate against the same list
* Game events beyond the existing `RoomEnter` / `RoomExit`, including the first **reactive triggers** (e.g. failed sneak → enqueue NPC alert)
* Basic NPC awareness states:

```text
Unaware
Suspicious
Alerted
Incapacitated
```

NPCs use the same test/modifier/effect infrastructure as player characters — the engine never branches on "is this a PC?". PC pools derive from full character sheets; NPC pools come directly from simplified template values. Both are just answers to `IActor.GetDicePool()`.

## Example Demonstration

Admin places a `Street Ganger` NPC into an existing room. The player enters and sees viewer-appropriate affordances:

```text
Observe Ganger
Sneak Past Ganger
Approach Ganger
```

Sneaking performs `Agility + Sneaking` vs the NPC's perception pool. Failure enqueues a reaction changing NPC state `Unaware → Alerted`. A successful Perception test reveals a hidden interactable object that was present but unknown to the character.

## Milestone 3 Definition of Done

> An admin-created room can contain an instantiated NPC and interactable objects, and a player can use their real character sheet to make automated opposed or threshold checks against them through the shared `IActor` abstraction, producing persistent encounter state such as detection, discovery, or NPC alertness — with hidden content invisible until discovered and available actions computed server-side per viewer.

---

# Milestone 4 — Structured Time + Minimal Combat

## Goal

Prove real SR5 combat end-to-end using the pipeline built in Milestones 1–3: initiative, action economy, opposed attack/defense, soak, damage, decision pauses under fire, and deterministic NPC opposition.

This is deliberately the hardest proof of the architecture (§42) — it exercises every prior system simultaneously.

## Primary Systems

Implement:

* **Time model** (§40): `Freeform` vs `StructuredTime` encounter modes with defined entry triggers (attack action, NPC engages) and exit conditions (hostiles incapacitated/fled/surrendered, or player flees/incapacitated)
* **Structured-time driver** — a hosted background service (following the existing `PlaySessionExpirationService` pattern) that executes NPC turns and enforces player turn timers
* **AFK/turn-timeout policy** — default action on timeout (Full Defense, then delay); combined with decision defaults, no absent player can freeze an encounter
* Initiative: score + dice from derived values, initiative passes (subtract 10), simple surprise flag
* `CombatParticipantState` (§41): initiative score, current pass, actions remaining, movement used, recoil, defense modifier — ephemeral, discarded when combat ends
* SR5 action economy: 1 Free + 2 Simple or 1 Complex per pass, plus movement
* Combat actions: ranged attack (SS/SA; simplified burst bonus acceptable), basic melee/unarmed, reload, move/sprint, take cover, Full Defense, delay
* Ranged resolution chain: `Agility + skill [Accuracy]` vs `Reaction + Intuition (+Willpower on Full Defense)` → net hits to DV → soak `Body + (Armor − AP)` → apply Physical/Stun per ammo/armor rules
* Simplified progressive recoil vs recoil compensation, and a single collapsed environmental modifier per room (record both simplifications in `SR5_RULE_DECISIONS.md`)
* Condition monitors, wound modifiers, stun overflow into physical
* **Combat decision pauses**: DefenseResponse (standard vs Full Defense) and Edge (Push the Limit, Second Chance) — reusing the Milestone 2 framework under real pressure
* **Incapacitation**: NPC monitor filled → `Incapacitated`/`Dead`; player monitor filled → unconscious, **no PC death in MVP** — defined defeat path (wake at safe location, damage persists)
* Deterministic NPC combat behavior (§48): target selection, attack with template pool, cover when hurt, flee/surrender when critically injured
* Combat end as a **commit point** (§3): damage, ammo, Edge, and deaths become durable; combat-scoped state is discarded
* Golden combat tests with seeded dice covering the full attack→defense→soak chain

Explicitly out of scope (§42): called shots, grenades, suppressive fire, multiple attacks, interception, knockdown, martial arts, full environmental tables, vehicle/astral/Matrix combat, full healing rules.

## Example Demonstration

The player attacks the Milestone 3 street ganger (or is caught sneaking and the ganger attacks). The encounter enters structured time, initiative is rolled, and a full fight plays out — including at least one DefenseResponse prompt and one Edge spend — until the ganger is incapacitated, flees, or the player is defeated and wakes at the defined safe location.

## Milestone 4 Definition of Done

> A player and an NPC can fight a complete structured-time SR5 combat: initiative rolled from real derived values, action economy enforced per pass, ranged and basic melee attacks resolved as opposed tests with soak and condition-monitor damage, defense and Edge decisions offered through the pause framework with working defaults and turn timers, NPC turns driven automatically, incapacitation ending the fight down defined victory and defeat paths, and all lasting consequences committed while combat-scoped state is discarded.

---

# Milestone 5 — Missions + Instanced Encounters

## Goal

Turn individual room interactions into an automated job structure with private, resumable encounter instances.

## Primary Systems

Implement:

* Mission definitions (§34) — including **repeatability rules** (one-time / cooldown) from day one to prevent reward farming
* Mission instances + status (§35)
* Objectives + objective state
* Encounter definitions (§28), authored as **versioned JSON in the repository** following the existing SR5 catalog conventions — split files, merged at load, validated with clear errors (§50). Hand-authoring this JSON is the admin-tool MVP.
* Encounter instances with a participant collection (§29) — single player for now, no hard-coded `CharacterId`
* Private mission encounters layered over existing room definitions
* **Encounter instance lifecycle** (§30): disconnect grace window + resume, abandonment/timeout, cleanup sweep via background service, periodic snapshots, and the crash-recovery policy (restore from snapshot, else abandon with last commit standing)
* Mission-linked rooms/interactions
* Mission items using real item-instance mechanics (§38)
* Mission item possession
* Basic completion/failure triggers — objective completion as a **synchronous domain consequence** of `ItemPickedUp` (same commit)
* **Karma + nuyen rewards via the career advancement ledger** (§39): a `MissionReward` entry keyed by `MissionInstanceId` (idempotent, grant-once), appended atomically with the mission's `Completed` transition — no raw field writes

The existing MUSH rooms should be reused where appropriate rather than recreating room infrastructure. Determine how mission encounter instances layer private state over existing room definitions.

## Example Demonstration

A development/admin command assigns `Gang Warehouse Retrieval` to a player. The mission creates a private encounter instance from repo-authored JSON. Objectives:

```text
1. Enter Warehouse
2. Retrieve Package
3. Leave Warehouse
```

The player navigates the warehouse using existing room mechanics, takes the package (objective completes in the same commit), and exits to mark the encounter complete. Additionally demonstrate: disconnecting mid-encounter and resuming within the grace window; and completing the mission twice does not grant rewards twice.

## Milestone 5 Definition of Done

> A player can receive a mission instance, enter a private instanced encounter built from repo-authored definition data on the existing room system, satisfy objectives through normal room interactions, retrieve a mission-specific item, disconnect and resume without losing the instance, and reach mission completion with rewards granted exactly once through the career ledger and persistent state isolated from other players.

---

# Milestone 6 — Johnson, Dialogue + First Complete Automated Run

## Goal

Turn the systems developed in Milestones 1–5 into the first fully playable automated Shadowrun job — the full MVP vertical slice from the architecture document, with all three routes through the warehouse.

## Mission

### Corporate-Sponsored Gang Warehouse Retrieval

Full flow:

```text
Find Johnson
        ↓
Discuss Job
        ↓
Negotiate Reward
        ↓
Accept Contract
        ↓
Travel to Warehouse
        ↓
Enter Mission Instance
        ↓
Navigate Warehouse
        ↓
Stealth / Talk / Fight the Gang   (player's choice; failed stealth or
                                   talk can escalate into combat)
        ↓
Retrieve Package
        ↓
Leave Warehouse
        ↓
Return to Johnson
        ↓
Deliver Package
        ↓
Receive Karma + Nuyen (ledgered)
```

## Primary Additions

Implement:

* Johnson NPC/interaction
* **Dialogue node graph** (§37) — nodes, choices with conditions, optional skill tests resolved through the real test engine, success/failure branches, and State Changes; enough nodes for the Johnson scene plus simple gang-lookout talk options. Dialogue choices flow through the universal pipeline and appear in the audit log.
* Job offer, accept/decline
* Negotiation opposed test (`Charisma + Negotiation` vs Johnson pool) determining the nuyen reward
* Mission contract creation
* Warehouse mission hookup with all three routes, including combat escalation from failed stealth/talk and the defeat path (player KO → mission failure → wake at safe location)
* Mission turn-in and item handoff
* Persistent Karma and nuyen awards via the Milestone 5 ledger path
* End-to-end mission completion
* **Headless scripted playthrough test in CI** (§51): drives the entire run — dialogue, negotiation, a stealth route, a combat route, retrieval, turn-in, ledger grant — through the real action pipeline with a seeded `IDiceRoller`, asserting mission state, ledger entries, and audit records at each step. The warehouse is the engine's test harness; this makes that literal and keeps the vertical slice from regressing.

## Milestone 6 Definition of Done

> A player can discover a Johnson in the existing MUSH world, receive an automated job offer through the dialogue graph, use their actual SR5 social statistics to negotiate payment, accept the contract, enter a private gang warehouse encounter, progress by stealth, talk, or structured-time combat using automated Shadowrun tests, survive or fail the fight down defined paths, retrieve the mission package, return it to the Johnson, and receive persistent Karma and nuyen through the career ledger — without administrator or Storyteller intervention, and with the entire flow exercised by a seeded headless playthrough in CI.

This matches the Milestone Definition of Done in the architecture document.

---

# Systems Deliberately Deferred Until After the Vertical Slice

The following should **not block Milestones 1–6** unless a tiny portion is specifically required:

* Full combat extras (called shots, grenades, suppressive fire, multiple attacks, interception, martial arts, full environmental tables, full healing)
* PC death / permadeath rules
* Full weapon/ammunition simulation beyond magazines and the MVP fire modes
* Full Matrix mechanics
* SR6-inspired Matrix access implementation
* Full spellcasting
* Summoning/binding
* Rigging
* Vehicles
* Drones
* Advanced NPC combat AI
* Group missions
* PvP
* Mission spectators
* Persistent world consequences
* Reputation/faction systems
* Procedural jobs
* Complete admin encounter-builder UI

Once the first automated run works from beginning to end, these should be implemented as additional focused milestones using the same action/test/state-change/event architecture.

---

# Development Principle

Each milestone should leave behind a usable vertical capability rather than a collection of unfinished systems.

Avoid solving future requirements unless doing so prevents an obvious architectural dead end. Generalize on the **second** consumer of an abstraction, not the first.

The immediate sequence is:

```text
Milestone 1
Character → Dice (pure, seeded, explained)

Milestone 2
Action → Queue → Decision Pause → State Change

Milestone 3
Player ↔ NPC / Environment (one IActor)

Milestone 4
Structured Time → Minimal Combat

Milestone 5
Interactions → Mission (instanced, resumable, ledgered)

Milestone 6
Mission → Complete Automated Shadowrun (CI-proven)
```

The larger Game Engine Architecture document describes the intended destination.

This roadmap describes the order in which we should reach it.
