# Milestone 10: Game Engine — Character Runtime And Dice Resolution

This is **Game Engine Milestone 1** from
[`GAME_ENGINE_MILESTONE_1.MD`](GAME_ENGINE_MILESTONE_1.MD), sequenced as project
Milestone 10 so it slots behind Milestone 9 in
[`../ROADMAP.md`](../ROADMAP.md). The target design it builds toward is
[`GAME_ENGINE_FULL_ARCHITECTURE_PLAN.md`](GAME_ENGINE_FULL_ARCHITECTURE_PLAN.md);
that document describes the destination, this one describes only the first step.

**Outcome:** A player with a finalized SR5 character can resolve Shadowrun tests
from a live room in the existing MUSH world through **two paths that share one
engine**: a server-authored test attached to an action option, resolved
automatically when the player selects it; and a free-form roll the player builds
themselves from their own resolved values. Both compose a dice pool from real
sheet data, current wound penalties, and applicable limits; both support Edge
spent before or after the roll; both return an attributed breakdown of every die;
and both are visible to the room and permanently auditable.

The product consequence is that **Milestone 1 is playable two ways.** The
automated path is the foundation the pre-made adventures of Milestones 3-5 are
built on. The manual path means groups running human-refereed sessions get value
immediately, without waiting for those milestones. Neither is a detour from the
other — they are the same pipeline with different sources of intent.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification
commands. All Shadowrun mechanics remain governed by the approved local core
rulebook and Run Faster PDFs described in `PROJECT_CONTEXT.md`.

## Two Paths, One Engine

This is the milestone's central architectural commitment:

```text
Action option in an encounter                Player's roll builder
  (server-authored TestSpecification)          (FreeFormTestRequest)
                    \                            /
                     \                          /
                      v                        v
                          TestComposer
                               |
                               v
                         ShadowrunTest
                               |
                               v
                     IShadowrunDiceRoller
                               |
                               v
                     TestResolutionResult
                               |
                               v
                Audit  +  Room broadcast  +  UI card
```

The split between them is only **who supplies intent**:

| | Automated (action option) | Manual (roll builder) |
| --- | --- | --- |
| Attribute, skill, limit, threshold | The specification | The player |
| Situational modifiers | The specification | The player, bounded and labeled |
| Specialization applicability | The specification declares what qualifies | The player asserts it |
| Opposition | The specification | The player, from the Storyteller's call |
| Edge | The player | The player |
| Every character-derived value and every die | The server | The server |

Milestone 1 ships the specification-driven path with a small in-memory catalog,
demonstrated by real action options in the room. Milestones 3-5 replace that
catalog's authoring with admin-created encounter content; they do not replace the
resolution path, because it is already the one the builder uses.

## Scope Boundary

In scope:

- Character rules adapter over the finalized sheet and career state.
- Character runtime state: physical damage, stun damage, current Edge — with a
  live tracker surfaced in the cockpit.
- Player-authored state commands: `/damage`, `/heal`, `/edge`, each emitting a
  room-visible summary.
- Shared derived-value calculation (effective attributes, three inherent limits,
  wound modifier).
- A universal test model covering success, threshold, and opposed tests.
- `TestSpecification` and a code-defined catalog, with action options that
  resolve a roll on selection.
- A free-form roll builder for play outside an encounter.
- A structured modifier engine with attributed sources.
- An SR5 dice roller: hits, limits, glitches, critical glitches, Rule of Six.
- Edge spending — pre-declared or post-roll reroll, mutually exclusive.
- Room-visible structured results plus persisted transcript lines.
- Append-only audit of every resolution and every runtime-state change.

Explicitly **not** in scope — these belong to Game Engine Milestones 2-5:

- The `GameAction` framework, rule context, action requirements and costs.
- Admin authoring of specifications and encounter content.
- The active-effect framework, effect payloads, durations, and stacking.
- Item instances, equipment state, ammunition, and magazines.
- Posture and movement state.
- NPC templates and instances. Opposition is a number the specification or the
  player supplies.
- Encounters, missions, objectives, dialogue, rewards.
- Extended tests, teamwork tests, initiative, and combat turn structure.
- Edge uses beyond pre-declared and Second Chance — Blitz, Seize the Initiative,
  Close Call, Dead Man's Trigger, and Not Dead Yet all depend on systems that do
  not exist yet.
- Condition Monitor overflow consequences: unconsciousness, stun overflowing into
  physical, death, and healing rates.
- Matrix, magic, rigging, and vehicles.

## The Trust Model

Milestone 1 has no Storyteller tooling and needs none. Mechanical state is
**player-authored and publicly recorded** rather than administratively enforced:

- The client submits **intent**: which attribute, which skill, whether a
  specialization applies, which limit, a threshold, an opposing pool, a signed
  situational modifier, whether to spend Edge, and — through `/damage`, `/heal`,
  and `/edge` — changes to their own trackers.
- The server supplies **every character-derived value**: attribute ratings, skill
  ratings including group-derived ones, specialization existence, limit values,
  the wound modifier, and every die rolled. The client never submits a pool
  total, a hit count, a die face, or an outcome.
- Every player-authored change is **bounded, validated against the character's
  own maximums, announced to the room, and permanently recorded.** A Storyteller
  adjudicates by reading the room, exactly as they would at a table.

This is why the audit trail matters more here than in a fully automated design.
The engine's job is to make a player's claims legible and arithmetically honest,
not to police them. When automated encounters arrive they supply modifiers and
damage from typed sources through the same pipeline, and player-authored changes
remain available for the manual path.

## Fixed Product Contract

- Selecting an action option resolves its roll immediately. The player is never
  asked to retype what the encounter already knows.
- The roll builder is populated from **the player's own resolved values**. A
  player cannot select a skill they do not have or a rating they did not earn.
- Every resolution returns a full breakdown. A final dice pool must never reach
  the client as an unexplained integer.
- Resolutions and tracker changes are visible to the room by default, with an
  explicit private option for a roll.
- **Edge is spent once per test.** Declaring Edge before the roll removes the
  post-roll reroll affordance, and taking the reroll is only possible on a test
  where Edge was not pre-declared. Either spend decrements durable Edge
  immediately.
- Trackers are live: damage and Edge changes are reflected in the cockpit and in
  the next roll's wound modifier without a reload.
- The finalized `CharacterSheet` canonical JSON stays immutable.
- Resolution and runtime history are append-only. An Edge reroll appends a linked
  amendment; it never rewrites the original.

