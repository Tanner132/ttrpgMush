# CHAR-812 Reconciliation — Augmentations (Cyberware & Bioware)

Scope: `augmentations` (91 entries), `augmentationGrades` (5 entries), `cyberlimbEnhancements` (3 entries) in
`backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0.json`, cross-checked
against SR5 Core Rulebook printed pp. 451–461 (PDF pp. 453–463).

## Summary

The catalog is in very good shape. All 91 `augmentations` entries trace cleanly to a book row, essence/cost/
availability/legality values were verified **exhaustively** (all 91 entries checked, not just a 15-item sample —
the book's clean per-page text extraction made full verification cheap) and matched the book precisely, and all
5 `augmentationGrades` multipliers are exact. No unexplained catalog entries were found.

Four PDF items have **no catalog entry at all** (Gaps §1–2), and two categories of PDF items have an **incomplete
field** on an otherwise-present entry (Gaps §3–4). Net: the catalog is missing 4 of the 95 named purchasable
options in this section (91/95 present), plus 6 present items are missing a Capacity-cost field they should have.

| Subcategory | Book items | Catalog items | Status |
|---|---|---|---|
| Headware | 15 (incl. 3 Cortex Bomb variants) | 15 | All present; **6 missing capacityCost** |
| Eyeware | 10 | 9 | **1 missing (Ocular drone)** |
| Earware | 7 | 7 | All present; **1 missing capacityCost** |
| Bodyware | 12 (incl. 3 Bone Lacing variants) | 12 | Full match |
| Cyberlimbs | 14 (7 obvious + 7 synthetic) | 14 | Full match; **Customization pricing rule unmodeled** |
| Cyber implant weapons | 14 (7 cyberguns + 3 accessories + 4 melee) | 11 | **3 missing (accessories)** |
| Basic bioware | 16 | 16 | Full match |
| Cultured bioware | 7 | 7 | Full match |
| **Total** | **95** | **91** | |

## Gaps

### 1. Eyeware — Ocular drone missing entirely
- **Ocular drone** (description p. 452, table row p. 454; PDF pp. 454/456): "A small spyball drone is placed in
  your ocular cavity... functions as a normal cybereye... until you remove it and control it as though it were a
  Horizon Flying Eye." Table stats: essence `--`, capacity `[6]`, availability `6`, cost `6,000¥`.
- Not present anywhere in the `eyeware` category (9 entries found, book has 10). This is a real, distinct
  purchasable item, not fluff — it should be added.

### 2. Cyber implant weapons — 3 cybergun accessories missing
Book section "Cyber Implant Weapons" (p. 458, PDF 460) lists, alongside the 7 cyberguns and 4 cyber-melee weapons
(all present in catalog as `implant-weapon` category), three cybergun-only accessories that are **absent**:
- **External clip port** — ess 0.1, capacity `[1]`, avail `--`, cost `+1,000¥`
- **Laser sight** (cyber/implant variant) — ess `--`, capacity `[1]`, avail `--`, cost `+1,000¥`
- **Silencer/suppressor** (cyber/implant variant) — ess `--`, capacity `[2]`, avail `--`, cost `+1,000¥`

Note: the catalog's top-level `weaponAccessories` array does contain `accessory-laser-sight` and
`accessory-silencer` (sourced to p. 431/PDF 433), but those are the **mundane, external weapon-accessory**
versions (cost 125¥/500¥, no capacity cost) — a different item from the implant/cybergun-specific versions on
p. 458 that cost Capacity and a flat `+1,000¥`. Neither the implant versions nor "External clip port" exist
anywhere in the catalog under any category.

### 3. Headware — 6 items missing their Capacity-cost field
Book rule (p. 451/453): "Items that have a Capacity Cost [in brackets] may be installed in cyberlimbs instead,
costing Capacity rather than Essence." The headware table (p. 453, PDF 455) shows bracketed Capacity values for
several items, but **every** headware entry in the catalog has `capacityCost: undefined` — including the ones the
book explicitly brackets:

| Item | Book capacity | Catalog `capacityCost` |
|---|---|---|
| Commlink | `[2]` | missing |
| Cortex Bomb, Kink | `[1]` | missing |
| Cortex Bomb, Microbomb | `[2]` | missing |
| Cortex Bomb, Area Bomb | `[3]` | missing |
| Cyberdeck | `[4]` | missing |
| Ultrasound Sensor | `[2]` | missing |

The other 9 headware items (Control Rig, Datajack, Data Lock, Olfactory Booster, Simrig, Skilljack, Taste
Booster, Tooth Compartment, Voice Modulator) correctly show `--` (no Capacity alternative) in the book, and
correctly have no `capacityCost` in the catalog — only the 6 above are actually wrong.

### 4. Earware — Spatial Recognizer missing its Capacity-cost field
Book table (p. 454, PDF 456): `Spatial Recognizer | 0.1 | [2] | 8 | 4,000¥`. Catalog's `spatial-recognizer-implanted`
has `essence.fixed: 0.1`, `availability.fixed: 8`, `cost.fixed: 4000`, but **no `capacityCost`** — it should be
`{"fixed": 2}`. (The other earware items with bracketed capacity — Audio Enhancement, Balance Augmenter, Damper,
Select Sound Filter — all have correct `capacityCost` values; Sound Link correctly has none since the book marks
it "Included in the basic cyberears system," i.e. free.)

### 5. Cyberlimbs — Customization pricing rule not represented (lower confidence — may be intentionally out of catalog scope)
Book (p. 456, PDF 458): "Customization... lets you add to your limb's base Strength and/or Agility ratings. Each
increase of either attribute increases the limb's Availability and cost." Table: cost `+5,000¥` and availability
`Cyberlimb + 1` per Strength or Agility point above the base 3. This is distinct from the `cyberlimbEnhancements`
mechanic (Agility/Armor/Strength Rating 1–3 add-ons, page 456, already confirmed correct and out of scope per
task brief) — it is the base limb's *inherent* Str/Agi stat being raised at time of purchase.

There is no field anywhere on the 14 `cyberlimb` catalog entries (no `customizationCostPerPoint` or similar), and
no separate catalog item for it, and no other reference to "customiz" anywhere in the JSON file. This may be
intentionally implemented as application logic outside the catalog (a formula, not data) rather than a genuine
gap — flagging for the fixing session to confirm one way or the other, since it's not clear from the catalog data
alone whether this purchasable option exists in the app at all.

## Unexplained catalog entries

None found. All 91 `augmentations` entries, all 5 `augmentationGrades`, and all 3 `cyberlimbEnhancements` trace
cleanly to a specific book row with a correct page citation.

## Grade multiplier check

Book table "`ware grades" (printed p. 451, PDF 453), re-extracted cleanly to rule out column-alignment artifacts
from the first-pass `-layout` extraction:

| Grade | Book Essence × | Book Avail modifier | Book Cost × | Catalog `essenceMultiplier` | Catalog `availabilityModifier` | Catalog `costMultiplier` | Match |
|---|---|---|---|---|---|---|---|
| Standard | 1.0 | — | 1 | 1 | 0 | 1 | Yes |
| Alphaware | 0.8 | +2 | 1.2 | 0.8 | 2 | 1.2 | Yes |
| Betaware | 0.7 | +4 | 1.5 | 0.7 | 4 | 1.5 | Yes |
| Deltaware | 0.5 | +8 | 2.5 | 0.5 | 8 | 2.5 | Yes |
| Used | 1.25 | −4 | 0.75 | 1.25 | −4 | 0.75 | Yes |

All five multiplier triples are exact. `creationEligible` flags (standard/alphaware/used = true, betaware/deltaware
= false) also match the book's "Only standard, alphaware, and used implants are available for purchase at
character creation" rule.

Minor citation-only note: `standard`/`alphaware`/`betaware`/`deltaware` cite `printedPage: 95` while `used` cites
`printedPage: 451`. Both are legitimate (the grade restriction is introduced in the priority/chargen rules around
p. 95, and the full multiplier table is on p. 451), but the inconsistency is worth a quick look — not a value bug.

## Spot-check results

Because the book's cyberware/bioware tables extract cleanly page-by-page without `-layout` (each row streams in
order), full verification of all 91 entries' essence/cost/availability/legality was performed rather than a
15-item sample. Representative results, one per subcategory:

- **Headware** — Control Rig: catalog `essence.byRating {1:1,2:2,3:3}`, `cost.byRating {1:43000,2:97000,3:208000}`,
  `availability.byRating {1:5,2:10,3:15}` restricted — matches book exactly (p. 453/PDF 455).
- **Eyeware** — Cybereyes basic system: catalog `essence.byRating {1:0.2,2:0.3,3:0.4,4:0.5}`,
  `cost.byRating {1:4000,2:6000,3:10000,4:14000}`, `capacity.perRating: 4` — matches book (p. 453/PDF 455). Note
  the catalog correctly distinguishes `capacity` (Capacity the item *provides*, for cybereyes/cyberears) from
  `capacityCost` (Capacity a modification *consumes* when installed in a limb) — good schema hygiene.
- **Earware** — Balance Augmenter: `essence.fixed 0.1`, `capacityCost.fixed 4`, `availability.fixed 8`,
  `cost.fixed 8000` — matches book exactly (p. 454/PDF 456).
- **Bodyware** — Wired Reflexes: `essence.byRating {1:2,2:3,3:5}`, `cost.byRating {1:39000,2:149000,3:217000}`,
  `availability.byRating {1:8,2:12,3:20}` restricted — matches book exactly (p. 455/PDF 457).
- **Cyberlimbs** — all 14 obvious/synthetic limb entries checked against the clean re-extraction of the cyberlimb
  table (p. 457/PDF 459); essence, capacity, availability, and cost match on every row (e.g. Obvious Full Leg:
  ess 1, cap 20, avail 4, cost 15,000¥; Synthetic Torso: ess 1.5, cap 5, avail 12, cost 25,000¥).
