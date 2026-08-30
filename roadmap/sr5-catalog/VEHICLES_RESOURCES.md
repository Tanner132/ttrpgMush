# Vehicles And Resources

This is the CHAR-801 row-level review ledger for core vehicles, drones, vehicle
modifications, magical equipment, lifestyles, identities, licenses, contacts,
and final resource bookkeeping. It is a review input for CHAR-802, not a runtime
catalog and not a substitute for the approved books. Only the pinned `sr5-core`
PDF and approved decisions in [`../SR5_RULE_DECISIONS.md`](../SR5_RULE_DECISIONS.md)
are used. No Run Faster catalog entry is admitted.

## Field Conventions And Global Creation Rules

- `none` means the source prints no Availability or legality suffix. `R` means
  Restricted and `F` means Forbidden. A numeric Availability above 12 is
  `creation-unavailable`; an `R` or `F` suffix alone does not make a product
  unavailable at creation. Restricted items require a license, while Forbidden
  items have no license. Source: `sr5-core` pp. 94, 416-419 (PDF 96, 418-421).
  Decision: `gear.legality-at-creation`.
- Explicit purchasable Rating and Force are capped at 6 before the Availability
  ceiling is applied. The cap does not apply to vehicle attributes, Sensor,
  Capacity, mount capacity, or quantities. Source: `sr5-core` pp. 94, 418
  (PDF 96, 420). Decision: `gear.rating-cap-force`.
- Dwarfs pay base gear cost +10% and lifestyle cost +20%; trolls pay base gear
  cost +50% and lifestyle cost +100%. These are separate category modifiers,
  not changes to Availability. Sources: `sr5-core` pp. 65-66, 94, 420
  (PDF 67-68, 96, 422). Decisions: `metatype.dwarf-costs`,
  `metatype.troll-costs`.
- Every vehicle has its printed Pilot and Sensor systems built in. Pilot replaces
  Mental attributes and Reaction while the vehicle operates itself; Sensor is
  the limit for vehicle-system Perception/detection. A vehicle Condition Monitor
  is `12 + ceil(Body / 2)`; a drone Condition Monitor is `6 + ceil(Body / 2)`.
  Sources: `sr5-core` p. 199 (PDF 201), pp. 461-466 (PDF 463-468).
- Vehicle mount capacity is `floor(unaugmented Body / 3)`. Standard mounts use
  one slot; heavy mounts use two. The Roto-Drone exceptionally uses Body +3 only
  for mounts/customizations. Generated profiles below calculate capacity but do
  not silently install a mount. Source: `sr5-core` pp. 461, 466
  (PDF 463, 468).
- Seats include the operator. Each unused seat can instead hold 250 kilograms.
  Occupancy can reach 150% of seats, but Handling and Speed each fall by 1.
  Drones have no seats. Source: `sr5-core` p. 461 (PDF 463).
- Cost is the printed base price before approved metatype modifiers. All vehicle
  and gear purchases remain subject to gamemaster approval. Source: `sr5-core`
  pp. 94, 103 (PDF 96, 105).

## Vehicle Modifications

Vehicle modification follows *Rigger 5.0*, which supersedes the core rulebook's
four-row table. The core rows (`rigger-interface` aside, which *Rigger 5.0*
reprints unchanged as a Cosmetic modification) are no longer catalogued:
`standard-weapon-mount`, `heavy-weapon-mount` and `manual-operation` are
replaced by `weapon-mount-light`/`-standard`/`-heavy` plus their option rows.
Source: `rigger-5` pp. 151-171 (PDF 152-172), drones pp. 122-127 (PDF 123-128).

### Modification Slots

Every vehicle has Modification Slots equal to its **Body** in **each** of six
independent categories — Power Train, Protection, Weapons, Body,
Electromagnetic, Cosmetic. A modification only draws on its own category's pool
and a category's pool can never be exceeded. Source: `rigger-5` p. 151 (PDF
152). A handful of core vehicles are printed with extra slots in one category
(`hyundai-shin-hyung` and `gmc-bulldog-step-van` +4 Body,
`mct-nissan-roto-drone` +3 Weapons), carried on the vehicle as
`modificationSlotBonuses`. Source: `rigger-5` p. 155 (PDF 156).

Drone modifications use the parallel Mod Point system — one pool, also equal to
Body — modelled here as a seventh category, `drone`. Drone Immobile is the only
entry with a negative slot cost: it hands back 2 Mod Points. Source: `rigger-5`
p. 122 (PDF 123).

### Cost scaling

Most modifications are priced off the host vehicle rather than as a flat figure,
so an entry carries either a flat `cost` or a `costScaling`
(`multiplier` x the product of `factors`):

| Printed form | Encoding | Examples |
| --- | --- | --- |
| `Body x N` | `multiplier: N, factors: [body]` | Rocket Booster (Body x 5,000), Multifuel Engine, SunCell, Touch Sensors, Life Support |
| `Handl x N` / `Speed x N` / `Accel x N` | `factors: [handling]` / `[speed]` / `[acceleration]` | Handling, Speed and Acceleration Enhancements. Handling and Speed use the leading on-road figure of a `4/3` pair |
| `Rating x N` | `factors: [rating]` | Vehicle Armor, Passenger Protection System, Personal Armor, Nanomaintenance |
| `Rating x Body x N` | `factors: [rating, body]` | Realistic Features |
| `(Body x Body) x N` | `factors: [body, body]` | Drone Realistic Features, Drone Amphibious |
| `Vehicle cost x 25%` | `multiplier: 0.25, factors: [vehicleCost]` | Off-Road Suspension |
| `N x seat` | `factors: [seats]` | Metahuman Adjustment |
| `N x mount MP` | `factors: [slotCost]` | Drone blow-away panels and pop-out mounts |

A drone whose Body is 0 counts as 0.5 in this arithmetic, so a microdrone's
modifications are not free. Source: `rigger-5` p. 123 (PDF 124).

### Availability and Rating

Availability is either fixed or `(R x N)` — encoded as `availability.perRating`
— for ECM, Nanomaintenance, Realistic Features, Personal Armor and Yerzed Out.
Slot cost is likewise flat or `Rating`-scaled (`Rating`, `Rating x 2` for
standard Armor, `Rating x 3` for concealed Armor).

Two Ratings are bounded by the host rather than by a printed maximum, recorded
as `ratingCap`: vehicle Armor caps at the vehicle's Body, and a Special Armor
Modification caps at the vehicle's Armor. Source: `rigger-5` pp. 159-160
(PDF 160-161). The general creation Rating cap of 6 deliberately does not apply
to vehicle modifications — vehicle Armor legitimately runs to the vehicle's Body
at a flat Availability of 6R.

### Weapon mounts and other option rows

A weapon mount is a size (Light/Standard/Heavy) plus one pick from each of three
axes: visibility (External/Internal/Concealed), flexibility (Fixed/Flexible/
Turret) and control (Remote/Manual/Armored Manual). The no-cost defaults
(External, Fixed, Remote) are already priced into the size rows, so only the
surcharge rows are catalogued, as `relative` entries carrying an
`optionGroupId` and `appliesToModificationIds`. Their Availability, cost and
slot cost are **modifiers** added to the base mount, and at most one may be
chosen per group. A Heavy mount (12F) with a Turret (+6) is therefore 18F and
out of reach at creation. The same shape carries drone blow-away panels and
pop-out mounts. Source: `rigger-5` p. 162 (PDF 163), drones p. 124 (PDF 125).

Options travel on the same purchase as the modification they qualify:
`AttachmentSelection.Options` holds their ids, and the canonical attachment
records them back.

### Known omissions

- **Workshop** (Body category, 6 slots) is not catalogued: *Rigger 5.0* prints
  the row with no Availability and no cost (`rigger-5` p. 167, PDF 168).
