# Magic And Resonance

This is the row-level CHAR-801 review ledger for the core Magic/Resonance
creation surface. It is a review input, not a runtime catalog and not a
substitute for the approved books. Stable IDs are project IDs. `F` means spell,
ritual, or spirit Force; `L` means complex-form or sprite Level; `PP` means Power
Points; `I`, `S`, and `P` mean Instantaneous, Sustained, and Permanent.

## Creation Paths And Global Rules

Magic and Resonance begin at 0 unless the Magic/Resonance priority supplies the
corresponding attribute. They are mutually exclusive creation paths. Special
attribute points may raise the selected attribute, normally to 6 or to 7 with
Exceptional Attribute. Apply cumulative Essence loss before final eligibility:
reduce current and maximum Magic or Resonance once for each point or fraction of
total Essence lost. Sources: `sr5-core` pp. 65-69, 278 (PDF 67-71, 280).
Decision: `essence.magic-resonance-order`.

| ID | Display name | Classification | Creation eligibility, grants, and limits | Source |
| --- | --- | --- | --- | --- |
| `mundane` | Mundane | `selectable` | Priority E; no Magic or Resonance, magical path grants, magical skills, formulae, powers, or complex forms. | `sr5-core` pp. 65, 68 (PDF 67, 70) |
| `magician` | Magician | `selectable` | Priority A: Magic 6, two Rating 5 Magical skills, 10 formula grants. B: Magic 4, two Rating 4 Magical skills, 7 grants. C: Magic 3, 5 grants. May astrally perceive/project; may use Sorcery, Conjuring, and Enchanting; tradition required. | `sr5-core` pp. 65, 69 (PDF 67, 71); `run-faster` p. 63 (PDF 65) |
| `mystic-adept` | Mystic Adept | `selectable` | Same A/B/C Magic, skill, and formula grants as magician. May use all three magical groups; cannot astrally project; astral perception requires its adept power. No free PP: may buy whole PP for 5 Karma each (published errata; `sr5-core` prints 2 Karma — see `mystic-adept.power-point-cost-errata`), maximum PP equal to Magic. Tradition required. | `sr5-core` pp. 65, 69, 71, 101 (PDF 67, 71, 73, 103); `run-faster` p. 63 (PDF 65). Decision: `mystic-adept.power-points`, `mystic-adept.power-point-cost-errata` |
| `adept` | Adept | `selectable` | Priority B: Magic 6 and one Rating 4 Active skill. C: Magic 4 and one Rating 2 Active skill. D: Magic 2. Receives PP equal to Magic; cannot use Sorcery, Conjuring, or Enchanting; cannot project; perception requires its power. No tradition required. | `sr5-core` pp. 65, 69, 308 (PDF 67, 71, 310). Decision: `magic.tradition-by-path` |
| `aspected-magician` | Aspected Magician | `parameterized` | Priority B: Magic 5 and one Rating 4 selected Magical skill group. C: Magic 3 and one Rating 2 selected group. D: Magic 2. Exactly one permanent aspect; may perceive but not project; tradition required. | `sr5-core` pp. 65, 68-69 (PDF 67, 70-71). Decision: `magic.tradition-by-path` |
| `technomancer` | Technomancer | `selectable` | Priority A: Resonance 6, two Rating 5 Resonance skills, 5 forms. B: Resonance 4, two Rating 4 Resonance skills, 2 forms. C: Resonance 3 and 1 form. Grant-eligible Resonance-linked skills are Compiling, Decompiling, and Registering; no Magic selections. | `sr5-core` pp. 65, 68, 143, 250-252 (PDF 67, 70, 145, 252-254). Decision: `technomancer.priority-grants` |

Aspected values are included components of `aspected-magician`, not separately
purchased paths:

| ID | Display name | Classification | Permanent eligibility | Source |
| --- | --- | --- | --- | --- |
| `sorcery` | Sorcery | `included-component` | May use Spellcasting, Counterspelling, and Ritual Spellcasting; may buy spells and rituals, but not preparations. | `sr5-core` pp. 68-69, 278 (PDF 70-71, 280). Decision: `magic.aspected-purchase-scope` |
| `conjuring` | Conjuring | `included-component` | May use Summoning, Binding, and Banishing; may buy neither spells, rituals, nor preparations. | `sr5-core` pp. 68-69, 278 (PDF 70-71, 280). Decision: `magic.aspected-purchase-scope` |
| `enchanting` | Enchanting | `included-component` | May use Alchemy, Artificing, and Disenchanting; may buy preparations, but not spells or rituals. | `sr5-core` pp. 68-69, 278 (PDF 70-71, 280). Decision: `magic.aspected-purchase-scope` |

### Formula Grants And Caps

- Each magician or mystic-adept priority grant may be assigned independently to
  a spell, ritual, and/or alchemical preparation. This is the sole approved
  Run Faster content clarification; it admits no Run Faster catalog entry.
  Source: `run-faster` p. 63 (PDF 65). Decision:
  `magic.priority-grant-formula-types`.
- A formula granted by priority is included and costs no Karma. An additional
  spell, ritual, or preparation costs 5 Karma at creation. A spell and its
  alchemical version are different formulae and separate selections. Sources:
  `sr5-core` pp. 69, 98, 299, 304 (PDF 71, 100, 301, 306).
- Apply three independent creation caps: spells <= Magic x 2, rituals <= Magic x
  2, and preparations <= Magic x 2. Grants and purchases both count. Source:
  `sr5-core` pp. 69, 98 (PDF 71, 100). Decision:
  `magic.formula-cap-scope`.
- Formula selections must also satisfy the path/aspect eligibility above. A
  required tradition is selected before formula-dependent lodge, spirit, focus,
  and learning validation. Decision: `magic.aspected-purchase-scope` and
  `magic.tradition-by-path`.
- Priority-granted magical or Resonance skills may be raised later. Reject a
  duplicate grant/purchase allocation that would discard value, and enforce the
  final natural skill cap across all sources. Source: `sr5-core` pp. 68, 88
  (PDF 70, 90). Decision: `skill.priority-grant-collision`.
- A technomancer priority grant is included and an additional complex form costs
  4 Karma. Total known forms, including grants, cannot exceed
  `min(Logic, Resonance x 2)` at creation. Sources: `sr5-core` pp. 98, 252
  (PDF 100, 254). Decision: `resonance.complex-form-cap`.

## Traditions

| ID | Display name | Classification | Drain | Combat / Detection / Health / Illusion / Manipulation spirit | Creation facts | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `hermetic` | Hermetic | `selectable` | Logic + Willpower | Fire / Air / Man / Water / Earth | Reagents favor minerals, ores, elements, and old urban objects; lodge and formula must match tradition or use the stated translation/different-tradition rules. | `sr5-core` pp. 279-280, 299, 306, 316 (PDF 281-282, 301, 308, 318) |
| `shamanic` | Shamanic | `selectable` | Charisma + Willpower | Beasts / Water / Earth / Air / Man | Reagents favor natural plant, animal, stone, water, and handcrafted objects; lodge and formula must match tradition or use the stated translation/different-tradition rules. | `sr5-core` pp. 279-280, 299, 306, 316 (PDF 281-282, 301, 308, 318) |

Custom traditions are excluded because the core gives examples and narrative
latitude but no deterministic construction rules. Source: `sr5-core` p. 279
(PDF 281).

## Spells And Generated Preparations

Every row is one learnable spell formula and generates one separately learnable
alchemical-preparation formula with ID `<spell-id>-preparation`. The generated
formula inherits category, type, range, duration, Drain, keywords, parameter,
and effect, subject to the preparation rules below. Spell Force is selected at
cast time up to Magic x 2; Force limits hits; Drain is at least 2 and becomes
Physical when post-limit casting hits exceed Magic. Sources: `sr5-core`
pp. 281-285, 304-306 (PDF 283-287, 306-308).

### Combat

Direct spells inflict net hits as unresisted damage after the opposed test
(Mana: Willpower; Physical: Body). Indirect spells use Spellcasting + Magic
versus Reaction + Intuition; DV is F + net hits, AP is -F, and damage is resisted
with Body + modified Armor. Area indirect spells use threshold 3 and grenade
scatter. Source: `sr5-core` pp. 283-285 (PDF 285-287).

