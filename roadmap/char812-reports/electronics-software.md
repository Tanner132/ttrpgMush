# CHAR-812 Reconciliation: Electronics, Commlinks, Cyberdecks, Sensors, Software & Programs

Scope: commlinks, cyberdecks, RFID tags, communications/countermeasures gear,
sensors (optical/audio/sensor devices), software, skillsofts, and
programs/autosofts. Source: `Shadowrun 5th Edition Core Rulebook (...).pdf`,
printed pp. 438-446 (PDF 440-448) for electronics/sensors, printed pp. 243-246,
269-270 (PDF 245-248, 271-272) for programs/autosofts. Runtime catalog:
`backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/Resources/sr5-core-1.0.0.json`.

## Summary

- **Reviewed:** 9 cyberdecks (`cyberdecks` array) + 73 `gear` entries across the
  9 assigned categories (commlink 9, electronics-accessory 9, rfid-tag 5,
  communications 8, software 8, skillsoft 3, optical-imaging 19, audio-device
  8, sensor-device 4) = **82 catalog items total**, plus the full PDF-side
  checklist of commlinks, decks, accessories, RFID tags, comms/countermeasures,
  software, skillsofts, optical/audio/sensor devices and enhancements, and
  Common/Hacking programs, Agents, and autosofts.
- **Gaps found: 1** (Autosofts — see below; qualified, not a simple "PDF has
  it, catalog doesn't" case).
- **Unexplained catalog entries: 0.** Every one of the 82 `gear`/`cyberdecks`
  items traces cleanly to a specific PDF row.
- **Out-of-scope observation: 1** (Rigger Command Consoles — not part of this
  slice's assigned categories; flagged only for the record).

This category is in excellent shape. Every named commlink, cyberdeck, RFID
tag, comms/countermeasures device, sensor/optical/audio device (both fixed-cost
and Capacity-scaled), and software/skillsoft line item from the PDF's priced
tables is present in the catalog with matching cost/availability/rating
figures. The one real gap (Autosofts) is a genuine hole in the source book's
own pricing, not a transcription miss.

## Gaps

### 1. Autosofts have no runtime catalog representation (in scope, unresolved)

The PDF (printed pp. 269-270, PDF 271-272) describes autosofts narratively —
Clearsight, Electronic Warfare, `[Model]` Evasion, `[Model]` Maneuvering,
`[Model]` Stealth, `[Weapon]` Targeting — but **the reviewed page range
contains no priced Autosoft table** (no Availability/Cost columns anywhere
near the autosoft description, unlike every other purchasable category in this
slice). I extended the search beyond the assigned range (full-text grep for
"autosoft" across the entire PDF, plus manual inspection of PDF pp. 267-268 and
273-276 covering the surrounding Rigger Command Console and rigging-example
pages) and found no autosoft price/availability table anywhere in the book.

This matters because `roadmap/sr5-catalog/ELECTRONICS_GEAR.md` (the CHAR-801
review ledger) lists an "Agents And Autosofts" table with a cost of
"Rating x 500 nuyen" and Availability "Rating x 2", citing `sr5-core` pp.
269-270, 461 (PDF 271-272, 463). I checked PDF page 463 directly — it is the
Cultured Bioware / Magical Equipment page (Cerebral Booster, Synaptic Booster,
Foci table, start of Vehicle Modifications); it contains no autosoft pricing.
I could not verify that citation against the actual PDF text. This looks like
either a bad citation in the ledger or a value carried over from general SR5
knowledge/errata rather than this specific book's printed text.

The runtime catalog has **no autosoft category or items at all** (`gear` has
no `autosoft` `categoryId`, `cyberdecks`/`vehicles` have no autosoft-adjacent
array). Given the assigned task scope explicitly includes "programs/autosofts"
and asks to verify they're "present," I'm flagging this as open rather than
silently treating the absence as correct. Recommend one of:
- Track down whether a genuine printed Autosoft price table exists elsewhere
  in this specific PDF (I did not find one in pp. 260-280 or via full-text
  search), or
- Record an explicit, approved exclusion decision in `SR5_RULE_DECISIONS.md`
  documenting that Autosofts are out of scope for creation-time purchase
  because the core rulebook (this copy) supplies no cost/Availability for
  them — mirroring how cyberdeck program slots already got an explicit
  deferred-gap note rather than being silently missing.

This is different from a normal "PDF has it, catalog doesn't" gap — there is
no priced PDF row to catalog. It is a documentation/decision gap, not a data
entry miss.

### Named Common/Hacking cyberprograms — verified NOT a gap

The book lists 7 Common Programs (Browse, Configurator, Edit, Encryption,
Signal Scrub, Toolbox, Virtual Machine; PDF pp. 245, 247) and 19 Hacking
Programs (Armor, Baby Monitor, Biofeedback, Biofeedback Filter, Blackout,
Decryption, Defuse, Demolition, Exploit, Fork, Guard, Hammer, Lockdown,
Mugger, Shell, Sneak, Stealth, Track, Wrapper; PDF pp. 245-246, 247-248) by
name, but the book's own pricing tables (PDF p. 244/246 and p. 442/444) give
**one flat price per category**, not a price per named program: Common
Program 80 nuyen (no Availability), Hacking Program 250 nuyen (Availability
6R on the Street Gear table, 4R on the Matrix-chapter table — a book-internal
inconsistency, not a catalog issue). The catalog's `cyberprogram-common` and
`cyberprogram-hacking` `gear` entries (80/250 nuyen, matching availabilities)
correctly capture the only purchasable facts the book provides; the 26 named
programs are flavor/mechanical-effect text, not separately priced SKUs. This
mirrors the documented cost-modeling-simplification convention already used
elsewhere in this catalog (see `sr5_catalog_conventions.md`). **Not a gap.**

## Unexplained catalog entries

None. Every `cyberdecks` entry and every `gear` entry in the 9 assigned
categories was traced to a specific named row in the PDF's electronics tables
(pp. 438-446 / PDF 440-448). No orphaned or unattributable catalog items were
found in this slice.

## Out-of-scope observation (for the record only)

**Rigger Command Consoles (RCC)** — the book has a full 11-model RCC table
(PDF p. 269, printed p. 267: Scratch-Built Junk through Triox UberMensch, with
Device Rating, Data Processing/Firewall, Availability, and Cost) and the
CHAR-801 ledger reviewed it in detail, but RCCs were never listed in this
slice's assigned categories (commlinks, cyberdecks, RFID tags,
communications, sensors, software, skillsofts, programs/autosofts) and there
is no `rcc`/`rigger-command-console` category or array in the runtime catalog.
This is a rigging/vehicles-adjacent mechanic, not core "electronics," so I am
not counting it as a gap in this report — but the reviewing session should
confirm it's tracked under the Vehicles & Drones slice (`SR5_CATALOG_LEDGER.md`
does not list RCCs as implemented there either, so it may be a genuine
cross-slice gap worth a follow-up ticket).