- **Special Equipment** (Body category) is `variable` on every column — a
  gamemaster-defined row rather than a purchasable entry.
- **Drone attribute modifications** (buying up a drone's Handling, Speed,
  Acceleration, Armor or Sensor for Mod Points) are per-point purchases against
  the drone's own stat line rather than catalog rows, and are not modelled.
  Source: `rigger-5` pp. 123-124 (PDF 124-125).
- The **standard upgrades** core vehicles ship with (`rigger-5` p. 155, PDF 156)
  are recorded only where they grant extra slots; the pre-installed
  modifications themselves do not yet consume their categories' slots.
- `special-armor-radiation-shielding` and `special-armor-universal-mirror-material`
  are named by *Rigger 5.0* but have no personal-armor modification in this
  catalog to double the cost of, so both follow the same Rating x 500 shape as
  the other four special armor modifications.

## Vehicle Profiles

`Generated profile` is `Condition Monitor / mount slots / maximum cargo if every
seat is used as cargo`. Handling or Speed values separated by `/` are On Road /
Off Road. A single value applies in both contexts. A product may be purchased in
a positive integer quantity and may carry an authored cosmetic description;
neither creates a new mechanical model.

### Bikes

All bikes use Pilot Ground Craft. Most are available with electric or hybrid
biofuel engines; engine choice has no separate printed statistics or price.
Source: `sr5-core` p. 462 (PDF 464).

| ID | Display name | Class | Handling | Speed | Accel | Body | Armor | Pilot | Sensor | Seats | Availability / legality | Cost | Generated profile | Included facts | Source |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |
| `dodge-scoot` | Dodge Scoot | `selectable` | 4/3 | 3 | 1 | 4 | 4 | 1 | 1 | 1 | none | 3,000 nuyen | 14 / 1 / 250 kg | Electric scooter. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `harley-davidson-scorpion` | Harley-Davidson Scorpion | `selectable` | 4/3 | 4 | 2 | 8 | 9 | 1 | 2 | 1 | none | 12,000 nuyen | 16 / 2 / 250 kg | Heavy road bike; no mount is included. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `yamaha-growler` | Yamaha Growler | `selectable` | 4/5 | 3/4 | 1 | 5 | 5 | 1 | 1 | 1 | none | 5,000 nuyen | 15 / 1 / 250 kg | Off-road bike. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `suzuki-mirage` | Suzuki Mirage | `selectable` | 5/3 | 6 | 3 | 5 | 6 | 1 | 2 | 1 | none | 8,500 nuyen | 15 / 1 / 250 kg | Racing bike; no mount is included. | `sr5-core` pp. 462-463 (PDF 464-465) |

### Cars

All cars use Pilot Ground Craft. Most are available with electric or hybrid
biofuel engines; engine choice has no separate printed profile. Source:
`sr5-core` pp. 462-463 (PDF 464-465).

| ID | Display name | Class | Handling | Speed | Accel | Body | Armor | Pilot | Sensor | Seats | Availability / legality | Cost | Generated profile | Included facts | Source |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |
| `chrysler-nissan-jackrabbit` | Chrysler-Nissan Jackrabbit | `selectable` | 4/3 | 3 | 2 | 8 | 4 | 1 | 2 | 2 | none | 10,000 nuyen | 16 / 2 / 500 kg | Electric subcompact. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `honda-spirit` | Honda Spirit | `selectable` | 3/2 | 4 | 2 | 8 | 6 | 1 | 2 | 2 | none | 12,000 nuyen | 16 / 2 / 500 kg | Three-wheeled electric car with bubble hood. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `hyundai-shin-hyung` | Hyundai Shin-Hyung | `selectable` | 5/4 | 6 | 3 | 10 | 6 | 1 | 2 | 4 | none | 28,500 nuyen | 17 / 3 / 1,000 kg | Four-door sports sedan. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `eurocar-westwind-3000` | Eurocar Westwind 3000 | `creation-unavailable` | 6/4 | 7 | 3 | 10 | 8 | 3 | 5 | 2 | 13 / none | 110,000 nuyen | 17 / 3 / 500 kg | Availability 13 exceeds the creation ceiling. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `ford-americar` | Ford Americar | `selectable` | 4/3 | 3 | 2 | 11 | 6 | 1 | 2 | 4 | none | 16,000 nuyen | 18 / 3 / 1,000 kg | Four-door sedan. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `saeder-krupp-bentley-concordat` | Saeder-Krupp-Bentley Concordat | `selectable` | 5/4 | 5 | 2 | 12 | 12 | 2 | 4 | 4 | 10 / none | 65,000 nuyen | 18 / 4 / 1,000 kg | Luxury sedan. | `sr5-core` pp. 462-463 (PDF 464-465) |
| `mitsubishi-nightsky` | Mitsubishi Nightsky | `creation-unavailable` | 4/3 | 4 | 2 | 15 | 15 | 3 | 5 | 8 | 16 / none | 320,000 nuyen | 20 / 5 / 2,000 kg | Armored limousine; Availability 16 exceeds the creation ceiling. | `sr5-core` pp. 462-463 (PDF 464-465) |

### Trucks And Vans

All use Pilot Ground Craft. Most are available with electric or hybrid biofuel
engines; engine choice has no separate printed profile. Source: `sr5-core`
p. 463 (PDF 465).

| ID | Display name | Class | Handling | Speed | Accel | Body | Armor | Pilot | Sensor | Seats | Availability / legality | Cost | Generated profile | Included facts | Source |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |
| `toyota-gopher` | Toyota Gopher | `selectable` | 5/5 | 4 | 2 | 14 | 10 | 1 | 2 | 3 | none | 25,000 nuyen | 19 / 4 / 750 kg | Pickup with off-road suspension. | `sr5-core` p. 463 (PDF 465) |
| `gmc-bulldog-step-van` | GMC Bulldog Step-Van | `selectable` | 3/3 | 3 | 1 | 16 | 12 | 1 | 2 | 6 | none | 35,000 nuyen | 20 / 5 / 1,500 kg | Armored delivery van. | `sr5-core` pp. 463-464 (PDF 465-466) |
| `rover-model-2072` | Rover Model 2072 | `selectable` | 5/5 | 4 | 2 | 15 | 12 | 2 | 4 | 6 | 10 / none | 68,000 nuyen | 20 / 5 / 1,500 kg | Luxury wilderness van. | `sr5-core` pp. 463-464 (PDF 465-466) |
| `ares-roadmaster` | Ares Roadmaster | `selectable` | 3/3 | 3 | 1 | 18 | 18 | 3 | 3 | 8 | 8 / none | 52,000 nuyen | 21 / 6 / 2,000 kg | Armored car; no weapon mount is included. | `sr5-core` pp. 463-464 (PDF 465-466) |

### Watercraft

All boats and submarines use Pilot Watercraft. Source: `sr5-core` pp. 464-465
(PDF 466-467).