## Findings From The Current Codebase

These were verified against the current `main` and shape several tickets.

### Already in place and directly reusable

- `CharacterCreationBaselineReader` returns a typed, schema-pinned,
  digest-validated `CanonicalCharacterSheet`. The adapter builds on this, not on
  raw JSON.
- `ICharacterCareerStateStore` and `GetComposedCharacterSheetQuery` already
  compose the immutable baseline with mutable career state. The adapter must
  consume the same composition so future advancement flows in automatically.
- `IDiceRandom` / `CryptographicDiceRandom` already provide an unbiased,
  injectable random source with a deterministic test seam.
- `RollDiceCommandHandler` already demonstrates the exact shape this milestone
  needs: parse intent, resolve server-side, persist a canonical rendered message,
  then broadcast.
- The play-session row-lock discipline in `MovementStore` is the template for
  every transactional gameplay operation here — rolls, Edge spends, and tracker
  changes all mutate state under it.
- The command parser in `frontEnd/src/commands/` already has the shape `/damage`,
  `/heal`, and `/edge` need, including usage-error handling.

### Gaps that must be closed by this milestone

1. **Skill-group ratings do not reach skills.** `CanonicalSkill` entries are
   built only from explicit skill allocations plus magic/resonance path grants
   (`QualitiesSkillsKnowledgeEvaluator.BuildCanonicalSkills` /
   `GrantedSkillRatings`). A character who bought the Stealth **group** at 4 has
   no `sneaking` entry at all. Nothing in the backend or the career-sheet
   frontend resolves group membership into per-skill ratings. In a builder this
   is immediately visible — the skill would be missing from the player's own
   dropdown — and in an encounter it would silently default. See
   `test.group-derived-skill-rating`.
2. **Limits are frozen at creation.** `CanonicalDerivedStatistics` is computed
   once by `DerivedStatisticsEvaluator` and stored in the sheet. Milestone 9
   advancement will change Strength, Body, Willpower, and Essence, and the stored
   limits will silently go stale. The engine must recompute limits from current
   effective attributes, and the formulas must be extracted into one shared pure
   calculator so creation and gameplay cannot diverge.
3. **Career progression is an empty envelope.** `CareerProgressionDocument` is
   `Empty` and SHEET-906+ have not shipped, so "current attribute value" equals
   the creation baseline today. The adapter must still route through the
   composition seam rather than reading the baseline directly.
4. **`DiceOptions.MaxDice` is 100.** The chat dice limits are not the engine's
   limits, and the Rule of Six means the engine can exceed its own starting pool.
   The engine needs its own options section with a pool ceiling and an
   explosion-depth ceiling.
5. **Edge spending and tracker commands make idempotency mandatory.** A
   double-submitted request can spend Edge twice or apply damage twice.
   Request-ID deduplication is a correctness requirement, not a convenience.

## Rules Contract

ENGINE-1001 must resolve every row below against the approved PDFs and record
each one in [`SR5_RULE_DECISIONS.md`](SR5_RULE_DECISIONS.md) with an exact
printed and PDF page citation before ENGINE-1004, ENGINE-1005, or ENGINE-1007
begins. No page numbers are asserted here; producing them is the deliverable.

### Source-resolved rules to cite

| ID | Rule to freeze |
| --- | --- |
| `test.hit` | A die showing 5 or 6 is a hit. |
| `test.glitch` | More than half the dice rolled showing 1 is a glitch. |
| `test.critical-glitch` | A glitch with zero hits is a critical glitch. |
| `test.limit` | Hits above the applicable limit are discarded; the limit never changes the number of dice rolled. |
| `test.threshold` | A threshold test succeeds when hits after the limit meet or exceed the threshold. |
| `test.opposed` | Both sides roll; net hits are the actor's limited hits minus the opponent's limited hits; how ties resolve. |
| `test.defaulting` | Defaulting on an untrained skill, and which skills cannot be defaulted. |
| `test.specialization` | The bonus dice a matching specialization contributes. |
| `test.attribute-only` | Tests composed of two attributes with no skill, and whether defaulting applies. |
| `edge.pre-declared` | Declaring Edge before the roll: the dice added, the Rule of Six, and the effect on the limit. |
| `edge.rule-of-six` | Exactly how sixes are rerolled and how additional hits accrue. |
| `edge.second-chance` | Spending Edge after a roll to reroll dice that did not generate hits. |
| `edge.glitch-interaction` | How spending Edge affects glitch and critical-glitch determination. |
| `edge.once-per-test` | Whether the rules bar spending Edge twice on one test. The project owner has decided the product behaves this way regardless; the citation records whether RAW agrees. |
| `condition.wound-modifier` | The dice-pool penalty per filled block of Condition Monitor boxes, applied separately to each track and cumulatively. |
| `condition.monitor-maximums` | Physical and Stun Condition Monitor box counts and overflow (already cited as `derived.condition-monitor`; confirm reuse). |
| `derived.inherent-limits` | Already cited. Confirm the engine reuses it verbatim. |
| `attribute.edge-starting-value` | Current Edge at the start of play equals the Edge attribute. |

### Owner decisions

Rows marked **Decided** were settled by the project owner during planning.
Remaining recommendations are engineering proposals.

