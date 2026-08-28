# CHAR-812 Reconciliation: Vehicles, Drones, Vehicle Modifications, Weapon Mounts

Scope: `vehicles` (40 entries) and `vehicleModifications` (4 entries) arrays in
`backEnd\src\SeattleByNight.Application\CharacterCreation\Catalog\Resources\sr5-core-1.0.0.json`,
cross-checked against the SR5 core rulebook, printed pp. 461-466 (PDF pages 463-468).

Extraction method note: `pdftotext -layout` mangles these particular tables badly (columns
bleed across the two-column layout, several rows silently disappear). All stats below were
re-extracted with `pdfplumber`'s word-position data (grouping words by `top` coordinate and
sorting by `x0`), which reproduced every row cleanly. Anyone re-verifying this later should not
trust a plain `pdftotext -layout` dump of these pages at face value.

## Summary

- All **40** book vehicles/drones are present in the catalog, correctly named, and correctly
  statted. This was **not** a 10-vehicle spot check — every one of the 40 rows was reconstructed
  from the PDF and diffed against the catalog field-by-field (Handling/Speed/Accel/Body/Armor/
  Pilot/Sensor/Seats/Availability/Cost). Zero stat mismatches found.
- The category breakdown the project's own tracking predicted is **exactly correct**: 4 bikes,
  7 cars, 4 trucks/vans, 3 boats, 2 submarines, 9 aircraft (3 fixed-wing + 3 rotorcraft + 3
  VTOL/VSTOL, all filed under one `aircraft` category id), 11 drones (2 micro + 2 mini + 2 small
  + 3 medium + 2 large). 15 (bikes+cars+trucks/vans) + 5 (boats+subs) + 9 (aircraft) + 11
  (drones) = 40.
- The 4 `vehicleModifications` entries (rigger interface, standard mount, heavy mount, manual
  operation) are the complete and only set of purchasable vehicle modifications the core
  rulebook lists in this section (printed p. 461 / PDF 463) — nothing is missing there.
- No unexplained (untraceable) catalog entries were found among vehicles or vehicle
  modifications.
- Two **source-citation bugs** were found (wrong printed/PDF page recorded, though the stat
  data itself is correct) — see Gaps section. One **data-modeling concern** was found on
  `manual-operation`'s Availability encoding — see Vehicle modifications check section.

## Gaps

None. Every purchasable vehicle/drone the book presents as a named, buyable option is present
in the catalog under all four book category groupings:

- **Bikes** (printed p. 461/463 prose, stat table printed p. 463/PDF 465): Dodge Scoot, Harley-Davidson Scorpion, Yamaha Growler, Suzuki Mirage — all 4 present.
- **Cars** (printed p. 462/PDF 464 prose, stat table PDF 465): Chrysler-Nissan Jackrabbit, Honda Spirit, Hyundai Shin-Hyung, Eurocar Westwind 3000, Ford Americar, Saeder-Krupp-Bentley Concordat, Mitsubishi Nightsky — all 7 present.
- **Trucks and Vans** (printed p. 463/PDF 465 prose+table): Toyota Gopher, GMC Bulldog Step-Van, Rover Model 2072, Ares Roadmaster — all 4 present.
- **Boats** (printed p. 463-464/PDF 465-466 prose, stat table PDF 467): Samuvani-Criscraft Otter, Yongkang-Gala Trinity, Morgan Cutlass — all 3 present.
- **Submarines** (PDF 466 prose, table PDF 467): Proteus Lamprey, Vulkan Electronaut — both present.
- **Fixed-Wing Aircraft** (PDF 466 prose, table PDF 467): Artemis Industries Nightwing, Cessna C750, Renault-Fiat Fokker Tundra-9 — all 3 present.
- **Rotorcraft** (PDF 466-467 prose, table PDF 467): Ares Dragon, Nissan Hound, Northrup Wasp — all 3 present.
- **VTOL/VSTOL** (PDF 467 prose, table PDF 467): Ares Venture, GMC Banshee, Federated Boeing Commuter — all 3 present.
- **Microdrones** (PDF 467 prose, table PDF 468): Shiawase Kanmushi, Sikorsky-Bell Microskimmer — both present.
- **Minidrones** (PDF 467-468 prose, table PDF 468): Horizon Flying Eye, MCT Fly-Spy — both present.
- **Small Drones** (PDF 468): Aztechnology Crawler, Lockheed Optic-X2 — both present.
- **Medium Drones** (PDF 468): Ares Duelist, GM-Nissan Doberman, MCT-Nissan Roto-Drone — all 3 present.
- **Large Drones** (PDF 468): Cyberspace Designs Dalmatian, Steel Lynx Combat Drone — both present.

