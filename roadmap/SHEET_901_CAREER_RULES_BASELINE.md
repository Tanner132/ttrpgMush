# SHEET-901: Career Rules And Catalog Contract

This document is the frozen rules and catalog-gap contract for Milestone 9
(`../MILESTONE_09_CAREER_CHARACTER_SHEETS.md`). It follows the same review
discipline as the Milestone 8 baseline (`SR5_RULESET_BASELINE.md`,
`SR5_RULE_DECISIONS.md`, and the `sr5-catalog/` ledgers): every rule below cites
an approved-PDF page, and every gap is named rather than invented. It is a
planning and verification contract, not a runtime catalog.

**Sequencing note:** the project's own delivery rule (`../ROADMAP.md`, "Release
Sequence" item 9) says CHAR-812 (the Milestone 8 completeness/accessibility
release gate) should land before this freeze. CHAR-812 has not been started.
The project owner explicitly approved starting Milestone 9 rules work ahead of
CHAR-812 on 2026-08-25; this is a recorded process exception, not a rules
decision, and does not change any catalog fact below. SHEET-902 onward should
not begin until this document and its new decision-register entries are
reviewed.

Approved sources are unchanged from Milestone 8: the pinned `sr5-core` and
`run-faster` PDFs listed in `SR5_RULESET_BASELINE.md`, same SHA-256 checksums.
PDF page numbers below are the physical PDF page index; printed page = PDF page
minus 2, consistent with the existing ledgers.

## 1. Canonical Career Advancement Cost Table

Source: `sr5-core` pp. 105-107 (PDF 107-109), "Character Improvement Table" and
"Training Rate Table". This is the same table already summarized in the
Milestone 9 file's Rules Contract and is restated here as the reviewed,
citation-backed original:

| Advancement | Karma cost | Source |
| --- | ---: | --- |
| Attribute (Physical, Mental, Edge, Magic, Resonance) | New Rating x 5 | p. 105 (PDF 107); table p. 106 (PDF 108) |
| Active skill | New Rating x 2 | p. 106 (PDF 108); table p. 106 (PDF 108) |
| Active skill group | New Rating x 5 | p. 106 (PDF 108); table p. 106 (PDF 108) |
| Knowledge or Language skill | New Rating x 1 | p. 106 (PDF 108); table p. 106 (PDF 108) |
| New Knowledge or Language skill (first rank) | 1 | p. 107 (PDF 109) |
| New specialization (Active, Knowledge, or Language) | 7 | p. 107 (PDF 109) |
| New Positive quality | Listed Karma cost x 2 | pp. 71, 106-107 (PDF 73, 108-109) |
| Remove a Negative quality | Listed bonus Karma x 2 | pp. 71, 106-107 (PDF 73, 108-109) |
| New complex form | 4 | pp. 106-107 (PDF 108-109) |
| New spell, ritual, or alchemical preparation | 5 | pp. 106-107 (PDF 108-109) |
| New initiate grade (magician/mystic adept/adept) | 10 + (Grade x 3) | p. 107 (PDF 109) |
| New Submersion grade (technomancer) | 10 + (Grade x 3) — approved reading of a garbled source line; see `career.submersion-cost-formula`, Section 8 | p. 257 (PDF 259) |

Training time (downtime, instructors, Training Rate Table) is retained here for
rules completeness but is explicitly ignored by the Fixed Product Contract; the
application does not schedule or delay any advancement.

The Karma Advancement Table for Attributes (p. 106/PDF 108) and for Skills
(p. 106/PDF 108) are cumulative-cost lookup tables that encode exactly the
marginal formulas above; the application must compute marginal cost from
current rating to new rating rather than embedding the printed tables verbatim
(this mirrors how `attribute.karma-overflow` and `skill.karma-overflow` already
compute creation-time Karma overflow with the same formulas).

### 1.1 Skill and attribute career ceilings

- Active, Knowledge, and Language skill ratings: the printed Karma Advancement
  Table for Skills runs through column 12, with column 13 marked "only
  available to characters with the correct quality." This is the same table
  already used for creation Karma overflow. The Aptitude quality's `Career`
  cap is already recorded as 13 in `sr5-catalog/QUALITIES.md`. **Career
  skill-rating ceiling: 12, or 13 for the one Aptitude-selected skill.**
  Source: p. 106 (PDF 108); `sr5-catalog/QUALITIES.md` `aptitude` row.
- Skill groups: no separate group ceiling is printed beyond each member skill's
  own ceiling; a group's rating cannot exceed the lowest member ceiling.
