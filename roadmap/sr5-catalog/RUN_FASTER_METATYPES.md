# Run Faster Metavariants Ledger (CHAR-813)

This is the CHAR-813 source ledger for Run Faster's 17 metavariants. It is a
review input for a future runtime catalog change, not a runtime catalog and
not a substitute for the approved book. It extends
[`PRIORITIES_METATYPES.md`](PRIORITIES_METATYPES.md), which remains the
ledger for the five core metatypes and the Standard Priority / Sum-to-Ten
creation methods.

CHAR-813 is a project-owner-approved expansion of the previously excluded
`run-faster-option-catalogs` row in `PRIORITIES_METATYPES.md`. Per the project
owner's 2026-08-26 decision, only the 17 metavariants of the five core
metatypes are in scope. Metasapients (Centaur, Naga, Pixie, Sasquatch),
shapeshifters (10 forms), and changelings/SURGE remain excluded: they require
an inherent Magic attribute, natural weapons, and movement-rate mechanics that
do not exist anywhere in the current catalog or evaluator, and are a
materially different, larger feature. Run Faster's Point Buy and Life Modules
creation methods remain excluded per the existing manifest.

## Source

Only `run-faster`, pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md), is used. This
chapter ("The Mess of Metahumanity") carries the same two-page printed/PDF
offset as the character-creation range already reviewed for that manifest.

## Scope

Included:

- The 17 metavariants of the four non-human core metatypes plus Human, their
  natural attribute ranges, their racial-trait bundles, and their Standard
  Priority / Sum-to-Ten Karma surcharges from the Extended Priority Charts.

Excluded (unchanged from `PRIORITIES_METATYPES.md`, restated here for this
ledger's own reconciliation):

- Metasapients: Centaur, Naga, Pixie, Sasquatch (`run-faster` pp. 98-101, PDF
  100-103).
- Shapeshifters: Bovine, Canine, Equine, Falconine, Leonine, Lupine,
  Pantherine, Tigrine, Ursine, Vulpine (`run-faster` pp. 100-101, 104-105, PDF
  102-103, 106-107).
- Changelings/SURGE, including the full Positive/Negative Metagenic Qualities
  catalog and Infected creation (`run-faster` pp. 101-141, PDF 103-143).
- Point Buy and Life Modules creation methods (`run-faster` p. 62, PDF 64;
  unchanged from `PRIORITIES_METATYPES.md`).
- The Point-Buy-only Metatype Cost Table (`run-faster` p. 66, PDF 68); it does
  not apply to Standard Priority or Sum-to-Ten and is superseded here by the
  Extended Priority Charts below.

## Metavariant Selection Model

A metavariant is a `parameterized` sub-choice of its parent metatype's
Metatype-priority-cell selection, not an independent priority-cell option. A
player who assigns a priority level to Metatype and picks that level's core
metatype (Dwarf, Elf, Ork, Troll, or Human) may additionally pick one of that
metatype's approved metavariants. Picking a metavariant:

- Replaces the parent metatype's natural attribute ranges with the
  metavariant's own ranges (below), not an additive change.
- Replaces the parent metatype's `Traits and cost modifiers` text with the
  metavariant's own racial-trait bundle (below); the bundle is exhaustive, so
  a metavariant does not automatically keep a parent-metatype trait its own
  bundle omits (for example, Gnome and Hanuman do not carry the Dwarf pathogen
  toxin resistance that Koborokuru and Menehune do carry). Source: `run-faster`
  pp. 87-99 (PDF 89-101).
- Replaces the priority cell's special attribute point grant for that
  metatype/level pair with the metavariant's own value from the Extended
  Priority Charts, and adds that chart's flat "Additional Karma Cost" to the
  character's Karma ledger (spent, never granted; every value in the approved
  range is zero or a positive spend). Source: `run-faster` pp. 106-107 (PDF
  108-109).