| ID | Display name | Class | Type | Handling | Speed | Accel | Body | Armor | Pilot | Sensor | Seats | Availability / legality | Cost | Generated profile | Included facts | Source |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |
| `samuvani-criscraft-otter` | Samuvani-Criscraft Otter | `selectable` | Boat | 4 | 3 | 2 | 12 | 6 | 2 | 2 | 8 | none | 21,000 nuyen | 18 / 4 / 2,000 kg | Open-hull utility boat. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `yongkang-gala-trinity` | Yongkang-Gala Trinity | `selectable` | Boat | 5 | 6 | 3 | 10 | 6 | 1 | 1 | 3 | 8 / none | 37,000 nuyen | 17 / 3 / 750 kg | Speedboat assembled around concealed cargo; payload is not numerically specified. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `morgan-cutlass` | Morgan Cutlass | `creation-unavailable` | Boat | 5 | 4 | 2 | 16 | 10 | 3 | 5 | 6 | 14R | 96,000 nuyen | 20 / 5 / 1,500 kg | Includes two heavy manually operated mounts; Availability 14 exceeds the creation ceiling. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `proteus-lamprey` | Proteus Lamprey | `selectable` | Submarine sea-sled | 3 | 2 | 1 | 6 | 6 | 1 | 3 | 4 | none | 14,000 nuyen | 15 / 2 / 1,000 kg | Passengers require scuba gear; includes one rack for one seaworthy Medium-or-smaller drone. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `vulkan-electronaut` | Vulkan Electronaut | `selectable` | Mini-submarine | 3 | 3 | 1 | 12 | 10 | 4 | 4 | 2 | 10 / none | 108,000 nuyen | 18 / 4 / 500 kg | Two-person enclosed mini-sub. | `sr5-core` pp. 464-465 (PDF 466-467) |

### Aircraft

Fixed-wing aircraft, rotorcraft, and VTOL/VSTOL craft use Pilot Aircraft.
Source: `sr5-core` pp. 464-465 (PDF 466-467).

| ID | Display name | Class | Type | Handling | Speed | Accel | Body | Armor | Pilot | Sensor | Seats | Availability / legality | Cost | Generated profile | Included facts | Source |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |
| `artemis-industries-nightwing` | Artemis Industries Nightwing | `selectable` | Fixed-wing | 6 | 3 | 1 | 4 | 0 | 1 | 1 | 1 | 8 / none | 20,000 nuyen | 14 / 1 / 250 kg | Shrouded electric motor minimizes sound and heat; no numeric concealment modifier is printed. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `cessna-c750` | Cessna C750 | `selectable` | Fixed-wing | 3 | 5 | 3 | 18 | 4 | 2 | 2 | 4 | 8 / none | 146,000 nuyen | 21 / 6 / 1,000 kg | Dual-prop civilian plane. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `renault-fiat-fokker-tundra-9` | Renault-Fiat Fokker Tundra-9 | `selectable` | Fixed-wing amphibious jet | 3 | 4 | 3 | 20 | 10 | 3 | 3 | 24 | 12 / none | 300,000 nuyen | 22 / 6 / 6,000 kg | Includes buoyancy and flotation upgrades and can take off from land or water. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `ares-dragon` | Ares Dragon | `selectable` | Rotorcraft | 4 | 4 | 3 | 22 | 8 | 3 | 3 | 18 | 12 / none | 355,000 nuyen | 23 / 7 / 4,500 kg | Double-prop cargo helicopter; no mount is included. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `nissan-hound` | Nissan Hound | `creation-unavailable` | Rotorcraft | 5 | 4 | 3 | 16 | 16 | 2 | 4 | 12 | 13R | 425,000 nuyen | 20 / 5 / 3,000 kg | Includes two weapon mounts; mount weight is not named. Availability 13 exceeds the creation ceiling. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `northrup-wasp` | Northrup Wasp | `selectable` | Rotorcraft | 5 | 5 | 3 | 10 | 8 | 3 | 3 | 1 | 12R | 86,000 nuyen | 17 / 3 / 250 kg | Includes one heavy weapon mount, consuming two mount slots. Requires a matching fake license. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `ares-venture` | Ares Venture | `selectable` | VTOL/VSTOL | 5 | 7 | 4 | 16 | 14 | 4 | 4 | 6 | 12F | 400,000 nuyen | 20 / 5 / 1,500 kg | Small LAV; no mount is included. Forbidden, so no license exists. | `sr5-core` p. 465 (PDF 467) |
| `gmc-banshee` | GMC Banshee | `creation-unavailable` | VTOL/VSTOL | 6 | 8 | 4 | 20 | 18 | 4 | 6 | 12 | 24F | 2,500,000 nuyen | 22 / 6 / 3,000 kg | Prose mentions armaments/sensor suites but specifies no included mount or weapon; Availability 24 exceeds the creation ceiling. | `sr5-core` p. 465 (PDF 467) |
| `federated-boeing-commuter` | Federated Boeing Commuter | `selectable` | VTOL/VSTOL tilt-wing | 3 | 3 | 3 | 16 | 8 | 3 | 3 | 30 | 10 / none | 350,000 nuyen | 20 / 5 / 7,500 kg | Passenger shuttle. The table prints `Fed-Boing`; prose and similar-model list support Boeing. | `sr5-core` pp. 462, 465 (PDF 464, 467) |

## Drone Profiles

Drone sizes are classifications only; they do not change the printed statistics.
`Generated profile` is `Condition Monitor / mount slots`. Every drone has seats
`not applicable`. Source: `sr5-core` pp. 465-466 (PDF 467-468).

| ID | Display name | Class | Size | Pilot skill | Handling | Speed | Accel | Body | Armor | Pilot | Sensor | Availability / legality | Cost | Generated profile | Included facts | Source |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |
| `shiawase-kanmushi` | Shiawase Kanmushi | `selectable` | Micro | Pilot Walker | 4 | 2 | 1 | 0 | 0 | 3 | 3 | 8 / none | 1,000 nuyen | 6 / 0 | Four-legged crawler with gecko-tipped extremities; walls/ceilings capable; fragile. | `sr5-core` pp. 465-466 (PDF 467-468) |
| `sikorsky-bell-microskimmer` | Sikorsky-Bell Microskimmer | `selectable` | Micro | Pilot Ground Craft (Hovercraft applies) | 3 | 3 | 1 | 0 | 0 | 3 | 3 | 6 / none | 1,000 nuyen | 6 / 0 | Disc skimmer; can skim over water. | `sr5-core` pp. 465-466 (PDF 467-468) |
| `horizon-flying-eye` | Horizon Flying Eye | `selectable` | Mini | Pilot Aircraft | 4 | 3 | 2 | 1 | 0 | 3 | 3 | 8 / none | 2,000 nuyen | 7 / 0 | Spherical flyer; may roll on flat ground. Optional payload is the generated child profile below. | `sr5-core` pp. 465-466 (PDF 467-468) |
| `mct-fly-spy` | MCT Fly-Spy | `selectable` | Mini | Pilot Aircraft | 4 | 3 | 2 | 1 | 0 | 3 | 3 | 8 / none | 2,000 nuyen | 7 / 0 | Insect-shaped aerial observation drone. | `sr5-core` pp. 465-466 (PDF 467-468) |
| `aztechnology-crawler` | Aztechnology Crawler | `selectable` | Small | Pilot Walker | 4 | 3 | 1 | 3 | 3 | 4 | 3 | 4 / none | 4,000 nuyen | 8 / 1 | Stair/obstacle-capable crawler. | `sr5-core` p. 466 (PDF 468) |
| `lockheed-optic-x2` | Lockheed Optic-X2 | `selectable` | Small | Pilot Aircraft | 4 | 4 | 3 | 2 | 2 | 3 | 3 | 10 / none | 21,000 nuyen | 7 / 0 | VSTOL; radar, visual, and audio Perception Tests take -3 dice to spot it. | `sr5-core` p. 466 (PDF 468) |
| `ares-duelist` | Ares Duelist | `selectable` | Medium | Pilot Walker | 3 | 3 | 1 | 4 | 4 | 3 | 3 | 5R | 4,500 nuyen | 8 / 1 | Includes Rating 3 Targeting (Swords), two standard swords, and two non-replaceable special mounts; normal mount capacity remains available. Requires a matching fake license. | `sr5-core` p. 466 (PDF 468) |
| `gm-nissan-doberman` | GM-Nissan Doberman | `selectable` | Medium | Pilot Ground Craft | 5 | 3 | 1 | 4 | 4 | 3 | 3 | 4R | 5,000 nuyen | 8 / 1 | Includes one standard weapon mount, consuming its one normal slot. Requires a matching fake license. | `sr5-core` p. 466 (PDF 468) |
| `mct-nissan-roto-drone` | MCT-Nissan Roto-Drone | `selectable` | Medium | Pilot Aircraft | 4 | 4 | 2 | 4 | 4 | 3 | 3 | 6 / none | 5,000 nuyen | 8 / 2 | Uses effective Body 7 only for mount/customization capacity; actual Body remains 4. | `sr5-core` p. 466 (PDF 468) |
| `cyberspace-designs-dalmatian` | Cyberspace Designs Dalmatian | `selectable` | Large | Pilot Aircraft | 5 | 5 | 3 | 5 | 5 | 3 | 3 | 6R | 10,000 nuyen | 9 / 1 | VTOL recon drone. Requires a matching fake license. | `sr5-core` p. 466 (PDF 468) |
| `steel-lynx-combat-drone` | Steel Lynx Combat Drone | `selectable` | Large | Pilot Ground Craft | 5 | 4 | 2 | 6 | 12 | 3 | 3 | 10R | 25,000 nuyen | 9 / 2 | Includes one heavy weapon mount, consuming both slots. Requires a matching fake license. | `sr5-core` p. 466 (PDF 468) |