| ID | Display name | Class | Type | Range | Duration | Drain | Parameters, keywords, and effect limits | Preparation | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `acid-stream` | Acid Stream | `selectable` | P | LOS | I | F - 3 | Indirect, elemental; Physical acid damage and acid secondary effects; one target. | `acid-stream-preparation` (`generated`) | `sr5-core` p. 283 (PDF 285) |
| `toxic-wave` | Toxic Wave | `selectable` | P | LOS (A) | I | F - 1 | Indirect, elemental; Physical acid damage and secondary effects; area. | `toxic-wave-preparation` (`generated`) | `sr5-core` p. 283 (PDF 285) |
| `punch` | Punch | `selectable` | P | T | I | F - 6 | Indirect; Stun damage; touched target. | `punch-preparation` (`generated`) | `sr5-core` pp. 283-284 (PDF 285-286) |
| `clout` | Clout | `selectable` | P | LOS | I | F - 3 | Indirect; Stun damage; one target. | `clout-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `blast` | Blast | `selectable` | P | LOS (A) | I | F | Indirect; Stun damage; area. | `blast-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `death-touch` | Death Touch | `selectable` | M | T | I | F - 6 | Direct; Physical damage; touched living/magical target resists with Willpower. | `death-touch-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `manabolt` | Manabolt | `selectable` | M | LOS | I | F - 3 | Direct; Physical damage; one living/magical target resists with Willpower. | `manabolt-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `manaball` | Manaball | `selectable` | M | LOS (A) | I | F | Direct; Physical damage; living/magical targets in area resist with Willpower. | `manaball-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `flamethrower` | Flamethrower | `selectable` | P | LOS | I | F - 3 | Indirect, elemental; Physical fire damage; may ignite flammables; one target. | `flamethrower-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `fireball` | Fireball | `selectable` | P | LOS (A) | I | F - 1 | Indirect, elemental; Physical fire damage; may ignite flammables; area. | `fireball-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `lightning-bolt` | Lightning Bolt | `selectable` | P | LOS | I | F - 3 | Indirect, elemental; Physical electricity damage; one target. | `lightning-bolt-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `ball-lightning` | Ball Lightning | `selectable` | P | LOS (A) | I | F - 1 | Indirect, elemental; Physical electricity damage; area. | `ball-lightning-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `shatter` | Shatter | `selectable` | P | T | I | F - 6 | Direct; Physical damage; touched living or nonliving target resists with Body. | `shatter-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `powerbolt` | Powerbolt | `selectable` | P | LOS | I | F - 3 | Direct; Physical damage; one living or nonliving target resists with Body. | `powerbolt-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `powerball` | Powerball | `selectable` | P | LOS (A) | I | F | Direct; Physical damage; living and nonliving targets in area resist with Body. | `powerball-preparation` (`generated`) | `sr5-core` p. 284 (PDF 286) |
| `knockout` | Knockout | `selectable` | M | T | I | F - 6 | Direct; Stun damage; touched living/magical target resists with Willpower. | `knockout-preparation` (`generated`) | `sr5-core` pp. 284-285 (PDF 286-287) |
| `stunbolt` | Stunbolt | `selectable` | M | LOS | I | F - 3 | Direct; Stun damage; one living/magical target resists with Willpower. | `stunbolt-preparation` (`generated`) | `sr5-core` pp. 284-285 (PDF 286-287) |
| `stunball` | Stunball | `selectable` | M | LOS (A) | I | F | Direct; Stun damage; living/magical targets in area resist with Willpower. | `stunball-preparation` (`generated`) | `sr5-core` p. 285 (PDF 287) |

### Detection

All have range T because the spell gives its touched subject a sense. Standard
sense radius is F x caster Magic meters; Extended Area is ten times that.
`Active` uses the category's opposed tests; `Passive` substitutes casting net
hits for the subject's Mental limit on relevant Perception Tests. Source:
`sr5-core` pp. 285-289 (PDF 287-291).

| ID | Display name | Class | Type | Range | Duration | Drain | Parameters, keywords, and effect limits | Preparation | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `analyze-device` | Analyze Device | `selectable` | P | T | S | F - 3 | Active, directional; oppose Object Resistance; each net hit gives one non-obvious fact and +1 die to operate it; ignores defaulting while sustained. | `analyze-device-preparation` (`generated`) | `sr5-core` p. 285 (PDF 287) |
| `analyze-magic` | Analyze Magic | `selectable` | P | T | S | F - 3 | Active, directional; analyzes a magical object; net hits count as Assensing-table hits without astral perception. | `analyze-magic-preparation` (`generated`) | `sr5-core` p. 285 (PDF 287) |
| `analyze-truth` | Analyze Truth | `selectable` | M | T | S | F - 2 | Active, directional; at least 1 net hit judges an in-person heard statement by speaker belief; no recordings/writing. | `analyze-truth-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `clairaudience` | Clairaudience | `selectable` | M | T | S | F - 3 | Passive, directional; remote natural hearing point, movable as Complex Action; replaces normal hearing; no augmented range. | `clairaudience-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `clairvoyance` | Clairvoyance | `selectable` | M | T | S | F - 3 | Passive, directional; remote natural visual point, movable as Complex Action; replaces normal/astral vision; no sound, augmentation, or spell targeting through it. | `clairvoyance-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `combat-sense` | Combat Sense | `selectable` | M | T | S | F | Active, psychic; each casting hit adds +1 die to Reaction for Surprise and ranged/melee defense. | `combat-sense-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `detect-enemies` | Detect Enemies | `selectable` | M | T | S | F - 2 | Active, area; detects living beings with hostility directed at subject; excludes traps and undirected random violence. | `detect-enemies-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `detect-enemies-extended` | Detect Enemies, Extended | `selectable` | M | T | S | F | Active, Extended Area; same target limits as Detect Enemies at x10 range. | `detect-enemies-extended-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `detect-individual` | Detect Individual | `selectable` | M | T | S | F - 3 | Active, area; at casting, specify an individual known/met previously; detects that individual in range. | `detect-individual-preparation` (`generated`) | `sr5-core` p. 286 (PDF 288) |
| `detect-life` | Detect Life | `selectable` | M | T | S | F - 3 | Active, area; detects count and relative location of living beings, not spirits; crowd detail degrades. | `detect-life-preparation` (`generated`) | `sr5-core` pp. 286-287 (PDF 288-289) |
| `detect-life-extended` | Detect Life, Extended | `selectable` | M | T | S | F - 1 | Active, Extended Area; same targets and information as Detect Life at x10 range. | `detect-life-extended-preparation` (`generated`) | `sr5-core` pp. 286-287 (PDF 288-289) |
| `detect-life-form` | Detect [Life Form] | `parameterized` | M | T | S | F - 2 | Active, area; required life-form type; each type is learned separately; reports count and relative location. | `detect-life-form-preparation` with same parameter (`generated`) | `sr5-core` p. 287 (PDF 289) |
| `detect-life-form-extended` | Detect [Life Form], Extended | `parameterized` | M | T | S | F | Active, Extended Area; required life-form type; separately learned; x10 range. | `detect-life-form-extended-preparation` with same parameter (`generated`) | `sr5-core` p. 287 (PDF 289) |
| `detect-magic` | Detect Magic | `selectable` | M | T | S | F - 2 | Active, area; detects active foci, spells, wards, lodges, preparations, rituals, and spirits; excludes Awakened/critter status, signatures, expired/triggered preparations, and completed permanent effects. | `detect-magic-preparation` (`generated`) | `sr5-core` p. 287 (PDF 289) |
| `detect-magic-extended` | Detect Magic, Extended | `selectable` | M | T | S | F | Active, Extended Area; same inclusions/exclusions as Detect Magic at x10 range. | `detect-magic-extended-preparation` (`generated`) | `sr5-core` p. 287 (PDF 289) |
| `detect-object` | Detect [Object] | `parameterized` | P | T | S | F - 2 | Active, area; required object type; each type is separately learned; reports count and relative location. | `detect-object-preparation` with same parameter (`generated`) | `sr5-core` p. 287 (PDF 289) |
| `mindlink` | Mindlink | `selectable` | M | T | S | F - 1 | Active, psychic; one voluntary target; 1 hit establishes exchange of speech, emotion, and images while target remains in sense range. | `mindlink-preparation` (`generated`) | `sr5-core` p. 287 (PDF 289) |
| `mind-probe` | Mind Probe | `selectable` | M | T | S | F | Active, directional; chosen target knows probing; 1-2 net hits surface thoughts, 3-4 conscious knowledge/recent 72-hour memories, 5+ subconscious; one fact per Complex Action; repeated within target Willpower hours is -2 dice. | `mind-probe-preparation` (`generated`) | `sr5-core` p. 287 (PDF 289) |

### Health

All require touching the subject. Low-Essence spells marked `Essence` apply the
target's actual Essence minus maximum Essence, rounded up, as a casting-pool
modifier. Health magic cannot erase Stun damage or cure psychological
conditions. Source: `sr5-core` pp. 287-289 (PDF 289-291).

| ID | Display name | Class | Type | Range | Duration | Drain | Parameters, keywords, and effect limits | Preparation | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `antidote` | Antidote | `selectable` | M | T | P | F - 3 | Before toxin resistance; casting hits add directly to that test. | `antidote-preparation` (`generated`, Command only) | `sr5-core` p. 288 (PDF 290) |
| `cure-disease` | Cure Disease | `selectable` | M | T | P | F - 4 | Essence; after infection, net hits add to Disease Resistance until recovery/death; does not heal existing damage. | `cure-disease-preparation` (`generated`, Command only) | `sr5-core` p. 288 (PDF 290) |
| `decrease-attribute` | Decrease [Attribute] | `parameterized` | P | T | S | F - 2 | Essence; required one Physical/Mental attribute, not Special; opposed by attribute + Willpower; reduce natural/augmented value by net hits; 0 incapacitates/paralyzes Physical or leaves Mental target confused; derived values change. | `decrease-attribute-preparation` with same parameter (`generated`) | `sr5-core` p. 288 (PDF 290) |
| `detox` | Detox | `selectable` | M | T | P | F - 6 | F must equal/exceed toxin base DV; 1 hit removes symptoms/side effects, not damage or future damage. | `detox-preparation` (`generated`, Command only) | `sr5-core` p. 288 (PDF 290) |
| `heal` | Heal | `selectable` | M | T | P | F - 4 | Essence; each hit heals one Physical box or reduces permanence time one Combat Turn; remaining damage cannot later be magically healed. | `heal-preparation` (`generated`, Command only) | `sr5-core` p. 288 (PDF 290) |
| `increase-attribute` | Increase [Attribute] | `parameterized` | P | T | S | F - 3 | Essence; required one Physical/Mental attribute, not Special; voluntary; F >= augmented value; +1 per hit to augmented maximum; one such spell per attribute. | `increase-attribute-preparation` with same parameter (`generated`) | `sr5-core` p. 288 (PDF 290) |
| `increase-reflexes` | Increase Reflexes | `selectable` | P | T | S | F | Essence; voluntary; each hit +1 Initiative, each 2 hits +1D6; one instance; total Initiative Dice max 5D6. | `increase-reflexes-preparation` (`generated`) | `sr5-core` p. 288 (PDF 290) |
| `oxygenate` | Oxygenate | `selectable` | P | T | S | F - 5 | Voluntary; +1 Body die per hit against oxygen deprivation/inhaled gas; permits underwater breathing. | `oxygenate-preparation` (`generated`) | `sr5-core` pp. 288-289 (PDF 290-291) |
| `prophylaxis` | Prophylaxis | `selectable` | M | T | S | F - 4 | +1 die per hit against infection, drugs, toxins; each hit reduces beneficial-drug bonuses by 1; 3+ hits block effects without a bonus/penalty. | `prophylaxis-preparation` (`generated`) | `sr5-core` p. 289 (PDF 291) |
| `resist-pain` | Resist Pain | `selectable` | M | T | P | Damage Value - 6 | Each hit ignores modifiers from one box on both monitors, without healing; ends when damage rises beyond protected level or injuries heal; only highest-hit instance. | `resist-pain-preparation` (`generated`, Command only) | `sr5-core` p. 289 (PDF 291) |
| `stabilize` | Stabilize | `selectable` | M | T | P | F - 4 | F >= existing overflow; must become permanent; hits each reduce permanence by one Combat Turn; prevents further overflow damage. | `stabilize-preparation` (`generated`, Command only) | `sr5-core` p. 289 (PDF 291) |

### Illusion

Mana illusions affect minds and are resisted with Logic + Willpower; Physical
illusions also affect technology and are resisted with Intuition + Logic or
Object Resistance. Illusions cannot directly cause real damage. Source:
`sr5-core` pp. 289-292 (PDF 291-294).

| ID | Display name | Class | Type | Range | Duration | Drain | Parameters, keywords, and effect limits | Preparation | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `agony` | Agony | `selectable` | M | LOS | S | F - 4 | Realistic, single-sense; each net hit simulates 1 Physical and 1 Stun box; filled track prevents action; all simulated boxes vanish when spell ends; one target. | `agony-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `mass-agony` | Mass Agony | `selectable` | M | LOS (A) | S | F - 2 | As Agony, area. | `mass-agony-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `bugs` | Bugs | `selectable` | M | LOS | S | F - 3 | Realistic, multi-sense; each net hit reduces Initiative by 2 initially and again at each Combat Turn while sustained; one target. | `bugs-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `swarm` | Swarm | `selectable` | M | LOS (A) | S | F - 1 | As Bugs, area. | `swarm-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `confusion` | Confusion | `selectable` | M | LOS | S | F - 3 | Realistic, multi-sense; -1 die per net hit to all tests; one target. | `confusion-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `mass-confusion` | Mass Confusion | `selectable` | M | LOS (A) | S | F - 1 | Realistic, multi-sense, area; -1 die per net hit to all tests. | `mass-confusion-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `chaos` | Chaos | `selectable` | P | LOS | S | F - 2 | Realistic, multi-sense; physical Confusion also affecting technological systems; -1 die per net hit; one target. | `chaos-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `chaotic-world` | Chaotic World | `selectable` | P | LOS (A) | S | F | Realistic, multi-sense, area; physical Confusion; -1 die per net hit. | `chaotic-world-preparation` (`generated`) | `sr5-core` p. 290 (PDF 292) |
| `entertainment` | Entertainment | `selectable` | M | LOS (A) | S | F - 3 | Obvious, multi-sense, area; hits rate detail/appeal; affects minds only, not sensors. | `entertainment-preparation` (`generated`) | `sr5-core` pp. 290-291 (PDF 292-293) |
| `trid-entertainment` | Trid Entertainment | `selectable` | P | LOS (A) | S | F - 2 | Obvious, multi-sense, area; hits rate detail/appeal; perceivable by living beings and sensors. | `trid-entertainment-preparation` (`generated`) | `sr5-core` pp. 290-291 (PDF 292-293) |
| `invisibility` | Invisibility | `selectable` | M | LOS | S | F - 2 | Realistic, single-sense; visual concealment from living viewers, not other senses or astral perception; casting hits become later resistance threshold; attacks may suffer Blind Fire. | `invisibility-preparation` (`generated`) | `sr5-core` p. 291 (PDF 293) |
| `improved-invisibility` | Improved Invisibility | `selectable` | P | LOS | S | F - 1 | As Invisibility and also affects technological visual sensors. | `improved-invisibility-preparation` (`generated`) | `sr5-core` p. 291 (PDF 293) |
| `mask` | Mask | `selectable` | M | T | S | F - 2 | Realistic, multi-sense; same basic size/shape; caster chooses appearance and may alter voice/scent; affects living viewers; casting hits become resistance threshold. | `mask-preparation` (`generated`) | `sr5-core` p. 291 (PDF 293) |
| `physical-mask` | Physical Mask | `selectable` | P | T | S | F - 1 | As Mask and also affects technological sensors. | `physical-mask-preparation` (`generated`) | `sr5-core` p. 291 (PDF 293) |
| `phantasm` | Phantasm | `selectable` | M | LOS (A) | S | F - 1 | Realistic, multi-sense, area; caster-authored previously seen object/creature/scene no larger than area; living viewers only; casting hits become resistance threshold. | `phantasm-preparation` (`generated`) | `sr5-core` p. 291 (PDF 293) |
| `trid-phantasm` | Trid Phantasm | `selectable` | P | LOS (A) | S | F | As Phantasm and also affects technological sensors. | `trid-phantasm-preparation` (`generated`) | `sr5-core` p. 291 (PDF 293) |
| `hush` | Hush | `selectable` | M | LOS (A) | S | F - 2 | Realistic, single-sense, area; -1 per casting hit to sonic attacks across area; hearing requires resistance; affects living beings and magical sonic attacks only. | `hush-preparation` (`generated`) | `sr5-core` pp. 291-292 (PDF 293-294) |
| `silence` | Silence | `selectable` | P | LOS (A) | S | F - 1 | As Hush and affects devices, alarms, sonar, communications, and technological sonic weapons. | `silence-preparation` (`generated`) | `sr5-core` pp. 291-292 (PDF 293-294) |
| `stealth` | Stealth | `selectable` | P | LOS | S | F - 2 | Realistic, single-sense; subject/contact movement silent but indirectly moved objects still sound; casting hits become resistance threshold. | `stealth-preparation` (`generated`) | `sr5-core` p. 292 (PDF 294) |

