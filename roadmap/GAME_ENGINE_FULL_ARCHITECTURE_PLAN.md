# MUSH/MUD Game Engine Implementation — Proposed Milestone Plan

## Goal

Build a MUD/MUSH game engine that uses real saved player character sheets together with admin-created encounters to allow players to participate in automated Shadowrun jobs and interact with the game world without a Storyteller being present.

The engine should use existing character sheet data to resolve actions, skill checks, combat, Matrix actions, magic, environmental effects, NPC interactions, mission objectives, rewards, and persistent world/character state.

The initial MVP should be a **single-player, fully automated Shadowrun job** involving a corporate-sponsored retrieval mission from a neighborhood gang warehouse.

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
10. Retrieve the required mission item.
11. Return to the Johnson.
12. Complete the contract.
13. Receive persistent Karma and nuyen rewards.

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

# 3. Store Facts, Derive Consequences

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

# 4. Item Definitions vs Item Instances

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

# 5. Ammunition and Magazine State

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

# 6. Derived Character Values

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

# 7. Active Effect Framework

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

# 8. Effect Payloads

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

# 9. Effect Duration

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

# 10. Effect Stacking

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

# 11. Posture and Movement State

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

# 12. Universal Action Framework

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
* Effects produced
* Events produced

The common resolution pipeline should look approximately like:

```text
Player/NPC chooses action
        ↓
Validate action
        ↓
Create Rule Context
        ↓
Construct Test
        ↓
Collect Modifiers
        ↓
Resolve Roll
        ↓
Produce Resolution Result
        ↓
Apply Effects
        ↓
Commit State Changes
        ↓
Emit Game Events
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

# 13. Rule Context

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

# 14. Universal Dice/Test Resolution Engine

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

---

# 15. Modifier Engine

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

---

# 16. Resolution Results

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
    Effects
    Messages
}
```

The UI should consume this result rather than having to understand the underlying Shadowrun rule calculations.

---

# 17. Effects and State Changes

Actions should generally produce effects rather than directly modifying unrelated game systems.

Examples:

```text
DamageEffect
MovementEffect
ApplyStatusEffect
SpendAmmoEffect
GainMatrixAccessEffect
IncreaseOverwatchEffect
DoorUnlockEffect
GainItemEffect
SpendResourceEffect
GainNuyenEffect
GainKarmaEffect
AlertNPCsEffect
```

Conceptually:

```text
Action
    ↓
Resolution
    ↓
Effects
    ↓
State Changes
```

This allows multiple systems to reuse the same types of state mutations.

---

# 18. Game Event System

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

Encounter scripts, mission logic, NPC behavior, UI messages, logging, and future world systems should be able to react to these events.

Example:

```text
WHEN:
    WeaponFired

IN:
    WarehouseInterior

THEN:
    WarehouseAlerted = true
    Alert nearby gang NPCs
```

Another:

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

# 19. NPC Templates

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

# 20. NPC Instances

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

NPCs should still interact with the universal modifier/test system.

For example:

```text
Template Attack Pool: 8
Lighting: -2
Wound Modifier: -1

Final Attack Pool: 5
```

---

# 21. Encounter Definitions

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

# 22. Encounter Instances

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

# 23. Instancing and the Shared MUSH World

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

# 24. Rooms and Interactions

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

---

# 25. Character Knowledge / Discovery State

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

---

# 26. Mission Definitions

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
```

---

# 27. Mission Instances

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

# 28. Johnson / Contract Interaction

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

# 29. Mission Objectives and Mission Items

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

which causes the mission objective to complete.

Returning it to the Johnson should transition the mission into completion and reward distribution.

---

# 30. Reward System

Mission completion should support persistent rewards.

Initial supported rewards:

* Karma
* Nuyen

For the MVP:

```text
Negotiation
    ↓
Determines Nuyen Reward

Mission Completion
    ↓
Grant Nuyen
Grant Karma
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

---

# 31. Encounter / Turn State

We will eventually need encounter-specific state for action economy and combat.

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

# 32. Matrix State

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

# 33. Magic Runtime State

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

# 34. Vehicle and Drone State

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

# 35. Action Audit / History

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
* Dice result
* Hits
* Opposed hits
* Net hits
* Success/failure
* Effects applied

Example:

```text
13:42:51

Character attempted Sneak Past Lookout.

Agility: +5
Sneaking: +6
Wounds: -1
Lighting: +2

Final Pool: 12

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
* Future gameplay history/replays

---

# 36. Atomic / Transactional Action Resolution

Game actions should be resolved atomically wherever practical.

For example:

```text
Validate Current State
        ↓
Resolve Action
        ↓
Produce Effects
        ↓
Apply Effects
        ↓
Commit
        ↓
Emit Events
```

If the action cannot be completed safely, state should not be partially mutated.

The system should account for issues such as:

* Double-clicked actions
* Duplicate HTTP requests
* Simultaneous requests
* Attempting to take an already-taken item
* Spending the same ammunition/action/resource twice

Action requests may eventually need unique request/action IDs to support idempotency.

---

# 37. MVP NPC Intelligence

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

If critically injured:
    Potentially flee or surrender
```

Do not build advanced AI before the basic encounter works.

---

# 38. Admin Encounter Creation

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

# 39. First Playable Encounter

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
* NPCs work
* Rooms work
* Encounter state works
* Mission objectives work
* Inventory interactions work
* Persistent rewards work

---

# 40. Recommended Initial Warehouse Interactions

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
* Trigger combat later

---

# 41. Milestone 1 Required Systems

Game Engine Milestone 1 should establish:

* Existing character-sheet adapter
* Character runtime state
* Item instances
* Ammo/magazine state
* Derived-stat calculation
* Active-effect framework
* Effect duration framework
* Effect stacking rules
* Universal action framework
* Rule context
* Universal dice/test engine
* Modifier engine
* Resolution results
* Effects/state-change framework
* Game event system
* NPC templates
* NPC instances
* Room definitions
* Encounter definitions
* Encounter instances
* Player discovery/knowledge state
* Mission definitions
* Mission instances
* Mission objectives
* Dialogue/interactions
* Mission item handling
* Reward persistence
* Action audit/history
* Atomic action resolution
* Basic NPC behavior

Only implement each system deeply enough to support the first playable encounter.

---

# 42. Explicitly Out of Scope for Full Implementation

These systems should be architecturally considered but should **not** delay the first playable encounter:

* Full SR5 combat system
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

# 43. Architectural Principle

The intended architecture should approximately follow:

```text
                 SAVED CHARACTER
                       │
                       ▼
               Character Adapter
                       │
                       ▼
                Runtime State
                       │
                       ▼
 Player / UI ─────► Game Action
                       │
                       ▼
                  Rule Context
                       │
              ┌────────┴────────┐
              ▼                 ▼
        Test Engine       Modifier Engine
              │                 │
              └────────┬────────┘
                       ▼
               Resolution Result
                       │
                       ▼
                    Effects
                       │
                       ▼
                 State Changes
                       │
                       ▼
                  Game Events
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
       Mission      Encounter       NPC
       Engine        Engine        Logic
```

Shadowrun-specific systems such as combat, Matrix, magic, social interactions, stealth, and rigging should sit on top of this shared foundation rather than independently implementing their own dice rolling, modifiers, status effects, or state management.

---

# 44. Core Design Principle

Whenever possible:

```text
Action
    ↓
Test
    ↓
Result
    ↓
Effects
    ↓
State Changes
    ↓
Events
```

Combat, stealth, Matrix, magic, social interactions, NPC actions, and environmental interactions should all reuse this same underlying architecture.

The goal is to build a reusable **multiplayer RPG rules-resolution engine that executes Shadowrun 5e mechanics**, rather than building separate hard-coded systems for every Shadowrun subsystem.

---

# Milestone Definition of Done

Game Engine Milestone 1 is complete when:

> A saved SR5 player character can enter the live game world, discover and interact with a Johnson, receive an automated mission offer, perform a real character-sheet-derived Negotiation test, accept the mission, enter a private gang warehouse encounter, traverse its rooms, perform unopposed and opposed Shadowrun tests, interact with NPCs and objects, retrieve the required item, return to the Johnson, complete the mission, and receive persistent Karma and nuyen without requiring a Storyteller or administrator to manually resolve any part of the run.

The milestone should prioritize proving this complete vertical slice over implementing broad but unfinished versions of combat, Matrix, magic, rigging, or other advanced systems.