- Is available only at the priority levels where its parent metatype is
  itself available (Dwarf/Ork metavariants: A-C; Troll metavariants: A-B; Elf
  metavariants: A-D; Human's Nartaki: A-E), matching
  `PRIORITIES_METATYPES.md`'s existing per-metatype availability. The Extended
  Priority Charts independently confirm this pattern for every metavariant.
- Is available under both Standard Priority and Sum-to-Ten, since Run Faster
  states metavariant creation "follows the same procedures as for the
  standard metatypes" without restricting to one method, and Sum-to-Ten reuses
  the same priority-cell/level structure. Decision:
  `metavariant.creation-method-availability`. **Approved** (project owner,
  2026-08-26): both methods.

The "+4"/"+5" cost markers on the Hobgoblin and Oni rows of the Extended `C`
Priority Chart are typeset the same as every other row's plain integer
elsewhere (Hobgoblin: 5 at both A and B; Oni: 4 at both A and B), so they are
read here as the same plain Karma values, not a different mechanic. Decision:
`metavariant.plus-glyph-cost`. **Approved** (project owner, 2026-08-26): read
as the plain integer.

## Metavariants By Parent Metatype

Attribute values are natural minimum/maximum, in the same BOD/AGI/REA/STR/
WIL/LOG/INT/CHA/EDG order as `PRIORITIES_METATYPES.md`. Racial traits restate
the book's own trait names; the mechanical effect of each named trait is
recorded once in the Racial Trait Glossary below rather than repeated per
metavariant. Source for every attribute row and trait-name list: `run-faster`
p. 104 (PDF 106).

### Dwarf Metavariants

| ID | Display name | BOD | AGI | REA | STR | WIL | LOG | INT | CHA | EDG | Racial traits | Flavor source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `gnome` | Gnome | 1/4 | 2/7 | 1/6 | 1/4 | 2/7 | 2/7 | 1/6 | 1/6 | 1/6 | +20% lifestyle cost; Arcane Arrester 2; Neoteny; Thermographic Vision | `run-faster` pp. 87-88 (PDF 89-90) |
| `hanuman` | Hanuman | 1/6 | 2/7 | 1/6 | 2/7 | 1/6 | 1/5 | 2/7 | 1/5 | 1/6 | +20% lifestyle cost; Monkey Paws; Functional Tail (Prehensile); Thermographic Vision; Unusual Hair (Body) | `run-faster` pp. 88-89 (PDF 90-91) |
| `koborokuru` | Koborokuru | 2/7 | 1/6 | 1/6 | 2/7 | 2/7 | 1/6 | 1/6 | 1/6 | 1/6 | +20% lifestyle cost; Celerity; +2 dice pathogen/toxin resistance; Thermographic Vision; Unusual Hair | `run-faster` pp. 89-90 (PDF 91-92) |
| `menehune` | Menehune | 2/7 | 2/7 | 1/5 | 2/7 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | +20% lifestyle cost; +2 dice pathogen resistance; Thermographic Vision; Underwater Vision; Webbed Digits | `run-faster` pp. 89-90 (PDF 91-92) |

### Ork Metavariants

| ID | Display name | BOD | AGI | REA | STR | WIL | LOG | INT | CHA | EDG | Racial traits | Flavor source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `hobgoblin` | Hobgoblin | 3/8 | 1/6 | 1/6 | 2/7 | 1/6 | 1/5 | 1/6 | 1/5 | 1/6 | Fangs; Keen-Eared; Low-Light Vision; Poor Self Control (Vindictive, `run-faster` p. 158) | `run-faster` p. 91 (PDF 93) |
| `ogre` | Ogre | 4/9 | 1/6 | 1/5 | 3/8 | 2/7 | 1/5 | 1/6 | 1/4 | 1/6 | Low-Light Vision; Ogre Stomach | `run-faster` pp. 91-92 (PDF 93-94) |
| `oni` | Oni | 3/8 | 2/7 | 1/6 | 2/7 | 1/6 | 1/5 | 1/6 | 2/7 | 1/6 | Low-Light Vision; Striking Skin Pigmentation | `run-faster` p. 92 (PDF 94) |
| `satyr` | Satyr | 2/7 | 1/6 | 2/7 | 2/7 | 1/6 | 1/6 | 1/6 | 1/5 | 1/6 | Low-Light Vision; Satyr Legs | `run-faster` p. 92 (PDF 94) |

### Troll Metavariants

| ID | Display name | BOD | AGI | REA | STR | WIL | LOG | INT | CHA | EDG | Racial traits | Flavor source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cyclops` | Cyclops | 5/10 | 1/5 | 1/6 | 6/11 | 1/6 | 1/4 | 1/5 | 1/4 | 1/6 | +100% lifestyle cost; Cyclopean Eye; +1 Reach; Thermographic Vision | `run-faster` pp. 93-94 (PDF 95-96) |
| `fomorian` | Fomorian | 4/9 | 1/5 | 1/6 | 5/10 | 1/5 | 1/4 | 1/4 | 1/5 | 1/6 | +100% lifestyle cost; Arcane Arrester 1; Thermographic Vision; +1 Reach | `run-faster` pp. 94-95 (PDF 96-97) |
| `giant` | Giant | 5/10 | 1/5 | 1/5 | 5/10 | 1/6 | 1/5 | 1/5 | 1/5 | 1/6 | +100% lifestyle cost; Dermal Alteration (Bark Skin, +2 armor); Thermographic Vision; +1 Reach | `run-faster` p. 95 (PDF 97) |
| `minotaur` | Minotaur | 6/11 | 1/5 | 1/6 | 5/10 | 1/6 | 1/5 | 1/6 | 1/4 | 1/6 | +100% lifestyle cost; Goring Horns; Thermographic Vision; +1 Reach | `run-faster` pp. 95-96 (PDF 97-98) |

### Elf Metavariants

| ID | Display name | BOD | AGI | REA | STR | WIL | LOG | INT | CHA | EDG | Racial traits | Flavor source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `dryad` | Dryad | 1/6 | 2/7 | 1/6 | 1/5 | 1/6 | 1/6 | 1/6 | 3/8 | 1/6 | Glamour; Low-Light Vision; Symbiosis | `run-faster` p. 96 (PDF 98) |
| `nocturna` | Nocturna | 1/5 | 3/8 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 2/7 | 1/6 | Allergy (Sunlight, Mild); Low-Light Vision; Keen-Eared; Nocturnal; Unusual Hair (Colored Fur) | `run-faster` pp. 96-97 (PDF 98-99) |
| `wakyambi` | Wakyambi | 1/6 | 2/7 | 1/6 | 1/6 | 1/6 | 1/5 | 2/7 | 1/6 | 1/6 | Celerity; Elongated Limbs; Low-Light Vision | `run-faster` pp. 97-98 (PDF 99-100) |
| `xapiri-thepe` | Xapiri Thëpë | 1/6 | 2/7 | 1/6 | 1/6 | 1/6 | 1/5 | 1/6 | 2/7 | 1/6 | Allergy (Pollutants, Mild); Low-Light Vision; Photometabolism | `run-faster` p. 99 (PDF 101) |

### Human Metavariant

| ID | Display name | BOD | AGI | REA | STR | WIL | LOG | INT | CHA | EDG | Racial traits | Flavor source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `nartaki` | Nartaki | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 1/6 | 2/7 | Shiva Arms (extra pair of arms); Striking Skin Pigmentation | `run-faster` pp. 99-100 (PDF 101-102) |

## Extended Priority Charts (Special Attribute Points / Additional Karma Cost)

Source: `run-faster` pp. 106-107 (PDF 108-109). A blank cell means the
metavariant is unavailable at that priority level, matching its parent
metatype's own core availability.

| Metavariant | A | B | C | D | E |
| --- | --- | --- | --- | --- | --- |
| Gnome | 7 pts / 7 Karma | 4 pts / 7 Karma | 1 pt / 7 Karma | - | - |
| Hanuman | 7 pts / 5 Karma | 4 pts / 5 Karma | 1 pt / 5 Karma | - | - |
| Koborokuru | 7 pts / 0 Karma | 4 pts / 0 Karma | 1 pt / 0 Karma | - | - |
| Menehune | 7 pts / 2 Karma | 4 pts / 2 Karma | 1 pt / 2 Karma | - | - |
| Hobgoblin | 7 pts / 5 Karma | 4 pts / 5 Karma | 0 pts / 5 Karma | - | - |
| Ogre | 7 pts / 8 Karma | 4 pts / 8 Karma | 0 pts / 8 Karma | - | - |
| Oni | 7 pts / 4 Karma | 4 pts / 4 Karma | 0 pts / 4 Karma | - | - |
| Satyr | 7 pts / 10 Karma | 4 pts / 10 Karma | 0 pts / 10 Karma | - | - |
| Cyclops | 5 pts / 2 Karma | 0 pts / 2 Karma | - | - | - |
| Fomorian | 5 pts / 12 Karma | 0 pts / 12 Karma | - | - | - |
| Giant | 5 pts / 2 Karma | 0 pts / 2 Karma | - | - | - |
| Minotaur | 5 pts / 2 Karma | 0 pts / 2 Karma | - | - | - |
| Dryad | 8 pts / 0 Karma | 6 pts / 0 Karma | 3 pts / 0 Karma | 0 pts / 0 Karma | - |
| Nocturna | 8 pts / 0 Karma | 6 pts / 0 Karma | 3 pts / 0 Karma | 0 pts / 0 Karma | - |
| Wakyambi | 8 pts / 12 Karma | 6 pts / 12 Karma | 3 pts / 12 Karma | 0 pts / 12 Karma | - |
| Xapiri Thëpë | 8 pts / 0 Karma | 6 pts / 0 Karma | 3 pts / 0 Karma | 0 pts / 0 Karma | - |
| Nartaki | 8 pts / 0 Karma | 6 pts / 0 Karma | 4 pts / 0 Karma | 2 pts / 0 Karma | 1 pt / 0 Karma |

## Racial Trait Glossary

Mechanical effect of every trait name used above. Source: `run-faster` pp.
111-122 (PDF 113-124) unless noted otherwise. These traits are granted free as
part of the metavariant's exhaustive trait bundle (priced already by the
Extended Priority Chart's flat Karma cost above); they are not separately
purchasable qualities and do not consume the positive/negative quality Karma
caps.

| Trait | Effect | Source |
| --- | --- | --- |
| Thermographic Vision | Natural thermographic vision, same as dwarf/troll core trait. | p. 116 (PDF 118) |
| Low-Light Vision | Natural low-light vision; replaced if cybereyes are installed (same as core elf/ork). | p. 115 (PDF 117); `sr5-core` p. 66 (PDF 68) |
| Arcane Arrester (rating) | Add (rating x 2) dice to resist any spell targeted at the character; cannot combine with Magic Resistance. | pp. 111-112 (PDF 113-114) |
| Neoteny | Physical Condition Monitor becomes 6 + (Body/2, rounded up); +10% lifestyle cost; social stigma. | pp. 113, 121-122 (PDF 115, 123-124) |
| Monkey Paws | +2 dice pool (barefoot; +1 if shod) to non-tumbling Gymnastics, Climbing, and zero-G movement tests. | pp. 115-116 (PDF 117-118) |
| Functional Tail (Prehensile) | Prehensile tail can manipulate objects (-4 dice pool for fine manipulation); effective Strength is half unaugmented Strength, rounded down. | p. 115 (PDF 117) |
| Unusual Hair | Unusual hair color/texture/pattern; cosmetic only. | p. 122 (PDF 124) |
| Celerity | Walking Agility x 3, Running Agility x 6, +1 m/hit Sprint Increase; incompatible with leg/muscle augmentation and Satyr Legs. | p. 113 (PDF 115) |
| +2 dice pathogen/toxin resistance | Same mechanic as the core Dwarf trait. | `sr5-core` p. 66 (PDF 68) |
| Underwater Vision | Normal vision unrestricted underwater (as if wearing goggles/mask). | p. 116 (PDF 118) |
| Webbed Digits | +2 dice pool Swimming Tests; -1 dice pool fine manipulation with the affected digits. | p. 121 (PDF 123) |
| Fangs | Unarmed Combat attack DV (STR+1)P, Reach -1, AP --. | p. 114 (PDF 116) |
| Keen-Eared | +1 dice pool audio-based Perception Tests. | p. 114 (PDF 116) (also printed "Keen Eared" p. 104, PDF 106) |
| Poor Self Control (Vindictive) | Run Faster negative quality (not core SR5, corrected after page verification): the Vindictive variant costs 5 Karma. Modeled as its own fixed-cost quality entry rather than the full 5-variant Poor Self Control family (Braggart, Thrill-Seeker, Compulsive, Vindictive, Combat Monster), since only Vindictive is needed by an approved metavariant and the other variants' non-uniform costs do not fit the single-`Cost`-field `QualityDefinition` shape without a schema change out of this ticket's scope. | `run-faster` p. 158 (PDF 160) |
| Ogre Stomach | -20% lifestyle cost; +2 dice pool Toxin Resistance Tests for ingested toxins. | p. 119 (PDF 121) |
| Striking Skin Pigmentation | Unusual, obvious skin coloration; +2 dice pool to Matrix Search/identify/locate tests against the character. | p. 121 (PDF 123) |
| Satyr Legs | Running rate becomes Agility x 6, +1 m/hit Sprint Increase, +2 Strength to kicking-attack damage; social stigma. | p. 120 (PDF 122) |
| Cyclopean Eye | -1 dice pool to Combat Tests and precision skill tests; cannot be corrected by cybereye; social stigma. | pp. 120-121 (PDF 122-123) |
| +1 Reach | Flat +1 Reach in melee. | p. 104 (PDF 106) |
| Dermal Alteration (Bark Skin) | +2 armor, cumulative with worn armor. | p. 114 (PDF 116) |
| Goring Horns | Exotic Melee Weapon (Horns) attack DV (STR+2)P, Reach --, AP -1. | p. 116 (PDF 118) |
| Glamour | +2 Social limit; +1 dice pool to Social Skill Tests except Intimidation; also incurs the core Distinctive Style quality's effect. | p. 116 (PDF 118); `sr5-core` p. 80 |
| Symbiosis | After a season of residence, +1 dice pool Outdoor Skill Group tests and Social Tests with local residents; +1 to local Healing Tests; environmental harm to the area imposes a persistent Mild Allergy-equivalent penalty. | p. 122 (PDF 124) |
| Allergy (substance, Mild) | Reuses the existing core `allergy` quality mechanic and parameters; granted free as part of the bundle rather than purchased. | `sr5-core` core catalog `allergy` quality |
| Nocturnal | All Mental attributes -1 during daylight hours. | p. 123 (PDF 125) |
| Elongated Limbs | +1 Reach, cumulative with other Reach modifiers; +10% cost for accommodating armor/clothing. | p. 114 (PDF 116) |
| Photometabolism | -10% lifestyle cost; -1 dice pool to Social Tests at night/in shade. | p. 119 (PDF 121) |
| Shiva Arms (one pair) | Extra pair of arms; can wield/use extra weapons per the Multiple Attacks rules with the normal off-hand penalty; social stigma. | p. 118 (PDF 120) |

## Source Discrepancies And Decisions

| Subject | Approved ledger behavior | Source discrepancy and provenance |
| --- | --- | --- |
| Metavariant Karma pricing model | The Extended Priority Chart's flat per-level Karma value is the entire price; it is not derived by summing the Racial Trait Glossary's Changelings-chapter Karma costs, even though the trait names are shared vocabulary. Decision: `metavariant.pricing-source`. **Approved** (project owner, 2026-08-26). | The narrative text ("some options require part of the character's starting Karma") could be misread as pointing to the per-quality Changelings costs; the numeric Extended Priority Charts are lower than summing the individual quality costs would produce (e.g., Gnome's Arcane Arrester 2 alone costs 20 Karma standalone but Gnome's total chart surcharge is only 7 Karma at every level). `run-faster` pp. 106-112 (PDF 108-114). |
| Creation-method availability | Metavariants are available under both Standard Priority and Sum-to-Ten. Decision: `metavariant.creation-method-availability`. **Approved** (project owner, 2026-08-26). | Run Faster's "Creating a Metavariant Character" section does not explicitly restrict itself to one method; the recommendation follows from Sum-to-Ten reusing the same priority-cell structure. `run-faster` pp. 62-63, 102 (PDF 64-65, 104). |
| Hobgoblin/Oni "+" cost glyph at Priority C | Read as the same plain integer used at A/B (5 and 4 respectively), not a different value or a refund. Decision: `metavariant.plus-glyph-cost`. **Approved** (project owner, 2026-08-26). | The Extended `C` Priority Chart image shows "+5" and "+4" where every other cell in every chart shows a plain integer; no legend explains the "+". `run-faster` p. 106 (PDF 108). |
| Poor Self Control (Vindictive) | Add "Poor Self Control (Vindictive)" as a new Run Faster quality (5 Karma) as part of CHAR-813, so Hobgoblin's bundle is mechanically complete. Decision: `metavariant.hobgoblin-missing-quality`. **Approved** (project owner, 2026-08-26): add the quality now. | Initial drafting of this ledger mis-cited this as an `sr5-core` p. 79 quality from memory; page verification against both approved PDFs found no "Poor Self Control" text in `sr5-core` at all. It is in fact a `run-faster` quality (p. 158, PDF 160) not yet in the 59-entry catalog. Corrected before implementation; recorded here as a caution against citing from recollection instead of the PDF. |
| Selection architecture | A metavariant is modeled as a parameterized sub-choice of the Metatype priority cell, not a new top-level catalog category or a new creation method. Decision: `metavariant.selection-architecture` (affects `RulesetCatalog` schema). **Approved** (project owner, 2026-08-26): sub-choice of parent metatype. | Not a source discrepancy; recorded here because it is an implementation-shaping decision with no directly analogous existing schema field (`PriorityCellDefinition.AvailableMetatypeIds` currently assumes one flat metatype list per cell, not a metatype-with-sub-variant structure). |

## Review Footer

### Reviewed Ranges

- `run-faster` pp. 87-104, 106-109 (PDF 89-106, 108-111): metavariant flavor
  text, the Metasapient/Metavariant Attribute Table, and the Extended
  Priority Charts.
- `run-faster` pp. 111-124 (PDF 113-126): Positive and Negative Metagenic
  Qualities glossary, read only to source the mechanical effect of the
  specific trait names used by the 17 in-scope metavariants.
- `run-faster` pp. 62-66, 98-107 (PDF 64-68, 100-109): re-confirmed Point Buy
  Metatype Cost Table is Point-Buy-only, and the excluded Metasapient/
  Shapeshifter/Changeling sections' page ranges for this ledger's exclusion
  list.

### Entry Counts

| Classification | Approved-PDF entries |
| --- | ---: |
| `parameterized` (metavariants) | 17 |
| `bookkeeping` (Extended Priority Chart cells, non-blank) | 60 |
| `excluded` (metasapients, shapeshifters, changelings, Point Buy, Life Modules, Metatype Cost Table) | 6 categories |
| **Total metavariant-scope entries** | **77** |

### Remaining Unknown Facts

None. Every ambiguity found while compiling this ledger was resolved by the
project owner on 2026-08-26; see the decisions above.

### Runtime Reconciliation Status

`Not implemented`. All four gating decisions are approved (project owner,
2026-08-26); CHAR-813 implementation (schema, catalog data, loader,
evaluator, and creator UI changes) may now proceed.