### Manipulation

Damaging manipulation has DV F and AP 0, resisted with Body + Armor. Mental
targets resist with Logic + Willpower and may spend a Complex Action to erode
net hits using Logic + Willpower at -F. Physical manipulation normally opposes
Body + Strength or Object Resistance. Source: `sr5-core` pp. 292-294
(PDF 294-296).

| ID | Display name | Class | Type | Range | Duration | Drain | Parameters, keywords, and effect limits | Preparation | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `animate` | Animate | `selectable` | P | LOS | S | F - 1 | Physical; one inanimate object; oppose Object Resistance (+2 per full 200 kg over 200); rough movement max F m/turn; held/fastened objects require Force x 2 test. | `animate-preparation` (`generated`) | `sr5-core` p. 292 (PDF 294) |
| `mass-animate` | Mass Animate | `selectable` | P | LOS (A) | S | F + 1 | Physical, area; Animate limits applied to multiple inanimate objects. | `mass-animate-preparation` (`generated`) | `sr5-core` p. 292 (PDF 294) |
| `armor` | Armor | `selectable` | P | LOS | S | F - 2 | Physical; Armor equal to casting hits; stacks and does not count for encumbrance. | `armor-preparation` (`generated`) | `sr5-core` p. 292 (PDF 294) |
| `control-actions` | Control Actions | `selectable` | M | LOS | S | F - 1 | Mental; controls body, not consciousness; caster uses own skills and spends Complex Action per forced action; one target. | `control-actions-preparation` (`generated`) | `sr5-core` pp. 292-293 (PDF 294-295) |
| `mob-control` | Mob Control | `selectable` | M | LOS (A) | S | F + 1 | Mental, area; as Control Actions; individual commands need separate actions or one action commands group. | `mob-control-preparation` (`generated`) | `sr5-core` pp. 292-293 (PDF 294-295) |
| `control-thoughts` | Control Thoughts | `selectable` | M | LOS | S | F - 1 | Mental; Standard Action gives commands target obeys as own idea; one target. | `control-thoughts-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295) |
| `mob-mind` | Mob Mind | `selectable` | M | LOS (A) | S | F + 1 | Mental, area; as Control Thoughts; individual commands need separate actions or one action commands group. Range normalized to LOS (A). | `mob-mind-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295). Decision: `spell.mob-mind-area` |
| `fling` | Fling | `selectable` | P | LOS | I | F - 2 | Physical, damaging; object mass <= F kg; casting test replaces ranged attack, Magic replaces Strength for DV/range, using grenade ranges. | `fling-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295) |
| `ice-sheet` | Ice Sheet | `selectable` | P | LOS (A) | I | F | Environmental, area; Agility + Reaction threshold casting hits to avoid prone; vehicles Crash Test; melts 1 m2/minute at room temperature. | `ice-sheet-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295) |
| `ignite` | Ignite | `selectable` | P | LOS | P | F - 1 | Physical; nonliving target opposes Object Resistance; living target opposes Body + Reaction; target catches fire only when permanent. | `ignite-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295) |
| `influence` | Influence | `selectable` | M | LOS | P | F - 1 | Mental; required single authored suggestion; target treats as own; may resist when confronted; expires after net-hits minutes. | `influence-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295) |
| `levitate` | Levitate | `selectable` | P | LOS | S | F - 2 | Physical; threshold mass/200 kg rounded up; unwilling/held targets add Strength + Body; movement F m/Combat Turn within LOS. | `levitate-preparation` (`generated`) | `sr5-core` p. 293 (PDF 295) |
| `light` | Light | `selectable` | P | LOS (A) | S | F - 4 | Environmental, area; mobile point illuminates F-meter radius; each hit offsets one light penalty; cannot blind. | `light-preparation` (`generated`) | `sr5-core` pp. 293-294 (PDF 295-296) |
| `magic-fingers` | Magic Fingers | `selectable` | P | LOS | S | F - 2 | Physical; casting hits become effective Strength/Agility; remote skill use with F as limit; hands remain about one arm-span apart; fine control may require tests. | `magic-fingers-preparation` (`generated`) | `sr5-core` p. 294 (PDF 296) |
| `mana-barrier` | Mana Barrier | `selectable` | M | LOS (A) | S | F - 2 | Environmental, area; barrier Rating equals net hits; impedes magic/astral entities on cast plane, not mundane beings/objects. | `mana-barrier-preparation` (`generated`) | `sr5-core` p. 294 (PDF 296) |
| `physical-barrier` | Physical Barrier | `selectable` | P | LOS (A) | S | F - 1 | Environmental, area; Armor/Structure each equal hits; dome radius/height F or wall F high x 2F long; gas passes; regenerates Structure each turn; ends at Structure 0. | `physical-barrier-preparation` (`generated`) | `sr5-core` p. 294 (PDF 296) |
| `poltergeist` | Poltergeist | `selectable` | P | LOS (A) | S | F - 2 | Environmental, area; whirls objects <=1 kg; Light Fog visibility; 2 Stun/turn resisted Body + Armor, subject to debris adjudication. | `poltergeist-preparation` (`generated`) | `sr5-core` p. 294 (PDF 296) |
| `shadow` | Shadow | `selectable` | P | LOS (A) | S | F - 3 | Environmental, area; F-meter radius; every 2 hits worsens light one category, maximum Total Darkness. | `shadow-preparation` (`generated`) | `sr5-core` p. 294 (PDF 296) |