The "Similar Models" sidebar (printed p. 462/PDF 464) that lists cosmetic reskins (e.g. Hyundai
Hopper as a Dodge Scoot reskin, BMW Blitzen as a Scorpion reskin, etc.) is correctly *not*
represented as separate catalog rows — the book is explicit these are branding-only clones with
no mechanical difference, GM discretion only. This is a correct, deliberate exclusion, not a gap.

## Unexplained catalog entries

None. All 40 `vehicles` entries and all 4 `vehicleModifications` entries trace cleanly to a
named row in the book's tables.

## Vehicle modifications check

Book table, "VEHICLE MODIFICATIONS" (printed p. 461 / PDF page 463), transcribed in full via
word-position extraction:

| Modification | Avail | Cost |
|---|---|---|
| Rigger interface | 4 | 1,000¥ |
| Standard weapon mount | 8F | 2,500¥ |
| Heavy weapon mount | 14F | 5,000¥ |
| Manual operation | +1 | +500¥ |

This is the **entire** table — there is no continuation, no additional row, and no second
"vehicle modifications" list anywhere else in the extracted page range (PDF 463-468). The core
rulebook does not have a broader vehicle-customization catalog (armor upgrades, ECM, sensor
packages, etc.) the way the *Rigger 5.0* splatbook does — weapon mounts, the rigger interface,
and manual operation are the complete purchasable vehicle-modification list in core. **The
project's ledger of 4 entries is correct and complete; this is not an oversight.**

Cross-check against `vehicleModifications` in the catalog:

- `rigger-interface`: Avail 4 (legal), Cost 1,000¥ — matches book exactly.
- `standard-weapon-mount`: Avail 8, legality `forbidden`, Cost 2,500¥ — matches book's "8F" (F = Forbidden) exactly.
- `heavy-weapon-mount`: Avail 14, legality `forbidden`, Cost 5,000¥ — matches book's "14F" exactly.
- `manual-operation`: Cost 500¥ matches book's "+500¥" exactly. **Availability does not match as encoded.** The book lists Manual Operation's Availability as **"+1"** — a *modifier* added to whatever weapon mount it's attached to (no restriction letter of its own, so it inherits/adds to the base mount's rating). The catalog instead encodes it as an absolute `availability.fixed: 9` with `legality: "forbidden"`. This reads as "Standard weapon mount's Avail 8 + 1 = 9, Forbidden" — i.e. someone appears to have pre-baked the modifier onto the Standard mount's baseline rather than modeling it as a true relative "+1" adjustment. That baked-in assumption breaks if Manual Operation is ever added to a Heavy weapon mount (Avail 14F), where the correct combined Availability would be 15F, not 9. Recommend flagging this to whichever session owns the availability-calculation logic for vehicle mods: either (a) `manual-operation` needs a proper relative/additive availability representation, or (b) if the catalog schema has no way to express a modifier-style availability and 9 was a deliberate simplification for a specific known case, that should be documented rather than silently baked in.

## Spot-check results

Per the task instructions this category normally calls for ~10 spot-checked vehicles, but
because the PDF tables needed a from-scratch extraction anyway (pdftotext's `-layout` output
was too corrupted to trust piecemeal), all 40 rows were reconstructed and compared, not just 10.
Full results:

| Vehicle | Category | Book stats (H/S/A/Bod/Armor/Pilot/Sensor/Seats/Avail/Cost) | Catalog match? |
|---|---|---|---|
| Dodge Scoot | bike | 4/3, 3, 1, 4, 4, 1, 1, 1, –, 3,000¥ | Yes |
| Harley-Davidson Scorpion | bike | 4/3, 4, 2, 8, 9, 1, 2, 1, –, 12,000¥ | Yes |
| Yamaha Growler | bike | 4/5, 3/4, 1, 5, 5, 1, 1, 1, –, 5,000¥ | Yes |
| Suzuki Mirage | bike | 5/3, 6, 3, 5, 6, 1, 2, 1, –, 8,500¥ | Yes |
| Chrysler-Nissan Jackrabbit | car | 4/3, 3, 2, 8, 4, 1, 2, 2, –, 10,000¥ | Yes |
| Honda Spirit | car | 3/2, 4, 2, 8, 6, 1, 2, 2, –, 12,000¥ | Yes |
| Hyundai Shin-Hyung | car | 5/4, 6, 3, 10, 6, 1, 2, 4, –, 28,500¥ | Yes |
| Eurocar Westwind 3000 | car | 6/4, 7, 3, 10, 8, 3, 5, 2, 13, 110,000¥ | Yes |
| Ford Americar | car | 4/3, 3, 2, 11, 6, 1, 2, 4, –, 16,000¥ | Yes |
| Saeder-Krupp-Bentley Concordat | car | 5/4, 5, 2, 12, 12, 2, 4, 4, 10, 65,000¥ | Yes |
| Mitsubishi Nightsky | car | 4/3, 4, 2, 15, 15, 3, 5, 8, 16, 320,000¥ | Yes |
| Toyota Gopher | truck-van | 5/5, 4, 2, 14, 10, 1, 2, 3, –, 25,000¥ | Yes |
| GMC Bulldog Step-Van | truck-van | 3/3, 3, 1, 16, 12, 1, 2, 6, –, 35,000¥ | Yes |
| Rover Model 2072 | truck-van | 5/5, 4, 2, 15, 12, 2, 4, 6, 10, 68,000¥ | Yes |
| Ares Roadmaster | truck-van | 3/3, 3, 1, 18, 18, 3, 3, 8, 8, 52,000¥ | Yes |
| Samuvani-Criscraft Otter | boat | 4, 3, 2, 12, 6, 2, 2, 8, –, 21,000¥ | Yes |
| Yongkang-Gala Trinity | boat | 5, 6, 3, 10, 6, 1, 1, 3, 8, 37,000¥ | Yes |
| Morgan Cutlass | boat | 5, 4, 2, 16, 10, 3, 5, 6, 14R, 96,000¥ | Yes |
| Proteus Lamprey | submarine | 3, 2, 1, 6, 6, 1, 3, 4, –, 14,000¥ | Yes |
| Vulkan Electronaut | submarine | 3, 3, 1, 12, 10, 4, 4, 2, 10, 108,000¥ | Yes |
| Artemis Industries Nightwing | aircraft | 6, 3, 1, 4, 0, 1, 1, 1, 8, 20,000¥ | Yes |
| Cessna C750 | aircraft | 3, 5, 3, 18, 4, 2, 2, 4, 8, 146,000¥ | Yes |
| Renault-Fiat Fokker Tundra-9 | aircraft | 3, 4, 3, 20, 10, 3, 3, 24, 12, 300,000¥ | Yes |
| Ares Dragon | aircraft | 4, 4, 3, 22, 8, 3, 3, 18, 12, 355,000¥ | Yes |
| Nissan Hound | aircraft | 5, 4, 3, 16, 16, 2, 4, 12, 13R, 425,000¥ | Yes |
| Northrup Wasp | aircraft | 5, 5, 3, 10, 8, 3, 3, 1, 12R, 86,000¥ | Yes |
| Ares Venture | aircraft | 5, 7, 4, 16, 14, 4, 4, 6, 12F, 400,000¥ | Yes |
| GMC Banshee | aircraft | 6, 8, 4, 20, 18, 4, 6, 12, 24F, 2,500,000¥ | Yes |
| Federated Boeing Commuter | aircraft | 3, 3, 3, 16, 8, 3, 3, 30, 10, 350,000¥ | Yes |
| Shiawase Kanmushi | drone | 4, 2, 1, 0, 0, 3, 3, –, 8, 1,000¥ | Yes |
| Sikorsky-Bell Microskimmer | drone | 3, 3, 1, 0, 0, 3, 3, –, 6, 1,000¥ | Yes |
| MCT Fly-Spy | drone | 4, 3, 2, 1, 0, 3, 3, –, 8, 2,000¥ | Yes |
| Horizon Flying Eye | drone | 4, 3, 2, 1, 0, 3, 3, –, 8, 2,000¥ | Yes |
| Aztechnology Crawler | drone | 4, 3, 1, 3, 3, 4, 3, –, 4, 4,000¥ | Yes |
| Lockheed Optic-X2 | drone | 4, 4, 3, 2, 2, 3, 3, –, 10, 21,000¥ | Yes |
| Ares Duelist | drone | 3, 3, 1, 4, 4, 3, 3, –, 5R, 4,500¥ | Yes |
| GM-Nissan Doberman | drone | 5, 3, 1, 4, 4, 3, 3, –, 4R, 5,000¥ | Yes |
| MCT-Nissan Roto-Drone | drone | 4, 4, 2, 4, 4, 3, 3, –, 6, 5,000¥ | Yes |
| Cyberspace Designs Dalmatian | drone | 5, 5, 3, 5, 5, 3, 3, –, 6R, 10,000¥ | Yes |
| Steel Lynx Combat Drone | drone | 5, 4, 2, 6, 12, 3, 3, –, 10R, 25,000¥ | Yes |