### Generated And Included Vehicle Children

These rows prevent included equipment from being purchased or counted twice.
They inherit their parent's ownership and creation eligibility and have no
separate price or Availability.

| ID | Display name | Classification | Parent | Quantity and relationship | Source |
| --- | --- | --- | --- | --- | --- |
| `morgan-cutlass/heavy-weapon-mounts` | Morgan Cutlass Heavy Weapon Mounts | `included-component` | `morgan-cutlass` | Two `heavy-weapon-mount` components; consume four of five slots. | `sr5-core` pp. 464-465 (PDF 466-467) |
| `morgan-cutlass/manual-operation` | Morgan Cutlass Manual Operation | `included-component` | `morgan-cutlass/heavy-weapon-mounts` | Manual operation included on both heavy mounts. | `sr5-core` p. 464 (PDF 466) |
| `proteus-lamprey/drone-rack` | Proteus Lamprey Drone Rack | `included-component` | `proteus-lamprey` | One rack holding one separately owned seaworthy Medium-or-smaller drone. | `sr5-core` p. 464 (PDF 466) |
| `renault-fiat-fokker-tundra-9/buoyancy-upgrade` | Tundra-9 Buoyancy Upgrade | `included-component` | `renault-fiat-fokker-tundra-9` | Included; supports amphibious land/water operation. No separate statistics are printed. | `sr5-core` p. 464 (PDF 466) |
| `renault-fiat-fokker-tundra-9/flotation-upgrade` | Tundra-9 Flotation Upgrade | `included-component` | `renault-fiat-fokker-tundra-9` | Included; supports amphibious land/water operation. No separate statistics are printed. | `sr5-core` p. 464 (PDF 466) |
| `nissan-hound/weapon-mounts` | Nissan Hound Weapon Mounts | `included-component` | `nissan-hound` | Two mounts included; the source does not identify them as standard or heavy, so consumed slot count and weapon capacity are not invented. | `sr5-core` p. 465 (PDF 467) |
| `ares-duelist/targeting-swords-3` | Targeting (Swords) 3 | `included-component` | `ares-duelist` | Unique Rating 3 Targeting autosoft; not a separately purchased generic autosoft. | `sr5-core` p. 466 (PDF 468) |
| `ares-duelist/standard-swords` | Ares Duelist Standard Swords | `included-component` | `ares-duelist` | Two standard swords. They are fixed to the special mounts. | `sr5-core` p. 466 (PDF 468) |
| `ares-duelist/special-weapon-mounts` | Ares Duelist Special Weapon Mounts | `included-component` | `ares-duelist` | Two non-replaceable sword mounts; do not consume the one normal mount slot because the source expressly permits additional normal mounts. | `sr5-core` p. 466 (PDF 468) |
| `gm-nissan-doberman/standard-weapon-mount` | Doberman Standard Weapon Mount | `included-component` | `gm-nissan-doberman` | One `standard-weapon-mount`; consumes its one normal mount slot. Weapon and ammunition are separate purchases. | `sr5-core` p. 466 (PDF 468) |
| `steel-lynx-combat-drone/heavy-weapon-mount` | Steel Lynx Heavy Weapon Mount | `included-component` | `steel-lynx-combat-drone` | One `heavy-weapon-mount`; consumes both normal slots. Weapon and ammunition are separate purchases. | `sr5-core` p. 466 (PDF 468) |
| `horizon-flying-eye/flash-pak-smoke` | Horizon Flying Eye, Flash-Pak And Smoke Variant | `generated` | `horizon-flying-eye` | Same printed profile and Availability; cost 2,500 nuyen; includes one flash-pak and one smoke grenade. Detonating either destroys the drone. This is a child profile, not a second base-model purchase. | `sr5-core` pp. 465-466 (PDF 467-468) |

## Magical Equipment

### Focus Purchase Families

Every purchased focus requires one subtype below, Force, physical form, and
tradition. Family rows are pricing/legality components and are not purchased in
addition to the subtype profile. Full published Force is 1-6 under
`gear.rating-cap-force`; `Creation Force` also applies Availability 12. A focus
must be owned, bonded, active, and in the Awakened owner's possession to work.
Sources: `sr5-core` pp. 318-320, 326, 461 (PDF 320-322, 328, 463).

| ID | Display name | Classification | Full Force | Creation Force | Availability / legality | Cost | Child subtype requirement | Source |
| --- | --- | --- | --- | --- | --- | ---: | --- | --- |
| `enchanting-focus-family` | Enchanting Focus | `included-component` | 1-6 | 1-4 | Force x 3, R | Force x 5,000 nuyen | Exactly one of Alchemical or Disenchanting. | `sr5-core` pp. 318-319, 326 (PDF 320-321, 328) |
| `metamagic-focus-family` | Metamagic Focus | `included-component` | 1-6 | none | Force x 3, R | Force x 9,000 nuyen | Exactly one metamagic subtype; all are creation-unavailable because they require initiation/metamagic. | `sr5-core` pp. 319, 324-326 (PDF 321, 326-328) |
| `power-focus-family` | Power Focus | `included-component` | 1-6 | 1-3 | Force x 4, R | Force x 18,000 nuyen | Power Focus subtype. | `sr5-core` pp. 319, 326 (PDF 321, 328) |
| `qi-focus-family` | Qi Focus | `included-component` | 1-6 | 1-4 | Force x 3, R | Force x 3,000 nuyen | Qi Focus subtype and contained adept-power profile. | `sr5-core` pp. 319, 326 (PDF 321, 328) |
| `spell-focus-family` | Spell Focus | `included-component` | 1-6 | 1-4 | Force x 3, R | Force x 4,000 nuyen | Exactly one spell-focus subtype and one spell category. | `sr5-core` pp. 319-320, 326 (PDF 321-322, 328) |
| `spirit-focus-family` | Spirit Focus | `included-component` | 1-6 | 1-4 | Force x 3, R | Force x 4,000 nuyen | Exactly one spirit-focus subtype and one spirit type. | `sr5-core` pp. 320, 326 (PDF 322, 328) |
| `weapon-focus-family` | Weapon Focus | `included-component` | 1-6 | 1-3 | Force x 4, R | Force x 7,000 nuyen plus base melee weapon | Weapon Focus subtype and one owned melee weapon. Both costs apply under `gear.weapon-focus-base-cost`. | `sr5-core` pp. 315, 320, 326 (PDF 317, 322, 328). Decision: `gear.weapon-focus-base-cost` |

