# CHAR-812 Reconciliation Report: Weapons, Weapon Accessories, Ammunition, Explosives, Armor, Armor Modifications

Sources used:
- PDF: `Shadowrun 5th Edition Core Rulebook (...).pdf`, extracted via `pdftotext -layout` for PDF pages 424-440 (weapons/accessories/ammo/explosives/armor/armor mods), plus context pages PDF 96 (Availability-12 creation ceiling) and PDF 419 (gear ratings glossary / mount rules).
- Catalog: `backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0.json`, arrays `weapons` (77), `weaponAccessories` (17), `armor` (11), `armorModifications` (7), plus `gear` (141, searched for stray ammo/explosive/clothing entries).

## Summary

- PDF items reviewed: 74 core weapons (melee/projectile/firearms) + 3 already-approved gear-weapons (grapple gun, micro flare launcher, monofilament chainsaw) = 77 weapon-type rows; 18 firearm accessories; ~11 ammunition types + 4 projectile-ammo types (arrow/bolt/injection variants); 4 explosive-compound/accessory items; 7 grenade types; 3 rocket types (+ missile variants sharing stats); 16 clothing/armor line items + 2 shields + 1 helmet-accessory row; 7 armor modifications.
- Catalog items reviewed: 77 weapons, 17 weapon accessories, 11 armor, 7 armor modifications (all entries), plus a full-text search of `gear` (141 entries) for ammunition/explosive/clothing terms.
- Gaps found: 7 (2 single-item gaps, 5 whole-category gaps — see below; the 5 whole-category gaps affect ~25 named PDF items in aggregate).
- Unexplained entries found: 0 catalog entries with no PDF/exclusion trace. (One documentation mismatch was found — see "Unexplained catalog entries" — but it is a mismatch between the ledger's stated rationale and the data, not an untraceable entry.)

## Gaps

### 1. Weapon accessory: "Smartgun system, internal" — MISSING
PDF p. 431 (PDF 433), firearm accessories table. The table lists both "Smartgun system, internal" (mount: underbarrel/none, Avail (+2)R-style, cost far cheaper than the external variant) and "Smartgun system, external" (mount: top or underbarrel, Avail 4R, cost 200¥). The catalog's `weaponAccessories` array (17 entries) contains only `accessory-smartgun-external`; there is no `accessory-smartgun-internal` (or equivalent) entry, and no weapon entry carries a boolean/field indicating a built-in smartgun (checked `ares-predator-v` as a representative example — no smartgun field present at all, despite prose stating it "includes a smartgun system"). This is a genuine one-item gap: 17 of 18 named accessories are present.

### 2. Armor item: "Leather jacket/duster" / "(Synth)Leather" — MISSING
PDF pp. 436-437 (PDF 438-439). The clothing/armor table lists a "(Synth)Leather" row (Armor Rating 4, Avail --, Cost +200¥) and the accompanying prose ("Leather jacket/duster") describes it as a distinct, named product ("this type of jacket ... never goes out of style and even offers a modicum of protection"). This is a separate, low-tier body armor option from the "Armor jacket" (Armor 12) and "Armor clothing" (Armor 6) that are already catalogued. It is entirely absent from the catalog's 11-entry `armor` array and not found in `gear` either.

### 3. Ammunition types — ENTIRE CATEGORY MISSING
PDF p. 433 (PDF 435), "ammunition" table. Named types: APDS, Assault cannon rounds, Explosive rounds, Flechette rounds, Gel rounds, Hollow point rounds, Injection darts, Regular ammo, Stick-n-Shock, Tracer, Taser dart (11 types, each with its own damage/AP modifier, availability, and cost). None of these appear anywhere in the catalog: not in `weapons`, not in `gear`, and there is no `ammunition` concept in the schema at all — weapon entries carry only a static magazine-capacity string (e.g. `"ammo": "15 (c)"`) with no ammo-type field to select against. The core rulebook explicitly lists "Ammunition, p. 433" as an essential gear-purchase category in the Step 6 gear checklist (PDF p. 96), confirming this is intended to be a purchasable option set, not flavor text.

### 4. Projectile ammo (arrows/bolts) — MISSING
PDF pp. 423-424 (PDF 425-426). Arrow, Injection arrow (bow ammo) and Bolt, Injection bolt (crossbow ammo) are named, priced, rated items. `bow` has an `"ammo": "1 arrow"` descriptor field but no purchasable Arrow/Injection-arrow catalog rows; `crossbow-light/medium/heavy` have no ammo field at all and no corresponding Bolt/Injection-bolt rows exist anywhere in the catalog.

### 5. Explosives — ENTIRE CATEGORY MISSING
PDF p. 436 (PDF 438), "explosives" table. Commercial explosives (Rating 5, Avail 8R, 100¥/kg), Plastic explosives (Rating 6-25, Avail 16F, Rating x 100¥/kg), Explosive foam (Rating 6-25, Avail 12F, Rating x 100¥/kg), and the Detonator cap accessory (Avail 8R, 75¥) are completely absent from `weapons`, `gear`, and every other array.

### 6. Grenades — ENTIRE CATEGORY MISSING
PDF p. 435 (PDF 437), "grenades" table. Flash-bang, Flash-pak, Fragmentation, High explosive, Gas, Smoke, Thermal smoke (7 types, each with damage/AP/blast/avail/cost) are completely absent. This directly affects usability of catalogued launcher weapons: `ares-antioch-2` and `armtech-mgl-12` both have `"damage": "Grenade"` as a literal placeholder string with no actual grenade item to reference for real damage/blast values.

### 7. Rockets and Missiles — ENTIRE CATEGORY MISSING
PDF p. 435 (PDF 437), "rockets"/"missiles" tables. Anti-vehicle, Fragmentation, and High-explosive rocket types (missiles share the same stat lines per the book, at higher cost) are completely absent. Same downstream effect as grenades: `aztechnology-striker` and `onotari-interceptor` both have `"damage": "Missile"` with no catalogued missile/rocket item behind it.

**Note on gaps 3-7:** the project's own `roadmap/SR5_CATALOG_LEDGER.md` already flags "final ammunition/explosives pass pending CHAR-809" as open — this reconciliation confirms that work has not started: the categories are not partially modeled, they are entirely absent from the schema and data.

## Unexplained catalog entries

No catalog entries were found that cannot be traced to a PDF row or an approved exclusion — the 77/17/11/7 entries in `weapons`/`weaponAccessories`/`armor`/`armorModifications` all match named PDF items (74 core weapons + the 3 approved gear-weapons; 8 blades + 6 clubs + 3 other-melee + 5 projectile + 52 firearms = 74 checks out exactly against the 77-item array once the 3 gear-weapons are added).

One **documentation/data mismatch** worth flagging, not a stray entry:
- `roadmap/SR5_CATALOG_LEDGER.md` states ballistic/riot shields are "catalogued under `armor` (with weapon stats folded in)." The `ballistic-shield` and `riot-shield` armor entries are correctly present with armor stats (Armor 6, Avail 12R/1,200¥ and Avail 10R/1,500¥ respectively, matching PDF p. 438/PDF 440), but neither entry carries the melee-weapon stats the PDF also lists for them (Ballistic shield: Acc 4, dmg (STR+2)S; Riot shield: Acc 4, dmg 9S(e), AP -5, including its electrical-shock property) — there is no accuracy/reach/damage/ap field on either armor entry. The shields' "can also be used to bash/shock someone" capability described on PDF p. 438 is therefore not represented anywhere in the catalog, contrary to what the ledger claims. Low-impact (a niche interaction), but the ledger's description doesn't match the data as it stands.

## Spot-check results

Cost/availability/stat spot checks against legible PDF table rows (12 items across categories):

| Item | Field(s) checked | PDF value | Catalog value | Result |
|---|---|---|---|---|
| Combat Axe | Acc/Reach/DV/AP/Avail/Cost | 4/2/(STR+5)P/-4/12R/4,000¥ | matches | MATCH |
| Ares Predator V | Acc/DV/AP/Mode/Ammo/Avail/Cost | 5(7)/8P/-1/SA/15(c)/5R/725¥ | matches | MATCH |
| Fichetti Tiffani Needler | Acc/DV/AP/Mode/Ammo/Avail/Cost | 5/8P(f)/+5/SA/4(c)/5R/1,000¥ | matches | MATCH |
| Walther Palm Pistol | Acc/DV/Mode/Ammo/Avail/Cost | 4/7P/SS-BF/2(b)/4R/180¥ | matches | MATCH |
| AK-97 | Acc/DV/AP/Mode/Ammo/Avail/Cost | 5/10P/-2/SA-BF-FA/38(c)/4R/950¥ | matches | MATCH |
| Ares Alpha | Acc/DV/AP/Mode/RC/Ammo/Avail/Cost | 5(7)/11P/-2/SA-BF-FA/2/42(c)/11F/2,650¥ | matches | MATCH |
| Colt M23 | Acc/DV/AP/Mode/Ammo/Avail/Cost | 4/9P/-2/SA-BF-FA/40(c)/4R/550¥ | matches | MATCH |
| Ruger 101 | Acc/DV/AP/Mode/RC/Ammo/Avail/Cost | 6/11P/-3/SA/(1)/8(m)/4R/1,300¥ | matches (catalog correctly uses "Ruger 101" per prose, not the table header's "Ruger 100" — a known book typo) | MATCH |
| Actioneer Business Clothes | ArmorRating/Avail/Cost | 8/8/1,500¥ | matches | MATCH |
| Full Body Armor | ArmorRating/Avail/Cost/classification | 15/14R/2,000¥, avail>12 → creation-unavailable | matches, correctly flagged `creationUnavailable` | MATCH |
| Ballistic Shield / Riot Shield | ArmorRating/Avail/Cost | 6/12R/1,200¥ and 6/10R/1,500¥ | matches | MATCH |
| Helmet | ArmorRating/Avail/Cost | +2 / "--" (no restriction, i.e. legal) / +100¥ | ArmorRating 2 and Cost 100¥ match; Availability is stored as `{"fixed":2,"legality":"legal"}` rather than no availability value | MINOR MISMATCH |
| Silencer/Suppressor | Avail/Cost | table cell badly corrupted by pdftotext's column-merging around a sidebar; legible fragments suggest Avail in the 8F-12F range and a variable "(Weapon Cost) x 2" cost formula | `{"fixed":9,"legality":"forbidden"}`, flat `cost.fixed: 500` (not weapon-cost-scaled) | INCONCLUSIVE (OCR too garbled to confirm exact Avail; the flat-cost approach is consistent with this project's documented cost-modeling-simplification convention per `sr5_catalog_conventions` notes, but is worth a manual PDF-page check if precision matters) |

A programmatic sweep of every weapon/armor entry with a fixed Availability found no items above 12 that were left `selectable` and no items at/below 12 that were incorrectly marked `creationUnavailable` — the Availability-12 creation-ceiling rule (PDF p. 96) is applied consistently across all 77 weapons and 11 armor entries.

## Verdict

Weapons, weapon categories, and armor (body armor + shields) are essentially fully reconciled: all 74 core PDF weapon rows plus the 3 approved gear-weapons are present and accounted for (77/77), all 11 armor rows match the PDF's clothing/armor table, and all 7 armor modifications match the PDF's Armor Modifications table exactly. Spot-checked stats matched in every case except one trivial Availability transcription (Helmet) and one ambiguous, possibly-simplified Silencer cost/avail figure.

However, this category is **not** fully reconciled for Milestone 8 purposes, for two reasons:
1. Two small, unambiguous single-item gaps remain: the internal Smartgun System accessory and the Leather jacket/duster armor item.
2. The Ammunition and Explosives sub-areas — which the project's own ledger already flagged as open pending CHAR-809 — are confirmed completely unimplemented: no ammunition types, no arrow/bolt ammo, no explosives, no grenades, and no rockets/missiles exist anywhere in the catalog schema or data. This isn't a partial gap; it's an entire missing category spanning roughly 25 named PDF items, and it leaves two already-catalogued launcher weapons (Ares Antioch-2, ArmTech MGL-12 for grenades; Aztechnology Striker, Onotari Interceptor for missiles) referencing a "Grenade"/"Missile" damage type with nothing behind it. Whether this blocks Milestone 8 is a scope decision for the deciding session, but it should not be characterized as "reconciled" — it is unstarted work, consistent with the ledger's own note.

Additionally, one documentation/data mismatch should be corrected or reconciled: the ledger's claim that ballistic/riot shields have their melee weapon stats "folded in" to the armor entries is not borne out by the data — those fields are simply absent.
