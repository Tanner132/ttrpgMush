# MUSH/MUD Game Engine Implementation — Proposed Milestone Plan

## Goal

Build a MUD/MUSH game engine that uses real saved player character sheets together with admin-created encounters to allow players to participate in automated Shadowrun jobs and interact with the game world without a Storyteller being present.

The engine should use existing character sheet data to resolve actions, skill checks, combat, Matrix actions, magic, environmental effects, NPC interactions, mission objectives, rewards, and persistent world/character state.

The initial MVP should be a **single-player, fully automated Shadowrun job** involving a corporate-sponsored retrieval mission from a neighborhood gang warehouse, **including minimal but real SR5 combat**.

The player should be able to:

1. Find and interact with a Johnson.
2. Receive a job offer.
3. Negotiate payment using character skills.
4. Accept the mission.
5. Travel to the mission location.
6. Enter a private/instanced gang warehouse encounter.
7. Traverse rooms and interact with the environment.
8. Perform character-sheet-derived skill tests.
9. Interact with NPCs.
10. Fight gang members using minimal structured-time SR5 combat when stealth or talk fails (or when the player chooses violence).
11. Retrieve the required mission item.
12. Return to the Johnson.
13. Complete the contract.
14. Receive persistent Karma and nuyen rewards.

The primary goal of this milestone is **not** to completely implement every SR5 subsystem. It is to establish the reusable game-engine architecture required to support those systems and implement only enough mechanics to make this encounter fully playable from beginning to end.

---

# 1. Existing Character Sheet Integration

Character creation is already fully implemented for the Shadowrun 5e core rulebook.

Saved characters contain JSON/data representations of:

* Attributes
* Skills
* Specializations
* Qualities
* Metatype information
* Magic/Resonance information
* Spells
* Adept powers
* Power Points
* Cyberware
* Bioware
* Weapons
* Armor
* General equipment
* Matrix equipment
* Other character-creation selections

The game engine should consume this existing data rather than recreate character creation.

We should avoid tightly coupling the game engine directly to the current JSON structure wherever possible.

Create an abstraction such as a `CharacterRulesAdapter` / `CharacterSheetService` responsible for translating saved character data into information the game engine can request.

Conceptually:

```text
Saved Character Data
        ↓
CharacterRulesAdapter
        ↓
Game Engine
```

Example responsibilities:

```csharp
GetAttribute(characterId, Attribute.Agility)

GetSkill(characterId, Skill.Sneaking)

GetEffectiveAttribute(characterId, Attribute.Reaction)

HasQuality(characterId, QualityId)

GetOwnedItems(characterId)

GetEquippedWeapons(characterId)

GetKnownSpells(characterId)

GetAdeptPowers(characterId)
```

If the character data structure changes later, ideally only this adapter layer needs significant modification rather than every game-engine subsystem.

The adapter is **read-only**. The game engine never writes back through it. Persistent consequences (Karma, nuyen, purchased gear, advancement) flow through dedicated ledgers instead — see the Reward System section.

---

# 2. Character Runtime State

Character creation primarily describes what a character **is**.

The game engine additionally needs to track what is happening to the character **right now**.

We therefore need persistent and encounter-specific runtime state.

## Persistent / Semi-Persistent Character State

At minimum, support:

* Current Physical Damage
* Current Stun Damage
* Current Edge
* Current location / room
* Equipped gear
* Equipped armor
* Equipped weapon(s)
* Weapon magazine state
* Ammunition quantities
* Consumable quantities
* Wireless state for applicable equipment
* Current Matrix connection state

  * Disconnected
  * AR
  * Cold-Sim VR
  * Hot-Sim VR
* Matrix Damage
* Overwatch Score
* Matrix Access state
* Current sustained spells
* Current summoned/bound spirits
* Remaining spirit services
* Active drug effects
* Active magical effects
* Active cyberware/bioware effects where activation matters
* Temporary buffs/debuffs
* Mission item possession
* Current nuyen
* Current Karma

Consumables may include:

* Ammunition
* Magazines
* Grenades
* Drugs
* Medkit supplies
* Reagents
* Disposable equipment
* Other limited-use resources

---

# 3. State Storage Tiers and Commit Points

Not all runtime state has the same durability requirements. Classify every piece of state into one of three tiers and be explicit about which tier each value in Section 2 belongs to.

## Tier 1 — Persistent Character State (database, always durable)

Survives everything. Examples:

```text
Physical/Stun Damage
Current Edge
Nuyen
Karma
Owned item instances
Ammunition/consumable quantities
Current world location (last committed)
```

## Tier 2 — Semi-Persistent Mission/World State (database, durable)

Survives server restarts but is scoped to an activity. Examples:

```text
MissionInstance state and objectives
Negotiated reward
Mission item possession
Encounter instance snapshot (see below)
```

## Tier 3 — Ephemeral Encounter State (in-memory authoritative)

The live `EncounterInstance` — NPC damage, encounter flags, initiative order, turn state, knowledge/discovery state, active effects with turn-scoped durations — lives **in memory** and is the authority while the encounter runs. Reading and writing the database for every micro-mutation of combat would be slow and gains nothing.

## Commit Points

Durable consequences flush to the database transactionally at defined commit points:

* Encounter enter / exit
* Mission state transitions (accepted, objective completed, completed, failed, abandoned)
* Reward grants
* End of each combat (damage, ammo, consumables, Edge)
* Periodic snapshots of the live encounter instance

## Crash / Restart Policy

Decide and document the recovery behavior now. MVP policy:

> If the server restarts mid-encounter, the encounter instance is restored from its most recent snapshot if one exists; otherwise the instance is abandoned, the player is returned to the encounter entry room, and all Tier 1 state from the last commit point stands (damage taken, ammo spent, Edge spent are not refunded).

This policy dictates exactly what must be snapshotted and how often.

---

# 4. Store Facts, Derive Consequences

A major design principle should be:

> **Store facts. Calculate consequences.**

Wherever possible, do not persist values that can be derived from existing character state.

For example:

Store:

```text
PhysicalDamage = 7
StunDamage = 2
```

Derive:

```text
Current Wound Modifier
```

Store:

```text
Armor Jacket Instance
Equipped = true
```

Derive:

```text
Current Armor Rating
```

Store:

```text
Wired Reflexes Rating 2
```

Derive:

```text
Effective Reaction
Initiative
Initiative Dice
```

Store:

```text
Character is Running
```

Derive any applicable:

```text
Movement modifiers
Attack modifiers
Defense modifiers
```

This should help prevent schema explosion and synchronization problems where multiple stored values represent the same underlying fact.

---

# 5. Item Definitions vs Item Instances

Catalog entries should remain definitions of items.

Runtime ownership should use item instances.

For example:

## Item Definition

```text
Ares Predator V

Damage
AP
Accuracy
Mode
Availability
Cost
Rules
```

## Item Instance

```text
InstanceId
OwnerCharacterId
CatalogItemId
Equipped
WirelessEnabled
CurrentLocation
CurrentCondition
LoadedMagazineId
Customizations
```