### Focus Subtypes And Bonding

Bonding takes Force hours and requires an Awakened owner. At creation, bonded
focus count cannot exceed Magic and total bonded Force cannot exceed `Magic x 2`;
the more permissive career total is `Magic x 5`. Pay the subtype bonding Karma
once; do not also pay a family bonding cost. Only one focus can add Force to one
test. Sources: `sr5-core` pp. 98, 318-320 (PDF 100, 320-322).

| ID | Display name | Classification | Family | Bonding Karma | Required parameter, effect, and creation eligibility | Source |
| --- | --- | --- | --- | ---: | --- | --- |
| `alchemical-focus` | Alchemical Focus | `parameterized` | Enchanting | Force x 3 | Alchemy-capable path; adds Force dice to Alchemy tests. Creation Force 1-4. | `sr5-core` pp. 318-319, 326 (PDF 320-321, 328) |
| `disenchanting-focus` | Disenchanting Focus | `parameterized` | Enchanting | Force x 3 | Disenchanting-capable path; must contact artifact; adds Force dice to Disenchanting. Creation Force 1-4. | `sr5-core` pp. 318-319, 326 (PDF 320-321, 328) |
| `centering-focus` | Centering Focus | `creation-unavailable` | Metamagic | Force x 3 | Requires Centering metamagic; adds Force to initiate grade for Drain Resistance. Initiation is career progression. | `sr5-core` pp. 319, 324-326 (PDF 321, 326-328) |
| `flexible-signature-focus` | Flexible Signature Focus | `creation-unavailable` | Metamagic | Force x 3 | Requires Flexible Signature; adds Force to grade for the Assensing threshold effect. | `sr5-core` pp. 319, 325-326 (PDF 321, 327-328) |
| `masking-focus` | Masking Focus | `creation-unavailable` | Metamagic | Force x 3 | Requires Masking; adds Force to dice resisting Assensing and does not expand masked-focus count. | `sr5-core` pp. 319, 326 (PDF 321, 328) |
| `spell-shaping-focus` | Spell Shaping Focus | `creation-unavailable` | Metamagic | Force x 3 | Requires Spell Shaping; treats Magic as +Force for shaping amount. | `sr5-core` pp. 319, 326 (PDF 321, 328) |
| `power-focus` | Power Focus | `parameterized` | Power | Force x 6 | Adds Force to tests involving Magic, including Sorcery, Conjuring, and Enchanting. Creation Force 1-3. | `sr5-core` pp. 319, 326 (PDF 321, 328) |
| `qi-focus` | Qi Focus | `parameterized` | Qi | Force x 2 | Adept/mystic adept; required one valid adept-power profile. Force equals four times contained PP cost; while active grants/adds that profile, with no benefit from duplicate unranked power. Creation Force 1-4 and all normal power caps apply. | `sr5-core` pp. 319, 326 (PDF 321, 328) |
| `counterspelling-focus` | Counterspelling Focus | `parameterized` | Spell | Force x 2 | Required Combat, Detection, Health, Illusion, or Manipulation category; adds Force to Counterspelling attempts and spell-defense pool for that category. Counterspelling-capable path; Creation Force 1-4. | `sr5-core` pp. 319-320, 326 (PDF 321-322, 328) |
| `ritual-spellcasting-focus` | Ritual Spellcasting Focus | `parameterized` | Spell | Force x 2 | Required spell category; adds Force dice to Ritual Spellcasting. Non-spell rituals may use it; spell rituals must match category. Ritual Spellcasting-capable path; Creation Force 1-4. | `sr5-core` pp. 319-320, 326 (PDF 321-322, 328) |
| `spellcasting-focus` | Spellcasting Focus | `parameterized` | Spell | Force x 2 | Required spell category; adds Force dice to matching Spellcasting tests. Spellcasting-capable path; Creation Force 1-4. | `sr5-core` pp. 319-320, 326 (PDF 321-322, 328) |
| `sustaining-focus` | Sustaining Focus | `parameterized` | Spell | Force x 2 | Required spell category; sustains one matching spell whose Force is no higher than focus Force; cannot sustain a spell ritual. Spellcasting-capable path; Creation Force 1-4. | `sr5-core` pp. 319-320, 326 (PDF 321-322, 328) |
| `summoning-focus` | Summoning Focus | `parameterized` | Spirit | Force x 2 | Required spirit type available to the selected tradition; adds Force dice to matching Summoning attempts. Creation Force 1-4. | `sr5-core` pp. 320, 326 (PDF 322, 328) |
| `banishing-focus` | Banishing Focus | `parameterized` | Spirit | Force x 2 | Required spirit type; adds Force to the Banishing limit against that type. Banishing-capable path; Creation Force 1-4. | `sr5-core` pp. 320, 326 (PDF 322, 328) |
| `binding-focus` | Binding Focus | `parameterized` | Spirit | Force x 2 | Required spirit type available to the selected tradition; adds Force dice to matching Binding tests. Creation Force 1-4. | `sr5-core` pp. 320, 326 (PDF 322, 328) |
| `weapon-focus` | Weapon Focus | `parameterized` | Weapon | Force x 3 | Required owned melee weapon; adds Force dice to physical melee and Astral Combat. Astral damage uses the weapon with Charisma replacing Strength and may be Stun or Physical. Creation Force 1-3; base weapon and enchantment are both charged. | `sr5-core` pp. 315, 320, 326 (PDF 317, 322, 328). Decision: `gear.weapon-focus-base-cost` |

### Formulae And Magical Supplies

Purchasing a recorded formula does not itself grant a known formula. Priority
grants or 5 Karma buy the learned spell, ritual, or preparation subject to path
eligibility and the separate `Magic x 2` caps. Spell and alchemical versions are
separate learned formulae. Learning later requires a same-tradition lodge and a
formula or teacher; different-tradition instruction gives -4 dice. Sources:
`sr5-core` pp. 69, 98, 299, 304, 326 (PDF 71, 100, 301, 306, 328).
Decisions: `magic.priority-grant-formula-types`, `magic.formula-cap-scope`,
`magic.aspected-purchase-scope`.