- **Cyber implant weapons** — Machine pistol: `essence 0.5`, `capacityCost [6]`, `availability 12` restricted,
  `cost 3,500¥` — matches book (p. 458/PDF 460).
- **Basic bioware** — Orthoskin: `essence.perRating 0.25`, `cost.perRating 6000`, `availability.perRating 4`
  restricted — matches book (p. 459/PDF 461). Muscle Toner, Symbiotes, Synthacardium, Tracheal Filter also
  checked and matched.
- **Cultured bioware** — Synaptic booster: `essence.perRating 0.5`, `cost.perRating 95000`,
  `availability.perRating 6` restricted — matches book (p. 460/PDF 462). Damage Compensators (rating range 1–12)
  and Pain Editor also checked and matched.
- Availability-based `creationEligible`/`creationUnavailable` classifications were spot-checked against the SR5
  chargen rule that items with Availability > 12 cannot be bought at character creation: Cortex Bomb
  Microbomb/Area Bomb (avail 16/20), Bone Lacing Titanium (avail 16), Suprathyroid Gland (avail 20), and Pain
  Editor (avail 18) are all correctly marked `creationUnavailable`, while Cortex Bomb Kink (avail exactly 12) is
  correctly left `selectable`. This is a nice consistency check, not a rules mechanic this report was asked to
  verify further.

## Verdict

**Not fully reconciled — 4 fixes and 6 field-completions recommended before CHAR-812 can close this slice:**

1. Add the missing **Ocular drone** eyeware item (p. 454).
2. Add the missing **External clip port**, **Laser sight** (implant variant), and **Silencer/suppressor** (implant
   variant) cybergun accessories (p. 458) — these are distinct from the existing mundane `weaponAccessories`
   entries of similar name and must not be conflated with them.
3. Add `capacityCost` to the 6 headware items that the book brackets: **Commlink** `[2]`, **Cortex Bomb Kink**
   `[1]`, **Cortex Bomb Microbomb** `[2]`, **Cortex Bomb Area Bomb** `[3]`, **Cyberdeck** `[4]`, **Ultrasound
   Sensor** `[2]` (p. 453).
4. Add `capacityCost: {"fixed": 2}` to **Spatial Recognizer** (earware, p. 454).
5. Investigate whether cyberlimb **Customization** (buying extra base Strength/Agility on a limb, at +5,000¥ and
   +1 Availability per point, p. 456) is implemented anywhere in the app; if not, it needs a design decision on
   how to represent it (likely a per-point cost formula on the cyberlimb items rather than a discrete catalog
   entry).

Everything else — all 91 present entries' essence/cost/availability/legality values, all 5 grade multipliers, and
all 3 cyberlimb enhancement types — traces cleanly to the book with no discrepancies and no unexplained entries.
