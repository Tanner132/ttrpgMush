# CHAR-812 Reconciliation: General Gear, Security/Survival/Medical Gear, and Drugs/BTL/Toxins

Sources: `Shadowrun 5th Edition Core Rulebook (...) .pdf`, printed pp. 442-451 (PDF pp. 444-453, actual tables found on PDF pp. 445, 449-453) and printed pp. 408-414 (PDF pp. 410-416). Runtime catalog: `backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0.json`.

## Summary

- The ten general-gear categories in scope (`survival`, `breaking-and-entering`, `credstick`, `tools`, `security-device`, `restraint`, `industrial-chemical`, `biotech`, `docwagon-contract`, `slap-patch`) plus the related `grapple-gun-gear` category contain **59 gear entries**, all of which trace cleanly to specific PDF table rows. No unexplained catalog entries were found.
- Cost/Availability spot-checks (15 items, see below) all matched the PDF exactly, including per-Rating multiplier patterns.
- Three dual-purpose items (**Monofilament Chainsaw**, **Micro Flare Launcher**, **Grapple Gun**) are correctly modeled in the catalog's `weapons` array instead of `gear`, since the PDF gives them full weapon stat blocks (accuracy/damage/AP) in addition to gear cost/availability. This is not a gap.
- **One small gap**: the security-device table's **Biometric Reader** line item has no catalog counterpart.
- **One major, previously-unflagged gap**: the runtime catalog contains **zero representation of drugs, BTL chips/downloads, or toxins** (printed pp. 408-414). This content is confirmed absent by a full-file keyword search, not just a missing categoryId in the ten target categories. The book's own worked chargen example shows a sample character buying a drug (Bliss) with starting nuyen using the standard gear-purchase mechanic, so this is a real chargen-purchase gap, not merely GM-facing flavor text that can be waived.

## Gaps

1. **Biometric Reader** (security device add-on) — printed p. 447 (PDF p. 449). The Security Devices table lists five line items: Key/Combination Lock, Maglock, Keypad or Card Reader, Anti-Tamper Circuits, and Biometric Reader (Availability `+4`, Cost `+200¥`). The catalog's `security-device` category (`key-combination-lock`, `maglock`, `keypad-card-reader`, `anti-tamper-circuits`) is missing this fifth entry. It reads as a maglock/biometric-lock add-on structurally identical in the table to "Keypad or Card Reader" (also a "+" modifier cost), which *is* present in the catalog as its own selectable entry — so Biometric Reader should get the same treatment for consistency.

2. **Drugs, BTL chips/downloads, and toxins — entirely absent from the runtime catalog** (printed pp. 408-414, PDF pp. 410-416). See the dedicated section below for the full breakdown of what's missing and why this is a genuine chargen-purchase gap rather than an approved exclusion.

## Unexplained catalog entries

None. All 59 gear entries across `survival`, `breaking-and-entering`, `credstick`, `tools`, `security-device`, `restraint`, `industrial-chemical`, `biotech`, `docwagon-contract`, `slap-patch`, and `grapple-gun-gear` were traced to a specific PDF row with matching (or at minimum plausible, unverified-in-detail) cost/availability. Every category's item count matches the PDF table row count for that category exactly, once the three dual-purpose weapon/gear items (Monofilament Chainsaw, Micro Flare Launcher, Grapple Gun) are accounted for in `weapons` rather than `gear`.

## Drugs/BTL/toxins findings

**Definitive determination: the runtime catalog has no representation of this content whatsoever.**

Verification method: dumped the entire catalog JSON to a string and searched (case-insensitive) for every named drug, BTL format, and sample toxin, plus the generic terms "drug", "toxin", and "btl". Results:

- No `categoryId` of `drug`, `toxin`, `btl`, or similar exists anywhere in `gear` (full category list: `survival`, `breaking-and-entering`, `commlink`, `electronics-accessory`, `rfid-tag`, `communications`, `software`, `skillsoft`, `credstick`, `tools`, `optical-imaging`, `audio-device`, `sensor-device`, `security-device`, `restraint`, `industrial-chemical`, `grapple-gun-gear`, `biotech`, `docwagon-contract`, `slap-patch`, `magical-supplies`, `formula`, `identity`).
- None of the 24 named substances/items below appear anywhere in the file.
- The only raw string hits were false positives: `"cram"` matched inside `"cramped"` (a lifestyle quality), `"toxin"` matched inside an elf metatype's `"Pathogen and toxin resistance"` trait text, and `"drug"` matched inside a dart rifle weapon's `"damage": "as Drug/Toxin"` field (a damage-type reference, not an item).

**What would need to be added**, with page citations:

- **10 Drugs** (Drug costs table, printed p. 410 / PDF p. 412, full effect/duration/addiction-type descriptions on printed pp. 410-412 / PDF pp. 412-414): Bliss, Cram, Deepweed, Jazz, Kamikaze, Long Haul, Nitro, Novacoke, Psyche, Zen. Each has Availability and Cost-per-dose plus a Speed/Duration/Addiction Type/Effect block that the current `gear` schema doesn't carry for any other item (closest analog is Slap Patches, which only need cost/availability/rating, not a mechanical effect block).
- **9 Sample Toxins** (Toxin costs table, printed p. 409 / PDF p. 411, descriptions on printed pp. 409-410 / PDF pp. 411-412): CS/Tear Gas, Gamma-Scopolamine, Narcoject, Nausea Gas, Neuro-Stun VIII, Neuro-Stun IX, Neuro-Stun X, Pepper Punch, Seven-7. Each has Vector/Speed/Penetration/Power/Effect stats plus Availability/Cost-per-dose. The book explicitly frames this table as "just a few of the chemical weapons that shadowrunners might encounter" (printed p. 409) — i.e., an illustrative sample, not an exhaustive purchasable catalog, but it is still costed and priced exactly like every other gear table in the book.
- **5 BTL chip/download formats** (BTL costs table, printed p. 412 / PDF p. 414, descriptions on printed pp. 412-413 / PDF pp. 414-415): Chip/download (generic one-time download), Dreamchip, Moodchip, Personafix (chip), Tripchip. All share common Speed/Duration/Addiction Type stats given once (printed p. 412).
- **Addiction Table** (printed p. 414 / PDF p. 416) supplies Addiction Rating/Threshold for the drugs above plus BTL Dreamchip, BTL Moodchip, and BTL Tripchip specifically (not Chip/download or Personafix by name), and also lists Alcohol, Soykaf, and generic "Simsense" — the latter three are not sold via an Availability/Cost gear table in this section and likely belong to lifestyle/quality mechanics rather than the `gear` catalog; flagging for awareness but not counting them as missing gear items.