| ID | Display name | Classification | Rating / quantity | Availability / legality | Cost | Required parameter, relationship, and creation eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- |
| `focus-formula` | Focus Formula | `parameterized` | Force 1-6 | Same as selected focus family | 25% of selected focus's Force-based enchantment cost | Required focus subtype, Force, physical form, and tradition. Formula is a separate owned record and does not include the focus/telesma. Its Force cannot exceed the creator's Magic for artificing; purchased creation eligibility also follows the corresponding focus family's Availability range. | `sr5-core` pp. 306-307, 326 (PDF 308-309, 328) |
| `combat-spell-formula` | Combat Spell Formula | `parameterized` | One target formula | 8R | 2,000 nuyen | Required one core Combat spell or its generated preparation profile and tradition; does not duplicate the learned selection. | `sr5-core` pp. 299, 304, 326 (PDF 301, 306, 328) |
| `detection-spell-formula` | Detection Spell Formula | `parameterized` | One target formula | 4R | 500 nuyen | Required one core Detection spell or its generated preparation profile and tradition. | `sr5-core` pp. 299, 304, 326 (PDF 301, 306, 328) |
| `health-spell-formula` | Health Spell Formula | `parameterized` | One target formula | 4R | 500 nuyen | Required one core Health spell or its generated preparation profile and tradition. | `sr5-core` pp. 299, 304, 326 (PDF 301, 306, 328) |
| `illusion-spell-formula` | Illusion Spell Formula | `parameterized` | One target formula | 8R | 1,000 nuyen | Required one core Illusion spell or its generated preparation profile and tradition. | `sr5-core` pp. 299, 304, 326 (PDF 301, 306, 328) |
| `manipulation-spell-formula` | Manipulation Spell Formula | `parameterized` | One target formula | 8R | 1,500 nuyen | Required one core Manipulation spell or its generated preparation profile and tradition. | `sr5-core` pp. 299, 304, 326 (PDF 301, 306, 328) |
| `magical-lodge-materials` | Magical Lodge Materials | `parameterized` | Force 1-6 | Force x 2 / none | Force x 500 nuyen | Required selected tradition and Force. Permanent lodge setup takes Force days, is stationary, acts as a Force mana barrier, shares the owner's astral signature, and is required for learning, rituals, and artificing as specified. Force 1-6 is creation-eligible. | `sr5-core` pp. 280, 299, 306, 326 (PDF 282, 301, 308, 328) |
| `reagents` | Reagents | `parameterized` | Positive integer drams | none | 20 nuyen per dram | Required reagent tradition and quantity. Same-tradition reagents work at full strength; other-tradition reagents generally work at half strength. Spending consumes them. Uses include changing listed magical limits, ritual offerings/Drain, binding, artificing, and temporary lodges. | `sr5-core` pp. 316-317, 326 (PDF 318-319, 328) |
| `temporary-magical-lodge` | Temporary Magical Lodge | `generated` | Force 1-6 | not applicable | Force reagent drams | Generated from owned reagents, not purchased separately. Takes Force hours and lasts until the next sunrise or sunset; consumes Force drams. | `sr5-core` p. 317 (PDF 319) |

## Lifestyles

Every character must select exactly one primary base lifestyle. Additional
residences/safehouses are allowed and each is paid in full. A paid lifestyle
record requires a positive integer prepaid month count; any number of months may
be prepaid and refunds are unlikely. Street is free and records no paid months.
The recurring cost receives the approved dwarf/troll lifestyle modifier. Sources:
`sr5-core` pp. 95, 373-375 (PDF 97, 375-377).

| ID | Display name | Classification | Base cost / month | Paid months | Starting-cash formula | Creation facts | Source |
| --- | --- | --- | ---: | --- | --- | --- | --- |
| `street-lifestyle` | Street Lifestyle | `selectable` | 0 nuyen | not applicable | 1D6 x 20 nuyen | May be primary/additional; no lifestyle options. | `sr5-core` pp. 95, 373-374 (PDF 97, 375-376) |
| `squatter-lifestyle` | Squatter Lifestyle | `parameterized` | 500 nuyen | 1 or more | 2D6 x 40 nuyen | May be primary/additional; options allowed. | `sr5-core` pp. 95, 373-374 (PDF 97, 375-376) |
| `low-lifestyle` | Low Lifestyle | `parameterized` | 2,000 nuyen | 1 or more | 3D6 x 60 nuyen | May be primary/additional; options allowed. | `sr5-core` pp. 95, 373-374 (PDF 97, 375-376) |
| `middle-lifestyle` | Middle Lifestyle | `parameterized` | 5,000 nuyen | 1 or more | 4D6 x 100 nuyen | May be primary/additional; options allowed. | `sr5-core` pp. 95, 373-374 (PDF 97, 375-376) |
| `high-lifestyle` | High Lifestyle | `parameterized` | 10,000 nuyen | 1 or more | 5D6 x 500 nuyen | May be primary/additional; options allowed. | `sr5-core` pp. 95, 373-374 (PDF 97, 375-376) |
| `luxury-lifestyle` | Luxury Lifestyle | `parameterized` | 100,000 nuyen | 1 or more | 6D6 x 1,000 nuyen | May be primary/additional; options allowed. The prose says `and up`; 100,000 is the listed creation base. | `sr5-core` pp. 95, 373-374 (PDF 97, 375-376) |

### Lifestyle Options

Each is a Boolean child of one Squatter-or-better lifestyle and cannot attach to
Street. Gamemaster plausibility approval is required. Percentage adjustments use
that residence's lifestyle cost; Special Work Area is fixed monthly. Options do
not change starting cash, which always uses the primary base tier under
`lifestyle.options-and-cash`. Source: `sr5-core` p. 374 (PDF 376).

| ID | Display name | Classification | Monthly adjustment | Effect | Source |
| --- | --- | --- | ---: | --- | --- |
| `special-work-area` | Special Work Area | `selectable` | +1,000 nuyen | Required authored workshop/garage/office/studio purpose; relevant tests performed there have Limit +2. | `sr5-core` p. 374 (PDF 376). Decision: `lifestyle.options-and-cash` |
| `extra-secure` | Extra Secure | `selectable` | +20% of lifestyle | HTR and similar security response rolls are one level better. | `sr5-core` p. 374 (PDF 376). Decision: `lifestyle.options-and-cash` |
| `obscure-difficult-to-find` | Obscure/Difficult to Find | `selectable` | +10% of lifestyle | Sneaking tests near the residence by anyone except its owner take -2 dice. | `sr5-core` p. 374 (PDF 376). Decision: `lifestyle.options-and-cash` |
| `cramped` | Cramped | `selectable` | -10% of lifestyle | Logic-linked skill-test Limits in the residence fall by 2, minimum 1. | `sr5-core` p. 374 (PDF 376). Decision: `lifestyle.options-and-cash` |
| `dangerous-area` | Dangerous Area | `selectable` | -20% of lifestyle | HTR and similar security response rolls are one level worse. | `sr5-core` p. 374 (PDF 376). Decision: `lifestyle.options-and-cash` |

### Lifestyle Bookkeeping

| ID | Display name | Classification | Rule | Source |
| --- | --- | --- | --- | --- |
| `primary-lifestyle` | Primary Lifestyle | `bookkeeping` | Exactly one of the six base tiers controls the required residence and starting-cash row. Additional lifestyles do not change starting cash. | `sr5-core` pp. 95, 373 (PDF 97, 375). Decision: `lifestyle.options-and-cash` |
| `lifestyle-prepaid-months` | Lifestyle Prepaid Months | `bookkeeping` | Purchase any positive integer number of months for each paid residence; cost is recurring monthly total x months. Prepayment is generally non-refundable. | `sr5-core` pp. 96-97, 374 (PDF 98-99, 376) |
| `permanent-lifestyle` | Permanent Lifestyle | `parameterized` | Required base tier and residence; cost equals 100 months' upkeep. It can later be lost through story events. This is a payment form, not a seventh tier. | `sr5-core` pp. 374-375 (PDF 376-377) |
| `team-lifestyle` | Team Lifestyle | `parameterized` | Required base tier and positive additional-person count; add 10% per additional person. Low or better requires one tenant of record. It is a shared payment form, not a base tier. | `sr5-core` p. 375 (PDF 377) |

## Fake Identities And Licenses

Fake SINs and licenses are digital records, not physical items. Every fake
license is a typed child of exactly one owned fake SIN and exactly one bounded
item/activity subject. License Rating is independent of parent SIN Rating; each
is verified using its own Rating. Subjects are bounded authored text because the
source examples are not a closed catalog. Sources: `sr5-core` pp. 367-368,
442-443 (PDF 369-370, 444-445). Decision: `identity.fake-license-link`.

| ID | Display name | Classification | Published rating | Creation rating | Availability / legality | Cost | Required fields and relationship | Source |
| --- | --- | --- | --- | --- | --- | ---: | --- | --- |
| `fake-sin` | Fake SIN | `parameterized` | 1-6 | 1-4 | Rating x 3, F | Rating x 2,500 nuyen | Required authored identity details and issuer/context. Rating 5-6 are creation-unavailable because Availability 15/18 exceeds 12. One SIN may own multiple separately purchased licenses. | `sr5-core` pp. 367-368, 442-443 (PDF 369-370, 444-445) |
| `fake-license` | Fake License | `parameterized` | 1-6 | 1-4 | Rating x 3, F | Rating x 200 nuyen | Required parent `fake-sin` and one specific Restricted item/activity subject. A separate license is required per subject. Rating 5-6 are creation-unavailable. Forbidden and unrestricted subjects are invalid because neither has an available license. | `sr5-core` pp. 367, 443 (PDF 369, 445). Decision: `identity.fake-license-link` |