## Spot-check results

All spot-checked items matched the PDF exactly on cost, Availability, and
(where applicable) Rating range/attribute array. Cross-checks below used a
clean single-page `pdftotext` extraction (without `-layout`, which reads
tables in column-major stream order and avoids the two-column interleaving
that garbles the `-layout` output for these particular pages).

**Cyberdecks (9/9, full stat-array verification):** All 9 decks present —
Erika MCD-1, Microdeck Summit, Microtrônica Azteca 200, Hermes Chariot,
Novatech Navigator, Renraku Tsurugi, Sony CIY-720, Shiawase Cyber-5, Fairlight
Excalibur. Attribute arrays, Device Rating, program-slot counts, cost, and
Availability all match the PDF table (p. 439/PDF 441) exactly — e.g. Erika
MCD-1 `[4,3,2,1]`/DR1/1 program/49,500¥/3R through Fairlight Excalibur
`[9,8,7,6]`/DR6/6 programs/823,250¥/18R, verified digit-for-digit against the
extracted `4321,4331,5432,5442,6543,6553,7654,8765,9876` array sequence and
cost sequence in the PDF.

**Commlinks (9/9, full sequence verification):** Meta Link (2/100¥), Sony
Emperor (4/700¥), Renraku Sensei (6/1,000¥), Erika Elite (8/2,500¥), Hermes
Ikon (10/3,000¥), Transys Avalon (12/5,000¥), Fairlight Caliban (14/8,000¥),
Sim Module (--/+100¥), Sim Module w/ Hot-Sim (+4 Forbidden/+250¥) — all 9
avail/cost pairs match the PDF table (p. 439/PDF 441) in order, including the
Hot-Sim upgrade's Forbidden legality.

**Electronics accessories (9/9):** AR Gloves (--/150¥), Biometric Reader
(4/200¥), Electronic Paper (--/5¥), Printer (--/25¥), Satellite Link
(6/500¥), Simrig (12/1,000¥), Subvocal Mic (4/50¥), Trid Projector (--/200¥),
Trodes (--/70¥) — all match PDF p. 439/PDF 441 exactly.

**RFID tags (5/5):** Standard Tags (--/1¥), Datachip (--/5¥), Security Tags
(3/5¥), Sensor Tags (5/40¥), Stealth Tags (7R/10¥) — all match PDF p.
440/PDF 442 exactly.

**Communications/countermeasures (8/8):** Bug Scanner (Rating-R /
Rating×100¥), Data Tap (6R/300¥), Headjammer (Rating-R / Rating×150¥), Area
Jammer (Rating×3 F / Rating×200¥), Directional Jammer (Rating×2 F /
Rating×200¥), Micro-Transceiver (2/100¥), Tag Eraser (6R/450¥), White Noise
Generator (Rating / Rating×50¥) — all match PDF p. 441/PDF 443 exactly.

**Software (8/8):** Agent R1-3 (Rating×3 / Rating×1,000¥), Agent R4-6
(Rating×3 / Rating×2,000¥), Cyberprogram Common (--/80¥), Cyberprogram
Hacking (6R/250¥), Datasoft (4/120¥), Mapsoft (4/100¥), Shopsoft (4/150¥),
Tutorsoft (Rating / Rating×400¥) — all match PDF p. 442/PDF 444 exactly.

