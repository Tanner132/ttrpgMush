# Priorities, Metatypes, Attributes, And Creation Budgets

This is the detailed CHAR-801 source ledger for creation methods, every cell of
the priority table, the five approved metatypes, attribute allocation, global
creation budgets, and final derived values. It is a review input for CHAR-802,
not a runtime catalog and not a substitute for the approved books.

Unless a row says otherwise, availability, legality, capacity, quantity, and
parent/child purchase relationships are `not applicable` to entries in this
file. Open-authored names are bounded parameters rather than additional catalog
entries. Only `sr5-core` content is selectable; `run-faster` supplies only the
approved Sum-to-Ten method and priority-grant wording.

## Creation Methods

| ID | Display name | Classification | Allocation and limits | Source |
| --- | --- | --- | --- | --- |
| `standard-priority` | Standard Priority | `selectable` | Assign exactly one level to each of Metatype, Attributes, Magic or Resonance, Skills, and Resources; use A, B, C, D, and E exactly once, with no duplicate level. | `sr5-core` pp. 65, 101 (PDF 67, 103) |
| `sum-to-ten` | Sum-to-Ten | `selectable` | Assign exactly one level to each of the same five categories; spend exactly 10 points; A/B/C/D/E cost 4/3/2/1/0; levels may repeat. Decision: `priority.sum-to-ten`. | `run-faster` pp. 62-63 (PDF 64-65) |