| ID | Gap | Choice |
| --- | --- | --- |
| `edge.once-per-test` | Whether one test may take both a pre-declared spend and a Second Chance. | **Decided:** no. Choosing one path disables the other in the UI and is rejected server-side. |
| `runtime.player-authored-state` | Damage and Edge must change during play, and nothing automates them in this milestone. | **Decided:** players author their own trackers with `/damage`, `/heal`, and `/edge`. Each change is bounded, validated, announced to the room, and audited. No Storyteller endpoint or role is introduced. |
| `test.invocation-paths` | Whether the manual builder replaces server-authored rolls. | **Decided:** it does not. Both paths ship in this milestone and share `TestComposer`, so encounter content built later needs no new resolution path. |
| `test.group-derived-skill-rating` | The canonical sheet stores skill-group ratings separately from skills, so a group-trained skill has no skill entry. | Effective active-skill rating = `max(individual TotalRating, owning group TotalRating)`. Never additive. Consistent with `skill.group-break-and-rebuild`. |
| `test.result-visibility` | Structured results and tracker changes have no place in the current transcript model, but a Storyteller who cannot see them cannot referee. | Add two `ChatMessageType` values: `TestResult` and `StateChange`. Both carry a server-rendered summary line, persisted and broadcast exactly as `Roll` already is. Rolls additionally deliver a structured payload for the result card. Offer a Private option on rolls only — tracker changes are always announced. |
| `test.second-component` | Many SR5 tests are attribute + attribute (Composure, Judge Intentions, damage resistance), not attribute + skill. | The second component may be an active skill, a knowledge or language skill, or a second attribute. Restricting to skills would make the builder unable to express common tests. |
| `test.specialization-applicability` | SR5 leaves "does this specialization apply?" to table judgment. | Split by path. A specification declares which specializations qualify and the server applies the bonus automatically. The builder offers a checkbox, shown only when the character holds a specialization on the chosen skill, and the server validates it exists before granting the bonus. |
| `test.modifier-bounds` | The builder's situational modifier is player-supplied. | A single signed integer bounded to ±20 with an optional bounded label. Enforced in Application validation and by check constraint; both recorded in the audit. |
| `test.zero-dice-pool` | Behavior when modifiers reduce a pool to zero or below. | Roll no dice; return zero hits, no glitch, and an explicit `PoolExhausted` flag rather than throwing or silently rolling one die. |
| `edge.second-chance-limit` | Whether the applicable limit still constrains a Second Chance, or whether only pre-declared Edge ignores limits. | Follow ENGINE-1001's citation. If the text is genuinely ambiguous, apply the limit to Second Chance and ignore it only for pre-declared Edge, and record the reading. |
| `edge.amendment-window` | How long a result stays eligible for a Second Chance. | Eligible while the original resolution belongs to the player's current play session and has not already been amended. Ending a session closes the window. |
| `condition.tracker-bounds` | What `/damage` does past a full Condition Monitor. | Clamp to the track's maximum and record the attempted overflow in the runtime event without acting on it. Unconsciousness, stun overflowing into physical, and death are combat-milestone concerns. Announce the clamp so the table can adjudicate. |
| `condition.healing` | Healing rates are time-and-test based in SR5 and no clock exists. | `/heal` reduces a track by a stated amount with no test and no rate limit. It is a bookkeeping command reflecting a Storyteller's call, not an implementation of the healing rules. Label it as such in `/help`. |
| `edge.refresh` | Edge regain is a Storyteller judgment in SR5. | `/edge` adjusts current Edge by a signed amount, clamped to the character's Edge attribute. Not automated, not time-based. |
| `test.duplicate-request` | Duplicate submissions can double-spend Edge or double-apply damage. | Require a client request ID on every mutating operation and dedupe on it, returning the stored result. Not optional. |

## Target Architecture

Dependency direction is unchanged:
`Api -> Application -> Domain`, `Infrastructure -> Application`.

### Layout

```text
SeattleByNight.Domain/Entities/
    CharacterRuntimeState.cs
    CharacterRuntimeEvent.cs
    GameTestRecord.cs

SeattleByNight.Application/GameEngine/
    Characters/     ICharacterRulesAdapter, CharacterRulesSnapshot, RollOptions
    Runtime/        ICharacterRuntimeStateStore, CharacterRuntimeStateSnapshot,
                    CharacterRuntimeDocument, ApplyDamageCommand, HealDamageCommand,
                    AdjustEdgeCommand
    Derived/        DerivedValueCalculator, CharacterDerivedValues
    Specifications/ TestSpecification, TestSpecificationCatalog
    Tests/          FreeFormTestRequest, TestInvocation, TestComposer, ShadowrunTest,
                    DicePoolComponent, DicePoolModifier
    Dice/           IShadowrunDiceRoller, ShadowrunDiceRoller, DicePoolRoll
    Results/        TestResolutionResult, TestResultFormatter, StateChangeFormatter
    Audit/          IGameTestRecordStore, ICharacterRuntimeEventStore
    ResolveTestCommand.cs
    SpendEdgeOnResolutionCommand.cs
    GetRollOptionsQuery.cs
    GameEngineOptions.cs

SeattleByNight.Infrastructure/GameEngine/
    CharacterRuntimeStateStore.cs
    CharacterRuntimeEventStore.cs
    GameTestRecordStore.cs

SeattleByNight.Api/
    Hubs/RoomChatHub.cs      (+ ResolveTest, SpendEdgeOnResolution, ApplyDamage,
                              HealDamage, AdjustEdge)
    Hubs/IRoomChatClient.cs  (+ TestResolved, RuntimeStateChanged)
    Endpoints/GameEngineEndpoints.cs
```

`ShadowrunDiceRoller` and the formatters need no persistence and stay in
Application, matching how `DiceResultFormatter` sits beside `IDiceEngine`.

`DerivedStatisticsEvaluator` is refactored to call the new shared
`DerivedValueCalculator` so creation and gameplay use one implementation of the
limit and Condition Monitor formulas. Its existing tests must pass unchanged.

There is **no Storyteller endpoint and no new role.**

### Character Rules Adapter

One boundary between saved character data and everything else. Nothing
downstream of it parses `CanonicalSheetJson`, indexes the catalog, or knows that
skill groups exist.

```csharp
public interface ICharacterRulesAdapter
{
    Task<CharacterRulesSnapshot?> GetAsync(Guid characterId, CancellationToken ct = default);
}
```

`CharacterRulesSnapshot` is an immutable, fully resolved value object built once
per operation from the composed sheet plus runtime state:

```csharp
int GetAttribute(string attributeId);            // natural, from baseline + career
int GetEffectiveAttribute(string attributeId);   // natural + effects (identity in M1)
SkillRating GetSkill(string skillId);            // rating + source + defaulting
string? GetSpecialization(string skillId);
bool HasQuality(string qualityId);
int GetPhysicalLimit();
int GetMentalLimit();
int GetSocialLimit();
int GetWoundModifier();
int GetCurrentEdge();
```

`SkillRating` carries the resolved rating, whether it came from the individual
skill or the owning group, and whether the character is defaulting. That
distinction survives into the breakdown so the player sees "Sneaking (Stealth
group) 4" rather than a bare number.

`GetEffectiveAttribute` returning the natural value is deliberate: it is the seam
Milestone 2's effect framework fills in, and every consumer must go through it
from day one so that ticket changes one method instead of every call site.

### Roll Options Projection

The builder's dropdowns and the cockpit's tracker are populated from the server:

```csharp
public sealed record RollOptions(
    Guid CharacterId,
    IReadOnlyList<AttributeOption> Attributes,     // id, name, current rating
    IReadOnlyList<SkillOption> Skills,             // id, name, rating, source, specialization
    int PhysicalLimit, int MentalLimit, int SocialLimit,
    int WoundModifier,
    int CurrentEdge, int EdgeAttribute,
    int PhysicalDamage, int PhysicalConditionMonitor,
    int StunDamage, int StunConditionMonitor);
```

This is `CharacterRulesSnapshot` projected for the owner, and it is what the live
tracker renders. It is refetched — or patched from the `RuntimeStateChanged`
broadcast — after every roll and every tracker command.

### Character Runtime State

One row per finalized character, mirroring the `CharacterCareerState` envelope
pattern: hot typed columns plus a versioned JSON document for future expansion.

```text
character_runtime_state
    character_id            uuid  PK, FK -> characters, ON DELETE RESTRICT
    physical_damage         int   NOT NULL, CHECK >= 0
    stun_damage             int   NOT NULL, CHECK >= 0
    current_edge            int   NOT NULL, CHECK >= 0
    runtime_schema_version  int   NOT NULL
    runtime_document        jsonb NOT NULL DEFAULT '{}'
    version                 uuid  NOT NULL          -- optimistic concurrency
    created_at_utc          timestamptz NOT NULL
    updated_at_utc          timestamptz NOT NULL
```

`CharacterRuntimeDocument` starts as an empty typed record, exactly as
`CareerProgressionDocument` did. Milestone 2's effects, Milestone 3's encounter
state, and later equipment, ammo, Matrix, and magic state land inside it without
a migration per subsystem.

Initialization is lazy and idempotent: the first operation for a character
creates the row with zero damage and `current_edge` set to the Edge special
attribute. Concurrent first operations must not create two rows.

Damage and Edge are **stored facts**. The wound modifier is **never** persisted.

### Runtime Event Audit

Every change to runtime state appends one row, whatever caused it:

```text
character_runtime_events
    id                 uuid PK
    character_id       uuid NOT NULL, FK -> characters, ON DELETE RESTRICT
    request_id         uuid NOT NULL
    play_session_id    uuid NULL, FK -> play_sessions
    room_id            uuid NULL, FK -> rooms
    chat_message_id    uuid NULL, FK -> chat_messages
    resolution_id      uuid NULL, FK -> game_test_records   -- set for Edge spends
    track              int  NOT NULL     -- PhysicalDamage | StunDamage | Edge
    source             int  NOT NULL     -- PlayerCommand | EdgePreDeclared | EdgeSecondChance
    requested_delta    int  NOT NULL
    applied_delta      int  NOT NULL     -- differs when clamped
    value_after        int  NOT NULL
    created_at_utc     timestamptz NOT NULL
    UNIQUE (character_id, request_id)
    INDEX (character_id, created_at_utc DESC)
```

Edge spent by a roll writes here too, so one query answers "what happened to this
character's live state, and why" — the review surface a Storyteller needs.
`requested_delta` versus `applied_delta` is what makes a clamp visible rather
than silent. Append-only, and the natural precursor to Milestone 2's effects.

### Derived Values

```csharp
public sealed record CharacterDerivedValues(
    int PhysicalLimit, int MentalLimit, int SocialLimit,
    int PhysicalConditionMonitor, int StunConditionMonitor,
    int ConditionMonitorOverflow,
    int WoundModifier);
```

`DerivedValueCalculator` is pure: attributes and Essence in, derived values out.
It owns the `derived.inherent-limits` and `derived.condition-monitor` formulas
currently inlined in `DerivedStatisticsEvaluator`, plus the new
`condition.wound-modifier` calculation. Limits are recomputed from current
effective attributes on every resolution; `CanonicalDerivedStatistics` is a
creation-time record, not a live value.

### Test Specification And Free-Form Request

```csharp
public enum ShadowrunTestKind { Success, Threshold, Opposed }
public enum TestLimit { None, Physical, Mental, Social }
public enum EdgeUse { None, PreDeclared }
public enum TestVisibility { Room, Private }

// Server-authored intent. An action option carries one of these. In this
// milestone the catalog is code-defined; Milestones 3-5 make it admin-authored
// content without changing this shape or the resolution path.
public sealed record TestSpecification(
    string Id,
    string DisplayName,                       // the action option's label
    string AttributeId,
    string? SecondAttributeId,
    string? SkillId,
    IReadOnlyList<string> QualifyingSpecializations,
    TestLimit Limit,
    int? Threshold,
    int? OpposingPool,
    IReadOnlyList<DicePoolModifier> FixedModifiers,
    bool IsDevelopmentOnly);

// Player-authored intent, for play outside an encounter.
public sealed record FreeFormTestRequest(
    string AttributeId,
    string? SecondAttributeId,
    string? SkillId,
    bool ApplySpecialization,
    TestLimit Limit,
    int? Threshold,
    int? OpposingPool,
    int SituationalModifier,
    string? ModifierLabel,
    string? Description);

public abstract record TestInvocation
{
    public sealed record Specified(string SpecificationId) : TestInvocation;
    public sealed record FreeForm(FreeFormTestRequest Request) : TestInvocation;
}

public sealed record ShadowrunTest(
    ShadowrunTestKind Kind,
    IReadOnlyList<DicePoolComponent> BaseComponents,
    IReadOnlyList<DicePoolModifier> Modifiers,
    int FinalDicePool,
    int? Limit,
    bool IgnoreLimit,                         // pre-declared Edge
    bool RuleOfSix,
    int? Threshold,
    int? OpposingPool);
```

`TestComposer` is the single place that turns either kind of intent plus a
snapshot into a `ShadowrunTest`:

```csharp
ShadowrunTestResult Compose(TestInvocation invocation, EdgeUse edge, CharacterRulesSnapshot snapshot);
```

It rejects unknown attribute and skill IDs, a request naming both a second
attribute and a skill, a specialization claim the character cannot back, an
out-of-bounds modifier, and a pre-declared Edge spend at zero Edge. Test kind is
derived: an opposing pool means `Opposed`, a threshold means `Threshold`,
otherwise `Success`.

Milestone 2's `GameAction` produces a `ShadowrunTest` through this same composer.
That is the entire reason `ShadowrunTest` is separate from both intent types.

### Modifier Engine

```csharp
public sealed record DicePoolComponent(string Source, string Label, int Value);
public sealed record DicePoolModifier(ModifierSource Source, string Label, int Value);

public enum ModifierSource { Wounds, Specialization, Defaulting, Edge, Situational, Specification }
```