### Preparation Trigger And Record Rules

| ID | Display name | Classification | Drain adjustment | Trigger and target restrictions | Source |
| --- | --- | --- | ---: | --- | --- |
| `command` | Command | `included-component` | +2 | Creator on physical plane (or manifesting), LOS to preparation, Simple Action. Creator chooses LOS target and chooses among multiple touching targets. Only trigger allowed for healing spells. | `sr5-core` pp. 305-306 (PDF 307-308) |
| `contact` | Contact | `included-component` | +1 | Next living being to touch activates. Touch spell affects toucher (random if multiple); LOS spell chooses nearest viable LOS target; area centers on preparation. Not allowed for healing spells. | `sr5-core` pp. 305-306 (PDF 307-308) |
| `time` | Time | `included-component` | +2 | Required delay parameter; countdown begins after creation; declared hours cannot exceed final Potency or activation is premature. Non-Command LOS uses nearest viable target; area centers on preparation. Not allowed for healing spells. | `sr5-core` pp. 305-306 (PDF 307-308) |

A preparation selection records generated formula ID and inherited parameter,
plus trigger and (for Time) delay. At use time it generates a preparation record
with creator, F (<= Magic x 2), small manipulable nonliving lynchpin, Potency
(Alchemy + Magic [F] versus F net hits), and creation time (F minutes). Full
Potency lasts Potency x 2 hours, then loses 1/hour. Activation uses Potency as
Spellcasting, F as Magic and limit, no Edge or new Drain; Sustained effects last
Potency minutes and Permanent effects use normal permanence. Every target must
be on the physical plane; LOS range is Potency x F meters and area radius is
Potency meters. Sources: `sr5-core` pp. 304-306 (PDF 306-308). Decision:
`preparation.basic-eligibility`.

## Rituals

All rituals require the leader to know the ritual; only the leader must know a
required incorporated spell. Common prerequisites are a same-tradition magical
lodge with Force >= ritual F, an initial offering of F reagent drams, and the
Ritual Spellcasting sealing test. Each participant takes Drain equal to twice
the opposing F x 2 test's hits, minimum 2. Sources: `sr5-core` pp. 295-297
(PDF 297-299). Drain is Physical if the leader's hits in the sealing teamwork
resolution exceed the leader's Magic; otherwise it is Stun.

| ID | Display name | Classification | Keywords | Incorporated-spell prerequisite | Time and result limits | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `curse` | Curse | `selectable` | Material Link, Spell | One Illusion spell; one consumed material link per target. | F hours; casts selected illusion through links; normal spell tests/Drain; link to group exists while sustained. | `sr5-core` p. 297 (PDF 299) |
| `prodigal-spell` | Prodigal Spell | `selectable` | Spell, Spotter | One Combat spell; astrally perceiving participant/bound-spirit spotter must assense target. | F hours; sends direct spell astrally or indirect spell by clear physical path to out-of-LOS target. | `sr5-core` p. 297 (PDF 299) |
| `remote-sensing` | Remote Sensing | `selectable` | Spell, Spotter | One Detection spell; subject in foundation; spotter required if spell has target. | F hours; range F x sum participant Magic x 100 m; all participants share subject's sense; ends if any stops sustaining. | `sr5-core` p. 297 (PDF 299) |
| `ward` | Ward | `selectable` | Anchored | None. | F hours; astral barrier F; volume <=50 m3 x sum participant Magic; lasts sealing-net-hits weeks or permanent for F Karma. | `sr5-core` p. 297 (PDF 299) |
| `circle-of-protection` | Circle of Protection | `selectable` | Anchored | None. | F hours; sphere radius leader Magic; dual mana barrier plus physical barrier at F; lasts sealing-net-hits hours; crossing outward ends it. | `sr5-core` p. 298 (PDF 300) |
| `circle-of-healing` | Circle of Healing | `selectable` | Anchored, Spell | One healing spell known by leader. | F hours; sphere radius leader Magic; sealing net hits become spell net hits for everyone remaining inside; lasts F days. | `sr5-core` p. 298 (PDF 300) |
| `renascence` | Renascence | `selectable` | Anchored, Spell | One area Manipulation spell known by leader. | F hours; sphere radius leader Magic; result uses ritual F/sealing net hits; 1-hour base duration doubled per sealing net hit. | `sr5-core` p. 298 (PDF 300) |
| `watcher` | Watcher | `selectable` | Minion | None; total bound minions <= leader Charisma. | F minutes; lasts F x sealing net hits hours; skills ceil(F/2); generated watcher record uses printed profile and creators' languages. | `sr5-core` pp. 297-298 (PDF 299-300) |
| `homunculus` | Homunculus | `selectable` | Minion | Inanimate body <=F x 10 kg; its Object Resistance joins opposition; total bound minions <= leader Charisma. | F hours; lasts F x sealing net hits days; skills ceil(F/2); generated profile Body=material Structure, Agility/Reaction F-2 minimum 1, Strength F, W/L/I 1, listed skills/power. | `sr5-core` pp. 297-299 (PDF 299-301) |