All 40/40 match. Names all match too, using the book's abbreviated table names resolved against
its full prose names (e.g. "SK-Bentley Concordat" in the table = "Saeder-Krupp-Bentley
Concordat" in prose and in the catalog; "C-D Dalmatian" = "Cyberspace Designs Dalmatian"; "R-F
Fokker Tundra-9" = "Renault-Fiat Fokker Tundra-9"; "GM-Nissan Doberman" and "MCT-Nissan
Roto-Drone" retain their table abbreviations verbatim in the catalog, consistent with how the
book itself refers to them in prose).

### Source-citation issues found (data, not stats, is affected)

While diffing, two `source.printedPage`/`source.pdfPage` values were found to point at the
wrong page relative to every sibling entry in the same category — the stat values themselves are
still correct, only the citation is off:

- **`harley-davidson-scorpion`**: cites `printedPage: 462, pdfPage: 464`. Every other bike
  (Dodge Scoot, Yamaha Growler, Suzuki Mirage) cites `printedPage: 463, pdfPage: 465`, which is
  where the actual groundcraft stat table lives. PDF page 464 / printed 462 is the *cars*
  description page (Honda Spirit/Hyundai Shin-Hyung/Eurocar Westwind 3000/Ford Americar prose),
  not bikes at all.
- **`horizon-flying-eye`**: cites `printedPage: 465, pdfPage: 467`. Every other drone (Shiawase
  Kanmushi, Sikorsky-Bell Microskimmer, MCT Fly-Spy, Aztechnology Crawler, Lockheed Optic-X2,
  Ares Duelist, GM-Nissan Doberman, MCT-Nissan Roto-Drone, Cyberspace Designs Dalmatian, Steel
  Lynx) cites `printedPage: 466, pdfPage: 468`, which is where the drone stat table actually
  lives. PDF page 467 / printed 465 is the boats/submarines/aircraft stat table page.

Both are minor citation/metadata bugs (wrong page number recorded, not wrong game data) but
worth a one-line fix since CHAR-812 is specifically a source-traceability gate.

## Verdict

**Pass, with two small citation fixes and one modeling concern to route to the right owner.**

- Vehicle/drone catalog content (all 40 entries): complete and correct. No missing vehicles, no
  unexplained vehicles, no stat mismatches.
- Vehicle modifications (all 4 entries): complete — the book has no additional purchasable
  vehicle modifications in this section beyond rigger interface + standard mount + heavy mount +
  manual operation. Nothing to add.
- Two entries (`harley-davidson-scorpion`, `horizon-flying-eye`) have a wrong source page
  citation and should have their `source.printedPage`/`source.pdfPage` corrected to match their
  category siblings (463/465 and 466/468 respectively).
- `manual-operation`'s `availability.fixed: 9` should be reviewed — it appears to bake in "+1
  applied to the Standard weapon mount's Avail 8" rather than representing the book's true
  relative "+1" modifier, which would produce the wrong number (should be 15F, not 9) if applied
  to a Heavy weapon mount instead.
