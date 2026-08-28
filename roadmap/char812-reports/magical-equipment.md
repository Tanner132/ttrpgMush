# CHAR-812 Reconciliation — Magical Equipment (Foci, Spell Formulae, Lodges, Reagents)

Sources: SR5 Core Rulebook PDF (`Shadowrun 5th Edition Core Rulebook (Jennifer Brozek, Raymond Croteau etc.) (z-library.sk, 1lib.sk, z-lib.sk).pdf`), extracted with `pdftotext` (both `-layout` and plain reading-order — plain order proved more reliable for the two-column price tables and is what the figures below are drawn from). Catalog: `backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0.json` (the `1.2.0`/`1.3.0` files are qualities/metavariant overlays only — they do not touch `foci` or `gear`, so `1.0.0` is authoritative for this slice). Model definitions: `backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/RulesetCatalog.cs`.

The book states its focus/formula/lodge/reagent pricing twice, verbatim-identical: once in the Magic chapter's "magical goods" table (printed p.326, PDF p.328) and once in the Street Gear chapter's "magical equipment" reference table (printed p.461, PDF p.463). Both extractions agree, so the numbers below are high-confidence.

## Summary

- All **16 named foci** in the PDF (the specific types listed under the book's 7 focus categories: Enchanting, Metamagic, Power, Qi, Spell, Spirit, Weapon) are present in the catalog's `foci` array by name, with plausible page citations. No missing foci, no unexplained extra foci.
- The 2 `magical-supplies` gear entries (Magical Lodge Materials, Reagents) and 5 `formula` gear entries (Spell Formula × Combat/Detection/Health/Illusion/Manipulation) all match the book's pricing table **exactly** — Availability, cost, and Force/Rating scaling all check out.
- The already-documented gap (Focus Formulae absent, spell-formula-to-known-spell linkage missing) is confirmed accurate — see dedicated section below.
- **New finding, not previously documented**: the catalog's `foci` array carries no pricing data at all. `FocusDefinition` (RulesetCatalog.cs:216) has exactly 4 fields — `Id`, `DisplayName`, `CreationUnavailable`, `Source` — no cost, no availability, no Force-scaling. The book prices every focus category in nuyen-per-Force with an Availability rating (and separately, a Karma-per-Force bonding cost), but none of that exists anywhere in the JSON catalog or in the backend code (grepped the whole `backEnd/src` tree for focus-related cost logic — found none outside the catalog loader/DTO plumbing that just passes the 4 bare fields through). This is a real gap against the task's own checklist item 4 ("confirm Force-scaled costs are modeled correctly") — the honest answer is they aren't modeled at all, not even as a flat price.

## Gaps

### 1. Focus pricing/availability data — entirely absent from the catalog (NEW finding)

The book's focus pricing table (printed p.326 / PDF p.328, duplicated printed p.461 / PDF p.463) prices by **category**, not by individual named type:

| Category | Availability | Cost | Named catalog foci in this category |
|---|---|---|---|
| Enchanting Focus | (Force×3)R | Force×5,000¥ | Alchemical, Disenchanting |
| Metamagic Focus | (Force×3)R | Force×9,000¥ | Centering, Flexible Signature, Masking, Spell Shaping |
| Power Focus | (Force×4)R | Force×18,000¥ | Power |
| Qi Focus | (Force×3)R | Force×3,000¥ | Qi |
| Spell Focus | (Force×3)R | Force×4,000¥ | Counterspelling, Ritual Spellcasting, Spellcasting, Sustaining |
| Spirit Focus | (Force×3)R | Force×4,000¥ | Summoning, Banishing, Binding |
| Weapon Focus | (Force×4)R | Force×7,000¥ | Weapon |

None of these Availability/Cost values, nor the category groupings themselves, exist anywhere in `sr5-core-1.0.0.json` or in backend code. The `foci` array entries (`alchemical-focus`, `power-focus`, `weapon-focus`, etc. — all 16, printed pp.318-320 / PDF pp.320-322) have no `categoryId`, `cost`, or `availability` field of any kind, unlike every other purchasable item type in the catalog (`gear`, `weapons`, `armor` all carry `cost`/`availability`). Compare `GearDefinition`'s use of `cost.perRating` / `availability.perRating` (used correctly for Magical Lodge Materials and reagents, see below) — the same modeling pattern was simply never applied to foci.

Separately (not requested by the task checklist but adjacent and worth flagging together since it's the same root cause): the book's **Karma bonding-cost table** (printed p.318, PDF p.320) — Enchanting Focus Force×3, Metamagic Force×3, Power Force×6, Qi Force×2, Spell Force×2, Spirit Force×2, Weapon Force×3 — is likewise not modeled anywhere. This may be intentionally out of scope for the "catalog" layer if bonding is handled as a career/advancement action (per the Milestone 9 career-sheet advancement pattern) rather than a chargen purchase, but it's worth the fixing session confirming that's actually where it lives rather than assuming.

**Verdict on this item**: MISSING — PDF pp.318-320 & 326/461 (PDF 320-322, 328, 463) has full focus pricing; catalog has none of it.

### 2. Focus Formulae — confirmed absent (matches already-documented framing)

PDF printed p.326 / PDF p.328 (and duplicate p.461/463) lists a "Focus Formula" row: Availability "as Focus" (i.e., same Availability as the focus it's for), Cost "Focus Cost × 0.25". This is the recipe for building/binding a specific focus, distinct from a spell formula. It is **not present** in the catalog's `gear` array under `formula` or anywhere else — the 5 `formula`-category entries are all `spell-formula-*` (Combat/Detection/Health/Illusion/Manipulation), confirmed as genuine spell formulae, not focus formulae. This matches the pre-documented framing exactly; no correction needed.

## Unexplained catalog entries

None. Every one of the 16 `foci` entries, both `magical-supplies` gear entries, and all 5 `formula` gear entries trace cleanly to a named row in the PDF's magical-goods/magical-equipment tables. No orphaned catalog items found in this slice.

## Focus formulae / spell-formula-linkage status

Confirmed accurate as already documented:

- **Focus formulae** (the Force×0.25-of-focus-cost recipe for binding/crafting a specific focus, PDF printed p.326/461, PDF pp.328/463) are genuinely absent from the catalog. This is a distinct, correctly-identified gap — see Gap #2 above. It doesn't overlap with the newly-found pricing gap in Gap #1 (that's about the focus's own purchase price; this is about the formula-to-make-a-focus, which is a separate SKU the book prices but the catalog omits entirely).
- **Spell-formula-to-known-spell linkage** is confirmed still missing — the catalog's 5 `spell-formula-*` gear entries are flagged `"classification": "parameterized"` / `"requiresParameter": true` but there is no field or mechanism tying a purchased formula to a specific learned spell. Their basic presence, Availability, and Cost model is sound and matches the book exactly (see Spot-check results). No changes needed to the pricing itself — only the deferred linkage mechanic remains open, as already tracked.

## Spot-check results

**Foci (16/16 present, names verified against PDF pp.318-320/PDF 320-322):**

| Catalog id | Book source | Printed/PDF page in catalog | Verified against text |
|---|---|---|---|
| alchemical-focus | Alchemical focus (Enchanting) | 318/320 | ✓ matches |
| disenchanting-focus | Disenchanting focus (Enchanting) | 318/320 | ✓ matches |
| centering-focus | Centering focus (Metamagic) | 319/321 | ✓ matches |
| flexible-signature-focus | Flexible signature focus (Metamagic) | 319/321 | ✓ matches |
| masking-focus | Masking focus (Metamagic) | 319/321 | ✓ matches |
| spell-shaping-focus | Spell shaping focus (Metamagic) | 319/321 | ✓ matches |
| power-focus | Power Foci | 319/321 | ✓ matches |
| qi-focus | Qi Foci | 319/321 | ✓ matches |
| counterspelling-focus | Counterspelling (Spell) | 319/321 | ✓ matches |
| ritual-spellcasting-focus | Ritual Spellcasting (Spell) | 319/321 | ✓ matches |
| spellcasting-focus | Spellcasting (Spell) | 319/321 | ✓ matches |
| sustaining-focus | Sustaining (Spell) | 319/321 | ✓ matches |
| summoning-focus | Summoning (Spirit) | 320/322 | ✓ matches |
| banishing-focus | Banishing (Spirit) | 320/322 | ✓ matches |
| binding-focus | Binding (Spirit) | 320/322 | ✓ matches |
| weapon-focus | Weapon Foci | 320/322 | ✓ matches |

Cost-per-Force / Availability: **could not spot-check the values themselves because none exist in the catalog** — see Gap #1. Page citations on all 16 entries are accurate.

Note: `centering-focus`, `flexible-signature-focus`, `masking-focus`, and `spell-shaping-focus` (the 4 Metamagic-category foci) are flagged `"creationUnavailable": true` in the catalog; the other 12 are `false`. This is an app-level design choice (these foci are only useful to initiates, who typically start at grade 0 at chargen) rather than a PDF/catalog mismatch — flagging for awareness, not as a gap, since it wasn't asked about but affects what "present" means for these 4 in practice.

**Magical supplies (2/2 present, PDF printed p.280 for lodge narrative / p.316-317 for reagent narrative, pricing table p.326 & 461):**

| Catalog id | Book value | Catalog value | Match |
|---|---|---|---|
| magical-lodge-materials | Avail Force×2 (legal), Cost Force×500¥, lodge Force ranges 1-6 in practice | `availability.perRating: 2, legality: legal`; `cost.perRating: 500`; `ratingRange: 1-6` | ✓ exact match |
| reagents | Avail "--" (no restriction), Cost 20¥/dram, no Force scaling | `availability.legality: legal` (no fixed/perRating value); `cost.fixed: 20` | ✓ exact match |

**Spell formulae (5/5 present, PDF printed p.326/461, PDF pp.328/463):**

| Catalog id | Book Avail | Book Cost | Catalog Avail | Catalog Cost | Match |
|---|---|---|---|---|---|
| spell-formula-combat | 8R | 2,000¥ | `fixed: 8, restricted` | `fixed: 2000` | ✓ |
| spell-formula-detection | 4R | 500¥ | `fixed: 4, restricted` | `fixed: 500` | ✓ |
| spell-formula-health | 4R | 500¥ | `fixed: 4, restricted` | `fixed: 500` | ✓ |
| spell-formula-illusion | 8R | 1,000¥ | `fixed: 8, restricted` | `fixed: 1000` | ✓ |
| spell-formula-manipulation | 8R | 1,500¥ | `fixed: 8, restricted` | `fixed: 1500` | ✓ |

All 5 are flat prices (no Force scaling) in both the book and the catalog — correct, since a spell formula's price depends on spell category, not on the Force at which the spell is later cast.

## Verdict

**Not fully reconciled — one real gap, plus one confirmed pre-existing gap.**

- Spell formulae, magical lodge materials, and reagents are fully and correctly modeled; nothing to fix there.
- The already-known Focus Formulae gap and spell-formula-linkage gap are both confirmed accurate as documented — no correction needed to that framing.
- The catalog's 16 named foci are complete and correctly named/cited, **but they carry zero pricing or availability data**, which is inconsistent with every other purchasable item type in this catalog (`gear`, presumably `weapons`/`armor` too) and with the task's own assumption that foci "scale by Force rating" with "a fixed cost-per-Force multiplier." That assumption doesn't hold today because there's no multiplier recorded anywhere — this should be treated as an open item for CHAR-812 before sign-off, distinct from the two already-approved deferrals. Recommend adding a `categoryId` (Enchanting/Metamagic/Power/Qi/Spell/Spirit/Weapon) plus `cost.perRating` and `availability.perRating` to `FocusDefinition`/the `foci` JSON array, populated from the table in Gap #1 above.