This distinction is necessary because one character may own multiple copies of the same item and each may have different runtime state.

Example:

```text
Ares Predator #1
Loaded: APDS
Rounds: 8/15
Equipped: true

Ares Predator #2
Loaded: Regular Ammo
Rounds: 15/15
Stored: Apartment
```

Do not place runtime properties such as `Equipped` directly onto the shared catalog definition.

---

# 6. Ammunition and Magazine State

Weapons that use ammunition should reference the magazine currently loaded into that weapon.

Avoid tracking only:

```text
Weapon.CurrentAmmo = 8
```

Instead use something conceptually similar to:

```text
WeaponInstance
    LoadedMagazineId
```

and:

```text
MagazineInstance
    AmmoType
    CurrentRounds
    Capacity
```

This allows the game to distinguish:

```text
APDS 8/15
Regular 15/15
Gel 10/15
```

and allows reload actions to swap magazines rather than merely resetting an integer.

---

# 7. Derived Character Values

Before implementing complex actions, establish a system for calculating effective and derived values.

Examples include:

* Effective Attributes
* Physical Condition Monitor maximum
* Stun Condition Monitor maximum
* Wound Modifier
* Initiative
* Initiative Dice
* Current Armor
* Defense Pool
* Movement Rate
* Physical Limit
* Mental Limit
* Social Limit
* Recoil Compensation
* Matrix Condition Monitor
* Effective weapon statistics
* Other values affected by gear, ware, qualities, effects, or environment

Other game systems should request these values through rules services rather than independently recreating their calculations.

For example:

```csharp
GetEffectiveAttribute(characterId, Attribute.Agility)

GetCurrentDefensePool(characterId, context)

GetPhysicalConditionMonitor(characterId)

GetWoundModifier(characterId)
```

---

# 8. Terminology: Active Effects vs State Changes

Two different concepts must never share the word "effect" in code or documentation, or they will be confused constantly:

## Active Effect (a condition)

An **ongoing** status attached to a character, NPC, room, or item, with a duration and possible stacking rules. Examples: Prone, Burning, a sustained Increase Reflexes spell, a drug high.

## State Change (a mutation)

A **one-shot** mutation produced by resolving an action. Examples: apply 4 boxes of damage, spend 3 rounds of ammo, unlock a door, grant an item.

A resolved action produces **State Changes**. One kind of State Change is "attach an Active Effect."

Use these two terms consistently. Sections 9–12 describe Active Effects; the State Changes section describes mutations.

---

# 9. Active Effect Framework

We need a generalized mechanism for temporary and ongoing effects.

Examples include:

* Running
* Prone
* Full Defense
* Surprise
* Unconscious
* Immobilized
* Burning
* Poisoned
* Drug effects
* Sustained spells
* Attribute increases/decreases
* Dice-pool modifiers
* Environmental penalties
* Initiative modifiers
* Adept power effects
* Cyberware activations
* Other temporary buffs/debuffs

An effect should have clearly separated concepts for:

## Effect Source

Examples:

```text
Spell
Drug
Quality
Gear
Cyberware
Bioware
Environment
Action
Injury
NPC Ability
Mission
```

## Effect Type

Examples:

```text
Status
AttributeModifier
DicePoolModifier
InitiativeModifier
MovementModifier
ArmorModifier
VisibilityModifier
DamageOverTime
```

Example:

```csharp
SourceType = Spell
SourceId = IncreaseReflexes

EffectType = InitiativeModifier

Payload =
{
    InitiativeDice = +2
}
```

---

# 10. Effect Payloads

Effects that modify values should contain structured payloads.

For example:

```csharp
new AttributeModifierPayload
{
    Attribute = CharacterAttribute.Agility,
    Amount = 3
}
```

Another effect might use:

```csharp
new DicePoolModifierPayload
{
    AppliesTo = DicePoolCategory.PhysicalTests,
    Amount = -2
}
```

Avoid encoding mechanical behavior into arbitrary text strings.

---

# 11. Effect Duration

Effects should have structured duration rules.

Possible duration types:

```text
Permanent
UntilRemoved
UntilEndOfTurn
UntilStartOfNextTurn
UntilEndOfRound
Sustained
Timed
```

An effect also needs whatever metadata is necessary to determine when it expires.

Examples:

```text
AppliedRound
AppliedTurn
TurnOwnerId
ExpiresAt
```

The game/encounter lifecycle should automatically remove effects when their expiration condition is reached.

---

# 12. Effect Stacking

The active effect system should support stacking restrictions.

Possible rules:

```text
Stack
HighestOnly
LowestOnly
ReplaceSameSource
Unique
```

Effects may additionally belong to a stacking group such as:

```text
InitiativeEnhancement
AttributeEnhancement
ArmorBonus
```

This should make it possible to enforce Shadowrun stacking restrictions without littering the engine with special-case checks.

Complex exceptions can still use custom rule code where necessary.

---

# 13. Posture and Movement State

Avoid combining posture and movement into one enum.

These represent separate concepts.

## Posture

```text
Standing
Crouched
Prone
```

## Movement Mode

```text
Stationary
Walking
Running
Sprinting
```

A character can therefore be represented more accurately without incompatible state values competing with one another.

---

# 14. Universal Action Framework

Do not implement game mechanics directly inside UI controllers, API endpoints, room definitions, or mission scripts.

Player and NPC interactions should resolve through a reusable action framework.

Examples:

```text
MoveAction
PerceptionAction
SneakAction
NegotiateAction
AttackAction
ReloadAction
UseItemAction
PickLockAction
HackAction
CastSpellAction
TakeItemAction
TalkAction
```

An action should generally know:

* Actor
* Target
* Requirements
* Cost
* Applicable test
* Result
* State changes produced
* Events produced

The common resolution pipeline should look approximately like:

```text
Player/NPC submits action request
        ↓
Enqueue on instance command queue
        ↓
Validate action
        ↓
Create Rule Context
        ↓
Construct Test
        ↓
Collect Modifiers
        ↓
[Await declared decisions — pre-roll]        (see Interactive Decisions)
        ↓
Resolve Roll
        ↓
[Await declared decisions — post-roll]       (see Interactive Decisions)
        ↓
Produce Resolution Result
        ↓
Produce State Changes
        ↓
Apply State Changes
        ↓
Commit
        ↓
Emit Notifications
        ↓
Enqueue Reactions
```

Typed MUSH commands and graphical UI interactions should ultimately resolve through the same action system.

For example:

```text
shoot ganger
```

and clicking:

```text
[Shoot Ganger]
```

should invoke the same underlying `AttackAction`.

---

# 15. Action Execution Model

The engine needs an explicit answer to "where and how do actions run," because atomicity, ordering, and race prevention all follow from it.

## Per-Instance Command Queue

Every `EncounterInstance` (and, later, every shared room/region) owns a **command queue** — conceptually a `Channel<GameCommand>` drained by a single logical consumer. All actions targeting that instance execute **strictly one at a time, in arrival order**.