Typical subject domains include one Restricted item, firearm possession,
concealed carry, hunting by weapon type, practicing magic/registered Awakened
status, one Combat spell, technomancy, or an occupation. These are examples and
validation categories, not separate purchasable catalog entries. Source:
`sr5-core` pp. 367, 443 (PDF 369, 445).

## Contacts

Contact identity and role are bounded authored text; example archetypes are not
a closed catalog. Ratings use natural Charisma for the free budget, not augmented
Charisma. Sources: `sr5-core` pp. 55, 95, 98-100 (PDF 57, 97, 100-102).

| ID | Display name | Classification | Cost / grant | Required fields and finalization limits | Source |
| --- | --- | --- | --- | --- |
| `free-contact-karma` | Free Contact Karma | `bookkeeping` | Natural Charisma x 3 dedicated Karma | May fund contact Connection/Loyalty only; augmentations do not increase it. Drafts may leave it unallocated, but finalization must spend all of it and it never converts to general Karma. | `sr5-core` pp. 95, 98 (PDF 97, 100). Decision: `contact.unused-free-karma` |
| `contact` | Contact | `parameterized` | 1 Karma per Connection point plus 1 per Loyalty point | Required authored identity/role, Connection 1-12, Loyalty 1-6. At creation, Connection + Loyalty must be no more than 7. Number of contacts is unlimited; general Karma may purchase ratings in addition to the dedicated grant. | `sr5-core` pp. 55, 98-100 (PDF 57, 100-102). Decision: `contact.creation-cap` |

## Final Resource Bookkeeping

These are dependency records for atomic finalization. The five Resource priority
cells remain authoritative in `PRIORITIES_METATYPES.md`; they are repeated here
only to make the resource ledger independently auditable.

