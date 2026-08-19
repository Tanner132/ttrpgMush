# SR5 Rules Decision Register

This register prevents ambiguous or conflicting text in the approved PDFs from
becoming an accidental implementation decision. `Source-resolved` entries may be
implemented as cited. `Owner decision required` entries block their affected
slice until the project owner records a selection, reviewer, and date.

## Source-Resolved Rules

| ID | Resolution | Source |
| --- | --- | --- |
| `priority.sum-to-ten` | Spend exactly 10; A/B/C/D/E cost 4/3/2/1/0; levels may repeat; each category is assigned once. | `run-faster` pp. 62-63 (PDF 64-65) |
| `quality.creation-caps` | Positive and negative quality totals each have a separate 25 Karma ceiling. | `sr5-core` p. 71 (PDF 73) |
| `attribute.one-natural-maximum` | At most one Physical or Mental attribute may be at its natural maximum during creation; special attributes are excluded. | `sr5-core` pp. 66, 101 (PDF 68, 103) |
| `skill.creation-maximum` | Natural skill maximum is 6, or 7 for the one skill selected by Aptitude. | `sr5-core` pp. 72, 88 (PDF 74, 90) |
| `mystic-adept.power-points` | Mystic adepts receive no free Power Points and may buy full points for 2 Karma each, up to Magic. | `sr5-core` pp. 69, 71, 101 (PDF 71, 73, 103) |
| `gear.legality-at-creation` | Numeric Availability may not exceed 12. Restricted items require an appropriate license; Forbidden items cannot be licensed. The creation ceiling does not exclude an item solely for an R/F suffix. | `sr5-core` pp. 94, 416-419 (PDF 96, 418-421) |
| `contact.creation-cap` | Connection and Loyalty are each at least 1 and no contact may consume more than 7 Karma total at creation. | `sr5-core` p. 98 (PDF 100) |
| `identity.fake-license-link` | A fake license belongs to one fake SIN and one item/activity type; Forbidden items have no license. | `sr5-core` pp. 419, 443 (PDF 421, 445) |
| `allocation.unused-priority-points` | Physical/Mental attribute points, priority skill/group points, and metatype special-attribute points must be spent at finalization; special-attribute points never convert to another currency. | `sr5-core` pp. 66, 88 (PDF 68, 90); special-attribute clause superseded by `special-attribute.full-allocation` |
| `carryover` | At most 5,000 nuyen and 7 Karma carry into play. | `sr5-core` pp. 94, 98, 101 (PDF 96, 100, 103) |
| `technomancer.priority-grants` | A grants Resonance 6, two Rating 5 Resonance skills, and 5 complex forms; B grants 4, two Rating 4 skills, and 2 forms; C grants 3 and 1 form. | `sr5-core` p. 65 (PDF 67) |
| `preparation.basic-eligibility` | A preparation requires its separately learned alchemical formula; healing preparations require Command; targets must be on the physical plane. | `sr5-core` pp. 304-306 (PDF 306-308) |

## Owner Decisions Required

Recommended choices are engineering proposals, not approved rules.