Example breakdown for a wounded character pre-declaring Edge on an action option:

```text
Agility                          5     (component, attribute)
Sneaking (Stealth group)         4     (component, skill)
Specialization: Urban           +2     (modifier, Specialization)
Wound Modifier                  -1     (modifier, Wounds)
Poor lighting                   -2     (modifier, Specification)
Edge (pre-declared)             +3     (modifier, Edge)
--------------------------------------------------------------
Final Dice Pool                 11
Physical Limit                   5     (ignored - Edge)
Rule of Six                     on
```

`FinalDicePool` must always equal the sum of components and modifiers, clamped at
zero. That invariant is a test.

`Specification` and `Situational` are the two authored channels — encounter
content and Storyteller ruling respectively. When automated encounters mature,
`Environment`, `Gear`, `Ware`, and `Effect` join them rather than replacing them.

### Dice Roller

```csharp
public interface IShadowrunDiceRoller
{
    DicePoolRoll Roll(int dicePool, int? limit, bool ruleOfSix);

    DicePoolRoll RerollFailures(IReadOnlyList<int> originalFaces, int? limit, bool ruleOfSix);
}

public sealed record DicePoolRoll(
    int DicePool,
    IReadOnlyList<int> Faces,
    int RawHits,
    int? Limit,
    int LimitedHits,
    int Ones,
    bool Glitch,
    bool CriticalGlitch,
    int ExplosionCount);
```

The roller consumes the existing `IDiceRandom` and knows nothing about
characters, skills, wounds, or why the pool is what it is. `RerollFailures` takes
the original faces so a Second Chance is computed from the recorded roll rather
than a re-derived pool — the character's state may have changed since.

The Rule of Six needs a bounded iteration cap so a pathological random source
cannot loop; `GameEngineOptions.MaxExplosionDepth` owns it alongside
`MaxDicePool`. This section is separate from the chat `Dice` section.

Determinism in tests comes from a stub `IDiceRandom`, matching `DiceEngineTests`.

### Resolution Result

```csharp
public sealed record TestResolutionResult(
    Guid ResolutionId,
    Guid? AmendsResolutionId,
    Guid CharacterId,
    string CharacterName,
    string? SpecificationId,               // null for free-form
    string DisplayName,
    ShadowrunTestKind Kind,
    IReadOnlyList<DicePoolComponent> BaseComponents,
    IReadOnlyList<DicePoolModifier> Modifiers,
    int FinalDicePool,
    int? Limit, string? LimitLabel, bool LimitIgnored,
    bool RuleOfSix,
    int RawHits, int LimitedHits,
    bool Glitch, bool CriticalGlitch,
    int? Threshold,
    int? OpponentPool, int? OpponentHits, int? NetHits,
    bool Success,
    bool PoolExhausted,
    EdgeUse EdgeUse,
    bool EdgeRerollAvailable,              // false whenever Edge was pre-declared
    int CurrentEdgeAfter,
    DateTimeOffset ResolvedAtUtc);
```

One record serves the hub response, the room broadcast, the audit payload, and
the UI. Raw die faces stay out of the transport contract — they are in the audit
row — so the surface does not imply a client-side reroll.

`EdgeRerollAvailable` is the server's single source of truth for the mutual
exclusion: it is false when Edge was pre-declared, when Edge is zero, when the
resolution is already amended, when the session has moved on, and for anyone who
is not the acting player.

### Resolution Pipeline

```text
Client selects an action option, or submits a built roll
        v
Hub resolves the authenticated user's active play session
        v
Begin transaction; lock the play-session row (MovementStore discipline)
        v
Return the recorded result if (characterId, requestId) already exists
        v
Load composed sheet + runtime state -> CharacterRulesSnapshot
        v
TestComposer validates intent -> ShadowrunTest   (rejects here, before any spend)
        v
Spend Edge if pre-declared; decrement runtime state under its version token;
append a runtime event
        v
IShadowrunDiceRoller -> actor roll, then opponent roll if opposed
        v
TestResolutionResult
        v
Persist GameTestRecord; persist the TestResult chat message if visibility is Room
        v
Renew session activity; commit
        v
Broadcast to the room group; return the result to the caller
```

Persist before broadcasting, as with chat and movement. Second Chance and the
tracker commands follow the same shape: lock, dedupe, validate, mutate under the
version token, append the audit row, persist the message, commit, broadcast.

The steps Milestone 2 inserts — validate action, apply effects, commit state
changes, emit events — slot between resolution and commit without reordering
anything here.

### Resolution Audit

```text
game_test_records
    id                    uuid PK
    character_id          uuid NOT NULL, FK -> characters, ON DELETE RESTRICT
    request_id            uuid NOT NULL
    amends_resolution_id  uuid NULL, FK -> game_test_records
    play_session_id       uuid NULL, FK -> play_sessions
    room_id               uuid NULL, FK -> rooms
    chat_message_id       uuid NULL, FK -> chat_messages
    specification_id      text NULL
    kind                  int  NOT NULL
    final_dice_pool       int  NOT NULL
    limited_hits          int  NOT NULL
    success               bool NOT NULL
    edge_use              int  NOT NULL
    result                jsonb NOT NULL     -- full result + raw faces, bounded
    created_at_utc        timestamptz NOT NULL
    UNIQUE (character_id, request_id)
    UNIQUE (amends_resolution_id)            -- NULLs permitted; one amendment max
    INDEX (character_id, created_at_utc DESC)
```

Two unique indexes carry the correctness guarantees. `(character_id, request_id)`
stops a double-submit from spending Edge twice. `(amends_resolution_id)` stops
two concurrent Second Chance clicks from both amending one roll — Postgres
permits multiple NULLs, so unamended rows are unaffected. Both are enforced in
the database, not only in Application code.

Append-only. An amendment is a new linked row, never an edit.
`character_action_receipts` is left alone; it stays reserved for state-mutating
career operations as SHEET-903 designed it.

### Transport

SignalR, on the existing hub — all of these are room-scoped gameplay actions:

```csharp
Task<TestResolutionResult> ResolveTest(TestInvocation invocation, EdgeUse edge,
                                       TestVisibility visibility, Guid requestId);
Task<TestResolutionResult> SpendEdgeOnResolution(Guid resolutionId, Guid requestId);
Task<RuntimeStateSummary> ApplyDamage(DamageTrack track, int amount, Guid requestId);
Task<RuntimeStateSummary> HealDamage(DamageTrack track, int amount, Guid requestId);
Task<RuntimeStateSummary> AdjustEdge(int delta, Guid requestId);
```

