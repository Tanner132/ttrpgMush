Yes — I’d keep the big document as the **target architecture**, then give Sol this smaller implementation roadmap so it can work incrementally without trying to solve the whole engine at once.

# MUSH/MUD Game Engine — Incremental Milestone Roadmap

## Context

The project already has:

* Fully implemented Shadowrun 5e core-rulebook character creation.
* Saved character sheets containing attributes, skills, qualities, equipment, spells, adept powers, ware, and other character data.
* Existing MUSH/MUD rooms.
* Admin-created rooms.
* Live SignalR room connections.
* Existing `RoomEnter` and `RoomExit` events.

We are **not building the basic MUSH room system from scratch**.

We are now layering automated Shadowrun game mechanics on top of the existing multiplayer room/world infrastructure.

A separate full **Game Engine Architecture / Target Design** document exists in the project and should be consulted when architectural context is needed.

The following milestones intentionally implement that architecture incrementally.

The priority is:

> Build the smallest reusable foundation, prove it works, then add complexity.

Do not prematurely implement combat, Matrix, magic, rigging, or full mission automation unless required by the current milestone.

---

# Milestone 1 — Character Runtime + Dice Resolution

## Goal

Prove that a saved SR5 character can use their actual character sheet to perform automated Shadowrun tests inside the existing MUSH/MUD world.

This milestone establishes the foundational rules-resolution engine that later combat, Matrix, magic, social, stealth, NPC, and mission systems will reuse.

## Scope

### 1. Character Sheet Rules Adapter

Create an abstraction between saved character data and the game engine.

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

Do not redesign the existing character-creation system unless absolutely necessary.

---

### 2. Basic Character Runtime State

Establish a runtime-state model separate from permanent character creation data.

For this milestone, only implement the minimum required live values:

* Current Physical Damage
* Current Stun Damage
* Current Edge

The model should be designed to expand later for:

* Equipment state
* Ammo
* Active effects
* Matrix state
* Magic state
* Encounter/combat state

Do not implement all of those yet.

---

### 3. Derived Character Values

Implement a small derived-value service.

For the first milestone, support values necessary for basic tests, especially:

* Effective attribute value
* Physical Limit
* Mental Limit
* Social Limit
* Current wound modifier

Follow the principle:

> Store facts. Derive consequences.

For example:

```text
Stored:
Physical Damage = 5

Derived:
Wound Modifier = -1
```

Do not independently persist the wound modifier.

---

### 4. Universal Dice Test Model

Create a reusable test abstraction capable of representing Shadowrun dice tests.

The initial system should support:

#### Simple / Success Test

Example:

```text
Intuition + Perception
```

#### Threshold Test

Example:

```text
Agility + Gymnastics
Threshold: 2
```

#### Opposed Test

Example:

```text
Agility + Sneaking

vs.

Perception Pool
```

Extended tests do not need to be implemented yet.

The architecture should allow them later.

---

### 5. Modifier Collection

Implement structured dice-pool modifiers.

A modifier should contain at minimum:

```text
Source
Value
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

The final pool should never be calculated as an unexplained integer if the engine can provide a breakdown.

The initial milestone only needs enough modifier sources to prove the framework works.

Suggested initial modifiers:

* Wound modifier
* Skill specialization
* Manually supplied test modifier for development/testing

Do not attempt to encode every SR5 quality, environmental modifier, item, or piece of cyberware yet.

---

### 6. Shadowrun Dice Roller

Implement SR5 dice resolution.

The roller should determine:

* Number of dice rolled
* Hits
* Applicable limit
* Hits after limit
* Glitch
* Critical glitch

For opposed tests:

* Actor hits
* Opponent hits
* Net hits
* Winner / success state

For threshold tests:

* Required threshold
* Whether threshold was reached

The dice-rolling component itself should not need to understand why the character received a particular dice pool.

Its job is only to resolve the supplied test.

---

### 7. Structured Resolution Result

Every roll should return a structured result.

Conceptually:

```text
ResolutionResult