**Purchasability determination**: This content **is** framed as chargen-purchasable, not GM-only/in-play content. Two lines of evidence:
1. The Drug costs, Toxin costs, and BTL costs tables use the exact same Availability + Cost-per-dose table format as every other Street Gear table in this ledger's scope (credsticks, slap patches, tools, etc.), with no "GM only" or "not available for purchase" caveat in the surrounding rules text.
2. The book's own worked character-creation example (printed p. 97, "Kyra" sample street-level character) has the player spending starting nuyen on **"Bliss (5 doses) — 75¥"** in the same gear-purchase list as her Autopicker, credstick, and licenses, funded from the same Priority-based resource pool used for all other starting gear. This is direct proof drugs are intended to be buyable at character creation via the standard gear-purchase mechanic.

Toxins are introduced with framing that they are "used primarily as weapons against the characters" (printed p. 408), suggesting more common use as an NPC/GM tool, but nothing in the rules text or table format restricts player purchase — the Availability/Cost columns work identically to every other restricted/forbidden gear item elsewhere in this chapter that PCs are expected to buy (e.g., Thermite Burning Bar, Certified Credstick Ebony). No sample character in the book is shown buying a toxin or BTL directly, but the mechanic (Availability/Cost-per-dose, same as drugs) is identical, and there's no textual carve-out excluding them from chargen purchase.

**Bottom line for CHAR-812**: this is a real, previously-unflagged gap. The existing ledger apparently cites source pages 408-414 without ever adding corresponding catalog entries. Closing it will also require extending the `gear` item shape (or adding a parallel mechanism) to carry Speed/Duration/Addiction/Effect data that no other gear category in the catalog currently models — this is more than a simple "add rows to an existing category" fix.

## Spot-check results

| Item | PDF value (avail / cost) | Catalog value | Result |
|---|---|---|---|
| Certified Credstick, Standard | -- / 5¥ | legal / fixed 5 | MATCH |
| Certified Credstick, Ebony | 20 / 1,000¥ | fixed 20 legal / fixed 1000 | MATCH |
| Tool Shop | 8 / 5,000¥ | fixed 8 legal / fixed 5000 | MATCH |
| Autopicker (Rating 1-6) | 8R / Rating x 500¥ | fixed 8 restricted / perRating 500, range 1-6 | MATCH |
| Maglock Passkey (Rating 1-4) | (Rating x 3)F / Rating x 2,000¥ | perRating 3 forbidden / perRating 2000, range 1-4 | MATCH |
| Cellular Glove Molder (Rating 1-4) | 12F / Rating x 500¥ | fixed 12 forbidden / perRating 500, range 1-4 | MATCH |
| Chemsuit (Rating 1-6) | Rating x 2 / Rating x 150¥ | perRating 2 / perRating 150, range 1-6 | MATCH |
| Respirator (Rating 1-6) | -- / Rating x 50¥ | legal (no perRating avail) / perRating 50, range 1-6 | MATCH |
| Thermite Burning Bar | 16R / 500¥ | fixed 16 restricted / fixed 500 | MATCH |
| Restraint, Plasteel | 6R / 50¥ | fixed 6 restricted / fixed 50 | MATCH |
| DocWagon Contract, Platinum | -- / 50,000¥ per year | legal / fixed 50000 | MATCH |
| Antidote Patch (Rating 1-6) | Rating / Rating x 50¥ | perRating 1 / perRating 50, range 1-6 | MATCH |
| Tranq Patch (Rating 1-10) | Rating x 2 / Rating x 20¥ | perRating 2 / perRating 20, range 1-10 | MATCH |
| Catalyst Stick | 8F / 120¥ | fixed 8 forbidden / fixed 120 | MATCH |
| Biomonitor | 3 / 300¥ | fixed 3 legal / fixed 300 | MATCH |

15/15 spot-checked items matched exactly, including correct per-Rating multiplier modeling and legality codes (R=restricted, F=forbidden, plain number/-- = legal).

## Verdict

The general gear slice (security devices, survival gear, medical/biotech, B&E tools, credsticks, tools, industrial chemicals, restraints, DocWagon contracts, slap patches) is in very good shape: 59/59 items trace to the PDF, 15/15 spot-checks matched exactly, and no unexplained entries exist. It needs one small fix (add Biometric Reader to `security-device`, printed p. 447/PDF p. 449).

The drugs/BTL/toxins portion of this ticket's scope is **not** in good shape — it is a confirmed, complete gap with no catalog representation at all, and the book's own chargen example demonstrates this content is meant to be purchasable at character creation like any other gear. CHAR-812 should not be signed off for this slice until a decision is made on how to model drugs/toxins/BTLs (new categoryIds, ~24 named items, and likely a schema extension to carry effect/duration/addiction data that the current `gear` shape doesn't support for any existing category).