All follow `RollDice`: `RequireAuthoritativeJoinedStateAsync`, delegate to
MediatR, map failures to `HubException`. Room-visible operations broadcast the
persisted `MessageReceived` plus a structured callback — `TestResolved` for
rolls, `RuntimeStateChanged` for tracker changes — so cards and trackers update
without a refetch.

HTTP:

```text
GET /api/game-engine/characters/{id}/roll-options   -> builder and tracker source
GET /api/game-engine/action-options                 -> available specifications
GET /api/game-engine/characters/{id}/tests          -> cursor-paginated own history
```

`GET /action-options` is the seam Milestones 3-5 grow into: in this milestone it
returns the code-defined catalog filtered for the environment; later it returns
the options the player's current room and encounter offer.

## Frontend Surface

- `frontEnd/src/api/gameEngine.ts` — typed contracts mirroring `RollOptions`,
  `TestSpecification`, `TestInvocation`, and `TestResolutionResult`.
- `useRoomChat` gains `resolveTest`, `spendEdge`, `applyDamage`, `healDamage`,
  and `adjustEdge`, generating request IDs with `crypto.randomUUID()` and
  exposing a `resolving` flag alongside the existing `rolling`.
- **`ActionOptions`** panel: one button per available specification, fetched from
  the server. Selecting one resolves its roll immediately — no intermediate
  form — with an Edge checkbox alongside for the player's own choice. This is the
  surface encounter content will populate.
- **`RollBuilder`** panel for manual rolls: attribute select, second-component
  select offering skills (with resolved ratings and group provenance) and
  attributes in one grouped list, a specialization checkbox shown only when one
  exists, limit select showing each computed value, optional threshold and
  opposing-pool fields, a bounded modifier stepper with a label field, a
  non-authoritative live pool preview, an Edge checkbox, and a Room/Private
  toggle.
- **`ConditionTracker`** in the cockpit: physical and stun boxes filled against
  their maximums, the current wound modifier, and current Edge out of the Edge
  attribute. Updated from `RuntimeStateChanged` without a reload.
- **`TestResultCard`**: every component and modifier with its source label, final
  pool, limit (struck through when ignored), hits, glitch state, threshold or net
  hits, and outcome. Rendered for every player's room-visible roll so the
  Storyteller sees them.
- The **"Spend Edge — Reroll Failures"** button appears only on the acting
  player's own card and only when `edgeRerollAvailable` is true — which the
  server sets false for any pre-declared roll. Clicking disables it immediately.
  The amendment renders as a linked follow-up card; the original stays visible.
- Commands added to the parser and `COMMANDS`:
  `/damage <physical|stun> <amount>`, `/heal <physical|stun> <amount>`,
  `/edge <+n|-n>`. Each validates locally for usage, then delegates. `/help`
  states plainly that `/heal` is bookkeeping, not the SR5 healing rules.
- `/roll` is unchanged. No `/test` command: the builder has too many inputs to
  express on a command line, and a half-expressive command would mislead.

## Tickets

### ENGINE-1001: Freeze The Test-Resolution And Edge Rules Contract

**Depends on:** nothing. Blocks ENGINE-1004, ENGINE-1005, ENGINE-1007.

**Scope:**

- Record every row of the Rules Contract in `SR5_RULE_DECISIONS.md` with printed
  and PDF page citations.
- Record the owner decisions, marking the four already decided and routing the
  rest for approval.
- Produce `roadmap/ENGINE_1001_TEST_RESOLUTION_BASELINE.md` with the cited rules,
  the exact wound-modifier table, and worked examples for: a plain success test,
  a threshold test, an opposed test, a pre-declared Edge test exercising the Rule
  of Six, a Second Chance reroll, a glitch, and a critical glitch.

**Acceptance criteria:**

- Every rule the engine implements has a citation; none is implemented from an
  external summary or from memory.
- `edge.second-chance-limit` and `edge.glitch-interaction` are resolved
  explicitly, since they change observable outcomes.
- `edge.once-per-test` records whether RAW agrees with the decided product
  behavior, and says so either way.
- Worked examples are reusable verbatim as test fixtures.

### ENGINE-1002: Character Rules Adapter, Derived Values, And Roll Options

**Depends on:** ENGINE-1001 (`test.group-derived-skill-rating` approval).

**Scope:**

- Extract the limit and Condition Monitor formulas from
  `DerivedStatisticsEvaluator` into a pure `DerivedValueCalculator`; refactor the
  evaluator to call it. Add the wound-modifier calculation.
- Add `ICharacterRulesAdapter` and `CharacterRulesSnapshot`, resolving
  skill-group ratings into effective skill ratings.
- Add `GetRollOptionsQuery` and its owner-scoped HTTP endpoint.
- Resolve every ID against the pinned catalog and semantic digest, reusing
  `IRulesetCatalogProvider`.

**Acceptance criteria:**

- Existing `DerivedStatisticsEvaluatorTests` pass unchanged after the refactor.
- A stealth-group character's Sneaking appears in roll options with the correct
  rating and its source identified as the group.
- Active, knowledge, and language skills all appear; untrained skills report
  defaulting rather than rating zero.
- Limits are recomputed from current attributes and do not read
  `CanonicalDerivedStatistics`.
- Unowned, draft, unsupported-schema, and digest-mismatched characters produce
  distinct, non-enumerating failures.
- The adapter and the query perform no writes.

### ENGINE-1003: Runtime State, Runtime Events, And Tracker Commands

**Depends on:** ENGINE-1002.

**Scope:**

- Add `CharacterRuntimeState` and `CharacterRuntimeEvent`, explicit EF
  configurations, and a migration.
- Add `ICharacterRuntimeStateStore` with idempotent lazy initialization and a
  version-checked update path, plus `ICharacterRuntimeEventStore`.
- Add `ApplyDamageCommand`, `HealDamageCommand`, and `AdjustEdgeCommand` with
  their hub methods, the `StateChange` message type, and `StateChangeFormatter`.
- Wire the adapter to compose runtime state into the snapshot.

**Acceptance criteria:**

- Concurrent first operations create exactly one runtime row.
- Edge initializes to the Edge special attribute exactly once and is never
  re-initialized.