`Anchored` requires an immobile physical object/symbol relative to Earth;
movement collapses the ritual. `Material Link` consumes an integral target sample.
`Minion` creates a semi-autonomous entity bound to the leader, maximum Charisma.
`Spell` imports a known spell and mentor modifiers. `Spotter` is a participant or
participant's bound spirit who starts in the foundation, leaves to assense the
target, contributes no Teamwork roll, and still takes Drain. Source: `sr5-core`
pp. 296-297 (PDF 298-299).

## Adept Powers

Adepts receive PP equal to Magic; mystic adepts buy whole PP as stated above.
For ranked powers, maximum rank is Magic unless the row gives a lower cap.
Intrinsic powers require no activation unless stated. Source: `sr5-core`
pp. 308-311 (PDF 310-313).

| ID | Display name | Classification | PP | Required parameter / prerequisite | Activation, effect, and limits | Source |
| --- | --- | --- | ---: | --- | --- | --- |
| `adrenaline-boost` | Adrenaline Boost | `parameterized` | 0.25/rank | Rank 1..Magic. | Free Action; +2 Initiative/rank for current turn; next turn Drain equal rank. | `sr5-core` pp. 308-309 (PDF 310-311) |
| `astral-perception` | Astral Perception | `selectable` | 1 | None. | Simple Action; becomes dual-natured and uses normal astral-perception rules; enables Assensing eligibility. | `sr5-core` p. 309 (PDF 311) |
| `attribute-boost` | Attribute Boost ([Attribute]) | `parameterized` | 0.25/rank | Agility, Body, Reaction, or Strength; rank 1..Magic; separate selection per attribute. | Simple Action; Magic + rank hits add to selected attribute up to augmented max for twice hits Combat Turns; dice pools only, not limit/Initiative; Drain=rank afterward. | `sr5-core` p. 309 (PDF 311) |
| `combat-sense` | Combat Sense | `parameterized` | 0.5/rank | Rank 1..Magic. | +1 die/rank to ranged/melee defense; always allowed Perception before possible surprise. | `sr5-core` p. 309 (PDF 311) |
| `critical-strike` | Critical Strike ([Skill]) | `parameterized` | 0.5 | Unarmed Combat, Clubs, Blades, Astral Combat, or one Exotic Melee skill; one selection per distinct skill. | +1 DV with selected skill; compatible with weapons/powers; unranked. | `sr5-core` p. 309 (PDF 311) |
| `danger-sense` | Danger Sense | `parameterized` | 0.25/rank | Rank 1..Magic. | +1 die/rank on Surprise Tests. | `sr5-core` p. 309 (PDF 311) |
| `enhanced-perception` | Enhanced Perception | `parameterized` | 0.5/rank | Rank 1..Magic. | +1 die/rank to all Perception and Assensing Tests. | `sr5-core` p. 309 (PDF 311) |
| `enhanced-accuracy` | Enhanced Accuracy ([Skill]) | `parameterized` | 0.25 | One Combat skill other than Unarmed Combat; distinct skill per repeat. | +1 Accuracy to weapon used with selected skill. | `sr5-core` p. 309 (PDF 311) |
| `improved-ability` | Improved Ability ([Skill]) | `parameterized` | 0.5/rank | Known Combat, Physical, Social, Technical, or Vehicle skill; no groups; rank 1..Magic. | +1 skill Rating/rank; final improved Rating cannot exceed current natural Rating x 1.5 rounded up. | `sr5-core` p. 309 (PDF 311) |
| `improved-physical-attribute` | Improved Physical Attribute ([Attribute]) | `parameterized` | 1/rank | Body, Agility, Reaction, or Strength; rank 1..Magic. | +1 augmented attribute/rank; may exceed natural maximum only to augmented maximum; derived Physical limit may change. | `sr5-core` p. 309 (PDF 311) |
| `improved-potential` | Improved Potential ([Limit]) | `parameterized` | 0.5/rank | Physical, Mental, or Social; at most one selection per limit; rank 1..Magic. | +1/rank to selected inherent limit. | `sr5-core` p. 309 (PDF 311) |
| `improved-reflexes` | Improved Reflexes | `parameterized` | 1.5/2.5/3.5 | Rank 1..3 and <=Magic. | +1 Reaction and +1D6 Initiative/rank; total Initiative Dice max 5D6; cannot combine with other magical/technological Initiative increases. | `sr5-core` p. 310 (PDF 312) |
| `improved-sense` | Improved Sense ([Sense]) | `parameterized` | 0.25 each | Initial closed options: `direction-sense`, `improved-tactile`, `perfect-pitch`, `human-scale`; distinct option per selection. | Direction: +2 Navigation and test to know facing/elevation. Tactile: +2 tactile Perception. Pitch: test to identify tone. Scale: test to determine lifted object's weight to gram. | `sr5-core` p. 310 (PDF 312). Decision: `power.improved-sense-domain` |
| `killing-hands` | Killing Hands | `selectable` | 0.5 | Unarmed Combat use. | Free Action; choose Physical or Stun unarmed damage; attack is magical and works with Astral Perception/other unarmed powers. | `sr5-core` p. 310 (PDF 312) |
| `kinesics` | Kinesics | `parameterized` | 0.25/rank | Rank 1..Magic. | +1 die/rank to resist Social Tests and emotion/truth-reading tests. | `sr5-core` p. 310 (PDF 312) |
| `light-body` | Light Body | `parameterized` | 0.25/rank | Rank 1..Magic. | Add rank to Agility for jump distance and as dice to jumping Gymnastics; reduce fall distance by rank meters. | `sr5-core` p. 310 (PDF 312) |
| `missile-parry` | Missile Parry | `parameterized` | 0.25/rank | Rank 1..Magic; one empty hand. | Interrupt (-5 Initiative); +1 defense die/rank against slow projectile; net hits catch it. | `sr5-core` p. 310 (PDF 312) |
| `mystic-armor` | Mystic Armor | `parameterized` | 0.5/rank | Rank 1..Magic. | +1 Armor/rank, cumulative and no encumbrance; also protects in astral combat. | `sr5-core` p. 310 (PDF 312) |
| `natural-immunity` | Natural Immunity | `parameterized` | 0.25/rank | Rank 1..Magic. | +1 die/rank against toxins and disease. | `sr5-core` p. 311 (PDF 313) |
| `pain-resistance` | Pain Resistance | `parameterized` | 0.5/rank | Rank 1..Magic. | Shift wound penalties one box/rank on both monitors; +2 dice/rank to withstand suffering; does not remove damage. | `sr5-core` p. 311 (PDF 313) |
| `rapid-healing` | Rapid Healing | `parameterized` | 0.5/rank | Rank 1..Magic. | +1 Body die/rank for own Healing Tests and +1 die/rank to any magical/mundane test healing the adept. | `sr5-core` p. 311 (PDF 313) |
| `spell-resistance` | Spell Resistance | `parameterized` | 0.5/rank | Rank 1..Magic. | +1 die/rank to resist spells, spell rituals, preparations, and Innate Spell; not other critter powers; voluntary spells may be allowed. | `sr5-core` p. 311 (PDF 313) |
| `traceless-walk` | Traceless Walk | `selectable` | 1 | None. | No visible/contact-noise traces or pressure/vibration triggers; hearing Perception -4, scent Tracking -2; cannot cross liquids. | `sr5-core` p. 311 (PDF 313) |
| `voice-control` | Voice Control | `parameterized` | 0.5/rank | Rank 1..Magic. | Voice/sound mimicry within metahuman range; +rank to impersonation pool; +1 Social limit/rank. | `sr5-core` p. 311 (PDF 313) |
| `wall-running` | Wall Running | `selectable` | 0.5 | Running skill used for test. | Simple Action; Running + Strength [Magic], meters vertical per hit; horizontal requires Sprint; falls at movement end. | `sr5-core` p. 311 (PDF 313) |

## Mentor Spirits

Requires the 5-Karma Mentor Spirit positive quality and any Awakened path.
Mystic adepts choose either the magician or adept grant when taking the mentor
and cannot change it. All grants and disadvantages are always active. Source:
`sr5-core` pp. 76, 320-321 (PDF 78, 322-323).