- Physical/Mental attributes: the printed Karma Advancement Table for
  Attributes runs through column 11, with column 11 marked "only available to
  characters with the correct quality" (Exceptional Attribute). Absolute
  ceiling is the metatype natural maximum from `sr5-catalog/PRIORITIES_METATYPES.md`,
  or that maximum + 1 with Exceptional Attribute (`quality.exceptional-attribute`,
  already cataloged). Physical/Mental attributes have **no** Initiation-based
  increase; only Magic and Resonance do. Source: pp. 66, 101, 106 (PDF 68, 103,
  108); `attribute.exceptional-maximum-count`.
- Edge: natural metatype maximum, or +1 with the Lucky quality
  (`sr5-catalog/QUALITIES.md` `lucky` row). No Initiation interaction.
- Magic / Resonance: natural maximum 6, or 6 + current initiate/Submersion
  grade (Section 4). Exceptional Attribute may also apply to Magic or
  Resonance under the existing catalog row, stacking with the initiate-grade
  bonus since they are different sources (metatype/quality maximum vs.
  initiation maximum). No published core interaction caps their sum below
  additive; treat them as additive per `special-attribute.edge-range`'s
  existing pattern of literal range enforcement. **Decision:**
  `career.magic-resonance-maximum-stacking` (see Section 8).
- The creation-only rule that "only one Physical or Mental attribute may sit at
  its natural maximum" (p. 101/PDF 103, restated for Karma spending on p. 98)
  is a character-creation allocation constraint tied to Step Five/Seven point
  budgets, not a standing rule about attribute values in general play. Once a
  character is finalized, career Karma spending is not re-validated against
  it: a second Physical/Mental attribute may reach its own natural maximum in
  career, because doing so no longer competes for a shared creation budget.
  **Decision:** `career.natural-maximum-count-is-creation-only` (Section 8).

## 2. Attribute And Special-Attribute Advancement (SHEET-906 rules)

- Cost formula, ceilings: Section 1/1.1.
- Edge is explicitly callable "anytime the character has the Karma to do so" —
  no eligibility gate beyond sufficient Karma and the ceiling above. Source:
  p. 105 (PDF 107).
- Magic and Resonance advancement additionally requires the character to have
  a nonzero value in that attribute already (i.e., an Awakened or Emergent
  path from creation); a mundane character cannot buy into Magic or Resonance
  in career play. No core rule grants Magic/Resonance to a mundane character
  post-creation. **Decision:** `career.no-post-creation-awakening` (Section 8;
  source-resolved by absence — no purchase mechanic exists).