- Damage clamps at the track maximum and Edge clamps at the Edge attribute; each
  clamp records `requested_delta` and `applied_delta` differing, and the room
  summary says so.
- A repeated request ID returns the recorded result and mutates nothing.
- A stale version token is rejected with a conflict.
- Negative or out-of-bounds amounts are rejected with field-level diagnostics
  before any write; check constraints back this up.
- Every tracker change appends exactly one runtime event and persists exactly one
  `StateChange` message.
- Runtime state and events cannot outlive their character.
- The wound modifier is nowhere persisted.

### ENGINE-1004: Test Composition And The Modifier Engine

**Depends on:** ENGINE-1001, ENGINE-1002. May run in parallel with ENGINE-1005.

**Scope:**

- Add `TestSpecification`, `TestSpecificationCatalog`, `FreeFormTestRequest`,
  `TestInvocation`, `TestComposer`, `ShadowrunTest`, `DicePoolComponent`, and
  `DicePoolModifier`.
- Implement attribute and skill components, attribute-only tests, defaulting,
  specialization resolution for both paths, wound modifier, specification and
  situational modifiers, and the pre-declared Edge component.
- Derive test kind; implement limit selection and the ignore-limit flag.

**Acceptance criteria:**

- Both invocation paths produce an identical `ShadowrunTest` given equivalent
  intent — proven by a test that composes the same roll each way and compares.
- The final pool always equals the sum of its components and modifiers, clamped
  at zero.
- A specification applies its declared specialization automatically when the
  character holds it, and not otherwise.
- A free-form specialization claim the character cannot back is rejected, not
  silently ignored.
- An out-of-bounds situational modifier is rejected with a field-level
  diagnostic; specification modifiers are not subject to the player bound.
- A request naming both a second attribute and a skill is rejected.
- Pre-declared Edge with zero current Edge is rejected before any state changes.
- Specifications naming an unknown attribute or skill fail at catalog
  construction, not at resolution time.
- A zero-or-below pool yields a zero pool and an explicit exhausted flag.

### ENGINE-1005: SR5 Dice Roller With Rule Of Six And Rerolls

**Depends on:** ENGINE-1001. May run in parallel with ENGINE-1004.

**Scope:**

- Add `IShadowrunDiceRoller`, `ShadowrunDiceRoller`, and `DicePoolRoll` over the
  existing `IDiceRandom`.
- Implement hits, limits, glitches, critical glitches, the Rule of Six, and
  `RerollFailures`.
- Add `GameEngineOptions` with the pool and explosion-depth ceilings.

**Acceptance criteria:**

- Hits, glitch, and critical-glitch determination match ENGINE-1001's worked
  examples exactly, verified with a deterministic stub random source.
- Rule-of-Six explosions accrue hits correctly and terminate at the configured
  depth even against a random source that always returns six.
- `RerollFailures` rerolls exactly the non-hit faces of the supplied roll and
  preserves the original hits.
- Limited hits never exceed the limit; the limit never reduces dice rolled; an
  ignored limit is reflected in the result rather than by passing null.
- Zero dice returns zero hits and no glitch.
- The roller has no dependency on characters, skills, or the adapter — enforced
  by its constructor signature.

### ENGINE-1006: Resolve Tests, Persist Audit, Broadcast Results

**Depends on:** ENGINE-1003, ENGINE-1004, ENGINE-1005.

**Scope:**

- Add `ResolveTestCommand` accepting either invocation path.
- Add `GameTestRecord`, its EF configuration with both unique indexes, migration,
  and store.
- Add `ChatMessageType.TestResult` and `TestResultFormatter`; persist and
  broadcast room-visible results.
- Add the `ResolveTest` hub method, the `TestResolved` callback, the action-option
  list endpoint, and the history endpoint.
- Add request-ID idempotency and session-activity renewal.

**Acceptance criteria:**

- Success, threshold, and opposed tests resolve through one command from either
  path.
- A repeated request ID returns the recorded result, rolls no new dice, and
  spends no Edge.
- Pre-declared Edge decrements durable Edge exactly once, appends one runtime
  event, and is reflected in the breakdown.
- A resolution without an active play session is rejected.
- A development-only specification is not resolvable outside Development and is
  absent from the action-option list.
- A room-visible result persists exactly one `TestResult` message and broadcasts
  one structured payload; a private result persists no message and broadcasts
  nothing.
- Existing transcript pagination and room-visit visibility rules apply unchanged
  to the new message types.
- The persisted record reconstructs the full breakdown including raw faces.
- No resolution mutates the canonical sheet or career state.
- History is owner-scoped and cursor-paginated; other users receive
  non-enumerating not-found responses.

### ENGINE-1007: Post-Roll Edge — Second Chance

**Depends on:** ENGINE-1006.

**Scope:**

- Add `SpendEdgeOnResolutionCommand` and its hub method.
- Implement the amendment path: verify ownership, session window, and
  amendability; spend one Edge; reroll the recorded failures; append a linked
  record and a runtime event; persist and broadcast.
- Compute `EdgeRerollAvailable` on every result.

**Acceptance criteria:**

- A Second Chance rerolls exactly the failed dice of the recorded original and
  never re-derives the pool from current state.
- A roll made with pre-declared Edge reports `EdgeRerollAvailable = false` and
  rejects a forged reroll request — the mutual exclusion is enforced server-side,
  not only by a disabled control.
- Two concurrent reroll requests against one resolution produce exactly one
  amendment; the loser receives a conflict, not a second spend.
- A resolution already amended, belonging to another player, or from an ended
  session offers no reroll and rejects a forged request.
- Spending with zero Edge is rejected before any write.
- The amendment appends a linked record and one runtime event; the original row
  is never updated.
- A room-visible original produces a room-visible amendment.
- Edge decrements exactly once per amendment.

### ENGINE-1008: Action Options, Roll Builder, And Result Cards

**Depends on:** ENGINE-1007.

**Scope:**

- Add typed frontend contracts and the `useRoomChat` methods.
- Build `ActionOptions`, `RollBuilder`, `TestResultCard`, and the Edge
  affordances.
- Render `TestResult` messages in the transcript for every player in the room.

**Acceptance criteria:**

- Selecting an action option resolves its roll in one interaction with no
  intermediate form.
- Builder dropdowns are populated only from server-supplied roll options and show
  current ratings; a player cannot select a skill they do not have.
- The specialization checkbox appears only when a specialization exists.
- The live pool preview is visibly non-authoritative and is replaced by the
  server breakdown after rolling.