This gives us, almost for free:

* Atomicity — no interleaved mutations within an instance
* Ordering — no "who shot first" ambiguity
* Race elimination — "take the already-taken item" and "spend the same ammo twice" become impossible by construction
* A natural fit with the existing SignalR hub infrastructure

Do **not** attempt cross-cutting optimistic-concurrency retries in the ORM as the primary mechanism. A single action touches character state, item instances, NPC state, encounter flags, and mission state; serializing per instance is dramatically simpler and fast enough.

## No Reentrancy

An action must never resolve another action inline. Anything an action causes (an NPC reacting, an alarm triggering a guard's move) is **enqueued as a new command** on the same queue. See the Game Event System section for cascade rules.

## Idempotency

Every action request carries a client-generated request ID. The queue consumer discards requests whose ID has already been processed. This handles double-clicks, duplicate HTTP requests, and client retries.

---

# 16. Interactive Decisions and Pausable Resolution

SR5 is full of moments where resolution cannot proceed without a decision from a participant:

* Choosing a defense response (standard defense vs Full Defense)
* Spending Edge before a roll (Push the Limit) or after a roll (Second Chance)
* Interrupt actions (Block, Dodge, Intercept — later milestones)
* Choosing which sustained spell to drop

The pipeline must therefore be able to **pause**. Model this as a declared wait step:

```text
Resolution reaches a decision point
        ↓
Engine emits DecisionRequest
    {
        DecisionId
        ParticipantId
        DecisionType          (DefenseResponse, EdgeSpend, Interrupt, ...)
        Options
        DefaultOption
        TimeoutSeconds
    }
        ↓
Resolution enters AwaitingDecision state (instance queue moves on ONLY
for commands that cannot affect this resolution; simplest MVP rule:
the queue blocks until the decision resolves)
        ↓
DecisionResponse event arrives (or timeout fires)
        ↓
Resolution resumes with the chosen (or default) option
```

Rules:

* **Every DecisionRequest has a default and a timeout.** An AFK or disconnected participant never deadlocks an encounter; the default applies when the timeout fires.
* NPC participants answer DecisionRequests synchronously through their behavior logic — same mechanism, no pause.
* A paused resolution is part of the encounter's ephemeral state and is covered by the snapshot policy (a restore may simply apply defaults).
* The MVP should implement at minimum: **DefenseResponse** (standard vs Full Defense) and **EdgeSpend** (pre-roll and post-roll). This is deliberately chosen to prove the pause mechanism with real rules.

Design `ResolutionResult` so that a post-roll Edge spend can amend a not-yet-committed result: the result is **pending** until the post-roll decision window closes, then becomes final and is committed. Never reopen a committed result.

---

# 17. Rule Context

Actions and dice tests need access to information about the situation in which they occur.

Implement a shared context object or equivalent abstraction containing relevant data such as:

```text
Actor
Target
Room
Encounter
Mission
Action
Equipped Items
Active Effects
Environmental Effects
Combat State
Matrix State
```

The exact implementation may differ, but modifier and rule calculations should not each independently search the entire database for context.

Conceptually:

```csharp
RuleContext
{
    Actor
    Target
    Room
    Encounter
    Action
    Equipment
    ActiveEffects
    Environment
}
```

---

# 18. Universal Dice/Test Resolution Engine

Implement a common Shadowrun test-resolution framework.

It should eventually support:

* Simple tests
* Success tests
* Threshold tests
* Opposed tests
* Extended tests

For the MVP, only the types necessary for the first encounter need to be fully supported.

The resolution engine should pull values from:

* Attributes
* Skills
* Specializations
* Qualities
* Gear
* Cyberware
* Bioware
* Adept powers
* Active effects
* Wounds
* Environment
* Action-specific modifiers
* Other relevant sources

Example:

```text
Sneaking Test

Agility                    6
Sneaking                   5
Catlike                   +2
Wound Modifier            -1
Environmental Modifier    +1
--------------------------------
Final Dice Pool            13

Physical Limit              5
```

The engine should then perform the dice roll and apply SR5 rules for:

* Hits
* Limits where applicable
* Glitches
* Critical Glitches
* Thresholds
* Opposing tests
* Net hits

## Test Tags

Every test carries a set of tags describing what kind of test it is:

```text
Physical
Mental
Social
Combat
Ranged
Melee
Defense
Perception
Stealth
Resistance
...
```

Tags are how modifiers, qualities, and active effects select which tests they apply to (see Modifier Engine). This generalizes `DicePoolCategory` and prevents "Catlike applies to Sneaking but not other Agility tests" logic from spreading through the engine as special cases.

---

# 19. Dice Rolling, RNG, and Determinism

* All rolls are **server-authoritative**. The client only ever sends intent; it never sends or influences roll outcomes.
* The dice roller is an injected abstraction (`IDiceRoller`) so tests can substitute deterministic sequences.
* **Every resolution logs its RNG seed** in the audit record (see Action Audit / History). Any disputed, suspicious, or buggy roll can then be replayed deterministically.
* A seeded roller plus the per-instance command queue makes full encounter replays possible later without additional architecture.

This costs almost nothing now and is nearly impossible to retrofit honestly later.

---

# 20. Edge Integration

Edge is SR5's most cross-cutting mechanic and must be a first-class citizen of the test engine from the start, even though most Edge uses ship after the MVP.

Edge hooks into the pipeline at defined points:

## Pre-roll (declared before dice hit the table)

```text
Push the Limit     — add Edge dice, ignore Limit, exploding sixes (Rule of Six)
Blitz              — maximum initiative dice (combat)
```

## Post-roll (amends a pending result)

```text
Second Chance      — reroll all non-hits on a pending resolution
Seize the Initiative / others — later milestones
```

Requirements:

* The test engine natively supports **exploding sixes** and **ignore-limit** as roll options, selected by the Edge spend.
* Post-roll Edge operates on a **pending** `ResolutionResult` during the post-roll decision window (see Interactive Decisions) and never on a committed one.
* Edge spends are State Changes (spend the resource) and appear in the audit record with the amended roll.
* MVP scope: Push the Limit and Second Chance. The hooks for the rest exist; the implementations wait.

---

# 21. Modifier Engine

Modifiers should be collected as structured objects with identifiable sources.

For example:

```text
Source: Wounds
Value: -1

Source: Catlike
Value: +2

Source: Lighting
Value: -2
```

Do not simply modify the final dice pool silently.

The engine should be able to explain exactly how a final pool was produced.

This is important for:

* Debugging
* Player understanding
* Admin tools
* Balance work
* Rule verification

NPCs and player characters should both use this same modifier framework.

## Modifier Targets

SR5 modifiers touch more than dice-pool size. Every modifier declares **what** it modifies:

```text
DicePool
Limit
Threshold
DamageValue
ArmorPenetration
InitiativeScore
InitiativeDice
Defense
```

## Applicability

Every modifier declares **which tests it applies to**, expressed against test tags (see Universal Dice/Test Resolution Engine):

```csharp
new Modifier
{
    Source = "Catlike",
    Target = ModifierTarget.DicePool,
    Amount = +2,
    AppliesTo = TestTag.Stealth
}
```

## Operations Beyond Addition

Some rules **replace** rather than add (attribute substitutions such as "use Willpower instead of Body," fixed-value overrides, caps). The modifier model should support at minimum:

```text
Add
Replace
Cap (maximum)
Floor (minimum)
```

Additive is the overwhelmingly common case; the others exist so substitution rules do not require bypassing the engine.

---

# 22. Resolution Results

Every test/action should return a structured resolution result.

Conceptually:

```csharp
ResolutionResult
{
    Success
    BaseDicePool
    Modifiers
    FinalDicePool
    Limit
    Hits
    OpponentHits
    NetHits
    Glitch
    CriticalGlitch
    Threshold
    StateChanges
    Messages
    RngSeed
    Status            // Pending | Final
}
```

The UI should consume this result rather than having to understand the underlying Shadowrun rule calculations.

A result is `Pending` while a post-roll decision window (Edge) is open, and `Final` once committed. Only `Final` results produce committed State Changes.

---

# 23. State Changes

Actions should generally produce **State Changes** — declarative mutation records — rather than directly modifying unrelated game systems. (See Terminology section: these were previously called "Effects"; that word is now reserved for Active Effects/conditions.)

Examples:

```text
ApplyDamage
Move
AttachActiveEffect
RemoveActiveEffect
SpendAmmo
SpendEdge
GainMatrixAccess
IncreaseOverwatch
UnlockDoor
GainItem
SpendResource
GainNuyen
GainKarma
AlertNPCs
```

Conceptually:

```text
Action
    ↓
Resolution
    ↓
State Changes
    ↓
Applied Mutations
```

This allows multiple systems to reuse the same types of state mutations, makes every mutation auditable, and gives the commit step a uniform unit of work.

---

# 24. Game Event System

Important game actions and state changes should produce structured game events.

Examples:

```text
CharacterEnteredRoom
CharacterLeftRoom
TestResolved
WeaponFired
CharacterDamaged
NPCDamaged
NPCKilled
NPCAlerted
DoorUnlocked
ItemPickedUp
ItemDropped
MatrixAccessGained
AlarmTriggered
DialogueChoiceSelected
MissionAccepted
MissionObjectiveCompleted
MissionCompleted
```

## Three Kinds of Event Consumption

"Event" covers three mechanisms with different semantics. They must be routed differently or the engine will suffer ordering bugs and reentrancy corruption:

### 1. Synchronous Domain Consequences

Consequences that must be consistent with the action itself — resolved **inside the same commit**.

```text
ItemPickedUp → mission objective "Retrieve the package" completes
```

If these ran asynchronously, the audit log and mission state could disagree.

### 2. Reactive Triggers

Consequences that cause **new actions**. These must NOT run inline (an action resolving mid-action corrupts the pipeline). They **enqueue new commands** on the instance's command queue.

```text
WeaponFired in WarehouseInterior → enqueue: set WarehouseAlerted, enqueue NPC alert behaviors
```

Reactive cascades carry a **depth counter**; a cascade exceeding a small fixed depth is truncated and logged rather than allowed to loop.

### 3. Notifications

Fire-and-forget outputs published **after commit**: UI messages over the existing SignalR room hub, logging, analytics. Nothing in the rules engine ever depends on a notification being delivered.

The commit pipeline order is therefore:

```text
Resolve → Apply State Changes (+ sync consequences) → Commit → Publish Notifications → Enqueue Reactions
```

Example trigger definitions remain declarative:

```text
WHEN:
    WeaponFired

IN:
    WarehouseInterior

THEN:
    WarehouseAlerted = true
    Alert nearby gang NPCs
```

```text
WHEN:
    ItemPickedUp

ITEM:
    MissionPackage

THEN:
    CompleteObjective("Retrieve the package")
```

Actions should not need to know every downstream consequence they might cause.

---

# 25. Actor Abstraction

Player characters and NPCs must be interchangeable to the rules engine. Define a single abstraction — `IActor` (or `ICombatant`) — that the test engine, modifier engine, combat system, and action framework talk to exclusively:

```csharp
IActor
{
    ActorId
    GetDicePool(testSpec)          // pool for a given test
    GetLimit(testSpec)
    GetInitiative()                // score base + dice
    GetConditionMonitors()
    GetWoundModifier()
    GetActiveEffects()
    GetDefensePool(context)
    ResolveDecision(decisionRequest)   // players → pause pipeline; NPCs → behavior logic
}
```

Two implementations:

* **PlayerActor** — backed by the `CharacterRulesAdapter` + character runtime state.
* **NpcActor** — backed by an NPC template + NPC instance state.

The test engine must never branch on "is this a PC?". Simplified NPC pools and full character-sheet-derived pools are just two ways of answering `GetDicePool`.

---

# 26. NPC Templates

Admins need a fast way to create enemies and other NPCs without constructing full SR5 character sheets.

Create simplified reusable NPC templates.

Example:

```text
Street Ganger

Attack: 8
Defense: 7
Perception: 6
Sneaking: 5
Social: 4

Physical CM: 10
Stun CM: 10
Armor: 9

Weapon:
Ares Alpha
```

Possible simplified pools include:

* Attack
* Defense
* Perception
* Sneaking
* Social
* Athletics
* Magic
* Matrix
* Other role-specific pools

NPC templates may later support more detailed statistics where necessary.

---

# 27. NPC Instances

Separate reusable NPC definitions from individual encounter instances.

## NPC Template

```text
Street Ganger
Attack 8
Defense 7
Perception 6
Armor 9
```

## NPC Instance

```text
InstanceId
Name: Spike
TemplateId: Street Ganger
CurrentRoom: Warehouse Floor
PhysicalDamage: 4
StunDamage: 0
Ammo: 21
Status: Alerted
```

This allows an encounter to create several independent NPCs from one template.

NPCs interact with the universal modifier/test system through the `IActor` abstraction.

For example:

```text
Template Attack Pool: 8
Lighting: -2
Wound Modifier: -1

Final Attack Pool: 5
```

---

# 28. Encounter Definitions

Admins need to be able to define playable encounter spaces.

An `EncounterDefinition` should describe static encounter content such as:

* Rooms
* Exits
* Doors
* Items
* NPC placements
* Environmental properties
* Interactable objects
* Dialogue
* Action options
* Trigger conditions
* Mission-objective hooks

Example:

```text
Gang Warehouse

Rooms:
- Warehouse Exterior
- Alley
- Loading Dock
- Warehouse Floor
- Office
- Storage Room
```

---

# 29. Encounter Instances

Mission encounters should be instanced.

An `EncounterInstance` represents one active copy of an encounter.

Example:

```text
GangWarehouse Instance #5832

Participants:
- Character 123

WarehouseAlerted: false
OfficeDoorUnlocked: false
PackageTaken: false

GangMember1:
    Alive

GangMember2:
    Unconscious

GangMember3:
    Alive
```

The MVP supports one player per mission encounter.

However, architect `EncounterInstance` around a participant collection rather than a single hard-coded `CharacterId` so group missions can be supported later without redesigning the entire system.

---

# 30. Encounter Instance Lifecycle

A player dropping mid-mission is a day-one occurrence, not an edge case. The instance lifecycle must define:

## Creation

Created when the player travels to the mission site with an accepted mission. One instance per mission instance.

## Disconnect / Resume

If the participant disconnects, the instance **persists in memory** (and via snapshots) for a resume window:

```text
DisconnectGraceWindow: e.g. 15 minutes live + snapshot retained e.g. 24 hours
```

Reconnecting within the window returns the player to their current room in the instance. Any pending DecisionRequest resolves via its default/timeout as normal — a disconnect never freezes an encounter.

## Abandonment / Timeout

An instance with no participant activity past the window is **abandoned**: mission transitions to `Abandoned` (or `Failed` per mission rules), the player's location is committed to the encounter entry point, durable consequences from the last commit point stand, and the instance is torn down.

## Cleanup

Torn-down instances release memory, cancel their queue consumer, and delete or archive snapshots. A background service (following the existing `PlaySessionExpirationService` pattern) sweeps for expired instances.

---

# 31. Instancing and the Shared MUSH World

For the MVP:

> Mission encounters should be private instances.

Other players should not be able to enter another player's mission instance and interfere with it.

Possible future functionality may allow players outside the instance to observe indirect signs of ongoing missions, such as:

```text
Distant gunfire echoes through the Barrens.
```

or other diegetic world events.

This spectator/shared-world functionality is explicitly out of scope for the first milestone.

---

# 32. Rooms and Interactions

Rooms should expose possible interactions based on:

* Objects present
* NPCs present
* Encounter state
* Mission state
* Character state
* Skills/abilities
* Discovered information
* Active effects
* Previous decisions

Example:

```text
Warehouse Alley

Visible:
- Side Door
- Dumpster
- Gang Lookout

Possible actions:
- Observe Area
- Sneak Past Lookout
- Approach Lookout
- Inspect Side Door
- Return to Street
```

Actions should invoke the universal action system rather than embedding rules directly in the room.

## Server-Computed Affordances

The **server** computes the list of available actions per viewer per state and sends it to the client. The client never guesses what is possible; it renders what the server offers, and typed MUSH commands validate against the same affordance list. This keeps buttons and commands on one source of truth and prevents clients from submitting actions the state does not allow.

---

# 33. Character Knowledge / Discovery State

The engine should distinguish between:

> What exists in the encounter

and:

> What a particular character knows exists.

For example, a hidden security camera may exist in a room but should only become visible to a player after an appropriate Perception test or other discovery method.

Potential knowledge state includes:

* Discovered hidden objects
* Secret exits
* Identified NPCs
* Learned host/device information
* Mission clues
* Discovered dialogue information

This becomes particularly important later when multiple players share spaces.

## Viewer-Relative Rendering

Knowledge state implies that **every description and affordance list is composed for a specific viewer**. The room description composer takes a viewer and filters contents through that viewer's discovery state. Admin/GM views bypass the filter. Every "describe room / list actions" code path must go through this composer from the start.

---

# 34. Mission Definitions

A reusable `MissionDefinition` should describe:

* Mission giver
* Offer
* Base rewards
* Negotiation rules
* Encounter definition
* Objectives
* Optional objectives
* Failure conditions
* Completion conditions
* Reward rules
* **Repeatability rules** (one-time, cooldown period, or freely repeatable)

For the MVP:

```text
Corporate Retrieval Job

Mission Giver:
Johnson

Objective:
Retrieve package from gang warehouse.

Return:
Deliver package to Johnson.

Reward:
Nuyen + Karma

Negotiation:
Can increase nuyen reward.

Repeatability:
Cooldown (prevents reward farming)
```

---

# 35. Mission Instances

Accepting a mission should create a player-specific `MissionInstance`.

Possible states:

```text
Available
Offered
Accepted
InProgress
ReadyToTurnIn
Completed
Failed
Abandoned
```

Objectives should also have independent state:

```text
Inactive
Active
Completed
Failed
Optional
```

Example:

```text
MissionInstance

Mission:
Corporate Retrieval

Participant:
Character

NegotiatedReward:
6,500 nuyen

Objectives:

Meet Johnson
    Completed

Retrieve Package
    Active

Return Package
    Inactive
```

Mission progress should not be stored as ad hoc fields directly on the character.

---

# 36. Johnson / Contract Interaction

The MVP begins with an interactable Johnson.

The player should be able to:

1. Discover/select the Johnson.
2. Begin dialogue.
3. Receive the job description.
4. Ask basic questions.
5. Attempt negotiation.
6. Accept or decline the contract.

Negotiation should use real character-sheet values.

Example:

```text
Charisma + Negotiation
```

against an appropriate Johnson/NPC opposed pool.

The result should affect the offered nuyen.

For the MVP, Karma may remain fixed while nuyen changes based on negotiation.

Later reward logic may also account for mission decisions, optional objectives, stealth, casualties, evidence, alarms, and other consequences.

---

# 37. Dialogue System

Dialogue is a small node graph, defined as data, hooking into the same action framework as everything else. Do not invent it ad hoc inside the Johnson implementation.

```text
DialogueNode
    NodeId
    Text (or text variants)
    Choices[]

DialogueChoice
    Label
    Conditions          (mission state, knowledge state, character state)
    Test?               (optional skill test resolved through the test engine,
                         e.g. Charisma + Negotiation opposed)
    OnSuccess → NextNodeId + StateChanges
    OnFailure → NextNodeId + StateChanges
    StateChanges        (e.g. MissionOffered, NegotiatedRewardSet)
    Events              (e.g. DialogueChoiceSelected)
```

Selecting a choice is a `TalkAction` variant flowing through the universal pipeline: choices with tests roll real dice, produce ResolutionResults, and appear in the audit log like any other action.

MVP scope: enough nodes for the Johnson scene (description, questions, negotiation test, accept/decline) and simple gang-lookout talk options. Branching richness comes later; the data model comes now.

---

# 38. Mission Objectives and Mission Items

Mission items should use real inventory/item-instance mechanics where possible.

Example:

```text
Mission Package

InstanceId
EncounterInstanceId
MissionInstanceId
CurrentLocation
CurrentOwner
```

Taking the package should generate an event such as:

```text
ItemPickedUp
```

which causes the mission objective to complete (a synchronous domain consequence — same commit).

Returning it to the Johnson should transition the mission into completion and reward distribution.

---

# 39. Reward System

Mission completion should support persistent rewards.

Initial supported rewards:

* Karma
* Nuyen

## Rewards Flow Through the Career Ledger

The game engine does **not** write Karma or nuyen fields directly. Persistent rewards are recorded as a new advancement-ledger entry type using the existing career-sheet pattern (evaluator/composer/store/endpoint):

```text
MissionReward ledger entry

MissionInstanceId      (natural idempotency key — grant exactly once)
Karma
Nuyen
GrantedAt
```

This provides provenance, audit, atomicity, and grant-once semantics using machinery that already exists. The reward grant commits **in the same transaction** as the mission's `Completed` state transition.

For the MVP:

```text
Negotiation
    ↓
Determines Nuyen Reward

Mission Completion
    ↓
Append MissionReward ledger entry (atomic with completion)
    ↓
Karma + Nuyen reflected in character totals
```

Later systems may modify rewards based on:

* Optional objectives
* Alarms
* Civilian casualties
* Gang casualties
* Evidence left behind
* Mission approach
* Betrayal
* Employer satisfaction
* Other decisions

## Economy Safeguards

* Reward grants are idempotent (keyed by `MissionInstanceId`).
* `MissionDefinition.Repeatability` (one-time / cooldown) exists from day one so negotiation + repeatable missions cannot become an infinite nuyen/Karma farm.
* All reward mutations are ledgered and therefore auditable and reversible by admins.

---

# 40. Time Model: Freeform vs Structured Time

A MUSH world is real-time; SR5 combat is turn-based. The engine reconciles these with an explicit **encounter mode**:

## Freeform Mode

Default. Actions resolve immediately as they arrive on the instance queue. Exploration, dialogue, stealth, and skill tests all run freeform.

## Structured Time Mode

Entered when combat (or any initiative-ordered scene) begins:

```text
Trigger (attack action, NPC engages, alarm escalation)
        ↓
Roll initiative for all participants (IActor.GetInitiative)
        ↓
Build initiative order and passes
        ↓
Gate the command queue: only the current actor's actions
(and legal out-of-turn responses/interrupts) are accepted
        ↓
Advance turn → pass → round
        ↓
Exit condition met (all hostiles incapacitated/fled/surrendered,
or player flees/incapacitated)
        ↓
Return to Freeform mode; combat-scoped state discarded;
lasting consequences committed
```

## The Structured Time Driver

Something must tick structured time — NPC turns and player-turn timeouts do not drive themselves. A hosted background service (following the existing `PlaySessionExpirationService` pattern) owns:

* Prompting/executing NPC turns via their behavior logic
* Enforcing the player turn timer

## AFK Policy

A player who does not act within their turn window gets a default action:

```text
TurnTimeoutSeconds: e.g. 60 (configurable per encounter)
Default on timeout: Full Defense, then delay to end of pass
```

Combined with DecisionRequest defaults (see Interactive Decisions), no absent player can ever freeze an encounter. Single-player MVP still implements the timer — it is the same mechanism group play will need.

---

# 41. Encounter / Turn State

Structured time requires encounter-specific action-economy state.

Examples:

* Initiative score
* Initiative order
* Initiative passes
* Current actor
* Remaining actions
* Current recoil
* Movement used
* Defense penalties
* Surprise
* Turn/round number

This state should generally live on a participant's encounter/combat state rather than directly on the persistent character record.

For example:

```text
CombatParticipantState

ActorId
InitiativeScore
CurrentPass
ActionsRemaining
MovementUsed
Recoil
DefenseModifier
Surprised
```

When combat ends, most of this state disappears while actual injuries, ammunition consumption, and other lasting consequences remain.

---

# 42. Minimal MVP Combat

The MVP includes **minimal but real SR5 combat** — enough to fight the warehouse gang using actual character-sheet values, structured time, and the universal pipeline. This is deliberately the hardest proof of the architecture: it exercises initiative, action economy, opposed tests, decision pauses, damage, active effects, and NPC behavior together.

## In Scope

### Initiative

* Initiative score = derived Initiative attribute + initiative dice (from derived values / active effects such as Wired Reflexes)
* Initiative passes per SR5 (subtract 10 per pass)
* Rolled at combat start; surprise handled as a simple flag/modifier

### Action Economy

* Per pass: 1 Free Action, 2 Simple Actions or 1 Complex Action, plus movement
* MVP action set:

```text
Attack (ranged)        Complex/Simple per weapon mode
Attack (melee/unarmed) Complex          (basic version)
Reload                 per weapon rules (simplified acceptable)
Move / Sprint
Take Cover             (attach cover Active Effect)
Full Defense           (declared, -10 initiative)
Delay
Use Item               (e.g. medkit — stretch goal)
```

### Ranged Attack Resolution

```text
Attacker: Agility + Weapon Skill [Accuracy]
    vs
Defender: Reaction + Intuition (+ Full Defense: + Willpower)
        ↓
Net hits → modified Damage Value
        ↓
Soak: Body + (Armor − AP)
        ↓
Apply damage (Physical/Stun per ammo and armor rules)
```

* Fire modes: SS and SA fully; a single simplified burst/full-auto pool bonus is acceptable for MVP
* Recoil: simplified progressive recoil vs recoil compensation (flag as simplified in rule decisions)
* Range/environment: a single collapsed environmental modifier per room (lighting/cover) instead of the full SR5 environmental table

### Melee (basic)

Agility + skill [Physical] vs defense, net hits to DV, soak — no reach, no interception, no martial arts.

### Damage and Condition Monitors

* Physical and Stun monitors from derived values
* Wound modifiers derived from damage (already covered by Store Facts)
* Stun overflow into Physical per SR5

### Decision Pauses in Combat

* DefenseResponse (standard vs Full Defense) and EdgeSpend (Push the Limit, Second Chance) — see Interactive Decisions

### NPC Combat Behavior

Deterministic: pick visible target, attack with template pool, respect wound modifiers, flee/surrender when critically injured (see MVP NPC Intelligence).

## Incapacitation and Defeat

* **NPC** monitor filled → `Incapacitated` (unconscious) or `Dead` if overkill — both end their participation; either satisfies combat exit conditions.
* **Player** Physical monitor filled → unconscious. MVP policy: **no PC death.** The mission fails (or continues per mission rules), the player wakes at a defined safe location (encounter entry / street) with damage persisting until healed, and combat consequences from the last commit stand. Permadeath/overflow-death rules are an explicit later decision recorded in SR5_RULE_DECISIONS.
* Combat ending commits all lasting consequences (damage, ammo, Edge, deaths) as a commit point.

## Explicitly Out of Scope for MVP Combat

Called shots, full environmental modifier table, suppressive fire, multiple attacks/dual wielding, interception, knockdown, grenades and rockets, martial arts, subduing, vehicle combat, astral combat, Matrix combat actions, full medical/healing rules (a simple rest/medkit heal is acceptable).

---

# 43. Matrix State

Matrix gameplay will use Shadowrun 5e character statistics and dice mechanics combined with a streamlined access system inspired by Shadowrun 6e.

Full rule clarification will be provided separately before Matrix implementation.

At minimum the runtime architecture should anticipate:

* AR / Cold-Sim / Hot-Sim
* Connected/disconnected state
* Current persona
* Current host/network
* Matrix Condition Monitor
* Overwatch Score
* Access level

Initial conceptual access levels:

```text
Outsider
User
Admin
```

Do not attempt to implement the entire Matrix subsystem as part of the first infrastructure task unless required for the MVP encounter.

---

# 44. Magic Runtime State

The architecture should support:

* Sustained spells
* Active spell effects
* Drain
* Summoned spirits
* Bound spirits
* Remaining services
* Temporary magical modifiers

Full magic implementation is not required before the first encounter unless a minimal spell interaction is intentionally added to the MVP.

---

# 45. Vehicle and Drone State

Vehicle/rigging support should be considered architecturally but should **not** be fully implemented during this milestone unless required.

Future vehicle runtime state may include:

```text
VehicleInstanceState

Damage
Speed
Driver
Controller
Passengers
Mounted weapons
Ammo
Active autosofts
Matrix state
Control mode
```

Player state may eventually need:

```text
ControlledEntityId
ControlMode
```

Rigging is large enough that it should remain a separate subsystem after the first playable encounter.

---

# 46. Action Audit / History

Every important automated action should be logged.

At minimum record:

* Actor
* Action
* Target
* Timestamp
* Rule context where useful
* Base dice pool
* Modifiers and sources
* Final dice pool
* Limit
* **RNG seed**
* Dice result
* Hits
* Opposed hits
* Net hits
* Success/failure
* **Decisions made (defense responses, Edge spends) with chosen vs default**
* State changes applied

Example:

```text
13:42:51

Character attempted Sneak Past Lookout.

Agility: +5
Sneaking: +6
Wounds: -1
Lighting: +2

Final Pool: 12
Seed: 8f3a19c2

Hits: 5

Guard Perception Pool: 8
Guard Hits: 3

Result:
Success
2 Net Hits
```

This will be important for:

* Debugging
* Admin review
* Player-facing explanations
* Rule verification
* Exploit investigation
* Balance analysis
* Deterministic replay of disputed rolls (seed + inputs)
* Future gameplay history/replays

---

# 47. Atomic / Transactional Action Resolution

Game actions resolve atomically. The per-instance command queue (see Action Execution Model) provides serialization; commits provide durability.

```text
Dequeue command (idempotency check on request ID)
        ↓
Validate Current State
        ↓
Resolve Action (with any decision pauses)
        ↓
Produce State Changes
        ↓
Apply State Changes + synchronous domain consequences
        ↓
Commit (transactional at commit points; in-memory otherwise)
        ↓
Publish Notifications
        ↓
Enqueue Reactions
```

If the action cannot be completed safely, state is not partially mutated.

The combination of queue serialization + request IDs handles:

* Double-clicked actions
* Duplicate HTTP requests
* Simultaneous requests
* Attempting to take an already-taken item
* Spending the same ammunition/action/resource twice

---

# 48. MVP NPC Intelligence

NPC behavior should initially remain simple and deterministic.

For example:

```text
Unaware
Suspicious
Alerted
Combat
Fleeing
Incapacitated
```

Possible basic behaviors:

```text
If player fails Sneaking:
    Become Suspicious or Alerted

If gunshot occurs nearby:
    Become Alerted

If attacked:
    Enter Combat

In Combat, on own turn:
    Target nearest/most recently hostile visible enemy
    Attack with template pool (move to range if needed)
    Take cover if available and badly hurt

If critically injured:
    Flee or surrender
```

NPCs answer DecisionRequests (e.g. defense response) synchronously through this logic — no pauses, same interface.

Do not build advanced AI before the basic encounter works.

---

# 49. Admin Encounter Creation

Long-term, admins should be able to create encounters by combining:

* Rooms
* NPC templates
* Items
* Interactions
* Dialogue
* Skill checks
* Environmental conditions
* Event triggers
* Mission objectives

For the first milestone, it is acceptable for some encounter definition data to require manual configuration or development tooling.

The goal is to establish a data model that can eventually support proper admin-facing encounter creation.

---

# 50. Content Authoring Format

Encounter definitions, mission definitions, NPC templates, dialogue graphs, and trigger definitions are authored as **versioned JSON documents in the repository**, following the same conventions already established for the SR5 catalog:

* Split files by content type / encounter, merged at load
* Same schema-versioning and ledger discipline as the catalog
* Validated at load time with clear errors

Hand-authoring the gang-warehouse JSON **is** the admin-tool MVP: it proves the data model that the future encounter-builder UI will write.

---

# 51. Testing Strategy

The engine's correctness story is layered:

## Pure Core, Heavily Unit-Tested

The dice/test engine and modifier engine are **pure** (no I/O, injected RNG) and get the densest coverage — a subtle bug here silently mis-rolls every test in the game. Include property-based tests where cheap:

```text
Final pool == base pool + sum of modifiers (explainability invariant)
Pools never negative; hits ≤ limit when limit applies
Glitch/critical-glitch classification correct across generated rolls
Stacking rules never produce duplicate unique effects
```

## Golden Resolution Tests

Canonical SR5 scenarios (from rulebook examples where possible) resolved with seeded dice and asserted end-to-end through the pipeline.

## Headless Scripted Playthrough (CI)

A test that drives the **entire warehouse mission** — Johnson dialogue, negotiation, travel, stealth route, a combat route, package retrieval, turn-in, reward grant — through the real action pipeline with a seeded `IDiceRoller`, asserting mission state, ledger entries, and audit records at each step. The warehouse is explicitly a test harness; this makes that literal and keeps the vertical slice from regressing.

## Concurrency Tests

Duplicate request IDs, simultaneous submissions, and disconnect-mid-decision scenarios against the command queue.

---

# 52. First Playable Encounter

The first complete encounter should be:

## Corporate-Sponsored Gang Warehouse Retrieval

Basic flow:

```text
Find Johnson
        ↓
Receive Job Offer
        ↓
Negotiate Reward
        ↓
Accept Mission
        ↓
Travel to Warehouse
        ↓
Enter Private Encounter Instance
        ↓
Explore Warehouse
        ↓
Interact With Gang / Environment
        ↓
(Stealth route  OR  Talk route  OR  Combat route — player's choice,
 and failed stealth/talk can escalate into combat)
        ↓
Retrieve Package
        ↓
Leave Encounter
        ↓
Return to Johnson
        ↓
Turn In Package
        ↓
Receive Karma + Nuyen
```

The warehouse should be designed as a test harness for the game engine.

It should contain enough interaction to prove:

* Character-sheet-derived rolls work
* Derived values work
* Modifiers work
* Active effects work
* Threshold tests work
* Opposed tests work
* Structured-time combat works (initiative, action economy, attack/defense/soak)
* Decision pauses work (defense response, Edge spends)
* NPCs work (including combat behavior and incapacitation)
* Rooms work
* Encounter state works
* Mission objectives work
* Inventory interactions work
* Persistent rewards work (via ledger)

---

# 53. Recommended Initial Warehouse Interactions

The encounter should support a small number of meaningful routes rather than attempting to implement every Shadowrun mechanic immediately.

Potential examples:

## Outside Warehouse

```text
Observe Area
Sneak Toward Side Entrance
Approach Gang Lookout
Inspect Fence
Leave Area
```

## Possible Tests

Perception:

```text
Intuition + Perception
```

Sneaking:

```text
Agility + Sneaking
```

Negotiation / Fast Talk:

```text
Charisma + appropriate Social Skill
```

Physical obstacle:

```text
Agility/Strength + appropriate Physical Skill
```

Combat:

```text
Agility + Weapon Skill [Accuracy]  vs  Reaction + Intuition
Body + modified Armor (soak)
```

## Outcomes

Success may:

* Reveal hidden camera
* Avoid guard detection
* Unlock alternate route
* Gain information
* Reach another room

Failure may:

* Alert NPC
* Produce noise
* Increase suspicion
* Block an interaction
* Escalate into structured-time combat

Combat outcomes:

* Win → loot-free MVP, path to package clears
* Player incapacitated → mission failure path (see Minimal MVP Combat)
* NPCs flee/surrender → encounter continues in freeform mode

---

# 54. Milestone 1 Required Systems

Game Engine Milestone 1 should establish:

* Existing character-sheet adapter (read-only)
* Character runtime state
* State storage tiers, commit points, and crash-recovery policy
* Item instances
* Ammo/magazine state
* Derived-stat calculation
* Active-effect framework (conditions)
* Effect duration framework
* Effect stacking rules
* Universal action framework
* Per-instance command queue execution model (with idempotent request IDs)
* Interactive decision / pausable-resolution framework (defense response, Edge)
* Rule context
* Universal dice/test engine (with test tags)
* Seeded, injectable, server-authoritative dice roller
* Edge hooks (Push the Limit, Second Chance)
* Modifier engine (targets, applicability, add/replace/cap)
* Resolution results (pending → final)
* State-change framework (mutations)
* Game event system (sync consequences / enqueued reactions / notifications)
* Actor abstraction (PlayerActor + NpcActor)
* NPC templates
* NPC instances
* Room definitions
* Encounter definitions
* Encounter instances + lifecycle (disconnect/resume/abandon/cleanup)
* Player discovery/knowledge state + viewer-relative rendering
* Server-computed action affordances
* Mission definitions (with repeatability)
* Mission instances
* Mission objectives
* Dialogue node graph
* Mission item handling
* Reward persistence via career ledger (idempotent)
* Structured-time mode + driver + AFK/turn-timeout policy
* Minimal combat (initiative, action economy, ranged/basic melee, soak, incapacitation)
* Basic NPC behavior (including combat)
* Action audit/history (with seeds and decisions)
* Atomic action resolution
* Testing harness (pure-core tests + headless playthrough)

Only implement each system deeply enough to support the first playable encounter.

---

# 55. Explicitly Out of Scope for Full Implementation

These systems should be architecturally considered but should **not** delay the first playable encounter:

* Full SR5 combat (minimal combat IS in scope — the exclusions listed in Minimal MVP Combat are not: called shots, grenades, suppressive fire, martial arts, full environmental tables, multiple attacks, interception, etc.)
* PC death / permadeath rules
* Full Matrix system
* Full spellcasting system
* Full summoning system
* Full rigging system
* Vehicle combat
* Advanced NPC AI
* Group encounters
* PvP
* Spectating mission instances
* Persistent world consequences
* Complex faction reputation
* Procedural mission generation
* Full admin encounter-builder UI

They can be added incrementally after the core engine has been validated.

---

# 56. Architectural Principle

The intended architecture should approximately follow:

```text
                 SAVED CHARACTER
                       │
                       ▼
               Character Adapter (read-only)
                       │
                       ▼
                Runtime State (tiered)
                       │
                       ▼
 Player / UI ──► Action Request ──► Instance Command Queue
                                        │  (serialized, idempotent)
                                        ▼
                                   Game Action
                                        │
                                        ▼
                                   Rule Context
                                        │
                               ┌────────┴────────┐
                               ▼                 ▼
                         Test Engine       Modifier Engine
                          (seeded RNG,      (targets, tags)
                           Edge hooks)
                               │                 │
                               └────────┬────────┘
                                        ▼
                          [Decision Pause where declared]
                                        ▼
                               Resolution Result
                               (pending → final)
                                        │
                                        ▼
                                 State Changes
                                        │
                                        ▼
                              Commit (at commit points)
                                        │
                          ┌─────────────┼─────────────┐
                          ▼             ▼             ▼
                    Notifications   Sync Domain    Enqueued
                    (SignalR/log)   Consequences   Reactions
                                        │             │
                          ┌─────────────┼─────────────┐
                          ▼             ▼             ▼
                       Mission      Encounter        NPC
                       Engine        Engine         Logic
```

Shadowrun-specific systems such as combat, Matrix, magic, social interactions, stealth, and rigging should sit on top of this shared foundation rather than independently implementing their own dice rolling, modifiers, status effects, or state management.

---

# 57. Core Design Principle

Whenever possible:

```text
Action
    ↓
Test  (pausing for declared decisions where required)
    ↓
Result
    ↓
State Changes
    ↓
Committed State
    ↓
Events (sync consequences / reactions / notifications)
```

Combat, stealth, Matrix, magic, social interactions, NPC actions, and environmental interactions should all reuse this same underlying architecture.

The goal is to build a reusable **multiplayer RPG rules-resolution engine that executes Shadowrun 5e mechanics**, rather than building separate hard-coded systems for every Shadowrun subsystem.

A companion caution: generalize on the second consumer, not the first. Each framework above should be implemented only deeply enough for the warehouse encounter; the second encounter, second effect source, and second combat scenario are what earn each abstraction its generality.

---

# Milestone Definition of Done

Game Engine Milestone 1 is complete when:

> A saved SR5 player character can enter the live game world, discover and interact with a Johnson, receive an automated mission offer, perform a real character-sheet-derived Negotiation test, accept the mission, enter a private gang warehouse encounter, traverse its rooms, perform unopposed and opposed Shadowrun tests, interact with NPCs and objects, **fight at least one gang member in structured-time combat — rolling initiative, spending actions, attacking, defending (including a declared Full Defense or Edge spend through the decision-pause mechanism), and applying real damage —** retrieve the required item, return to the Johnson, complete the mission, and receive persistent Karma and nuyen **through the career ledger** without requiring a Storyteller or administrator to manually resolve any part of the run.

Additionally:

> The headless scripted playthrough test drives this entire flow with seeded dice in CI, and every resolved action in the run is reconstructible from the audit log (inputs, modifiers, seed, decisions, outcome).

The milestone should prioritize proving this complete vertical slice over implementing broad but unfinished versions of combat, Matrix, magic, rigging, or other advanced systems.