- Essence loss reduces current and maximum Magic/Resonance exactly as at
  creation (`essence.magic-resonance-order` already covers the calculation;
  it applies unchanged to career recomputation since Essence loss itself is
  out of Milestone 9's scope — augmentation installation is deferred).
- Every derived value keyed to an advanced attribute (Initiative, Inherent
  Limits, Condition Monitor boxes, Living Persona, Essence-derived values)
  must be recomputed by the same formulas as `DerivedStatisticsEvaluator`
  (CHAR-811), cited at p. 101 (PDF 103) and already implemented for creation.
  SHEET-906 reuses those pure formulas; it must not reuse the evaluator's
  creation-only orchestration.

## 3. Skills, Groups, And Specializations (SHEET-907 rules)

- Cost formulas, ceilings: Section 1/1.1.
- Learning a brand-new skill in career costs the same cumulative Karma
  Advancement Table value as raising it from 0, i.e., `new Rating x 2`
  Karma total for its first rating (there is no separate "new skill" fee
  distinct from the marginal-cost formula, unlike Knowledge/Language skills
  which do have a distinct "new skill" line). Source: p. 106 (PDF 108),
  worked example ("purchasing the running skill for the first time... pay
  the cumulative amount").
- New Knowledge/Language skill: 1 Karma for the first rating specifically
  (a flat fee distinct from the `new Rating x 1` marginal table), then
  `new Rating x 1` marginally for further ranks. Source: p. 107 (PDF 109),
  Character Improvement Table row "New Knowledge/Language Skill: 1".
- Specializations: flat 7 Karma regardless of parent skill, one specialization
  per skill (same one-per-skill rule as creation, `sr5-catalog/SKILLS.md`).
  A Knowledge/Language specialization follows the same 7-Karma flat cost.
- Native-language specialization restriction (`knowledge.native-specialization`)
  continues to apply in career: a native (`N`) rating is not numeric and
  cannot carry a specialization.
- Skill-group behavior reuses the existing creation decision verbatim:
  `skill.group-break-and-rebuild` (raising or specializing one member breaks
  the group into independent skills at the former group rating; a broken
  group may be rebuilt once all members match, at the cost of raising the
  group to that rating using the group formula). Source: pp. 89, 129
  (PDF 91, 131). Career adds no new group mechanic; only the trigger changes
  from "Step Seven Karma" to "career Karma."
- Priority/creation skill grants (magician/technomancer skill grants, etc.)
  are already-resolved starting values; career advancement raises from
  whatever the composed baseline+progression value is, using
  `skill.priority-grant-collision`'s existing final-rating-across-all-sources
  rule.
- Aptitude interaction: `sr5-catalog/QUALITIES.md`'s `aptitude` row already
  states the career cap (13) for the Aptitude-selected skill; no other skill
  gets an elevated career ceiling.

## 4. Magic And Resonance Advancement (SHEET-909 rules)

### 4.1 Spells, rituals, preparations, complex forms

- Cost: 5 Karma per spell/ritual/preparation; 4 Karma per complex form.
  Source: p. 106-107 (PDF 108-109); `sr5-catalog/MAGIC_RESONANCE.md`
  "Formula Grants And Caps".
- Eligibility gates from creation carry forward unchanged in career: path/
  aspect eligibility (`magic.aspected-purchase-scope`), tradition requirement
  (`magic.tradition-by-path`), and the technomancer complex-form cap
  (`resonance.complex-form-cap`, `min(Logic, Resonance x 2)`, recomputed
  against current Logic/Resonance as they rise).
- **Resolved:** the three independent `Magic x 2` formula caps
  (`magic.formula-cap-scope`) and the technomancer `min(Logic, Resonance x 2)`
  complex-form cap are **creation-only**. Both are worded in the source as
  governing counts "known at Character Creation" (p. 98/PDF 100 table
  heading), and that is the correct reading: a career character may keep
  learning spells, rituals, preparations, and complex forms for Karma without
  a running-total ceiling, subject only to sufficient Karma and the ordinary
  path/aspect/tradition eligibility gates above. **Decision:**
  `career.formula-cap-creation-only` (Section 8; project-owner override of
  the original recommendation).

### 4.2 Adept Power Points

- Adepts: Power Points = current Magic (no separate career purchase
  mechanic beyond raising Magic itself, or the Power Point metamagic below).
  Source: p. 308 (PDF 310).
- Mystic adepts: no free Power Points ever; may buy whole Power Points for
  **5 Karma each** (published errata overriding `sr5-core`'s printed 2 Karma
  — see `mystic-adept.power-point-cost-errata` in `SR5_RULE_DECISIONS.md`),
  up to current Magic — but **only during character creation**. Post-creation,
  a mystic adept gains **no further Power Points through direct Karma
  purchase at any cost**; the sole additional-Power-Point source in career is
  the `power-point` Initiation metamagic (Section 4.3), the same mechanic
  pure adepts use, which may be taken repeatedly and unlimited times.
  **Decision:** `career.mystic-adept-power-point-purchase-creation-only`
  (Section 8; project-owner override of the original recommendation, and of
  this document's own earlier hypothesis of a career-continuing 2-Karma
  purchase). **Implementation note:** the creation-time evaluator
  (`KarmaBudgetEvaluator.MysticAdeptPowerPointKarmaCost`, currently
  hardcoded to `2`) predates this errata and needs a follow-up fix — see
  `mystic-adept.power-point-cost-errata`.
- Adept power ratings: maximum rank equals current Magic unless the power's
  own row states a lower cap (`sr5-catalog/MAGIC_RESONANCE.md` "Adept Powers"
  header note); this scales automatically as Magic rises in career.
- Improved Ability's own printed cap (`current natural skill Rating x 1.5,
  rounded up`) must be recomputed against the skill's current (post-career-
  advancement) rating, not its creation rating.

### 4.3 Initiation (magician, mystic adept, adept)

Source: pp. 324-326 (PDF 326-328).

- Cost: 10 + (Grade x 3) Karma per grade, starting at Grade 1.
- Eligibility: current Magic > 0 (any Awakened path). Training time (Arcana +
  Intuition [Astral] Extended Test) is ignored per the Fixed Product Contract.
- Effect: initiate grade cannot exceed current Magic. Raises the natural
  maximum for Magic to `6 + grade`; the character must still pay ordinary
  attribute Karma cost to actually raise Magic into that expanded range.
- Grade reduction: "if your Magic is reduced below your initiate grade, you
  lose an initiate grade right along with it." Essence-driven Magic
  reduction is out of Milestone 9's scope (augmentation installation is
  deferred), so this interaction has no trigger in this release and needs no
  implementation; document it for the future Essence-change ticket.
- Metaplanar Access (astral-plane travel on first initiation) has no
  corresponding gameplay system in this codebase and is **excluded** from
  this milestone as inert flavor text, consistent with how astral
  projection/Matrix action systems are already absent.
- Metamagic: every initiate grade (including the first) grants exactly one
  metamagic selection. A metamagic cannot be selected twice, **except** Power
  Point, which is explicitly repeatable. There are exactly 9 core metamagics:

  | ID | Display name | Eligible path | Repeatable | Effect summary | Source |
  | --- | --- | --- | --- | --- | --- |
  | `centering` | Centering | Magician/mystic adept only (uses Drain Resistance) | No | Add initiate-grade dice to Drain Resistance Tests via a Free Action mundane ritual gesture. | p. 324 (PDF 326) |
  | `adept-centering` | Adept Centering | Adept/mystic adept only (uses Physical/Combat skill modifiers) | No | Reduce negative dice-pool modifiers to Physical/Combat skills by initiate grade via a Free Action. | p. 324 (PDF 326) |
  | `fixation` | Fixation | Any initiate able to create preparations | No | Spend 1..Force Karma when creating an alchemical preparation to slow Potency decay to 1/day and add a Disjoining-resistance bonus equal to Karma spent. | pp. 324-325 (PDF 326-327) |
  | `flexible-signature` | Flexible Signature | Any initiate | No | Alter/mask/shorten own astral signature at will by up to initiate grade. | p. 325 (PDF 327) |
  | `masking` | Masking | Any initiate | No | Disguise aura Magic rating or type by up to initiate grade; opposed by Magic + grade. | p. 325 (PDF 327) |
  | `power-point` | Power Point | Adept/mystic adept only | Yes, unlimited | Gain 1 Power Point instead of a metamagic. | p. 325 (PDF 327) |
  | `quickening` | Quickening | Magician/mystic adept only (sustained spells) | No | Spend 1..Force Karma as a Complex Action to make a sustained spell permanent without further sustaining. | p. 325 (PDF 327) |
  | `spell-shaping` | Spell Shaping | Magician/mystic adept only (area spells) | No | Trade Spellcasting dice-pool penalty for reshaping an area spell's radius/exclusion. | p. 325 (PDF 327) |
  | `shielding` | Shielding | Magician/mystic adept only (spell defense) | No | Add initiate-grade dice to the spell-defense pool (Counterspelling only, not dispelling). | p. 325 (PDF 327) |

  This table is new catalog-fact material beyond the CHAR-801 creation
  ledgers (Initiation was explicitly out of creation scope,
  `sr5-catalog/MAGIC_RESONANCE.md` "Initiation, metamagics, Submersion,
  echoes" exclusion row) and must be added to the runtime catalog under
  SHEET-902/909.
- Metamagic-gated foci (`centering-focus`, `flexible-signature-focus`,
  `masking-focus`, `spell-shaping-focus`) become purchase-eligible once their
  required metamagic is learned; they remain otherwise out of scope because
  Milestone 9 defers all focus purchase/bonding (Section 6).
- Career focus-bonding cap is Magic x 5 (vs. the creation cap of Magic x 2,
  already cataloged in `sr5-catalog/MAGIC_RESONANCE.md` "Foci And Bonding
  Dependencies"). This fact is recorded for the future bonding ticket; it is
  not exercised by Milestone 9 since bonding itself is deferred.

### 4.4 Submersion (technomancer)

Source: p. 257 (PDF 259).

- Cost, mechanically identical in structure to Initiation: the printed text
  reads "10 x (Grade x 3) Karma," which multiplies out to an implausibly
  steep cost (e.g., Grade 1 = 30, Grade 2 = 60 by literal multiplication,
  vs. Initiation's Grade 1 = 13, Grade 2 = 16 by addition) and does not match
  the Character Improvement Table's own omission of a separate Submersion
  row. This reads as a PDF text-extraction artifact of the printed `+` glyph
  (the identical construction "10 + (Grade x 3)" appears two pages earlier
  for Initiation). **Approved:** use `10 + (Grade x 3)`, identical to
  Initiation — this is also the standard published SR5 rule (Submersion
  costs the same as Initiation) and the only reading consistent with the
  source not printing a distinct cost table for it. **Decision:**
  `career.submersion-cost-formula` (Section 8).
- Eligibility: current Resonance > 0 (technomancer path only).
- Effect: Submersion grade cannot exceed current Resonance; raises the
  natural maximum for Resonance to `6 + grade`. If Resonance is reduced below
  the Submersion grade, the grade reduces with no refund (no trigger in this
  milestone; Resonance reduction is out of scope).
- Every Submersion grade (including the first) grants exactly one echo. No
  echo may be taken twice **except** where its own row says otherwise
  (several here are explicitly repeatable). There are exactly 9 core echoes:

  | ID | Display name | Repeatable | Effect summary | Source |
  | --- | --- | --- | --- | --- |
  | `attack-upgrade` | Attack Upgrade | Yes, up to twice | Living Persona Attack +1 per selection. | p. 257 (PDF 259) |
  | `data-processing-upgrade` | Data Processing Upgrade | Yes, up to twice | Living Persona Data Processing +1 per selection. | p. 257 (PDF 259) |
  | `firewall-upgrade` | Firewall Upgrade | Yes, up to twice | Living Persona Firewall +1 per selection. | p. 257 (PDF 259) |
  | `sleaze-upgrade` | Sleaze Upgrade | Yes, up to twice | Living Persona Sleaze +1 per selection. | p. 258 (PDF 260) |
  | `mind-over-machine` | Mind over Machine | Yes, up to three total | Grants/increases an effective Rating-1 control rig by 1 per selection. | p. 257 (PDF 259) |
  | `neurofilter` | NeuroFilter | Yes, up to twice | +1 dice pool to resist biofeedback damage per selection. | p. 257 (PDF 259) |
  | `overclocking` | Overclocking | No | +1D6 while in hot-sim VR. | p. 257 (PDF 259) |
  | `resonance-link` | Resonance Link | No (each direction is a separate selection between two specific technomancers) | One-way empathic link to a chosen technomancer; mutual if both take it toward each other. | p. 257 (PDF 259) |
  | `resonance-program` | Resonance [Program] | Yes, once per distinct program mimicked | Copies the effect of one common/hacking program. | p. 258 (PDF 260) |

  This table is new catalog-fact material beyond the CHAR-801 creation
  ledgers (same exclusion row as Initiation above) and must be added under
  SHEET-902/909. Sprite powers (Camouflage, Cookie, Electron Storm, Gremlins,
  Hash, Stability, Watermark, etc., pp. 255-257/PDF 257-259) are a distinct
  list used only by compiled/registered sprites and are **not** echoes; they
  are excluded from this table and from Milestone 9 entirely (no player
  character mechanic purchases them).

## 5. Contacts And Reputation (explicitly resolved, not deferred to guesswork)

- **Contacts: no Karma cost or formula exists in the approved core rulebook
  for acquiring or improving a contact after creation.** "Player characters
  are allowed to purchase a certain amount of contacts during character
  creation... After that, future contacts cannot be bought — they have to be
  earned" through roleplay/GM-adjudicated actions. Source: p. 55 (PDF 57);
  creation-time contact purchase mechanics at p. 98 (PDF 100) are explicitly
  creation-only ("during character creation").
- **Project-owner decision (2026-08-25), overriding this document's original
  "reject" recommendation:** new-contact acquisition is **included** in
  Milestone 9, at **zero Karma cost**, matching the RAW absence of any
  purchase price. This extends the Fixed Product Contract's existing
  "advancement does not require Storyteller approval" interpretation
  (already applied to qualities and every other operation in this milestone)
  to contact acquisition specifically. A new career contact starts at the
  creation-minimum **Connection 1, Loyalty 1** — the only ratings RAW ever
  assigns without a purchase — since no formula exists to price a higher
  starting rating, or to raise an existing contact's Connection/Loyalty at
  all; **raising an existing contact's rating remains unresolved by RAW and
  stays excluded from this milestone.** Contact count remains unlimited,
  matching the creation rule's own "no limits on how many contacts." This
  default (Connection 1 / Loyalty 1, unlimited count) is a product-shape
  assumption where RAW is silent, not itself a RAW citation — flag for
  confirmation if a different starting rating or a cap is wanted.
  **Decision:** `career.contact-free-addition-pending-st-gate` (Section 8).
- **Recorded follow-up:** the project owner noted that, per the table's
  spirit, contacts are normally earned through GM/Storyteller-adjudicated
  play, not granted on demand. Zero-cost self-service addition is accepted
  for this milestone only because no Storyteller-approval gate exists yet.
  **When a future Storyteller-approval-gate feature is built, contact
  acquisition (and any future contact-rating-increase mechanic) must be added
  to its gated-action list.** This is now also recorded in the Milestone 9
  file's "Deferred Beyond Milestone 9" section.
- **Street Cred:** `floor(lifetime Karma earned / 10)`, plus GM-discretionary
  awards for noteworthy accomplishments (not player-purchasable). Source:
  p. 372 (PDF 374). This confirms the Target Architecture's requirement that
  lifetime Karma earned be tracked separately from spendable Karma
  (`CharacterCareerState.LifetimeKarmaEarned`); Street Cred itself is a
  read-only derived display value, not a mutation, and is **out of scope**
  for this milestone per "Deferred Beyond Milestone 9."
  Formula is recorded here so a future ticket can compute it without a new
  PDF pass.
- **Notoriety and Public Awareness:** both are exclusively GM-awarded for
  specific narrative triggers (p. 372/PDF 374) with no player-purchase path
  and no Karma cost; Notoriety may only ever be *reduced* by permanently
  sacrificing 2 Street Cred per 1 Notoriety point, itself a GM-adjudicated
  action, not a catalog purchase. **Rejected** for this milestone, matching
  "Deferred Beyond Milestone 9."

## 6. Nuyen Purchase Eligibility Ledger (SHEET-910 rules)

### 6.1 Rule

An item is purchase-eligible for SHEET-910's initial surface when **all** of
the following hold against the runtime catalog
(`backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0.json`):

1. It belongs to one of the eligible top-level catalog collections
   (Section 6.2).
2. It carries a deterministic `fixed`, `byRating`, or per-unit price with no
   missing numeric input (already-known gaps: `gear.missile-sensor-range`,
   `gear.super-squirt-ammunition`, `gear.focus-formula-cost-reference`).
3. It requires no installation, Essence change, Capacity allocation, mount
   occupancy, attachment, or bonding step to be "owned" — i.e., simple
   possession is the complete mechanical effect at purchase time. This is
   the Fixed Product Contract's own dividing line ("Ordinary directly priced
   catalog items are the first purchase surface").
4. Its rating/parameter domain, if any, is closed and server-resolvable (no
   open-ended cross-item price reference, e.g. Focus Formula's `Focus Cost x
   0.25`, which the generic evaluator cannot resolve without a cross-item
   reference it does not support, per `gear.focus-formula-cost-reference`).

Availability/legality never block eligibility (Fixed Product Contract); a
Restricted or Forbidden item still becomes purchasable once the above four
conditions hold, with Availability/legality surfaced as informational fields
only.

### 6.2 Collection-level ledger

| Catalog collection | Eligibility | Reason / citation |
| --- | --- | --- |
| `weapons` | **Eligible** | Ordinary priced gear; matches Fixed Product Contract's explicit "Weapons" surface. Already reconciled in `sr5-catalog/WEAPONS_ARMOR.md`. |
| `armor` | **Eligible** | Explicit surface item; base armor has fixed/byRating price with no required modification at purchase. |
| `gear` | **Eligible for entries meeting Section 6.1**; Capacity-range host devices, formula/focus items, and undefined-Sensor missiles are excluded per their existing decisions (`gear.capacity-host-purchase-deferral`, `gear.focus-formula-cost-reference`, `gear.missile-sensor-range`, `gear.super-squirt-ammunition`). | `sr5-catalog/ELECTRONICS_GEAR.md`; `SR5_RULE_DECISIONS.md` rows cited. |
| `vehicles` | **Eligible** | Explicit surface item ("Vehicles and drones"); base vehicle/drone price is fixed, no purchase-time modification required. `sr5-catalog/VEHICLES_RESOURCES.md`. |
| `cyberdecks` | **Eligible** | Explicit surface item; fixed price, no installation step (a cyberdeck is carried/used, not implanted). |
| `augmentations` | **Excluded from this milestone** | Fixed Product Contract explicitly defers "Augmentation acquisition and installation, including grade, Essence, and Magic/Resonance effects." Purchasing an augmentation is inseparable from its Essence/installation effect. |
| `augmentationGrades` | **Not directly purchasable** | Support/reference data for `augmentations`, not a standalone catalog item. |
| `weaponAccessories` | **Excluded** | Fixed Product Contract: "Weapon accessories and mount occupancy." |
| `armorModifications` | **Excluded** | Fixed Product Contract: "Armor modifications and Capacity." |
| `cyberlimbEnhancements` | **Excluded** | Requires an owned cyberlimb host and Capacity allocation; falls under the same deferred-attachment behavior as `augmentations`. |
| `vehicleModifications` | **Excluded** | Fixed Product Contract: "Device, cyberlimb, and vehicle attachments or modifications." |
| `foci` | **Excluded** | Fixed Product Contract: "Focus Force, purchase, and bonding." A focus's mechanical value requires bonding, which is explicitly deferred. |
| `lifestyleTiers` / `lifestyleOptions` | **Excluded** | Fixed Product Contract: "Lifestyle recurring charges." Lifestyle remains owner-visible read-only creation data in this milestone. |
| `spells` / `rituals` / `complexForms` / `adeptPowers` / `mentorSpirits` | **Not a nuyen purchase at all** | These are Karma-costed advancements (Sections 3-4), not catalog purchases; they never appear on the SHEET-910 purchase surface regardless of price fields. |
| `spiritTypes` / `spriteTypes` | **Not purchasable** | Support records for summoning/registering (gameplay-generated), never owned inventory. |

### 6.3 Ammunition, consumables, and quantity

Ammunition and other consumable gear rows that already carry a deterministic
per-unit price (e.g., standard ammunition types in `gear`) are eligible under
Section 6.1 as ordinary priced purchases with a `quantity` field; equipping,
loading, or consuming them is Fixed Product Contract's own separate deferral
("Ammunition consumption, equipped state") and is not implemented — the
purchase only adds an inert `CharacterInventoryItem` with the purchased
quantity.

### 6.4 Follow-up required before SHEET-910 implementation

This ledger defines the *rule* and resolves every *category*. SHEET-910 (or a
dedicated pre-implementation pass) must still run the rule mechanically
against every row in `gear`, `weapons`, `armor`, `vehicles`, and `cyberdecks`
to produce the exhaustive per-SKU eligible/excluded list the milestone's
acceptance criteria require ("the purchase ledger proves that every exposed
item has a deterministic price and supported parameters"). That pass is a
mechanical audit against the rule above, not a rules-interpretation task, and
does not block this document's approval.

## 7. Sheet Schema, Legacy, And Creation-Only Catalog Facts

- Supported evaluated `CharacterSheet` schema versions for the typed baseline
  reader (SHEET-902): versions 1, 2, and 3, per the Milestone 9 Target
  Architecture text and PROJECT_CONTEXT.md's existing note that finalization
  currently writes schema version 3. Versions 1-2 exist from earlier
  finalization code paths before CHAR-811's `DerivedStatisticsEvaluator`
  was added; SHEET-902 must confirm their exact historical shape from
  migration history rather than this document, which is a rules contract,
  not a schema archaeology exercise.
- Legacy sheet kind (pre-SR5 profile-only characters from Milestone 7,
  `roadmap/MILESTONE_07_CHARACTER_PROFILES.md`) has no derivable mechanical
  baseline. Confirmed **no automatic balance** per the Milestone 9 Balance
  Initialization section; SHEET-902/903 must reject mechanical reads for
  legacy sheets without inventing zero-value balances.
- **Creation-only qualities/options that must remain career-unavailable**
  (from `sr5-catalog/QUALITIES.md`, cross-checked against Section 5's
  contact finding and this document's own research):
  - `bilingual` — explicitly "creation only" in its own catalog row; grants a
    second native language, a concept with no career acquisition path.
  - Priority-based path selection itself (`mundane`/`magician`/`mystic-adept`/
    `adept`/`aspected-magician`/`technomancer`) is creation-only; Section 2
    already resolves that no core rule grants a mundane character a magical
    or Resonance path post-creation.
  - Metatype selection, Sum-to-Ten/Standard Priority allocation, and all
    Priority-table grants are inherently creation-only (one-time budget
    allocation, not a repeatable purchase).
  - All other cataloged positive qualities (30 of the 31 catalog rows) have
    no creation-only restriction in their source text and are therefore
    **career-purchasable at listed cost x 2** by default, per Section 1's
    table and `sr5-catalog/QUALITIES.md`'s own "Career acquisition" shared
    rule. SHEET-908 must still add per-quality career metadata (parameter
    re-validation against the current composed sheet, e.g. Aptitude's
    skill-id must reference a skill the character currently has), but no
    additional core-rule research is needed to know *which* qualities are
    eligible — all except `bilingual`.
  - Negative qualities are **never player-purchased** in career (Section 1);
    the only player-initiated negative-quality operation is removal at 2x
    listed award, gated on "stipulated requirements" the Fixed Product
    Contract already resolves as not requiring GM/Storyteller approval, but
    still requiring the quality's own objective mechanical requirements
    (e.g., Bad Rep's "confronting/resolving the reputation source" clause,
    already noted in `sr5-catalog/QUALITIES.md`) where the catalog encodes
    one. SHEET-908 must record which negative qualities have an
    objective (vs. purely narrative) removal requirement; qualities whose
    removal requirement is purely narrative (e.g., "GM has given the player
    permission") are removable on demand under this milestone's no-approval
    interpretation.

## 8. Decision-Register Entries

All seven candidate decisions were reviewed and resolved by the project owner
on 2026-08-25. Two were approved as originally recommended; three were
overridden with the corrected rule below; one (contacts) changed from
"reject" to "include, at zero cost, pending a future approval gate"; one
(natural-maximum) was approved with the same substance, restated for
precision. The corresponding rows in `SR5_RULE_DECISIONS.md`'s "Milestone 9
Career Decisions" table carry the same resolutions and are the entries later
tickets should cite.

| ID | Resolution | Source |
| --- | --- | --- |
| `career.submersion-cost-formula` | Approved as recommended. Use `10 + (Grade x 3)` Karma per Submersion grade, identical to Initiation; the source's "10 x (Grade x 3)" is a PDF extraction/typesetting defect, retained as provenance only. | p. 257 (PDF 259), cross-checked against Initiation's identical construction at p. 107/324 (PDF 109/326) |
| `career.formula-cap-creation-only` | **Overridden.** The `Magic x 2` spell/ritual/preparation caps and the `min(Logic, Resonance x 2)` complex-form cap apply only at character creation. In career, a character may learn additional spells, rituals, preparations, and complex forms for Karma with no running-total ceiling, subject only to sufficient Karma and ordinary path/aspect/tradition eligibility. | p. 98 (PDF 100); project-owner ruling, 2026-08-25 |
| `career.mystic-adept-power-point-purchase-creation-only` | **Overridden.** The Power-Point purchase (`mystic-adept.power-points`) is creation-only, and costs **5 Karma each** per `mystic-adept.power-point-cost-errata` (not the printed 2 Karma — see `SR5_RULE_DECISIONS.md`). A mystic adept gains no further Power Points through any direct Karma purchase after creation; the only career source of additional Power Points is taking the `power-point` Initiation metamagic (Section 4.3), same as pure adepts, repeatable without limit. | p. 71 (PDF 73) for the creation-only purchase, superseded by errata; p. 325 (PDF 327) for the `power-point` metamagic; project-owner ruling, 2026-08-25 |
| `career.magic-resonance-maximum-stacking` | Approved as recommended. Exceptional Attribute's Magic/Resonance +1 and Initiation/Submersion's `6 + grade` maximum are additive independent sources. | project-owner ruling, 2026-08-25 |
| `career.natural-maximum-count-is-creation-only` | Approved, restated for precision: the "only one Physical/Mental attribute at natural maximum" cross-attribute restriction does not carry into career. Each attribute's own natural maximum (or metatype max + 1 with a qualifying source such as Exceptional Attribute) still applies individually; only the *cross-attribute count* restriction drops after creation. | pp. 66, 98, 101 (PDF 68, 100, 103); project-owner ruling, 2026-08-25 |
| `career.no-post-creation-awakening` | Approved as recommended. No core rule grants Magic or Resonance to a character who finished creation with neither; reject any attempt to advance Magic/Resonance, learn spells/complex forms, initiate, or submerge for a Magic = 0, Resonance = 0 creation baseline. | project-owner ruling, 2026-08-25 (source-resolved by absence) |
| `career.contact-free-addition-pending-st-gate` | **Changed from Rejected to Included.** New-contact acquisition is included at zero Karma cost, extending this milestone's existing "no Storyteller approval required" interpretation to contacts. A new contact starts at Connection 1 / Loyalty 1 (product-shape default where RAW is silent, flagged for confirmation), with no count limit. Raising an existing contact's rating remains unresolved by RAW and stays excluded. Recorded as a future Storyteller-approval-gate candidate: when that feature exists, contact acquisition must be added to its gated-action list. | p. 55 (PDF 57); project-owner ruling, 2026-08-25 |

Affected work: `career.submersion-cost-formula`, `career.formula-cap-creation-only`,
and `career.mystic-adept-power-point-purchase-creation-only` gate SHEET-909.
`career.magic-resonance-maximum-stacking` and `career.natural-maximum-count-is-
creation-only` gate SHEET-906. `career.no-post-creation-awakening` gates both
SHEET-906 and SHEET-909. `career.contact-free-addition-pending-st-gate` gates
SHEET-904/SHEET-905 and should be reflected in the Milestone 9 API contract
when those tickets are implemented.

## 9. SHEET-901 Acceptance Criteria Checklist

- [x] Every career operation explicitly included, deferred, or rejected:
  attributes/Edge/Magic/Resonance (included, Section 2), skills/groups/
  specializations (included, Section 3), spells/rituals/preparations/complex
  forms (included, no career cap, Section 4.1), adept Power Points (included
  for pure adepts; mystic-adept purchase is creation-only, Section 4.2),
  Initiation (included, Section 4.3), Submersion (included, Section 4.4),
  positive qualities (included except `bilingual`, Section 7), negative
  quality removal (included, Section 1/7), contacts (**included**, new
  acquisition at zero Karma cost pending a future Storyteller-approval gate;
  rating increases on existing contacts remain unresolved/excluded,
  Section 5), reputation/Street Cred/Notoriety/Public Awareness
  (**deferred** per existing milestone text, formulas recorded for later
  use, Section 5), nuyen purchases (included per collection ledger,
  Section 6).
- [x] Every included cost and legality rule cites an approved PDF page:
  Sections 1-6 throughout.
- [x] Missing or ambiguous rules have approved product decisions before
  evaluator work: all seven candidate decisions in Section 8 were reviewed
  and resolved by the project owner on 2026-08-25 (three overridden from
  the original recommendation, one changed from rejected to included, three
  approved as recommended) — SHEET-902 is unblocked.
- [x] No creation cap, budget, or grant behavior is silently reused as a
  career rule: Section 1.1 explicitly distinguishes creation-only
  restrictions (one-attribute-at-maximum, priority budgets) from rules that
  do carry over (group break/rebuild, Aptitude ceiling, formula caps).
- [ ] Shared rule helpers have creation regression tests and contain no
  draft/finalization/career transaction orchestration: not applicable until
  SHEET-902+ extracts the shared formulas; tracked as implementation work,
  not a rules gap.
- [ ] The catalog loader rejects invalid career metadata and digest changes
  cover every new semantic fact: not applicable until SHEET-902 adds the
  metamagic/echo catalog tables from Sections 4.3/4.4.
- [x] The purchase ledger proves that every exposed item has a deterministic
  price and supported parameters: Section 6 defines the rule and resolves
  every collection; Section 6.4 records the remaining mechanical per-SKU
  audit as implementation follow-up, not an open rules question.