| ID | Display name | Classification | All / magician / adept grants and required choices | Disadvantage | Source |
| --- | --- | --- | --- | --- | --- |
| `bear` | Bear | `selectable` | +2 dice to resist damage except Drain / +2 Health spells, preparations, and Health spell rituals / 1 free Rapid Healing rank. | On Physical damage in combat or severe injury to protected person, Simple Charisma + Willpower; berserk 3 turns minus hits (3 averts), attack responsible foes without safety concern; duration extends on retrigger and ends if foes incapacitated. | `sr5-core` p. 321 (PDF 323) |
| `cat` | Cat | `parameterized` | Choose +2 Gymnastics or Sneaking / +2 Illusion spells, preparations, and rituals / 2 free Light Body ranks. | At combat start Charisma + Willpower (3) or cannot make an incapacitating attack; restriction ends after taking Physical damage. Core says nonexistent Infiltration; mapped to Sneaking. | `sr5-core` p. 321 (PDF 323). Decision: `mentor.cat-infiltration` |
| `dog` | Dog | `parameterized` | +2 Tracking / +2 Detection spells, preparations, and rituals / choose 2 free Improved Sense selections from the approved four-option domain. | Charisma + Willpower (3) required to leave someone behind, betray comrades, or allow another to sacrifice themself in follower's place. | `sr5-core` p. 321 (PDF 323). Decision: `power.improved-sense-domain` |
| `dragonslayer` | Dragonslayer | `parameterized` | Choose one Social skill for +2 / +2 Combat spells, preparations, and rituals / one free Enhanced Accuracy selection (choose eligible skill) and 1 Danger Sense rank. | Breaking a promise gives -1 die to all actions until the promise is made good. | `sr5-core` pp. 321-322 (PDF 323-324) |
| `eagle` | Eagle | `selectable` | +2 Perception / +2 summoning spirits of air / 1 Combat Sense rank. | Gains Allergy (pollutants, mild) with no bonus Karma. | `sr5-core` p. 322 (PDF 324) |
| `fire-bringer` | Fire-Bringer | `parameterized` | Choose +2 Artisan or Alchemy / +2 Manipulation spells, preparations, and spell rituals / choose one known non-combat skill for 1 Improved Ability rank. | Simple Charisma + Willpower (3) required to refuse a sincere request for help. | `sr5-core` p. 322 (PDF 324) |
| `mountain` | Mountain | `selectable` | +2 Survival / +2 Counterspelling and anchored rituals / 1 Mystic Armor rank. | Charisma + Willpower (3) required to abandon a plan or proceed without one; failure requires original plan even alone. | `sr5-core` pp. 322-323 (PDF 324-325) |
| `rat` | Rat | `selectable` | +2 Sneaking / +2 Alchemy to harvest reagents and may use any tradition's reagents / 2 Natural Immunity ranks. | Charisma + Willpower (3) required not to flee or seek cover immediately in combat; must fight if neither is possible. | `sr5-core` p. 323 (PDF 325) |
| `raven` | Raven | `selectable` | +2 Con / +2 Manipulation spells, preparations, and spell rituals / free Traceless Walk and 1 Voice Control rank. | Charisma + Willpower (3) required to avoid exploiting misfortune or playing a clever trick/prank even against friends. | `sr5-core` p. 323 (PDF 325) |
| `sea` | Sea | `parameterized` | +2 Swimming / +2 summoning spirits of water / choose one Athletics-group skill for 1 Improved Ability rank. | Charisma + Willpower (3) required to give away owned property or act charitably. | `sr5-core` p. 323 (PDF 325) |
| `seducer` | Seducer | `parameterized` | +2 Con / +2 Illusion spells, preparations, and spell rituals / choose an Acting- or Influence-group skill for 1 Improved Ability rank. | Charisma + Willpower (3) required to avoid an available vice or indulgence. | `sr5-core` p. 323 (PDF 325) |
| `shark` | Shark | `selectable` | +2 Unarmed Combat / +2 Combat spells, preparations, and spell rituals / free Killing Hands. | On Physical damage, Simple Charisma + Willpower; berserk 3 turns minus hits (3 averts), attack responsible foes without safety concern; extends on retrigger and continues against bodies if targets run out. | `sr5-core` pp. 323-324 (PDF 325-326) |
| `snake` | Snake | `selectable` | +2 Arcana / +2 Detection spells, preparations, and spell rituals / 2 Kinesics ranks. | Charisma + Willpower (3) required to avoid pursuing rare secrets/knowledge after receiving hints. | `sr5-core` p. 324 (PDF 326) |
| `thunderbird` | Thunderbird | `parameterized` | +2 Intimidation / +2 summoning spirits of air / one free Critical Strike selection with required eligible skill choice. | Charisma + Willpower (3) required to avoid answering an insult in kind. | `sr5-core` p. 324 (PDF 326). Decision: `mentor.thunderbird-critical-strike` |
| `wise-warrior` | Wise Warrior | `parameterized` | Choose +2 Leadership or Instruction / +2 Combat spells, preparations, and spell rituals / choose one known Combat skill for 1 Improved Ability rank. | Dishonorable/discourteous action gives -1 die to all actions until atonement. | `sr5-core` p. 324 (PDF 326) |
| `wolf` | Wolf | `selectable` | +2 Tracking / +2 Combat spells, preparations, and rituals / 2 Attribute Boost (Agility) ranks. | Charisma + Willpower (3) required to retreat from a fight. | `sr5-core` p. 324 (PDF 326) |

Custom mentor archetypes are excluded: the core explicitly permits GM work but
provides no deterministic construction rules. Source: `sr5-core` p. 320
(PDF 322). Decision: `mentor.custom-archetypes`.

## Complex Forms

At use time choose L up to Resonance x 3. Thread with Software + Resonance [L];
Fading is at least 2, resisted by Resonance + Willpower, and is Physical when
threading hits exceed Resonance. Sustaining gives -2 dice per form. Device-target
forms may target personas. Source: `sr5-core` pp. 251-252 (PDF 253-254).

| ID | Display name | Classification | Target | Duration | Fade | Parameters, test, effect, and limits | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `cleaner` | Cleaner | `selectable` | Persona | P | L + 1 | Simple test; reduce target Overwatch Score 1 per hit. | `sr5-core` p. 252 (PDF 254) |
| `diffusion-of-attack` | Diffusion of Attack | `selectable` | Device | S | L + 1 | Opposed by Willpower + Firewall; Attack reduced by net hits, minimum 1. | `sr5-core` p. 252 (PDF 254) |
| `diffusion-of-sleaze` | Diffusion of Sleaze | `selectable` | Device | S | L + 1 | Opposed by Willpower + Firewall; Sleaze reduced by net hits, minimum 1. | `sr5-core` p. 252 (PDF 254) |
| `diffusion-of-data-processing` | Diffusion of Data Processing | `selectable` | Device | S | L + 1 | Opposed by Willpower + Firewall; Data Processing reduced by net hits, minimum 1. | `sr5-core` p. 252 (PDF 254) |
| `diffusion-of-firewall` | Diffusion of Firewall | `selectable` | Device | S | L + 1 | Opposed by Willpower + Firewall; Firewall reduced by net hits, minimum 1. | `sr5-core` p. 252 (PDF 254) |
| `editor` | Editor | `selectable` | File | P | L + 2 | Opposed by owner's Intuition + Data Processing; net hits permit equivalent Edit File changes. | `sr5-core` p. 252 (PDF 254) |
| `infusion-of-attack` | Infusion of Attack | `selectable` | Device | S | L + 1 | L >= current Attack; +1 Attack/hit, maximum twice normal; one Infusion per attribute; swapping boosted attribute ends form. | `sr5-core` p. 252 (PDF 254) |
| `infusion-of-sleaze` | Infusion of Sleaze | `selectable` | Device | S | L + 1 | L >= current Sleaze; +1 Sleaze/hit, maximum twice normal; one Infusion per attribute; swapping ends it. | `sr5-core` p. 252 (PDF 254) |
| `infusion-of-data-processing` | Infusion of Data Processing | `selectable` | Device | S | L + 1 | L >= current Data Processing; +1/hit, maximum twice normal; one Infusion per attribute; swapping ends it. | `sr5-core` p. 252 (PDF 254) |
| `infusion-of-firewall` | Infusion of Firewall | `selectable` | Device | S | L + 1 | L >= current Firewall; +1 Firewall/hit, maximum twice normal; one Infusion per attribute; swapping ends it. | `sr5-core` p. 252 (PDF 254) |
| `static-veil` | Static Veil | `selectable` | Persona | S | L - 1 | Simple threshold 1 public grid/2 other; time does not raise OS while sustained and target stays on grid; illegal actions still do. | `sr5-core` p. 252 (PDF 254) |
| `pulse-storm` | Pulse Storm | `selectable` | Persona | I | L | Opposed by Logic + Data Processing; target noise +1/net hit. | `sr5-core` p. 252 (PDF 254) |
| `puppeteer` | Puppeteer | `selectable` | Device | I | L + 4 | Required Matrix action at use; opposed by Willpower + Firewall, threshold 1 Free/2 Simple/3 Complex; target performs action next opportunity. | `sr5-core` p. 252 (PDF 254) |
| `resonance-channel` | Resonance Channel | `selectable` | Device | S | L - 1 | Simple test; reduce distance noise from target by 1/hit. | `sr5-core` p. 252 (PDF 254) |
| `resonance-spike` | Resonance Spike | `selectable` | Device | I | L | Opposed by Willpower + Firewall; 1 unresisted Matrix box/net hit. | `sr5-core` p. 253 (PDF 255) |
| `resonance-veil` | Resonance Veil | `selectable` | Device | S | L - 1 | Required authored Matrix illusion; opposed by Intuition + Data Processing; later Matrix Perception threshold net hits to disbelieve. | `sr5-core` p. 253 (PDF 255) |
| `static-bomb` | Static Bomb | `selectable` | Self | I | L + 2 | Opposed separately by Intuition + Data Processing of every icon spotting user; beaten icons lose user unless they hold a mark. | `sr5-core` p. 253 (PDF 255) |
| `stitches` | Stitches | `selectable` | Sprite | P | L - 2 | Simple test; heal 1 Matrix box/hit from sprite. | `sr5-core` p. 253 (PDF 255) |
| `transcendent-grid` | Transcendent Grid | `selectable` | Self | I | L - 3 | Simple test; user is on all grids, removing cross/public penalties both ways; lasts 1 minute/hit. | `sr5-core` p. 253 (PDF 255) |
| `tattletale` | Tattletale | `selectable` | Persona | P | L - 2 | Simple test; +1 target OS/hit; only target already having OS. | `sr5-core` p. 253 (PDF 255) |