Test Type
Base Components
Modifiers
Final Dice Pool
Limit
Raw Hits
Limited Hits
Glitch
Critical Glitch
Threshold
Opponent Hits
Net Hits
Success
```

The result should be usable by:

* UI
* SignalR messages
* Future action systems
* Logging
* Admin debugging

---

### 8. Basic Roll History / Audit

Persist or otherwise record enough information to inspect completed automated rolls.

Example:

```text
Character:
Test:
Timestamp:

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

The exact long-term audit architecture does not need to be finalized yet.

We simply need a reliable way to determine how and why a roll resolved the way it did.

---

## Milestone 1 Demonstration

Use the existing room system to demonstrate the engine.

A player enters an existing room through the current MUSH/MUD functionality.

Provide a temporary/simple interaction such as:

```text
[Observe Area]
```

This performs:

```text
Intuition + Perception
```

using values from the player's real saved character sheet.

The user receives a result similar to:

```text
Perception Test

Intuition: 4
Perception: 5
Wounds: -1

Final Pool: 8
Mental Limit: 5

Hits: 3

Success
```

Then add a temporary opposed test such as:

```text
[Sneaking Test]
```

against a hard-coded or development-only opposing pool.

Example:

```text
Player:
Agility + Sneaking
12 dice
4 hits

Opposition:
8 dice
2 hits

Result:
Success
Net Hits: 2
```

The temporary opposing pool is acceptable because proper NPC support belongs to Milestone 3.

---

## Milestone 1 Definition of Done

Milestone 1 is complete when:

> A player using an existing saved SR5 character can enter an existing live MUSH/MUD room and perform both a threshold test and an opposed Shadowrun test using real character-sheet attributes and skills, current wound penalties, applicable limits, and structured modifiers, with the result returned to the client and an auditable explanation of how the roll was calculated.

No NPC system, mission system, combat system, Matrix system, or spellcasting system is required yet.

---

# Milestone 2 — Actions + Effects + Character Status

## Goal

Move from manually triggering dice tests to a reusable game-action system capable of changing character/world state.

## Primary Systems

Implement:

* Universal `GameAction` abstraction
* Rule Context
* Action validation
* Active Effect framework
* Effect sources
* Effect types
* Effect payloads
* Effect duration
* Effect expiration
* Basic effect stacking
* Atomic action resolution
* Basic persistent/semi-persistent character statuses

Initial statuses/effects may include:

```text
Prone
Running
Temporary Attribute Modifier
Temporary Dice Pool Modifier
```

Do not attempt to model every SR5 status.

---

## Example Demonstration

Provide an action such as:

```text
[Run]
```

The action applies:

```text
MovementMode = Running
```

or an appropriate temporary state/effect.

A subsequent test automatically detects that state and applies a configured modifier.

Another test action could apply:

```text
Agility +2
Duration: Until End of Turn
```

Subsequent Agility-based tests should automatically use the effective Agility value.

The effect should expire according to its duration rules.

---

## Milestone 2 Definition of Done

> A player can execute reusable game actions that validate requirements, alter runtime state through structured effects, and automatically influence subsequent dice tests without the UI or individual action manually recalculating those consequences.

---

# Milestone 3 — NPCs + Room Interactions

## Goal

Allow existing MUSH rooms to contain automated NPCs and interactable gameplay objects.

## Primary Systems

Implement:

* NPC templates
* NPC instances
* Simplified NPC dice pools
* NPC Physical/Stun damage
* NPC active effects/statuses
* Room-associated NPC placement
* Room interactables
* Character discovery/knowledge state
* Basic game events beyond existing `RoomEnter` / `RoomExit`
* Basic NPC awareness states

Suggested NPC states:

```text
Unaware
Suspicious
Alerted
Incapacitated
```

NPCs should use the same test/modifier/effect infrastructure as player characters wherever possible.

PC dice pools may be derived from full character sheets.

NPC dice pools may come directly from simplified template values.

---

## Example Demonstration

Admin places a `Street Ganger` NPC into an existing room.