- Checking the pre-roll Edge box produces a result card with no reroll button;
  the two Edge affordances are never simultaneously available.
- The Edge checkbox and reroll button are disabled at zero Edge.
- Double-clicking any roll or Edge control produces exactly one resolution.
- The reroll button appears only on the acting player's own eligible card.
- Amendments render as linked follow-ups; the original card remains visible.
- Other players' room-visible results render with full breakdowns and no Edge
  affordance.
- Frontend tests, lint, and build pass.

### ENGINE-1009: Condition Tracker And State Commands

**Depends on:** ENGINE-1003 for the backend; ENGINE-1008 for cockpit layout.

**Scope:**

- Build `ConditionTracker` and wire `RuntimeStateChanged`.
- Add `/damage`, `/heal`, and `/edge` to the parser, `COMMANDS`, and
  `useGameplayCommands`.
- Render `StateChange` messages in the transcript.

**Acceptance criteria:**

- The tracker reflects damage and Edge changes from the player's own commands,
  from Edge spent by rolling, and from another connection of the same session,
  without a reload.
- The wound modifier shown matches the one the next roll's breakdown reports.
- Usage errors for all three commands are local-only and never reach the server.
- A clamped change is announced as clamped rather than silently succeeding.
- `/help` lists the three commands and states that `/heal` is bookkeeping, not
  the SR5 healing rules.
- Other players see `StateChange` summaries for characters in their room.
- Frontend tests, lint, and build pass.

### ENGINE-1010: Release Gate

**Depends on:** ENGINE-1009.

**Scope:**

- Register the demonstration specifications: `observe-area` (Intuition +
  Perception, Mental Limit, threshold 2) and `sneak-past` (Agility + Sneaking,
  Physical Limit, opposed against a fixed pool).
- Update `PROJECT_CONTEXT.md` with the game-engine boundary, the two invocation
  paths, the structured gameplay-roll model, the `TestResult` and `StateChange`
  message types, the player-authored trust model, and the three new tables.
  Update `README.md` and `ROADMAP.md`.
- Run the full verification suite and walk both play patterns end to end.

**Acceptance criteria:**

- **Automated path:** a player selects `[Observe Area]` in a room and a threshold
  test resolves from their real sheet with no further input; selecting
  `[Sneak Past]` resolves an opposed test the same way.
- **Manual path:** the same player builds an equivalent roll by hand with a
  Storyteller-supplied modifier and gets a comparable breakdown.
- **Trackers:** `/damage physical 3` announces to the room, updates the tracker,
  and visibly changes the next roll's wound modifier; `/heal` reverses it.
- **Edge:** the player pre-declares Edge on one roll — confirming no reroll is
  offered — and takes a Second Chance on another; Edge reaches zero, both
  affordances disable, and `/edge +2` restores it.
- **Audit:** the history explains every resolution and every tracker change,
  including which situational modifiers the player claimed and every clamp.
- `PROJECT_CONTEXT.md` records that structured gameplay rolls are a separate
  approved model from `/roll`, satisfying its own forward reference.
- All backend tests, frontend tests, lint, and build pass.

## Recommended Delivery Sequence

1. ENGINE-1001 — blocking; nothing mechanical starts until the citations and the
   open decisions are recorded. The Edge rows are the ones most likely to need
   discussion.
2. ENGINE-1002, then ENGINE-1003 — the read path, then the state it mutates.
3. ENGINE-1004 and ENGINE-1005 in parallel — the deliberate separation between
   "why is the pool this size" and "resolve these dice" makes them independent.
4. ENGINE-1006 — joins them, adds persistence and room visibility.
5. ENGINE-1007 — the only ticket that amends existing history; kept separate
   because its concurrency guarantees deserve isolated review.
6. ENGINE-1008 and ENGINE-1009 — mergeable if one person builds both.
7. ENGINE-1010.

There is a shippable intermediate release after ENGINE-1006: both invocation
paths work, trackers work, and only post-roll Edge is missing. If the milestone
needs splitting for delivery, that is the seam.

ENGINE-1003 and ENGINE-1006 each add a migration. Both are additive; the new
`ChatMessageType` values extend an existing enum without altering stored rows.

## Definition Of Done

> A player using an existing saved SR5 character can enter an existing live MUSH
> room and resolve Shadowrun tests two ways — by selecting an action option that
> triggers a server-authored roll, and by building a roll by hand — with both
> paths composing real character-sheet attributes and skills, current wound
> penalties, applicable limits, declared specializations, and authored modifiers
> through one engine; with Edge spendable either before or after a roll but never
> both; with damage and Edge tracked live and changeable by the player in view of
> the room; and with an auditable explanation of how every die was arrived at.

Concretely, all of the following hold:

- An action option and an equivalent hand-built roll produce the same breakdown.
- The client submitted intent only; every character-derived value and every die
  came from the server.
- The breakdown accounts for every die with an attributed source.
- The limit was recomputed from current attributes, not read from the frozen
  creation sheet.
- A character trained through a skill group did not default.
- Damage authored by the player changed the pool through a derived, unpersisted
  wound modifier.
- Edge spent before or after a roll decremented durable state exactly once, the
  two paths were never simultaneously offered, and a duplicate submission spent
  nothing.
- The persisted records reconstruct every roll, every amendment, and every
  tracker change.
- No NPC, mission, combat, Matrix, or spellcasting system was required.

## Deferred To Later Game Engine Milestones

`GameAction`, rule context, action requirements and costs; admin authoring of
specifications and encounter content; the active-effect framework with payloads,
durations and stacking; item instances and ammunition; posture and movement
state; NPC templates and instances; encounters and instancing; missions,
objectives, dialogue and rewards; the game event bus; extended and teamwork
tests; initiative and the combat turn; Condition Monitor overflow consequences
and the healing rules; the remaining Edge uses; and every Shadowrun subsystem
beyond the shared test pipeline.

The seams that keep them cheap are: `GetEffectiveAttribute` as the single
attribute read path, `CharacterRuntimeDocument` as the expansion envelope,
`character_runtime_events` as the record of every state change whatever its
source, `TestSpecification` as the shape encounter content will author,
`TestComposer` as the one place a `ShadowrunTest` is built, `ShadowrunTestKind`
for new test forms, `ModifierSource` for new modifier origins, and a resolution
pipeline whose commit boundary already sits where effects and events will be
inserted.