## Spirit Support Types And Creation Records

Spirit types are support catalog entries selected through the summoner's
tradition, not formula purchases. Attributes below are physical materialization
values; minimum attribute is 1. All listed skills are Rating F. All have Edge
F/2, Essence F, Magic F, astral Initiative 2F + 3D6, and one optional power per
full 3 F. Source: `sr5-core` pp. 300, 303-304 (PDF 302, 305-306).

| ID | Display name | Classification | Attributes B/A/R/S/W/L/I/C; Initiative | Skills | Innate powers; optional powers; special | Tradition eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `air-spirit` | Spirit of Air | `selectable` | F-2/F+3/F+4/F-3/F/F/F/F; 2F+4 +2D6 | Assensing, Astral Combat, Exotic Ranged Weapon, Perception, Running, Unarmed | Accident, Astral Form, Concealment, Confusion, Engulf, Materialization, Movement, Sapience, Search; optional Elemental Attack, Energy Aura, Fear, Guard, Noxious Breath, Psychokinesis; sprint +10 m/hit. | Hermetic Detection; Shamanic Illusion | `sr5-core` pp. 279-280, 303 (PDF 281-282, 305) |
| `beast-spirit` | Spirit of Beasts | `selectable` | F+2/F+1/F/F+2/F/F/F/F; 2F +2D6 | Assensing, Astral Combat, Perception, Unarmed | Animal Control, Astral Form, Enhanced Senses (Hearing, Low-Light, Smell), Fear, Materialization, Movement, Sapience; optional Concealment, Confusion, Guard, Natural Weapon (F Physical, AP none), Noxious Breath, Search, Venom. | Shamanic Combat only | `sr5-core` pp. 280, 303 (PDF 282, 305) |
| `earth-spirit` | Spirit of Earth | `selectable` | F+4/F-2/F-1/F+4/F/F-1/F/F; 2F-1 +2D6 | Assensing, Astral Combat, Exotic Ranged Weapon, Perception, Unarmed | Astral Form, Binding, Guard, Materialization, Movement, Sapience, Search; optional Concealment, Confusion, Engulf, Elemental Attack, Fear. | Hermetic Manipulation; Shamanic Health | `sr5-core` pp. 279-280, 303 (PDF 281-282, 305) |
| `fire-spirit` | Spirit of Fire | `selectable` | F+1/F+2/F+3/F-2/F/F/F+1/F; 2F+3 +2D6 | Assensing, Astral Combat, Exotic Ranged Weapon, Flight, Perception, Unarmed | Accident, Astral Form, Confusion, Elemental Attack, Energy Aura, Engulf, Materialization, Sapience; optional Fear, Guard, Noxious Breath, Search; severe Water allergy; sprint +5 m/hit. | Hermetic Combat only | `sr5-core` pp. 279, 303 (PDF 281, 305) |
| `man-spirit` | Spirit of Man | `selectable` | F+1/F/F+2/F-2/F/F/F+1/F; 2F+2 +2D6 | Assensing, Astral Combat, Perception, Spellcasting, Unarmed | Accident, Astral Form, Concealment, Confusion, Enhanced Senses (Low-Light, Thermographic), Guard, Influence, Materialization, Sapience, Search; optional Fear, Innate Spell (one summoner-known spell, Force <= spirit Magic), Movement, Psychokinesis. | Hermetic Health; Shamanic Manipulation | `sr5-core` pp. 279-280, 304 (PDF 281-282, 306) |
| `water-spirit` | Spirit of Water | `selectable` | F/F+1/F+2/F/F/F/F/F; 2F+2 +2D6 | Assensing, Astral Combat, Exotic Ranged Weapon, Perception, Unarmed | Astral Form, Concealment, Confusion, Engulf, Materialization, Movement, Sapience, Search; optional Accident, Binding, Elemental Attack, Energy Aura, Guard, Weather Control; severe Fire allergy; double movement in water. | Hermetic Illusion; Shamanic Detection | `sr5-core` pp. 279-280, 304 (PDF 281-282, 306) |

Summoned spirits are gameplay-generated and are not creation purchases. A bound
spirit is a `generated` creation record available to magician, mystic adept, or
Conjuring-aspected magician with Binding eligibility: required type available to
tradition, F fixed to final Magic, positive integer services at 1 Karma each,
and total bound-spirit records <= Charisma. Bound records do not consume formula
grants. Sources: `sr5-core` pp. 98, 300-304 (PDF 100, 302-306).

## Sprite Support Types And Creation Records

All sprites have Device Rating and Resonance L, Condition Monitor
`8 + (L / 2)`, 4D6 Initiative Dice, listed Matrix attributes, and listed skills
at L. Sources: `sr5-core` pp. 254, 258-259 (PDF 256, 260-261).

| ID | Display name | Classification | Attack / Sleaze / Data Processing / Firewall; Initiative | Skills; powers | Source |
| --- | --- | --- | --- | --- |
| `courier-sprite` | Courier Sprite | `selectable` | L / L+3 / L+1 / L+2; 2L+1 | Computer, Hacking; Cookie, Hash | `sr5-core` pp. 258-259 (PDF 260-261) |
| `crack-sprite` | Crack Sprite | `selectable` | L / L+3 / L+2 / L+1; 2L+2 | Computer, Electronic Warfare, Hacking; Suppression | `sr5-core` pp. 258-259 (PDF 260-261) |
| `data-sprite` | Data Sprite | `selectable` | L-1 / L / L+4 / L+1; 2L+4 | Computer, Electronic Warfare; Camouflage, Watermark | `sr5-core` pp. 258-259 (PDF 260-261) |
| `fault-sprite` | Fault Sprite | `selectable` | L+3 / L / L+1 / L+2; 2L+1 | Computer, Cybercombat, Hacking; Electron Storm | `sr5-core` pp. 258-259 (PDF 260-261) |
| `machine-sprite` | Machine Sprite | `selectable` | L+1 / L / L+3 / L+2; 2L+3 | Computer, Electronic Warfare, Hardware; Diagnostics, Gremlins, Stability | `sr5-core` pp. 258-259 (PDF 260-261) |

Compiled sprites are gameplay-generated and are not creation purchases. A
registered sprite is a `generated` creation record available only to a
technomancer: required core sprite type, L fixed to final Resonance, positive
integer tasks at 1 Karma each, and total records <= Charisma. It does not consume
complex-form grants. The creation table's `Registering Spirits` label is a source
defect; its operative text and example say sprites. Sources: `sr5-core` pp. 98-99,
254-259 (PDF 100-101, 256-261).

## Foci And Bonding Dependencies

These are dependency records for magic creation validation; purchase price,
Availability, and gear inventory remain owned by the magical-equipment ledger.
A focus must be purchased/owned, Awakened-eligible, and bonded before use.
Bonding takes F hours and the listed Karma. At creation: number bonded <= Magic,
sum bonded F <= Magic x 2 (the creation limit, stricter than the career limit of
Magic x 5), and explicit purchasable Force <=6. One focus may add F to any one
test. Sources: `sr5-core` pp. 98, 318-320 (PDF 100, 320-322). Decision:
`gear.rating-cap-force`.