Both methods use the priority-cell grants below and all downstream core rules.
Sum-to-Ten does not admit any other Run Faster option. Its copied table changes
the magician and mystic-adept formula grant from core's `spells` to `spells,
rituals, and/or alchemical preparations`; that grant composition applies to both
methods under approved decision `magic.priority-grant-formula-types`.
Sources: `sr5-core` p. 65 (PDF 67); `run-faster` pp. 62-63 (PDF 64-65).

## Priority Cells

The two numbers in a Skills grant are individual skill points / skill-group
points. Special attribute points in a Metatype cell are allocated under
`special-attribute-point-allocation` below. Each cell is selected only through
its parent category assignment; no cell is purchased separately.

### Metatype Cells

| ID | Display name | Classification | Grant | Source |
| --- | --- | --- | --- | --- |
| `priority-metatype-a` | Metatype A | `selectable` | Human 9; Elf 8; Dwarf 7; Ork 7; Troll 5 special attribute points. | `sr5-core` p. 65 (PDF 67) |
| `priority-metatype-b` | Metatype B | `selectable` | Human 7; Elf 6; Dwarf 4; Ork 4; Troll 0 special attribute points. | `sr5-core` p. 65 (PDF 67) |
| `priority-metatype-c` | Metatype C | `selectable` | Human 5; Elf 3; Dwarf 1; Ork 0 special attribute points; Troll is unavailable in this cell. | `sr5-core` p. 65 (PDF 67) |
| `priority-metatype-d` | Metatype D | `selectable` | Human 3; Elf 0 special attribute points; Dwarf, Ork, and Troll are unavailable in this cell. | `sr5-core` p. 65 (PDF 67) |
| `priority-metatype-e` | Metatype E | `selectable` | Human 1 special attribute point; Elf, Dwarf, Ork, and Troll are unavailable in this cell. | `sr5-core` p. 65 (PDF 67) |

### Attribute Cells

| ID | Display name | Classification | Grant | Source |
| --- | --- | --- | --- | --- |
| `priority-attributes-a` | Attributes A | `selectable` | 24 Physical/Mental attribute points. | `sr5-core` p. 65 (PDF 67) |
| `priority-attributes-b` | Attributes B | `selectable` | 20 Physical/Mental attribute points. | `sr5-core` p. 65 (PDF 67) |
| `priority-attributes-c` | Attributes C | `selectable` | 16 Physical/Mental attribute points. | `sr5-core` p. 65 (PDF 67) |
| `priority-attributes-d` | Attributes D | `selectable` | 14 Physical/Mental attribute points. | `sr5-core` p. 65 (PDF 67) |
| `priority-attributes-e` | Attributes E | `selectable` | 12 Physical/Mental attribute points. | `sr5-core` p. 65 (PDF 67) |

### Magic Or Resonance Cells

Magical skills mean skills linked to Magic; Resonance skills mean skills linked
to Resonance. An aspected magician's granted group must be exactly one of
Sorcery, Conjuring, or Enchanting and permanently excludes skills from the other
two groups. An adept's granted Active skill still obeys its normal eligibility.
Priority grants are already paid and cost no skill points or Karma.
Source: `sr5-core` pp. 68-70 (PDF 70-72).

| ID | Display name | Classification | Path grants | Source |
| --- | --- | --- | --- | --- |
| `priority-magic-resonance-a` | Magic or Resonance A | `selectable` | Magician or Mystic Adept: Magic 6, two Rating 5 Magical skills, and 10 formula selections. Technomancer: Resonance 6, two Rating 5 Resonance skills, and 5 complex forms. Other paths unavailable. Formula selections may be spells, rituals, and/or alchemical preparations under `magic.priority-grant-formula-types`; technomancer grant confirmed by `technomancer.priority-grants`. | `sr5-core` p. 65 (PDF 67); `run-faster` p. 63 (PDF 65) |
| `priority-magic-resonance-b` | Magic or Resonance B | `selectable` | Magician or Mystic Adept: Magic 4, two Rating 4 Magical skills, and 7 formula selections. Technomancer: Resonance 4, two Rating 4 Resonance skills, and 2 complex forms. Adept: Magic 6 and one Rating 4 Active skill. Aspected Magician: Magic 5 and one Rating 4 Magical skill group. Formula and technomancer decisions are the same as row A. | `sr5-core` p. 65 (PDF 67); `run-faster` p. 63 (PDF 65) |
| `priority-magic-resonance-c` | Magic or Resonance C | `selectable` | Magician or Mystic Adept: Magic 3 and 5 formula selections. Technomancer: Resonance 3 and 1 complex form. Adept: Magic 4 and one Rating 2 Active skill. Aspected Magician: Magic 3 and one Rating 2 Magical skill group. Formula and technomancer decisions are the same as row A. | `sr5-core` p. 65 (PDF 67); `run-faster` p. 63 (PDF 65) |
| `priority-magic-resonance-d` | Magic or Resonance D | `selectable` | Adept: Magic 2. Aspected Magician: Magic 2. Other paths unavailable. | `sr5-core` p. 65 (PDF 67) |
| `priority-magic-resonance-e` | Magic or Resonance E | `selectable` | Mundane only; Magic 0 and Resonance 0; no skills, groups, formulae, forms, or powers granted. | `sr5-core` pp. 65, 68 (PDF 67, 70) |

### Skill Cells

| ID | Display name | Classification | Grant | Source |
| --- | --- | --- | --- | --- |
| `priority-skills-a` | Skills A | `selectable` | 46 individual skill points and 10 skill-group points. | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| `priority-skills-b` | Skills B | `selectable` | 36 individual skill points and 5 skill-group points. | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| `priority-skills-c` | Skills C | `selectable` | 28 individual skill points and 2 skill-group points. | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| `priority-skills-d` | Skills D | `selectable` | 22 individual skill points and 0 skill-group points. | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| `priority-skills-e` | Skills E | `selectable` | 18 individual skill points and 0 skill-group points. | `sr5-core` pp. 65, 88 (PDF 67, 90) |

### Resource Cells

| ID | Display name | Classification | Grant | Source |
| --- | --- | --- | --- | --- |
| `priority-resources-a` | Resources A | `selectable` | 450,000 nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-b` | Resources B | `selectable` | 275,000 nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-c` | Resources C | `selectable` | 140,000 nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-d` | Resources D | `selectable` | 50,000 nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-e` | Resources E | `selectable` | 6,000 nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |

## Metatypes

Attribute values are natural minimum / natural maximum before qualities and
augmentations. Magic and Resonance are path-dependent special attributes and
therefore are recorded separately below. All five metatypes start with Essence
6 and physical Initiative `Reaction + Intuition`.
Source: `sr5-core` pp. 52, 65-66 (PDF 54, 67-68).

| ID | Display name | Classification | BOD | AGI | REA | STR | WIL | LOG | INT | CHA | EDG | Traits and cost modifiers | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `human` | Human | `selectable` | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 2/7 | No racial trait; gear +0%; lifestyle +0%; walk AGI x 2, run AGI x 4, sprint +2 m/hit. | `sr5-core` pp. 66, 162 (PDF 68, 164) |
| `elf` | Elf | `selectable` | 1/6 | 2/7 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 3/8 | 1/6 | Low-Light Vision; gear +0%; lifestyle +0%; walk AGI x 2, run AGI x 4, sprint +2 m/hit. Natural low-light vision is replaced if cybereyes are installed. | `sr5-core` pp. 66, 94, 162 (PDF 68, 96, 164) |
| `dwarf` | Dwarf | `selectable` | 3/8 | 1/6 | 1/5 | 3/8 | 2/7 | 1/6 | 1/6 | 1/6 | 1/6 | +2 dice for pathogen and toxin resistance; gear +10%; lifestyle +20%; walk AGI x 2, run AGI x 4, sprint +1 m/hit. Cost split applies `metatype.dwarf-costs`. | `sr5-core` pp. 66, 94, 162, 420 (PDF 68, 96, 164, 422) |
| `ork` | Ork | `selectable` | 4/9 | 1/6 | 1/6 | 3/8 | 1/6 | 1/5 | 1/6 | 1/5 | 1/6 | Low-Light Vision; gear +0%; lifestyle +0%; walk AGI x 2, run AGI x 4, sprint +2 m/hit. Natural low-light vision is replaced if cybereyes are installed. | `sr5-core` pp. 66, 94, 162 (PDF 68, 96, 164) |
| `troll` | Troll | `selectable` | 5/10 | 1/5 | 1/6 | 5/10 | 1/6 | 1/5 | 1/5 | 1/4 | 1/6 | Thermographic Vision, +1 Reach, +1 dermal armor; gear +50%; lifestyle +100%; walk AGI x 2, run AGI x 4, sprint +1 m/hit. Orthoskin replaces the natural dermal deposits and removes their armor. Cost split applies `metatype.troll-costs`. | `sr5-core` pp. 65-66, 94, 162, 420 (PDF 67-68, 96, 164, 422) |

The cost modifiers apply to the corresponding base gear or lifestyle costs; they
do not grant or consume separate priority resources. Dwarf- or troll-adapted
gear is represented by those costs. Unadapted human-sized gear gives dwarfs and
trolls a -2 dice-pool modifier; dwarf-versus-troll sizing gives -4, and wholly
incompatible sizing can make use impossible. Source: `sr5-core` p. 420
(PDF 422).

## Attribute Entries

| ID | Display name | Classification | Group | Creation fact | Source |
| --- | --- | --- | --- | --- | --- |
| `body` | Body | `bookkeeping` | Physical | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to Physical limit, Physical Condition Monitor, and Overflow. | `sr5-core` pp. 51, 66, 101 (PDF 53, 68, 103) |
| `agility` | Agility | `bookkeeping` | Physical | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Determines movement rates. | `sr5-core` pp. 51, 66, 162 (PDF 53, 68, 164) |
| `reaction` | Reaction | `bookkeeping` | Physical | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to physical/AR Initiative and Physical limit. | `sr5-core` pp. 51, 66, 101 (PDF 53, 68, 103) |
| `strength` | Strength | `bookkeeping` | Physical | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to Physical limit and sprint tests. | `sr5-core` pp. 51, 66, 101, 162 (PDF 53, 68, 103, 164) |
| `willpower` | Willpower | `bookkeeping` | Mental | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to Mental/Social limits, Stun Condition Monitor, and living-persona Firewall. | `sr5-core` pp. 51, 66, 101 (PDF 53, 68, 103) |
| `logic` | Logic | `bookkeeping` | Mental | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to Mental limit, free Knowledge/Language points, and living-persona Data Processing. | `sr5-core` pp. 51, 66, 89, 101 (PDF 53, 68, 91, 103) |
| `intuition` | Intuition | `bookkeeping` | Mental | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to Initiative, Mental limit, free Knowledge/Language points, and living-persona Sleaze. | `sr5-core` pp. 51, 66, 89, 101 (PDF 53, 68, 91, 103) |
| `charisma` | Charisma | `bookkeeping` | Mental | Starts at metatype minimum; raised with Physical/Mental attribute points or Karma; natural cap is metatype maximum. Contributes to Social limit, free Contact Karma, bound-spirit/registered-sprite count, and living-persona Attack. | `sr5-core` pp. 51, 66, 98, 101 (PDF 53, 68, 100, 103) |
| `edge` | Edge | `bookkeeping` | Special | Starts at metatype EDG minimum; raised by special attribute points or Karma; natural maximum is the metatype EDG maximum. Lucky, not Exceptional Attribute, may increase its maximum by 1; Lucky and Exceptional Attribute are mutually exclusive and require GM approval. | `sr5-core` pp. 52, 66, 76 (PDF 54, 68, 78) |
| `essence` | Essence | `bookkeeping` | Special | Starts at 6 for every metatype; augmentations reduce it. Remaining Essence contributes to Social limit. It is not raised by priority or special attribute points. | `sr5-core` pp. 52, 66, 95, 101 (PDF 54, 68, 97, 103) |
| `magic` | Magic | `bookkeeping` | Special | Starts at 0 unless a magical path grants a base rating; special attribute points and Karma may raise it to natural maximum 6. Exceptional Attribute may raise the maximum to 7. Magic and Resonance are mutually path-dependent; non-Awakened characters remain at Magic 0. | `sr5-core` pp. 52, 65-69 (PDF 54, 67-71) |
| `resonance` | Resonance | `bookkeeping` | Special | Starts at 0 unless Technomancer grants a base rating; special attribute points and Karma may raise it to natural maximum 6. Exceptional Attribute may raise the maximum to 7. Non-technomancers remain at Resonance 0. | `sr5-core` pp. 52, 65-68 (PDF 54, 67-70) |

## Attribute Allocation And Limits

| ID | Display name | Classification | Rule | Source |
| --- | --- | --- | --- | --- |
| `physical-mental-point-allocation` | Physical/Mental point allocation | `bookkeeping` | Every Physical and Mental attribute starts at its metatype minimum at no cost. Spend 1 Attributes-priority point per +1 natural rating and spend every granted point. Points cannot raise Edge, Magic, Resonance, or any other value. Decision: `allocation.unused-priority-points`. | `sr5-core` p. 66 (PDF 68) |
| `special-attribute-point-allocation` | Special attribute point allocation | `bookkeeping` | Spend metatype-cell points only on Edge and, when eligible, Magic or Resonance. They may all go to Edge or split between Edge and the one eligible supernatural attribute. Unspent points are lost; they cannot raise Physical/Mental attributes. Decision: `allocation.unused-priority-points`. | `sr5-core` pp. 65-66 (PDF 67-68) |
| `natural-maximum-count` | Natural maximum count | `bookkeeping` | At final creation, at most one Physical or Mental attribute may equal its natural maximum. Edge, Magic, and Resonance do not count. Decision: `attribute.one-natural-maximum`. | `sr5-core` pp. 66, 98, 101 (PDF 68, 100, 103) |
| `exceptional-attribute-maximum-count` | Exceptional Attribute maximum count | `bookkeeping` | Exceptional Attribute raises one selected non-Edge attribute's natural maximum by 1, but the selection consumes the one-at-natural-maximum allowance; no second Physical/Mental attribute may sit at its ordinary maximum. Decision: `attribute.exceptional-maximum-count`. | `sr5-core` pp. 66, 72, 101 (PDF 68, 74, 103) |
| `attribute-karma-cost` | Attribute Karma cost | `bookkeeping` | A natural Physical, Mental, Magic, Resonance, or Edge increase costs new rating x 5 Karma for each rating gained; sum each incremental cost when gaining multiple ratings. Creation maxima still apply. | `sr5-core` pp. 98, 103, 105-107 (PDF 100, 105, 107-109) |
| `augmentation-attribute-cap` | Augmentation attribute cap | `bookkeeping` | The augmentation bonus to each Physical or Mental attribute is at most +4. Track natural and augmented ratings separately. | `sr5-core` pp. 94-95 (PDF 96-97) |
| `essence-magic-resonance-loss` | Essence loss to Magic/Resonance | `bookkeeping` | Track natural and augmented values separately; total all selected ware Essence loss, then reduce current and maximum Magic or Resonance once for each point or fraction of cumulative Essence loss before final eligibility checks. Decision: `essence.magic-resonance-order`. | `sr5-core` pp. 52, 95, 250 (PDF 54, 97, 252) |
| `general-rounding` | General rounding | `bookkeeping` | Round up unless a specific rule says otherwise. Derived rows below retain their explicit rounding. | `sr5-core` p. 48 (PDF 50) |

## Global Creation Budgets And Limits

| ID | Display name | Classification | Budget, cost, or finalization rule | Source |
| --- | --- | --- | --- | --- |
| `starting-karma` | Starting Karma | `bookkeeping` | Start with 25 general Karma. Positive qualities spend it; negative qualities add their listed bonus; other permitted creation purchases also spend it. | `sr5-core` pp. 64, 71 (PDF 66, 73) |
| `positive-quality-cap` | Positive quality cap | `bookkeeping` | Selected Positive Qualities may total at most 25 Karma. Decision: `quality.creation-caps`. | `sr5-core` p. 71 (PDF 73) |
| `negative-quality-cap` | Negative quality cap | `bookkeeping` | Selected Negative Qualities may award at most 25 Karma, independently of the positive cap. Decision: `quality.creation-caps`. | `sr5-core` p. 71 (PDF 73) |
| `individual-skill-points` | Individual skill points | `bookkeeping` | Spend 1 priority skill point per new rank or +1 rank in an individual Active, Knowledge, or Language skill. All priority individual points must be spent and cannot become group points. | `sr5-core` pp. 88-90 (PDF 90-92) |
| `skill-group-points` | Skill-group points | `bookkeeping` | Spend 1 priority group point per +1 rating in an eligible group. All group points must be spent and cannot become individual points. Groups stay atomic in Step Five. | `sr5-core` pp. 88-90 (PDF 90-92) |
| `skill-natural-creation-cap` | Natural skill creation cap | `bookkeeping` | Final natural Active skill rating is at most 6, except the single Aptitude skill may be 7. Priority grants obey the same final cap. Decisions: `skill.creation-maximum`, `skill.priority-grant-collision`. | `sr5-core` pp. 72, 88 (PDF 74, 90) |
| `priority-grant-collision` | Priority grant collision | `bookkeeping` | A granted skill may be raised by later allocation, but duplicate sources that would discard grant value are invalid; apply the final natural-rating cap across all sources. Decision: `skill.priority-grant-collision`. | `sr5-core` pp. 68, 88 (PDF 70, 90) |
| `skill-karma-costs` | Skill Karma costs | `bookkeeping` | For every rating gained, Active skills cost new rating x 2 Karma, Active skill groups cost new rating x 5, and Knowledge or Language skills cost new rating x 1; sum incremental costs when gaining multiple ratings. Creation caps still apply. | `sr5-core` pp. 98, 105-107 (PDF 100, 107-109) |
| `creation-specialization` | Creation specialization | `bookkeeping` | A specialization gives +2 for its subject. In Step Five it costs 1 individual skill point; in Step Seven it costs 7 Karma. No individual skill may have more than one at creation; groups cannot take specializations. | `sr5-core` pp. 88-89, 107 (PDF 90-91, 109) |
| `free-knowledge-language-points` | Free Knowledge/Language points | `bookkeeping` | Grant `(natural Intuition + natural Logic) x 2` dedicated points. Spend 1 per rank on authored Knowledge or Language skills. Augmentations do not increase this grant. Drafts may leave points unallocated, but finalization must spend all of them; they never convert. Decision: `knowledge.unused-free-points`. | `sr5-core` pp. 89, 95 (PDF 91, 97) |
| `native-language` | Native language | `parameterized` | Choose one authored language name at rating `N` for no points; Bilingual grants a second. Native rating is nonnumeric and cannot be specialized during initial creation under `knowledge.native-specialization`. | `sr5-core` pp. 72, 89-91 (PDF 74, 91-93) |
| `knowledge-skill` | Knowledge skill | `parameterized` | Required authored name and exactly one category: Academic (Logic), Interests (Intuition), Professional (Logic), or Street (Intuition). Examples are not a closed catalog. Final natural creation rating is 1-6. | `sr5-core` pp. 89-91 (PDF 91-93) |
| `language-skill` | Language skill | `parameterized` | Required authored language name; paid languages use Intuition and a numeric natural creation rating of 1-6. Examples are not a closed catalog. | `sr5-core` pp. 89-91 (PDF 91-93) |
| `resources-budget` | Resources budget | `bookkeeping` | Spend the selected Resource-cell nuyen on creation purchases. Resource nuyen never converts to Karma. Any unspent amount over the permitted carryover is lost. | `sr5-core` pp. 94-95 (PDF 96-97) |
| `karma-to-nuyen` | Karma to nuyen conversion | `bookkeeping` | Convert 0-10 general Karma at 2,000 nuyen per Karma, for at most 20,000 additional creation nuyen. Conversion is one-way. | `sr5-core` pp. 94, 101 (PDF 96, 103) |
| `gear-availability-cap` | Gear Availability cap | `bookkeeping` | Numeric Availability may not exceed 12. Restricted items require an appropriate license; Forbidden items cannot be licensed; an R/F suffix alone does not change the numeric ceiling. Decision: `gear.legality-at-creation`. | `sr5-core` pp. 94, 416-419 (PDF 96, 418-421) |
| `gear-rating-cap` | Gear Rating/Force cap | `bookkeeping` | Explicit purchasable Rating and Force may not exceed 6 at creation; the cap does not apply to capacity, quantities, or vehicle attributes. Decision: `gear.rating-cap-force`. | `sr5-core` pp. 94, 418 (PDF 96, 420) |
| `ware-creation-grades` | Ware creation grades | `bookkeeping` | Standard and alphaware are available; betaware, deltaware, and used ware are unavailable at creation. Decision: `ware.creation-grades`. | `sr5-core` pp. 54, 95, 451 (PDF 56, 97, 453) |
| `formula-purchase` | Spell, ritual, or preparation purchase | `bookkeeping` | An eligible magic user pays 5 Karma per formula. Magician and mystic-adept creation caps are separate `Magic x 2` totals for spells, rituals, and preparations under `magic.formula-cap-scope`; aspected eligibility follows `magic.aspected-purchase-scope`. | `sr5-core` pp. 69, 98, 106 (PDF 71, 100, 108) |
| `complex-form-purchase` | Complex form purchase | `bookkeeping` | A technomancer pays 4 Karma per form. Final known forms, including grants and purchases, cannot exceed `min(Logic, Resonance x 2)` under `resonance.complex-form-cap`. | `sr5-core` pp. 98, 106, 252 (PDF 100, 108, 254) |
| `mystic-adept-power-points` | Mystic Adept Power Points | `bookkeeping` | No free Power Points; buy whole points for 2 Karma each, up to current Magic. Decision: `mystic-adept.power-points`. | `sr5-core` pp. 69, 71, 101 (PDF 71, 73, 103) |
| `adept-power-points` | Adept Power Points | `bookkeeping` | A pure adept receives free Power Points equal to Magic. | `sr5-core` pp. 68-69 (PDF 70-71) |
| `bound-spirit-budget` | Bound spirit services | `bookkeeping` | Eligible character pays 1 Karma per service; each spirit's Force equals the character's Magic; number of bound spirits cannot exceed Charisma. | `sr5-core` p. 98 (PDF 100) |
| `registered-sprite-budget` | Registered sprite tasks | `bookkeeping` | Technomancer pays 1 Karma per task; each sprite's Level equals Resonance; number of registered sprites cannot exceed Charisma. The source table mislabels this row `Registering Spirits`; operative text says sprites/Resonance. | `sr5-core` p. 98 (PDF 100) |
| `bonded-foci-cap` | Bonded foci cap | `bookkeeping` | Pay each focus's listed bonding cost; total Force bonded at creation cannot exceed `Magic x 2`. | `sr5-core` pp. 98, 318 (PDF 100, 320) |
| `free-contact-karma` | Free Contact Karma | `bookkeeping` | Grant `natural Charisma x 3` dedicated Karma; augmentations do not increase it. Drafts may leave it unspent, but finalization requires all of it to be allocated and it never converts to general Karma. Decision: `contact.unused-free-karma`. | `sr5-core` pp. 95, 98 (PDF 97, 100) |
| `contact` | Contact | `parameterized` | Required authored identity; Connection and Loyalty each cost 1 Contact Karma per rating and each must be at least 1. Connection's general range is 1-12; Loyalty's is 1-6; at creation their combined cost/rating sum may not exceed 7. Number of contacts is unlimited. Decision: `contact.creation-cap`. | `sr5-core` pp. 55, 98 (PDF 57, 100) |
| `resource-nuyen-carryover` | Resource nuyen carryover | `bookkeeping` | Carry 0-5,000 unspent Resource/Karma-conversion nuyen into starting nuyen; excess is lost. Decision: `carryover`. | `sr5-core` pp. 94-95, 101 (PDF 96-97, 103) |
| `karma-carryover` | Karma carryover | `bookkeeping` | Carry 0-7 unspent general Karma into play. Decision: `carryover`. | `sr5-core` pp. 98, 101 (PDF 100, 103) |
| `starting-nuyen` | Starting nuyen | `generated` | Add resource carryover to one lifestyle roll: Street `1D6 x 20`, Squatter `2D6 x 40`, Low `3D6 x 60`, Middle `4D6 x 100`, High `5D6 x 500`, or Luxury `6D6 x 1,000` nuyen. Roll once server-side during atomic finalization and persist the dice/result under `starting-cash.randomness`; base lifestyle tier controls the row under `lifestyle.options-and-cash`. | `sr5-core` p. 95 (PDF 97) |
| `gm-final-approval` | GM final approval | `bookkeeping` | All gear and the finished character remain subject to gamemaster approval; finalize background after mechanical calculations. | `sr5-core` pp. 94, 103 (PDF 96, 105) |

## Final Derived Values

Natural and augmented forms are retained where augmentations apply. Round all
three inherent limits up. For Social limit, first round remaining Essence up,
then evaluate and round the formula up. Physical and Stun monitor formulas round
the final result up. Source: `sr5-core` pp. 95, 100-101 (PDF 97, 102-103).

| ID | Display name | Classification | Formula or grant | Source |
| --- | --- | --- | --- | --- |
| `physical-initiative` | Physical Initiative | `generated` | `Intuition + Reaction + 1D6`; add applicable augmented attribute and Initiative Dice bonuses. Everyone starts with 1 die and the total cannot exceed 5 Initiative Dice. | `sr5-core` pp. 52, 100-101 (PDF 54, 102-103) |
| `astral-initiative` | Astral Initiative | `generated` | `Intuition x 2 + 2D6`; astral attributes map Agility=Logic, Body=Willpower, Reaction=Intuition, Strength=Charisma. | `sr5-core` pp. 101, 314 (PDF 103, 316) |
| `matrix-ar-initiative` | Matrix AR Initiative | `generated` | `Intuition + Reaction + 1D6`. | `sr5-core` p. 101 (PDF 103) |
| `matrix-vr-cold-initiative` | Matrix VR Initiative (Cold Sim) | `generated` | `Data Processing + Intuition + 3D6`, subject to the 5D6 global Initiative Dice cap. | `sr5-core` pp. 101, 229 (PDF 103, 231) |
| `matrix-vr-hot-initiative` | Matrix VR Initiative (Hot Sim) | `generated` | `Data Processing + Intuition + 4D6`, subject to the 5D6 global Initiative Dice cap. | `sr5-core` pp. 101, 229-230 (PDF 103, 231-232) |
| `mental-limit` | Mental Limit | `generated` | `[(Logic x 2) + Intuition + Willpower] / 3`, round up. | `sr5-core` pp. 100-101 (PDF 102-103) |
| `physical-limit` | Physical Limit | `generated` | `[(Strength x 2) + Body + Reaction] / 3`, round up. | `sr5-core` pp. 100-101 (PDF 102-103) |
| `social-limit` | Social Limit | `generated` | `[(Charisma x 2) + Willpower + rounded-up Essence] / 3`, round up final result. | `sr5-core` pp. 95, 100-101 (PDF 97, 102-103) |
| `physical-condition-monitor` | Physical Condition Monitor | `generated` | `(Body / 2) + 8`; add Body augmentation before calculation and round up final boxes. | `sr5-core` pp. 52, 101 (PDF 54, 103) |
| `stun-condition-monitor` | Stun Condition Monitor | `generated` | `(Willpower / 2) + 8`; add Willpower augmentation before calculation and round up final boxes. | `sr5-core` pp. 52, 101 (PDF 54, 103) |
| `overflow` | Overflow | `generated` | Body plus applicable augmentation bonuses. | `sr5-core` p. 101 (PDF 103) |
| `living-persona-device-rating` | Living Persona Device Rating | `generated` | Resonance. | `sr5-core` pp. 101, 251 (PDF 103, 253) |
| `living-persona-attack` | Living Persona Attack | `generated` | Charisma. | `sr5-core` pp. 101, 251 (PDF 103, 253) |
| `living-persona-sleaze` | Living Persona Sleaze | `generated` | Intuition. | `sr5-core` pp. 101, 251 (PDF 103, 253) |
| `living-persona-data-processing` | Living Persona Data Processing | `generated` | Logic. | `sr5-core` pp. 101, 251 (PDF 103, 253) |
| `living-persona-firewall` | Living Persona Firewall | `generated` | Willpower. | `sr5-core` pp. 101, 251 (PDF 103, 253) |
| `walk-rate` | Walk Rate | `generated` | All five metatypes: `Agility x 2` meters per Combat Turn. | `sr5-core` pp. 161-162 (PDF 163-164) |
| `run-rate` | Run Rate | `generated` | All five metatypes: `Agility x 4` meters per Combat Turn. | `sr5-core` pp. 161-162 (PDF 163-164) |
| `sprint-increase` | Sprint Increase | `generated` | Complex Action and `Running + Strength [Physical]` test; +1 meter/hit for dwarf/troll or +2 meters/hit for elf/human/ork. Maximum tests per Combat Turn are `floor(Running / 2)`, minimum 1, under `movement.sprint-rounding`. | `sr5-core` pp. 161-162 (PDF 163-164) |

The Final Calculations table names Reputation, Notoriety, Public Awareness, and
Street Cred but supplies no creation formula or initial value on that table;
they are not invented as CHAR-801 facts in this category. Source: `sr5-core`
p. 101 (PDF 103).

## Explicit Exclusions

| ID | Display name | Classification | Reason | Source |
| --- | --- | --- | --- | --- |
| `street-level-creation` | Street-Level Creation | `excluded` | Alternate lower-powered core creation tier; outside the approved experienced-runner baseline. | `sr5-core` p. 64 (PDF 66) |
| `prime-runner-creation` | Prime Runner Creation | `excluded` | Alternate higher-powered core creation tier; outside the approved experienced-runner baseline. | `sr5-core` p. 64 (PDF 66) |
| `point-buy` | Point Buy | `excluded` | Run Faster creation method outside approved scope. | `run-faster` p. 62 (PDF 64) |
| `life-modules` | Life Modules | `excluded` | Run Faster creation method outside approved scope. | `run-faster` p. 62 (PDF 64) |
| `run-faster-option-catalogs` | Run Faster Option Catalogs | `excluded` | Every Run Faster metatype, quality, skill, spell, item, and other option is excluded; only Sum-to-Ten and approved formula-grant wording are admitted. | `run-faster` pp. 62-63 (PDF 64-65) |

## Source Discrepancies And Decisions

| Subject | Approved ledger behavior | Source discrepancy and provenance |
| --- | --- | --- |
| Magician/mystic-adept priority formula grants | Grants may be spells, rituals, and/or alchemical preparations; no Run Faster catalog options enter scope. Decision: `magic.priority-grant-formula-types`. | Core says `spells`; Run Faster's copied table expands the wording. `sr5-core` p. 65 (PDF 67); `run-faster` p. 63 (PDF 65). |
| Dwarf costs | +10% gear and +20% lifestyle as separate modifiers. Decision: `metatype.dwarf-costs`. | The Metatype table and Street Gear specify +20% lifestyle, while Step Six separately specifies +10% gear. `sr5-core` pp. 66, 94, 420 (PDF 68, 96, 422). |
| Troll costs | +50% gear and +100% lifestyle as separate modifiers. Decision: `metatype.troll-costs`. | Step Two says +50% gear; the Metatype and Street Gear tables say +100% lifestyle; an attribute example and Step Six say +50% gear and lifestyle; the gear example again applies +100% lifestyle. `sr5-core` pp. 65-67, 94, 97, 420 (PDF 67-69, 96, 99, 422). |
| Natural maximum with Exceptional Attribute | The Exceptional Attribute selection consumes the sole Physical/Mental at-maximum allowance. Decision: `attribute.exceptional-maximum-count`. | The quality permits one attribute one point above its normal maximum, but the core does not expressly state whether another attribute may remain at ordinary maximum. `sr5-core` pp. 66, 72, 101 (PDF 68, 74, 103). |
| Complex-form cap | Grants plus purchases obey `min(Logic, Resonance x 2)`. Decision: `resonance.complex-form-cap`. | Step Seven caps forms at Logic; the Resonance Library caps known forms at Resonance x 2. `sr5-core` pp. 98, 252 (PDF 100, 254). |
| Formula cap scope | Separate `Magic x 2` caps for spells, rituals, and preparations. Decision: `magic.formula-cap-scope`. | Magic-user-type prose explicitly gives a cap for each group; Step Seven can read as one combined cap. `sr5-core` pp. 69, 98 (PDF 71, 100). |
| Registered sprites row | Treat it as registered sprites, not spirits. | The Additional Purchases table labels the row `Registering Spirits`, but its operative cost/restriction text uses sprite Level, Resonance, registered sprites, and tasks. `sr5-core` p. 98 (PDF 100). |
| Sprint-test rounding | Round half Running down, then apply minimum one. Decision: `movement.sprint-rounding`. | The source says half Running with minimum one but supplies no rounding direction. `sr5-core` p. 162 (PDF 164). |

## Review Footer

### Reviewed Ranges

- `sr5-core` pp. 48, 50-55, 62-107 (PDF 50, 52-57, 64-109): rounding,
  attributes, metatype context, complete experienced-runner creation sequence,
  priority cells, budgets, final calculations, and Karma costs.
- `sr5-core` pp. 151-153, 161-162 (PDF 153-155, 163-164): skill-list
  reconciliation, movement rates, sprint formula, and sprint-test limit.
- `sr5-core` pp. 227-230, 249-252 (PDF 229-232, 251-254): Matrix Initiative,
  Resonance maximum, living persona, and known complex-form cap.
- `sr5-core` pp. 314, 416-420, 451 (PDF 316, 418-422, 453): astral attributes,
  creation gear limits/legality, metatype size costs, and ware grades.
- `run-faster` pp. 62-63 (PDF 64-65): the complete approved Run Faster range.

### Entry Counts

Counts include every stable-ID row in this file and exclude explanatory prose:

| Classification | Approved-PDF entries |
| --- | ---: |
| `selectable` | 32 |
| `parameterized` | 4 |
| `included-component` | 0 |
| `generated` | 20 |
| `bookkeeping` | 46 |
| `creation-unavailable` | 0 |
| `excluded` | 5 |
| **Total** | **107** |

`creation-unavailable` is zero here because unavailable metatypes/paths are
eligibility states inside their priority cells, not independent options. Item
rows barred by global creation limits are counted in their equipment ledgers.

### Exclusions And Reconciliation

- Explicit exclusions: 5, all listed above with approved-PDF citations.
- Approved-PDF entries: 107.
- Runtime entries: 0; no CHAR-802 runtime catalog exists.
- Missing runtime entries: 107.
- Unexpected runtime entries: 0.
- Adjudicated differences: runtime absence is expected until CHAR-802; all
  approved source discrepancies affecting this category are recorded above.

### Remaining Unknown Facts

None.

The approved decisions resolve every ambiguity that affects this category. The
source-only Reputation labels are intentionally not asserted as facts without a
creation formula, and therefore are not unknown catalog values.

### Runtime Reconciliation Status

`Not implemented`. CHAR-802 must materialize and reconcile this approved
inventory before catalog version `1.0.0` can be published.