| ID | Display name | Classification | Finalization rule | Source |
| --- | --- | --- | --- | --- |
| `priority-resources-a` | Resources A | `bookkeeping` | Grants 450,000 creation nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-b` | Resources B | `bookkeeping` | Grants 275,000 creation nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-c` | Resources C | `bookkeeping` | Grants 140,000 creation nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-d` | Resources D | `bookkeeping` | Grants 50,000 creation nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `priority-resources-e` | Resources E | `bookkeeping` | Grants 6,000 creation nuyen. | `sr5-core` pp. 65, 94 (PDF 67, 96) |
| `resources-budget` | Resources Budget | `bookkeeping` | Creation nuyen pays gear, identities, licenses, vehicles, magical equipment, and lifestyles. It cannot become Karma. Apply quantity, option, month, and metatype costs before checking balance. | `sr5-core` pp. 94-95 (PDF 96-97) |
| `karma-to-nuyen` | Karma-to-Nuyen Conversion | `bookkeeping` | Convert 0-10 general Karma once at 2,000 nuyen each, maximum 20,000 nuyen. Conversion is one-way and joins the creation resource budget. | `sr5-core` pp. 94, 101 (PDF 96, 103) |
| `gear-availability-cap` | Gear Availability Cap | `bookkeeping` | Numeric Availability must be <=12. Restricted requires a matching license; Forbidden has no license but is not excluded solely by its suffix. | `sr5-core` pp. 94, 416-419 (PDF 96, 418-421). Decision: `gear.legality-at-creation` |
| `gear-rating-force-cap` | Gear Rating/Force Cap | `bookkeeping` | Explicit purchasable Rating and Force must be <=6; do not apply this to vehicle stats, Sensor, Capacity, or quantity. | `sr5-core` pp. 94, 418 (PDF 96, 420). Decision: `gear.rating-cap-force` |
| `dwarf-resource-modifiers` | Dwarf Resource Modifiers | `bookkeeping` | Add 10% to gear cost and 20% to lifestyle cost. | `sr5-core` pp. 66, 94, 420 (PDF 68, 96, 422). Decision: `metatype.dwarf-costs` |
| `troll-resource-modifiers` | Troll Resource Modifiers | `bookkeeping` | Add 50% to gear cost and 100% to lifestyle cost. | `sr5-core` pp. 65-66, 94, 420 (PDF 67-68, 96, 422). Decision: `metatype.troll-costs` |
| `resource-nuyen-carryover` | Resource Nuyen Carryover | `bookkeeping` | Choose 0-5,000 unspent creation nuyen to carry into play; any excess is lost. | `sr5-core` pp. 94-95, 101 (PDF 96-97, 103). Decision: `carryover` |
| `karma-carryover` | Karma Carryover | `bookkeeping` | Choose 0-7 unspent general Karma to carry into play; remaining unspent Karma above 7 cannot carry. | `sr5-core` pp. 98, 101 (PDF 100, 103). Decision: `carryover` |
| `starting-nuyen-roll` | Starting Nuyen Roll | `generated` | Roll exactly once from the primary base lifestyle: Street `1D6 x 20`, Squatter `2D6 x 40`, Low `3D6 x 60`, Middle `4D6 x 100`, High `5D6 x 500`, Luxury `6D6 x 1,000`. Server rolls during atomic finalization and persists expression, dice, multiplier, and result. Options, extra residences, and prepaid months do not change the row. | `sr5-core` p. 95 (PDF 97). Decisions: `starting-cash.randomness`, `lifestyle.options-and-cash` |
| `starting-nuyen` | Starting Nuyen | `generated` | `resource-nuyen-carryover + starting-nuyen-roll result`; persist the immutable total. | `sr5-core` p. 95 (PDF 97). Decision: `starting-cash.randomness` |
| `focus-bonding-finalization` | Focus Bonding Finalization | `bookkeeping` | Every active starting focus must be owned and bonded; count <= Magic, total Force <= Magic x 2, each Force within purchase eligibility, and all bonding Karma paid. | `sr5-core` pp. 98, 318 (PDF 100, 320) |
| `contact-finalization` | Contact Finalization | `bookkeeping` | Allocate all natural Charisma x3 dedicated Contact Karma; each contact satisfies rating minima and combined cap; deduct any additional general Karma spent. | `sr5-core` pp. 55, 98-100 (PDF 57, 100-102). Decisions: `contact.creation-cap`, `contact.unused-free-karma` |
| `resource-final-balance` | Resource Final Balance | `bookkeeping` | Resource grant + converted nuyen must equal all adjusted purchase/lifestyle costs + selected carryover + discarded excess. Negative balance is invalid. | `sr5-core` pp. 94-95, 101 (PDF 96-97, 103) |
| `gm-resource-approval` | Gamemaster Resource Approval | `bookkeeping` | Every gear choice and the completed character require gamemaster approval before finalization. | `sr5-core` pp. 94, 103 (PDF 96, 105) |

## Explicit Exclusions And Source Discrepancies

| ID or family | Classification | Reason | Source |
| --- | --- | --- | --- |
| `vehicle-similar-models` | `excluded` | Branding examples are explicitly near-identical alternatives with discretionary price/stat tweaks, not deterministic distinct products. | `sr5-core` p. 462 (PDF 464) |
| `example-vehicle-loadouts` | `excluded` | Character examples and prose suggestions do not create separately priced product profiles. | `sr5-core` pp. 96-97, 462-466 (PDF 98-99, 464-468) |
| `hospitalized-lifestyle` | `excluded` | Temporary sickness/injury billing at 500 nuyen/day basic or 1,000/day intensive; characters cannot own it, so it is not a creation lifestyle. | `sr5-core` pp. 373-374 (PDF 375-376) |
| `legal-sin-license-acquisition` | `excluded` | Issuance/application depends on citizenship and jurisdiction; no deterministic purchasable creation product is given. Legal SINs arise from the separately cataloged SINner quality. | `sr5-core` pp. 367, 442-443 (PDF 369, 444-445) |
| `contact-examples` | `excluded` | Arms dealer, fixer, talismonger, and other examples support bounded authored contacts and are not a closed catalog. | `sr5-core` pp. 98, 386 (PDF 100, 388) |
| `qi-focus-tattoo-examples` | `excluded` | Named yantra are examples of physical form/power combinations, not separate mechanical focus products. | `sr5-core` p. 319 (PDF 321) |
| `categoryless-ritual-formula-merchandise` | `excluded` | Learning prose permits ritual formulae, but the priced goods table supplies only five spell-category rows and no Availability/cost mapping for rituals without a spell category. Creation learning remains available through priority/5 Karma; no uncited merchandise price is invented. | `sr5-core` pp. 299, 326 (PDF 301, 328) |
| `permanent-lifestyle-resale-and-missed-payments` | `excluded` | Career-state sale, debt, eviction, and downgrade procedures do not create initial-character options; purchase forms are retained above. | `sr5-core` pp. 374-375 (PDF 376-377) |
| `run-faster-vehicles-resources` | `excluded` | Every Run Faster item, lifestyle, identity, contact, and resource option is outside the approved source scope. | `run-faster` pp. 62-63 (PDF 64-65) |

Retained source discrepancies and omissions:

- The aircraft table abbreviates/misspells `Fed-Boing Commuter`; prose and the
  similar-model list identify Federated Boeing. The canonical display name is
  Federated Boeing Commuter: `sr5-core` pp. 462, 465 (PDF 464, 467).
- The Nissan Hound includes two `weapon mounts` but does not state standard or
  heavy. The ledger retains unknown mount weight/capacity on that included child
  rather than selecting a type: `sr5-core` p. 465 (PDF 467).
- The GMC Banshee prose mentions armaments but gives no mount/weapon count. No
  included component is generated: `sr5-core` p. 465 (PDF 467).
- The core formula table labels all five priced category rows `Spell Formula`,
  while learning prose also discusses ritual and alchemical formulae. The five
  priced rows support spells and their category-inheriting preparations; no
  price is invented for categoryless rituals: `sr5-core` pp. 299, 326
  (PDF 301, 328).
- The Luxury prose says 100,000 nuyen per month `and up`, but the creation table
  gives 100,000. The ledger uses that deterministic listed base and creates no
  open surcharge: `sr5-core` pp. 95, 373 (PDF 97, 375).
- The printed gear examples apply the troll's +100% lifestyle modifier while
  other creation prose says +50% to gear/lifestyle. The approved result is +50%
  gear and +100% lifestyle; dwarf is +10% gear and +20% lifestyle:
  `sr5-core` pp. 65-67, 94, 97, 420 (PDF 67-69, 96, 99, 422).

## Review Footer

### Reviewed Printed/PDF Page Ranges

- `sr5-core` pp. 55, 65-66, 94-103 (PDF 57, 67-68, 96-105): contacts,
  resource grants, spending, months, starting cash, carryover, finalization, and
  metatype modifiers.
- `sr5-core` pp. 198-203 (PDF 200-205): vehicle attributes, generated Condition
  Monitors, Pilot/Sensor semantics, and profile rules.
- `sr5-core` pp. 280, 299, 304, 306-307, 315-320, 324-326
  (PDF 282, 301, 306, 308-309, 317-322, 326-328): lodges, formula learning,
  preparations, focus formulae, reagents, every focus family/subtype, bonding,
  initiation exclusion, and magical-goods prices.
- `sr5-core` pp. 367-368, 373-375 (PDF 369-370, 375-377): fake identity
  semantics and every lifestyle/option/payment form.
- `sr5-core` pp. 416-420, 442-443, 461-466 (PDF 418-422, 444-445, 463-468):
  legality, identification product formulas, every vehicle modification,
  vehicle, drone, component, and magical-equipment table row.
- `run-faster` pp. 62-63 (PDF 64-65): scope exclusion only; no catalog entry
  from this source is included here.

### Approved-PDF Entry Counts

Counts include stable-ID rows in this file. The five repeated Resource priority
dependencies are counted here and are not five new catalog products.

| Inventory | Count | Classification reconciliation |
| --- | ---: | --- |
| Vehicle modifications | 4 | 2 `parameterized`, 1 `selectable`, 1 `creation-unavailable` |
| Bikes | 4 | 4 `selectable` |
| Cars | 7 | 5 `selectable`, 2 `creation-unavailable` |
| Trucks/vans | 4 | 4 `selectable` |
| Watercraft | 5 | 4 `selectable`, 1 `creation-unavailable` |
| Aircraft | 9 | 7 `selectable`, 2 `creation-unavailable` |
| Drones | 11 | 11 `selectable` |
| Included/generated vehicle children | 12 | 11 `included-component`, 1 `generated` |
| Focus purchase families | 7 | 7 `included-component` pricing parents |
| Focus subtypes | 16 | 12 `parameterized`, 4 `creation-unavailable` |
| Formulae and magical supplies | 9 | 8 `parameterized`, 1 `generated` |
| Base lifestyles | 6 | 1 `selectable`, 5 `parameterized` |
| Lifestyle options | 5 | 5 `selectable` |
| Lifestyle bookkeeping/forms | 4 | 2 `bookkeeping`, 2 `parameterized` |
| Fake identities/licenses | 2 | 2 `parameterized` |
| Contacts | 2 | 1 `bookkeeping`, 1 `parameterized` |
| Final resource bookkeeping | 19 | 17 `bookkeeping`, 2 `generated` |
| Explicit excluded families | 9 | 9 `excluded` |
| **Total stable-ID rows** | **135** | 42 `selectable`, 32 `parameterized`, 18 `included-component`, 4 `generated`, 20 `bookkeeping`, 10 `creation-unavailable`, 9 `excluded`; repeated dependency rows included |

Vehicle product reconciliation: 29 vehicles + 11 drones = **40** priced model
rows, exactly matching the approved core tables. Creation eligibility is 35
selectable and 5 creation-unavailable. The separately generated Flying Eye
payload child does not increase the base-model count.

### Genuine Source Gaps And Adjudicated Differences

- The Nissan Hound's two included mount types are genuinely unspecified. The
  parent profile is complete, but runtime data must preserve an unspecified
  included-mount kind unless the owner approves an interpretation.
- No deterministic merchandise cost/Availability is supplied for a ritual
  formula lacking a spell category. Such merchandise is explicitly excluded;
  initial learned rituals remain complete through priority/Karma selection.
- Banshee `armaments` are descriptive only; no count or generated component is
  supported. This is not a missing table product.
- Runtime entries: 0. Missing runtime entries: 126 non-excluded reviewed rows.
  Unexpected runtime entries: 0. Runtime absence is expected until CHAR-802.

### Remaining Unknown Facts

None for permitted initial-character selections or calculations. The two genuine
source omissions above are represented as an unspecified included component and
an explicit merchandise exclusion, not silently guessed values. If CHAR-802
requires a concrete Nissan Hound mount type or categoryless ritual-formula shop
price, that would require a new approved owner decision.

### Runtime Reconciliation Status

`Not implemented`. CHAR-802 must materialize this reviewed inventory, validate
all parent/child references and parameter domains, and reconcile exact IDs and
counts before catalog version `1.0.0` is published.