| ID | Display name | Classification | Bonding Karma | Required parameter / prerequisite and effect dependency | Creation eligibility | Source |
| --- | --- | --- | ---: | --- | --- | --- |
| `alchemical-focus` | Alchemical Focus | `parameterized` | F x 3 | Adds F dice to Alchemy tests. | Alchemy-capable path. | `sr5-core` pp. 318-319 (PDF 320-321) |
| `disenchanting-focus` | Disenchanting Focus | `parameterized` | F x 3 | Must contact artifact; adds F dice to Disenchanting. | Disenchanting-capable path. | `sr5-core` pp. 318-319 (PDF 320-321) |
| `centering-focus` | Centering Focus | `creation-unavailable` | F x 3 | Requires Centering metamagic; adds F to initiate grade for Drain Resistance use. | Excluded at initial creation because initiation/metamagic is career progression. | `sr5-core` p. 319 (PDF 321) |
| `flexible-signature-focus` | Flexible Signature Focus | `creation-unavailable` | F x 3 | Requires Flexible Signature metamagic; adds F to grade for Assensing threshold. | Career progression only. | `sr5-core` p. 319 (PDF 321) |
| `masking-focus` | Masking Focus | `creation-unavailable` | F x 3 | Requires Masking metamagic; +F dice resisting Assensing; does not expand masked-focus count. | Career progression only. | `sr5-core` p. 319 (PDF 321) |
| `spell-shaping-focus` | Spell Shaping Focus | `creation-unavailable` | F x 3 | Requires Spell Shaping metamagic; treats Magic as +F for shaping amount. | Career progression only. | `sr5-core` p. 319 (PDF 321) |
| `power-focus` | Power Focus | `parameterized` | F x 6 | Adds F to tests involving Magic, including Sorcery, Conjuring, Enchanting. | Any Magic path able to use affected test. | `sr5-core` p. 319 (PDF 321) |
| `qi-focus` | Qi Focus | `parameterized` | F x 2 | Required one adept power and rank/profile; F must equal 4 x contained PP cost; grants/augments that power while active, with no benefit from duplicate unranked power. | Adept or mystic adept; contained selection must satisfy power parameters/caps. | `sr5-core` p. 319 (PDF 321) |
| `counterspelling-focus` | Counterspelling Focus | `parameterized` | F x 2 | Required one spell category; adds F to Counterspelling and spell-defense pool only for that category. | Counterspelling-capable path. | `sr5-core` pp. 319-320 (PDF 321-322) |
| `ritual-spellcasting-focus` | Ritual Spellcasting Focus | `parameterized` | F x 2 | Required category; adds F to Ritual Spellcasting; non-spell rituals allowed, spell rituals must match category. | Ritual Spellcasting-capable path. | `sr5-core` pp. 319-320 (PDF 321-322) |
| `spellcasting-focus` | Spellcasting Focus | `parameterized` | F x 2 | Required category; adds F to matching Spellcasting tests. | Spellcasting-capable path. | `sr5-core` pp. 319-320 (PDF 321-322) |
| `sustaining-focus` | Sustaining Focus | `parameterized` | F x 2 | Required category; sustains matching spell with spell F <= focus F; cannot sustain spell ritual. | Spellcasting-capable path. | `sr5-core` pp. 319-320 (PDF 321-322) |
| `summoning-focus` | Summoning Focus | `parameterized` | F x 2 | Required spirit type; +F dice summoning matching type. | Summoning-capable path and type available to tradition. | `sr5-core` p. 320 (PDF 322) |
| `banishing-focus` | Banishing Focus | `parameterized` | F x 2 | Required spirit type; adds F to Banishing limit against matching type. | Banishing-capable path. | `sr5-core` p. 320 (PDF 322) |
| `binding-focus` | Binding Focus | `parameterized` | F x 2 | Required spirit type; +F dice Binding matching type. | Binding-capable path and type available to tradition. | `sr5-core` p. 320 (PDF 322) |
| `weapon-focus` | Weapon Focus | `parameterized` | F x 3 | Required owned melee weapon; +F dice to physical melee attacks and Astral Combat with it; astral damage uses weapon with Charisma replacing Strength and may be Stun/Physical. | Awakened character able to bond; both mundane weapon and focus enchantment costs apply. | `sr5-core` pp. 315, 320 (PDF 317, 322). Decision: `gear.weapon-focus-base-cost` |

## Exclusions And Source Discrepancies

| ID or family | Classification | Reason | Source |
| --- | --- | --- | --- |
| `ram` / `ram-preparation` | `excluded` | Preparation example names Ram, but no core Ram spell/formula exists. | `sr5-core` p. 306 (PDF 308). Decision: `preparation.ram-example` |
| Custom traditions | `excluded` | Open GM-authored concept without deterministic core construction rules. | `sr5-core` p. 279 (PDF 281) |
| Custom mentor archetypes | `excluded` | Open GM-authored concept; only 16 printed archetypes approved. | `sr5-core` p. 320 (PDF 322). Decision: `mentor.custom-archetypes` |
| Ware-derived Improved Sense choices | `excluded` | Core imports an unaudited open subset; initial domain is the four explicit options. | `sr5-core` p. 310 (PDF 312). Decision: `power.improved-sense-domain` |
| Initiation, metamagics, Submersion, echoes | `creation-unavailable` | Career advancement, not initial creation; metamagic foci remain dependency records but cannot be selected. | `sr5-core` pp. 257-258, 324-326 (PDF 259-260, 326-328) |
| Toxic and blood magic | `excluded` | Setting material without an approved core creation path. | `sr5-core` pp. 277-278 (PDF 279-280) |
| Run Faster magic/Resonance catalogs | `excluded` | No Run Faster catalog option is admitted; p. 63 is used only for the approved core-priority formula-grant wording. | `run-faster` p. 63 (PDF 65). Decision: `magic.priority-grant-formula-types` |

Retained discrepancies:

- Cat grants `Infiltration`, but the core active skill is Sneaking. The catalog
  uses Sneaking under `mentor.cat-infiltration`: `sr5-core` p. 321 (PDF 323).
- Mob Mind prose says area while its printed range omits `(A)`. The catalog uses
  LOS (A) under `spell.mob-mind-area`: `sr5-core` p. 293 (PDF 295).
- Critical Strike is unranked despite chargen/power prose referring to levels.
  Thunderbird grants one parameterized selection under
  `mentor.thunderbird-critical-strike`: `sr5-core` pp. 69, 309, 324
  (PDF 71, 311, 326).
- The creation table says `Registering Spirits`, while the rule and example are
  for registered sprites and Resonance. No spirit-registration option is
  created: `sr5-core` pp. 98-99 (PDF 100-101).
- The core priority table says `spells`; the approved Run Faster copied table
  says spells, rituals, and/or preparations. The latter grant composition is
  used under `magic.priority-grant-formula-types`: `sr5-core` p. 65 (PDF 67);
  `run-faster` p. 63 (PDF 65).

## Review Footer

### Reviewed Pages

- `sr5-core` pp. 65, 68-71, 88, 98-101, 143, 250-259, 277-280,
  281-311, 315-320, 324-326 (PDF 67, 70-73, 90, 100-103, 145,
  252-261, 279-282, 283-313, 317-322, 326-328).
- `run-faster` p. 63 (PDF 65), only for the approved priority formula-grant
  clarification.

### Approved-PDF Counts

| Inventory | Count | Classification reconciliation |
| --- | ---: | --- |
| Creation paths | 6 | 5 `selectable`, 1 `parameterized` |
| Aspected values | 3 | 3 `included-component` |
| Traditions | 2 | 2 `selectable` |
| Spells | 84 | 79 `selectable`, 5 `parameterized` |
| Generated preparation families | 84 | 84 `generated`; inherit spell parameters |
| Preparation triggers | 3 | 3 `included-component` |
| Rituals | 9 | 9 `selectable` |
| Adept powers | 25 | 4 `selectable`, 21 `parameterized` |
| Mentor spirits | 16 | 8 `selectable`, 8 `parameterized` |
| Complex forms | 20 | 20 `selectable` |
| Spirit support types | 6 | 6 `selectable`; bound instances are `generated` records |
| Sprite support types | 5 | 5 `selectable`; registered instances are `generated` records |
| Focus dependency types | 16 | 12 `parameterized`, 4 `creation-unavailable` |
| Explicit excluded IDs/families | 6 | 6 `excluded` families/IDs; career-only progression separately marked `creation-unavailable` |

### Remaining Unknown Facts

None. All source conflicts affecting this ledger have approved decisions. Open
runtime-authored values (for example an Influence suggestion or Resonance Veil
fiction) are bounded parameters, not missing catalog facts.

### Runtime Reconciliation Status

`Not implemented`. CHAR-802 must materialize this reviewed inventory, validate
all references and parameter domains, and reconcile exact IDs/counts before
catalog version `1.0.0` is published.