The player enters the room.

Possible interactions:

```text
Observe Ganger
Sneak Past Ganger
Approach Ganger
```

Sneaking performs:

```text
Player:
Agility + Sneaking

vs.

NPC:
Perception Pool
```

Failure may change:

```text
NPC State:
Unaware → Alerted
```

A successful Perception test may reveal a hidden interactable object in the room.

---

## Milestone 3 Definition of Done

> An admin-created room can contain an instantiated NPC and interactable objects, and a player can use their real character sheet to make automated opposed or threshold checks against them, producing persistent encounter state such as detection, discovery, or NPC alertness.

---

# Milestone 4 — Missions + Instanced Encounters

## Goal

Turn individual room interactions into an automated job structure.

## Primary Systems

Implement:

* Mission definitions
* Mission instances
* Mission status
* Objectives
* Objective state
* Encounter definitions
* Encounter instances
* Encounter participant collection
* Private mission encounters
* Mission-linked rooms/interactions
* Mission items
* Mission item possession
* Basic completion/failure triggers
* Karma reward
* Nuyen reward

The existing MUSH rooms should be reused where appropriate rather than recreating the room infrastructure.

Determine how mission encounter instances should layer private state over existing room definitions.

---

## Example Demonstration

A development/admin command assigns:

```text
Gang Warehouse Retrieval
```

to a player.

The mission creates a private encounter instance.

Objectives:

```text
1. Enter Warehouse
2. Retrieve Package
3. Leave Warehouse
```

The player navigates the warehouse using existing room mechanics.

They locate and take the mission package.

The objective updates automatically.

Returning/exiting with the package marks the encounter complete.

---

## Milestone 4 Definition of Done

> A player can receive a mission instance, enter a private instanced encounter built on the existing room system, satisfy objectives through normal room interactions, retrieve a mission-specific item, and reach mission completion with persistent state isolated from other players.

---

# Milestone 5 — Johnson + First Complete Automated Run

## Goal

Turn the systems developed in Milestones 1–4 into the first fully playable automated Shadowrun job.

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
Avoid / Interact With Gang
        ↓
Retrieve Package
        ↓
Leave Warehouse
        ↓
Return to Johnson
        ↓
Deliver Package
        ↓
Receive Karma + Nuyen
```

---

## Primary Additions

Implement:

* Johnson NPC/interaction
* Basic dialogue state
* Job offer
* Accept/decline
* Negotiation opposed test
* Negotiated nuyen reward
* Mission contract creation
* Warehouse mission hookup
* Mission turn-in
* Item handoff
* Persistent Karma award
* Persistent nuyen award
* End-to-end mission completion

The first version does not require a full dialogue-tree engine if a simpler interaction system can prove the workflow.

---

## Milestone 5 Definition of Done

> A player can discover a Johnson in the existing MUSH world, receive an automated job offer, use their actual SR5 social statistics to negotiate payment, accept the contract, enter a private gang warehouse encounter, use automated Shadowrun tests to progress through the location, retrieve the mission package, return it to the Johnson, and receive persistent Karma and nuyen without administrator or Storyteller intervention.

---

# Systems Deliberately Deferred Until After the Vertical Slice

The following should **not block Milestones 1–5** unless a tiny portion is specifically required:

* Full combat
* Full initiative/action economy
* Full weapon/ammunition simulation
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

Once the first automated run works from beginning to end, these should be implemented as additional focused milestones using the same action/test/effect/event architecture.

---

# Development Principle

Each milestone should leave behind a usable vertical capability rather than a collection of unfinished systems.

Avoid solving future requirements unless doing so prevents an obvious architectural dead end.

The immediate sequence is:

```text
Milestone 1
Character → Dice

Milestone 2
Action → Effect → State

Milestone 3
Player ↔ NPC / Environment

Milestone 4
Interactions → Mission

Milestone 5
Mission → Complete Automated Shadowrun
```

The larger Game Engine Architecture document describes the intended destination.

This roadmap describes the order in which we should reach it.