**Skillsofts (3/3):** Activesoft (8 / Rating×5,000¥), Knowsoft (4 /
Rating×2,000¥), Linguasoft (2 / Rating×1,000¥) — match PDF p. 442/PDF 444
exactly. Modeled as `parameterized` items requiring an authored skill/subject
(same pattern as Datasoft/Mapsoft/Shopsoft/Tutorsoft) rather than one catalog
row per possible skill — reasonable, matches the book's own open-ended
"any skill" framing, and is consistent with how Knowledge skills/languages are
modeled elsewhere in this catalog (open-authored, not enumerated). Not a gap.

**Optical/imaging (19/19, both fixed-cost and Capacity-host waves present):**
Fixed-cost hosts — Binoculars Optical (--/50¥), Micro-Camera (100¥/Cap1),
Endoscope (8/250¥), Imaging Scope (2/300¥/Cap3), Periscope (3/50¥), Mage
Sight Goggles (12R/3,000¥). Capacity-scaled hosts — Binoculars (Cap1-3,
Rating×50¥), Camera (Cap1-6, Rating×100¥), Contacts (6, Cap1-3,
Rating×200¥), Glasses (Cap1-4, Rating×100¥), Goggles (Cap1-6, Rating×50¥),
Monocle (12R, Cap1-4, 3,000¥ — the base unit is fixed-cost even though it's a
Capacity host). Vision enhancements — Low-Light (+4/+500¥), Flare
Compensation (+1/+250¥), Image Link (--/+25¥), Smartlink (+4R/+2,000¥),
Thermographic Vision (+6/+500¥), Vision Enhancement (Rating×2 /
Rating×500¥), Vision Magnification (+2/+250¥). All match PDF pp. 443-444/PDF
445-446 exactly.

**Audio devices (8/8):** Directional Mic (4, Cap1-6, Rating×50¥), Ear Buds
(--, Cap1-3, Rating×50¥), Headphones (--, Cap1-6, Rating×50¥), Laser Mic (6R,
Cap1-6, Rating×100¥), Omni-Directional Mic (--, Cap1-6, Rating×50¥); Audio
Enhancement (Rating×2 / Rating×500¥), Select Sound Filter (Rating×3 /
Rating×250¥), Spatial Recognizer (+4/+1,000¥/Cap2) — all match PDF p.
445/PDF 447 exactly.

**Sensors (4/4):** Handheld Housing (--, Cap1-3, Rating×100¥), Wall-Mounted
Housing (--, Cap1-6, Rating×250¥), Sensor Array (7, Rating2-8,
Rating×1,000¥, Capacity[6]), Single Sensor (5, Rating2-8, Rating×100¥,
Capacity[1]) — all match PDF pp. 445-446/PDF 447-448 exactly. The 13-entry
"Sensor Functions" list (Atmosphere Sensor, Cyberware Scanner, Geiger
Counter, Laser Range Finder, MAD Scanner, Motion Sensor, Olfactory Sensor,
Radio Signal Scanner, Ultrasound, plus Camera/Directional
Mic/Omni-Directional Mic/Vision Magnification, which reuse those devices'
own entries) has **no separate price or Availability in the book** — the
book states each function's Capacity cost equals its Rating and folds into
the housing purchase. Correctly not modeled as separate catalog rows. Not a
gap.

## Verdict

**Substantially reconciled, with one open item requiring a decision rather
than a data fix.** Every priced item in the PDF's electronics tables —
commlinks, cyberdecks, electronics accessories, RFID tags,
communications/countermeasures, software, skillsofts, and both waves of
optical/audio/sensor devices — is present in the runtime catalog with correct
cost, Availability, and Rating/Capacity figures, and every catalog item in
this slice traces cleanly back to a PDF row. No unexplained entries.

The only unresolved item is **Autosofts**: they are explicitly in this
slice's scope, the catalog has none, and I could not locate any priced
Autosoft table in this specific PDF (the assigned range or the wider book).
This isn't a "go add the missing rows" gap — there's no PDF price to add —
it needs a decision: either locate a printed price table I missed (I'd
suggest a second pass specifically re-scanning PDF pp. 255-268 and the
Rigger-gear appendix tables, since my search covered the assigned range plus
the immediately surrounding pages but not the entire book page-by-page), or
formally record Autosofts as an approved out-of-scope-at-creation exclusion
in `SR5_RULE_DECISIONS.md`/`ELECTRONICS_GEAR.md` so CHAR-812 can close this
slice with a documented rationale instead of a silent absence.

Also flagged for the record (not this slice's responsibility): Rigger
Command Consoles are fully detailed in the PDF and in the CHAR-801 ledger but
absent from the runtime catalog entirely — worth confirming whether that's
tracked under the Vehicles & Drones slice before CHAR-812 signs off overall.

The already-approved cyberdeck program-slots gap was not re-investigated per
the task's instructions; the cyberdeck hardware entries themselves (Rating,
price, Attack/Sleaze/Data Processing/Firewall array) are confirmed present
and correct.