| ID | Conflict or gap | Recommended choice | Affected work | Status |
| --- | --- | --- | --- | --- |
| `magic.formula-cap-scope` | Core p. 69 permits Magic x 2 formulae from each of spells, rituals, and preparations; p. 98 can read as one combined cap. | Use three independent Magic x 2 caps because p. 69 is explicit and includes an example. | CHAR-808 | Approved 2026-08-18 |
| `magic.priority-grant-formula-types` | The core priority table grants `spells`; Run Faster's copied table adds rituals/preparations. | Allow magician and mystic-adept grants to be selected as spells, rituals, and/or alchemical preparations. This is the sole approved non-allocation Run Faster rule and admits no Run Faster catalog entries. | CHAR-802, CHAR-808 | Approved 2026-08-18 |
| `magic.aspected-purchase-scope` | Core broadly allows aspected magicians to buy formulae despite permanent skill-group restrictions. | Sorcery may buy spells/rituals; Enchanting may buy preparations; Conjuring may buy neither. | CHAR-808 | Approved 2026-08-18 |
| `magic.tradition-by-path` | `All magicians` must have a tradition, but usage may be generic or path-specific. | Require one for magician, mystic adept, and all aspected magicians; do not require one for a pure adept. | CHAR-808 | Approved 2026-08-18 |
| `resonance.complex-form-cap` | Creation cap is Logic while the general known-form cap is Resonance x 2. | Enforce both against grants and purchases: `min(Logic, Resonance x 2)`. | CHAR-808 | Approved 2026-08-18 |
| `metatype.troll-costs` | The PDF variously states +50% gear/lifestyle and +100% lifestyle. | Use +50% gear and +100% lifestyle, matching the dedicated metatype/resource tables rather than examples. | CHAR-806, CHAR-809, CHAR-810 | Approved 2026-08-18 |
| `metatype.dwarf-costs` | The PDF states +10% gear and +20% lifestyle in separate sections. | Treat them as separate category rules: +10% gear and +20% lifestyle. | CHAR-806, CHAR-809, CHAR-810 | Approved 2026-08-18 |
| `ware.creation-grades` | Character creation permits standard/alphaware; the augmentation chapter also permits used ware. | Exclude used ware at creation; standard and alphaware remain available. | CHAR-809 | Approved 2026-08-18 |
| `skill.priority-grant-collision` | No rule defines grants colliding with purchased individual/group ranks. | Permit raising a granted skill, reject duplicate sources that would discard value, and apply the final natural-rating cap across all sources. | CHAR-807, CHAR-808 | Approved 2026-08-18 |
| `attribute.exceptional-maximum-count` | The core permits only one Physical/Mental attribute at its natural maximum and describes Exceptional Attribute as allowing one to be one higher, but does not expressly say whether another attribute may also sit at its ordinary maximum. | The Exceptional Attribute selection consumes the one-at-natural-maximum allowance; no second Physical/Mental attribute may be at its ordinary natural maximum. | CHAR-806, CHAR-807 | Approved 2026-08-18 |
| `essence.magic-resonance-order` | Character creation and the Magic/Resonance chapters phrase fractional Essence loss and current/maximum reductions differently. | Track natural and augmented values separately; total all selected ware Essence loss, then reduce current and maximum Magic or Resonance once for each point or fraction of cumulative loss before final eligibility checks. | CHAR-808, CHAR-809 | Approved 2026-08-18 |
| `skill.group-break-and-rebuild` | Step Five forbids breaking groups; Step Seven allows it; p. 89 says specialization breakage cannot be reconstructed while p. 129 permits rebuilding equal ratings. | Keep groups atomic in Step Five; allow Step Seven breaks; permit rebuilding when all members match. | CHAR-807 | Approved 2026-08-18 |
| `skill.catalog-defects` | Summary/detail tables conflict on Herding, Lockpicking, Arcane/Arcana, and Enchanting/Artificing. | Use detailed descriptions and group membership: exclude Herding and Lockpicking, use Arcana and Artificing. | CHAR-807 | Approved 2026-08-18 |
| `mentor.cat-infiltration` | Cat names nonexistent Infiltration while the core skill is Sneaking. | Map this mentor choice to Sneaking and preserve the source discrepancy in provenance. | CHAR-808 | Approved 2026-08-18 |
| `spell.mob-mind-area` | Prose says area effect while the range line omits `(A)`. | Treat Mob Mind as an area spell. | CHAR-808 | Approved 2026-08-18 |
| `preparation.ram-example` | A preparation example names Ram, but no core Ram spell exists. | Exclude Ram and its preparation as an invalid example reference. | CHAR-808 | Approved 2026-08-18 |
| `power.improved-sense-domain` | Improved Sense imports an open-ended subset of ware enhancements. | Initially expose only Direction Sense, Improved Tactile, Perfect Pitch, and Human Scale; add ware-derived options after an eligibility audit. | CHAR-808, CHAR-809 | Approved 2026-08-18 |
| `quality.open-parameters` | Several qualities rely on player/GM-authored subjects without deterministic acceptance rules. | Use bounded plain-text parameters with structural validation; no inferred mechanical effects beyond typed fields. | CHAR-807 | Approved 2026-08-18 |
| `mentor.custom-archetypes` | Core permits GM-created mentors but supplies no construction rules. | Exclude custom mentors and include only the 16 printed archetypes. | CHAR-808 | Approved 2026-08-18 |
| `knowledge.native-specialization` | Native language has nonnumeric rating N, while language specializations assume a skill rating. | Do not permit native-language specializations during initial creation. | CHAR-807 | Approved 2026-08-18 |
| `knowledge.unused-free-points` | Free Knowledge/Language points have dedicated provenance, but their unused disposition is not stated independently. | Intermediate drafts may leave points unallocated; finalization requires all free Knowledge/Language points to be spent. They never convert to another currency. | CHAR-807 | Approved 2026-08-18 |
| `matrix.quality-action-domain` | Codeslinger and Codeblock require a tested Matrix action, while some action definitions waive their test in particular states. | An action is eligible when its definition contains a test, even if a specific use can waive that test. | CHAR-807 | Approved 2026-08-18 |
| `mentor.thunderbird-critical-strike` | Thunderbird grants one `level` of Critical Strike, but Critical Strike is an unlevelled power selected for a skill. | Grant one free `critical-strike` selection with its required skill parameter. | CHAR-808 | Approved 2026-08-18 |
| `contact.unused-free-karma` | The PDF does not state what happens to unused Charisma x 3 Contact Karma. | Intermediate drafts may leave points unallocated; finalization requires all free Contact Karma to be spent. It never converts to general Karma. | CHAR-810 | Approved 2026-08-18 |
| `lifestyle.options-and-cash` | Core options are selectable, but their interaction with starting-cash rows is unstated. | Allow core options at creation; compute starting cash from the base lifestyle tier only. | CHAR-810 | Approved 2026-08-18 |
| `starting-cash.randomness` | Starting cash is a dice formula with no fixed alternative. | Roll once server-side during atomic finalization and persist the dice/result in the immutable sheet. | CHAR-810, CHAR-811 | Approved 2026-08-18 |
| `movement.sprint-rounding` | Maximum Sprint tests is half Running, minimum one, without a rounding rule. | Round down, then apply minimum one. | Future gameplay use | Approved 2026-08-18 |
| `gear.weapon-focus-base-cost` | The focus must be a melee weapon, but reviewed text does not clearly say whether mundane weapon cost is additional. | Charge both the base weapon and focus enchantment costs. | CHAR-809 | Approved 2026-08-18 |
| `gear.rating-cap-force` | The creation limit says item Rating 6, while gear also uses Force, Sensor, and Capacity. | Apply it to explicit purchasable Rating and Force, not capacity, quantities, or vehicle attributes. | CHAR-809 | Approved 2026-08-18 |
| `gear.ruger-model-name` | The firearm table says Ruger 100 while prose and index say Ruger 101. | Use stable ID `ruger-101`, display name `Ruger 101`, and retain `Ruger 100` only as source-provenance text. | CHAR-809 | Approved 2026-08-18 |
| `ware.bone-density-cost` | The Bone Density Augmentation table contains malformed `Raxing x 5,000` text. | Interpret the cost as `Rating x 5,000 nuyen`, retaining the malformed source text in provenance. | CHAR-809 | Approved 2026-08-18 |
| `gear.defiance-ex-shocker-cost` | The product table prices the Defiance EX Shocker at 250 nuyen while the creation example prices it at 210 nuyen. | Use 250 nuyen from the dedicated product table and retain 210 nuyen as conflicting example provenance. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.chemical-seal-table` | The clothing/armor table gives Chemical Seal Availability +6 and cost +6,000 nuyen; the dedicated armor-modification table gives 12R and 3,000 nuyen. | Use Capacity 6, Availability 12R, and cost 3,000 nuyen from the dedicated modification table; retain the clothing-table values as conflicting provenance. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.helmet-availability` | The clothing/armor table prints no Helmet Availability while the dedicated helmet/shield table prints 2. | Use Availability 2 from the dedicated helmet/shield table and retain the omission as provenance. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.smoke-area` | Smoke-grenade prose says 10-meter diameter while the table says 10-meter radius; thermal smoke says it is identical and its table also says radius. | Use the table's 10-meter radius for smoke and thermal smoke and retain the prose conflict as provenance. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.launcher-arming-distance` | General launcher combat rules use a 5-meter minimum/arming distance while rocket/missile street-gear prose says 10 meters. | Use 5 meters for launched grenades and the more-specific 10 meters for rockets and missiles. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.missile-sensor-range` | Missile cost requires a Sensor rating, but the reviewed core rules provide no permitted missile Sensor-rating range. | Do not invent a range: retain Sensor rating as an unspecified source parameter on creation-unavailable missile rows and expose no missile purchase choice during initial creation. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.arrow-rating-range` | Arrow cost and Availability require a Rating, but only bows receive an explicit maximum of 10. | Use Rating 1-10 for arrows and injection arrows by matching their host bow's printed range; apply the global creation cap of 6. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `gear.super-squirt-ammunition` | The Ares S-III Super Squirt requires DMSO gel packs, but the ammunition catalog gives them no cost or Availability. | Keep the weapon selectable, but expose no separately purchasable gel-pack ammunition until an approved source supplies merchandise facts; retain its included 20-round clip capacity as a weapon fact. | CHAR-801, CHAR-802, CHAR-809 | Approved 2026-08-18 |
| `special-attribute.values-model` | `SpecialAttributeAllocation.Values` entries could be absolute ratings or points-spent deltas. | Treat each entry as points spent above the metatype minimum; absolute = minimum + delta. | TD-03 | Approved 2026-08-19 |
| `special-attribute.edge-range` | Edge's per-metatype racial minimum/maximum is defined but was never validated. | Enforce absolute Edge within `[metatype minimum, metatype maximum]`; out-of-range emits `attributes.edge-out-of-range`. | TD-03 | Approved 2026-08-19 |
| `special-attribute.full-allocation` | Core permits unspent special-attribute points to be lost, which can leave final sheets incomplete. | Require metatype special-attribute points to be fully allocated at finalization; underspend emits `attributes.special-points-underallocated`. Supersedes the "lost" clause of `allocation.unused-priority-points`. | TD-03 | Approved 2026-08-19 |
| `karma.creation-pool-semantics` | Negative-quality awards and the shared creation pool's interaction with quality and awakening purchases were computed inconsistently across evaluators. | Spendable pool = `25 + negative`; positive qualities (capped at 25), purchased formulae (5 each), mystic-adept Power Points (2 each), and complex forms (4 each) all draw from it; negative qualities add their award to the pool; independent 25-Karma caps remain on positive and negative qualities. Drop the unreachable `positive - negative > 25` check. | TD-04 | Approved 2026-08-19 |

## Source Defects Retained As Provenance

These entries must not silently create catalog options:

- `Registering Spirits` is a sprite-registration row whose operative text uses
  sprites and Resonance: `sr5-core` p. 98 (PDF 100).
- Ruger 100 in a table conflicts with Ruger 101 in prose/index:
  `sr5-core` pp. 428-429 (PDF 430-431).
- Bone Density Augmentation contains malformed `Raxing x 5,000` cost text:
  `sr5-core` p. 460 (PDF 462).
- Cat names Infiltration although no such core active skill exists:
  `sr5-core` p. 321 (PDF 323).
- An alchemical example names Ram although the core spell inventory does not:
  `sr5-core` p. 306 (PDF 308).

## Approval Record

| Decision ID | Selected behavior | Reviewer | Date |
| --- | --- | --- | --- |
| `magic.priority-grant-formula-types` | Grants may be spells, rituals, and/or alchemical preparations; no Run Faster catalog entries are admitted. | Project owner | 2026-08-18 |
| `magic.formula-cap-scope` | Separate Magic x 2 caps for spells, rituals, and preparations. | Project owner | 2026-08-18 |
| `resonance.complex-form-cap` | Grants and purchases obey both Logic and Resonance x 2 caps. | Project owner | 2026-08-18 |
| `skill.priority-grant-collision` | Grants may be raised; allocations that discard duplicate value are rejected. | Project owner | 2026-08-18 |
| `metatype.troll-costs` | Troll gear costs +50%; troll lifestyles cost +100%. | Project owner | 2026-08-18 |
| `metatype.dwarf-costs` | Dwarf gear costs +10%; dwarf lifestyles cost +20%. | Project owner | 2026-08-18 |
| `ware.creation-grades` | Used ware is unavailable at creation; standard and alphaware are available. | Project owner | 2026-08-18 |
| `skill.catalog-defects` | Exclude Herding and Lockpicking; catalog Arcana and Artificing. | Project owner | 2026-08-18 |
| `magic.aspected-purchase-scope` | Formula purchases are restricted by the selected magical aspect. | Project owner | 2026-08-18 |
| `magic.tradition-by-path` | All magical paths except pure adept require a tradition. | Project owner | 2026-08-18 |
| `mentor.cat-infiltration` | Cat's Infiltration reference maps to Sneaking. | Project owner | 2026-08-18 |
| `spell.mob-mind-area` | Mob Mind is an area spell. | Project owner | 2026-08-18 |
| `preparation.ram-example` | Ram and its preparation are excluded. | Project owner | 2026-08-18 |
| `skill.group-break-and-rebuild` | Groups are atomic in Step Five, may break in Step Seven, and may rebuild when all member ratings match. | Project owner | 2026-08-18 |
| `power.improved-sense-domain` | Initial creation exposes only the four explicitly defined sense options. | Project owner | 2026-08-18 |
| `quality.open-parameters` | GM-authored quality subjects use bounded plain text without inferred effects. | Project owner | 2026-08-18 |
| `mentor.custom-archetypes` | Only the 16 printed mentor archetypes are selectable. | Project owner | 2026-08-18 |
| `knowledge.native-specialization` | Native languages cannot be specialized during initial creation. | Project owner | 2026-08-18 |
| `contact.unused-free-karma` | Drafts may leave free Contact Karma unspent; finalization may not. | Project owner | 2026-08-18 |
| `lifestyle.options-and-cash` | Lifestyle options do not alter the base-tier starting-cash formula. | Project owner | 2026-08-18 |
| `starting-cash.randomness` | Starting cash is rolled once server-side during finalization and persisted. | Project owner | 2026-08-18 |
| `gear.weapon-focus-base-cost` | Charge both the mundane weapon and focus enchantment costs. | Project owner | 2026-08-18 |
| `gear.rating-cap-force` | The Rating 6 limit applies to purchasable Rating and Force only. | Project owner | 2026-08-18 |
| `attribute.exceptional-maximum-count` | Exceptional Attribute consumes the one-at-maximum allowance. | Project owner | 2026-08-18 |
| `essence.magic-resonance-order` | Apply cumulative Essence loss before final Magic/Resonance eligibility checks. | Project owner | 2026-08-18 |
| `knowledge.unused-free-points` | Finalization requires all free Knowledge/Language points to be allocated. | Project owner | 2026-08-18 |
| `matrix.quality-action-domain` | An action is eligible when its definition contains a test. | Project owner | 2026-08-18 |
| `mentor.thunderbird-critical-strike` | Grant one Critical Strike selection with a chosen skill. | Project owner | 2026-08-18 |
| `gear.ruger-model-name` | Canonicalize Ruger 101 and retain Ruger 100 as provenance. | Project owner | 2026-08-18 |
| `ware.bone-density-cost` | Interpret the malformed cost as Rating x 5,000 nuyen. | Project owner | 2026-08-18 |
| `movement.sprint-rounding` | Round half Running down, then apply the minimum of one test. | Project owner | 2026-08-18 |
| `gear.defiance-ex-shocker-cost` | Use the dedicated product-table cost of 250 nuyen. | Project owner | 2026-08-18 |
| `gear.chemical-seal-table` | Use Capacity 6, Availability 12R, and cost 3,000 nuyen. | Project owner | 2026-08-18 |
| `gear.helmet-availability` | Use Availability 2 from the dedicated helmet/shield table. | Project owner | 2026-08-18 |
| `gear.smoke-area` | Use a 10-meter radius for smoke and thermal-smoke grenades. | Project owner | 2026-08-18 |
| `gear.launcher-arming-distance` | Use 5 meters for launched grenades and 10 meters for rockets/missiles. | Project owner | 2026-08-18 |
| `gear.missile-sensor-range` | Preserve the unspecified range and expose no initial missile purchase. | Project owner | 2026-08-18 |
| `gear.arrow-rating-range` | Use Rating 1-10, limited to Rating 1-6 during creation. | Project owner | 2026-08-18 |
| `gear.super-squirt-ammunition` | Expose no gel-pack purchase without approved merchandise facts. | Project owner | 2026-08-18 |
| `special-attribute.values-model` | Entries are points-spent deltas above the metatype minimum. | Project owner | 2026-08-19 |
| `special-attribute.edge-range` | Absolute Edge must stay within the metatype racial range. | Project owner | 2026-08-19 |
| `special-attribute.full-allocation` | Metatype special-attribute points must be fully allocated; unused points are no longer silently lost. | Project owner | 2026-08-19 |
| `karma.creation-pool-semantics` | Spendable pool = 25 + negative; purchases draw from it; drop the `positive - negative > 25` check. | Project owner | 2026-08-19 |
