# SR5 Catalog Completeness Ledger

This ledger reconciles three sets required by the rules baseline:

1. Approved core-PDF inventory.
2. Project runtime catalog.
3. Explicit exclusions.

The detailed category ledgers verify option identities, source locations, costs,
ratings, effects, prerequisites, included components, generated profiles, and
explicit exclusions. All remaining source conflicts and omissions have approved
decisions. CHAR-802 published the priority-assignment foundation; later feature
slices publish and reconcile their complete option facts.

## Status

As of 2026-08-28 the runtime catalog was consolidated into a single mutable
pre-alpha development schema (`sr5-core` `1.0.0`); see
[`SR5_RULESET_MANIFEST.md`](SR5_RULESET_MANIFEST.md) "Schema Lifecycle". The
`catalog version sr5-core X.Y.Z` references throughout this ledger (including
below) are historical authorship markers for when content was reviewed and
added — they no longer name distinct retained runtime resources, and digest
pinning is not enforced during this phase. Runtime counts below reflect the
consolidated `1.0.0` resource, which is the union of everything previously
published across those historical version markers.

| Category | Approved-PDF identity review | Detailed fact review | Runtime catalog | Reconciliation |
| --- | --- | --- | --- | --- |
| Priority rows and metatypes | Complete | Complete | Methods, levels, categories, 25 cell identities, and 5 metatypes published | Reconciled (CHAR-806 complete) |
| Qualities | Complete: 31 positive, 28 negative (`sr5-core`); 43 positive, 41 negative (`run-faster`); 10 positive, 3 negative (`run-gun`); 9 positive, 2 negative (`better-than-bad`) | Complete | 168 qualities published: 59 `sr5-core` (CHAR-807), 85 `run-faster` (1 `poor-self-control-vindictive` under CHAR-813, 84 new under CHAR-814), 13 `run-gun` (CHAR-815), 11 `better-than-bad` (CHAR-819) | Reconciled (CHAR-807, CHAR-813, CHAR-814, CHAR-815, CHAR-819 complete) |
| Active skills and groups | Complete: 75 skills, 15 groups | Complete | 75 skills, 15 groups published | Reconciled (CHAR-807 complete) |
| Knowledge and languages | Complete as open-authored categories | Complete | 4 open categories published | Reconciled (CHAR-807 complete) |
| Magic and Resonance | Complete identity pass | Complete | 6 paths, 3 aspected values, 2 traditions, 84 spells, 9 rituals, 27 adept powers (25 `sr5-core` + 2 `better-than-bad`, CHAR-820), 16 mentor spirits, 20 complex forms, 6 spirit types, 5 sprite types published | Reconciled (CHAR-808, CHAR-820 complete) |
| Weapons and armor | Complete identity pass | Complete | 157 weapons published: 77 `sr5-core` (includes grapple gun, micro flare launcher, monofilament chainsaw), 80 `run-gun` (73 base + 7 generated alternate-configuration profiles, CHAR-816, see [`sr5-catalog/RUN_GUN_WEAPONS.md`](sr5-catalog/RUN_GUN_WEAPONS.md)); 12 armor (adds Leather Jacket/Duster); ammunition/arrow-bolt/explosive/grenade/rocket line items reviewed under CHAR-812 were never actually wired into the runtime `gear` array (confirmed by direct inspection during CHAR-816: 0 of 141 base `gear` entries match ammo/grenade/explosive/rocket/arrow/bolt patterns) — this row's "published in `gear`" claim for those items is stale and needs its own follow-up ticket | Weapons reconciled (CHAR-812, CHAR-816 complete); ammunition/grenade/explosive/rocket claim requires re-reconciliation; Autosofts excluded per `gear.autosoft-pricing-absent`, weapon-ammo damage linkage deferred per `gear.ammunition-grenade-rocket-linkage` |
| Electronics and software | Complete identity pass | Complete | Commlinks (7), a new typed `cyberdecks` catalog (9), electronics accessories, RFID tags, communications/countermeasures, software, and skillsofts published in `gear`/`cyberdecks`; Capacity-scaled optical/audio/sensor hosts and their enhancements published under CHAR-809A | Reconciled; cyberdeck program slots and Autosoft pricing remain excluded per `SR5_RULE_DECISIONS.md` |
| General gear and consumables | Complete identity pass | Complete | 100+ items published across credsticks, tools, fixed-capacity optical devices, security devices, restraints, breaking-and-entering gear, industrial chemicals, survival gear, biotech, DocWagon contracts, and slap patches; drugs (10 types), toxins (9 types), and BTL chips (4 types) published in `gear` under CHAR-812 | Reconciled (CHAR-809, CHAR-812 complete) |
| Augmentations | Complete identity pass | Complete | 95 augmentations across 5 grades (standard, alphaware, betaware, deltaware, used); adds Ocular Drone, External Clip Port, and cybergun Laser Sight/Silencer implant-weapon accessories; 7 existing headware/earware entries gained missing `capacityCost` fields | Reconciled (CHAR-809, CHAR-812 complete); Cyberlimb Customization implemented post-CHAR-812, see `ware.cyberlimb-customization-unmodeled` |
| Vehicles and drones | Complete identity pass | Complete | 40 items published: 4 bikes, 7 cars, 4 trucks/vans, 3 boats, 2 submarines, 9 aircraft, 11 drones; vehicle modifications (rigger interface, standard/heavy weapon mounts, manual operation) published under CHAR-809A; two citation fixes (Harley-Davidson Scorpion, Horizon Flying-Eye) applied under CHAR-812 | Reconciled |
| Magical equipment | Complete identity pass | Complete | 16 foci published under CHAR-808, now with Force-scaled cost/Availability/`focusCategoryId` added under CHAR-812; reagents and magical lodge materials published in `gear`; 5 spell formulae published as parameterized purchases without known-spell linkage | Reconciled; focus pricing has no purchase/evaluator flow yet (`focus.pricing-added-no-purchase-flow`), and focus formulae/spell-formula-to-known-spell linkage remain deferred |
| Accessories, modifications, and capacity hosts | Complete identity pass | Complete | Firearm mounts, armor Capacity, device Capacity, augmentation/cyberlimb Capacity, and vehicle weapon mounts all implemented: 17 weapon accessories (`weaponAccessories`), 7 armor modifications (`armorModifications`), 3 cyberlimb enhancements (`cyberlimbEnhancements`), 4 vehicle modifications (`vehicleModifications`), plus Capacity-cost fields on gear/augmentation entries, all consumed by `GearAttachmentEvaluator` | Reconciled (CHAR-809A complete) |
| Contacts, identities, lifestyles | Complete identity pass | Complete | Free-form contacts (Karma-priced, not a catalog collection); `fake-sin`/`fake-license` gear items; 6 lifestyle tiers and 5 lifestyle options published in `lifestyleTiers`/`lifestyleOptions` | Reconciled (CHAR-810 complete) |

## Classification

| Status | Meaning |
| --- | --- |
| `selectable` | Independently selected or purchased during creation. |
| `parameterized` | Selectable family requiring a rating, subject, host, or other typed parameter. |
| `included-component` | Bundled with a parent and not independently charged in that instance. |
| `generated` | Profile or record derived from another selection. |
| `bookkeeping` | Balance, allocation provenance, quantity, attachment, or ownership state. |
| `creation-unavailable` | Core entry that fails creation limits or requires career progression. |
| `excluded` | Outside approved scope, an alias/example, or unsupported custom content. |

## Priority And Metatypes

Priority table source: `sr5-core` p. 65 (PDF 67).

| Priority | Metatype special points | Attributes | Magic/Resonance paths | Skills | Resources |
| --- | --- | ---: | --- | --- | ---: |
| A | Human 9; Elf 8; Dwarf 7; Ork 7; Troll 5 | 24 | Magician, Mystic Adept, Technomancer | 46/10 | 450,000 nuyen |
| B | Human 7; Elf 6; Dwarf 4; Ork 4; Troll 0 | 20 | Magician, Mystic Adept, Technomancer, Adept, Aspected Magician | 36/5 | 275,000 nuyen |
| C | Human 5; Elf 3; Dwarf 1; Ork 0 | 16 | Magician, Mystic Adept, Technomancer, Adept, Aspected Magician | 28/2 | 140,000 nuyen |
| D | Human 3; Elf 0 | 14 | Adept, Aspected Magician | 22/0 | 50,000 nuyen |
| E | Human 1 | 12 | Mundane | 18/0 | 6,000 nuyen |

Metatype IDs: `human`, `elf`, `dwarf`, `ork`, `troll`.

Metatype attribute ranges and traits: `sr5-core` pp. 65-66 (PDF 67-68).

## Qualities

Positive quality inventory, including parameters and levels:
`sr5-core` pp. 71-77 (PDF 73-79).

```text
ambidextrous, analytical-mind, aptitude, astral-chameleon, bilingual,
blandness, catlike, codeslinger, double-jointed, exceptional-attribute,
first-impression, focused-concentration, gearhead, guts,
high-pain-tolerance, home-ground, human-looking, indomitable, juryrigger,
lucky, magic-resistance, mentor-spirit, natural-athlete, natural-hardening,
natural-immunity, photographic-memory, quick-healer,
resistance-to-pathogens-toxins, spirit-affinity, toughness, will-to-live
```

Negative quality inventory, including parameters and levels:
`sr5-core` pp. 77-87 (PDF 79-89).

```text
addiction, allergy, astral-beacon, bad-luck, bad-rep, code-of-honor,
codeblock, combat-paralysis, dependents, distinctive-style, elf-poser,
gremlins, incompetent, insomnia, loss-of-confidence, low-pain-tolerance,
ork-poser, prejudiced, scorched, sensitive-system, simsense-vertigo,
sinner, social-stress, spirit-bane, uncouth, uneducated, unsteady-hands,
weak-immune-system
```

Open parameters such as a prejudice target or code are typed bounded text, not
new catalog options. Closed subtypes and ratings remain catalog facts.

`run-faster` positive quality inventory (CHAR-813's `poor-self-control-vindictive`
plus CHAR-814): `run-faster` p. 86 (PDF 88), pp. 145-151 (PDF 147-153).

```text
rank, adrenaline-surge, animal-empathy, black-market-pipeline, born-rich,
city-slicker, college-education, common-sense, daredevil,
digital-doppelganger, disgraced, erased, fame, friends-in-high-places,
hawk-eye, inspired, jack-of-all-trades, lightning-reflexes, linguist,
made-man, night-vision, outdoorsman, overclocker, perceptive, perfect-time,
poor-link, privileged-family-name, restricted-gear, school-of-hard-knocks,
sense-of-direction, sensei, solid-legendary-rep, speed-reading,
spike-resistance, spirit-whisperer, steely-eyed-wheelman,
technical-school-education, tough-as-nails, trust-fund, trustworthy,
vehicle-empathy, water-sprite, witness-my-hate
```

`run-faster` negative quality inventory: `run-faster` pp. 151-159 (PDF 153-161).

```text
albinism, amnesia, asthma, bi-polar, big-regret, blind, borrowed-time,
computer-illiterate, creature-of-comfort, day-job, deaf,
did-you-just-call-me-dumb, dimmer-bulb, driven, emotional-attachment,
ex-con, flashbacks, hobo-with-a-shotgun, hung-out-to-dry, illiterate,
in-debt, incomplete-deprogramming, infirm, liar, night-blindness,
oblivious, pacifist, paranoia, paraplegic, phobia, pie-iesu-domine,
poor-self-control-braggart, poor-self-control-thrill-seeker,
poor-self-control-compulsive, poor-self-control-vindictive,
poor-self-control-combat-monster, records-on-file, reduced-sense,
sensory-overload-syndrome, signature, vendetta, wanted
```

`run-gun` positive quality inventory (CHAR-815): `run-gun` p. 127 (PDF 129),
p. 169 (PDF 171).

```text
acrobatic-defender, agile-defender, brand-loyalty, one-trick-pony,
perceptive-defender, sharpshooter, too-pretty-to-hit, radiation-sponge,
rad-tolerant, spacer
```

`run-gun` negative quality inventory (CHAR-815): `run-gun` p. 127 (PDF 129),
p. 169 (PDF 171).

```text
combat-junkie, blighted, earther
```

## Skills

Active-skill descriptions and canonical list: `sr5-core` pp. 130-151
(PDF 132-153). Skill groups: `sr5-core` pp. 90, 153 (PDF 92, 155).

```text
aeronautics-mechanic, alchemy, animal-handling, arcana, archery, armorer,
artisan, artificing, assensing, astral-combat, automatics,
automotive-mechanic, banishing, binding, biotechnology, blades, chemistry,
clubs, compiling, computer, con, counterspelling, cybercombat,
cybertechnology, decompiling, demolitions, disguise, disenchanting, diving,
electronic-warfare, escape-artist, etiquette, exotic-melee-weapon,
exotic-ranged-weapon, first-aid, forgery, free-fall, gunnery, gymnastics,
hacking, hardware, heavy-weapons, impersonation, industrial-mechanic,
instruction, intimidation, leadership, locksmith, longarms, medicine,
nautical-mechanic, navigation, negotiation, palming, perception,
performance, pilot-aerospace, pilot-aircraft, pilot-exotic-vehicle,
pilot-ground-craft, pilot-walker, pilot-watercraft, pistols, registering,
ritual-spellcasting, running, sneaking, software, spellcasting, summoning,
survival, swimming, throwing-weapons, tracking, unarmed-combat
```

Skill groups:

```text
acting, athletics, biotech, close-combat, conjuring, cracking, electronics,
enchanting, engineering, firearms, influence, outdoors, sorcery, stealth,
tasking
```

Knowledge skills use authored name plus category `academic`, `interests`,
`professional`, or `street`. Languages use an authored language name; common
language lists are examples rather than a closed catalog. Sources:
`sr5-core` pp. 89-91, 147-150 (PDF 91-93, 149-152).

## Magic And Resonance

Paths: `mundane`, `magician`, `mystic-adept`, `adept`,
`aspected-magician`, `technomancer`. Aspected choices are `sorcery`,
`conjuring`, and `enchanting`. Sources: `sr5-core` pp. 65, 68-70
(PDF 67, 70-72).

Core traditions: `hermetic`, `shamanic`. Sources: `sr5-core` pp. 279-280
(PDF 281-282).

Spell inventory: `sr5-core` pp. 283-294 (PDF 285-296). Parameterized spell
families retain their typed subject rather than generating arbitrary catalog IDs.

```text
acid-stream, toxic-wave, punch, clout, blast, death-touch, manabolt,
manaball, flamethrower, fireball, lightning-bolt, ball-lightning, shatter,
powerbolt, powerball, knockout, stunbolt, stunball, analyze-device,
analyze-magic, analyze-truth, clairaudience, clairvoyance, combat-sense,
detect-enemies, detect-enemies-extended, detect-individual, detect-life,
detect-life-extended, detect-life-form, detect-life-form-extended,
detect-magic, detect-magic-extended, detect-object, mindlink, mind-probe,
antidote, cure-disease, decrease-attribute, detox, heal,
increase-attribute, increase-reflexes, oxygenate, prophylaxis, resist-pain,
stabilize, agony, mass-agony, bugs, swarm, confusion, mass-confusion,
chaos, chaotic-world, entertainment, trid-entertainment, invisibility,
improved-invisibility, mask, physical-mask, phantasm, trid-phantasm, hush,
silence, stealth, animate, mass-animate, armor, control-actions,
mob-control, control-thoughts, mob-mind, fling, ice-sheet, ignite,
influence, levitate, light, magic-fingers, mana-barrier, physical-barrier,
poltergeist, shadow
```

Rituals: `curse`, `prodigal-spell`, `remote-sensing`, `ward`,
`circle-of-protection`, `circle-of-healing`, `renascence`, `watcher`,
`homunculus`. Sources: `sr5-core` pp. 295-299 (PDF 297-301).

Adept powers: `sr5-core` pp. 308-311 (PDF 310-313).

```text
adrenaline-boost, astral-perception, attribute-boost, combat-sense,
critical-strike, danger-sense, enhanced-perception, enhanced-accuracy,
improved-ability, improved-physical-attribute, improved-potential,
improved-reflexes, improved-sense, killing-hands, kinesics, light-body,
missile-parry, mystic-armor, natural-immunity, pain-resistance,
rapid-healing, spell-resistance, traceless-walk, voice-control, wall-running
```

Mentor spirits: `sr5-core` pp. 320-324 (PDF 322-326).

```text
bear, cat, dog, dragonslayer, eagle, fire-bringer, mountain, rat, raven,
sea, seducer, shark, snake, thunderbird, wise-warrior, wolf
```

Complex forms: `sr5-core` pp. 252-253 (PDF 254-255).

```text
cleaner, diffusion-of-attack, diffusion-of-sleaze,
diffusion-of-data-processing, diffusion-of-firewall, editor,
infusion-of-attack, infusion-of-sleaze, infusion-of-data-processing,
infusion-of-firewall, static-veil, pulse-storm, puppeteer,
resonance-channel, resonance-spike, resonance-veil, static-bomb, stitches,
transcendent-grid, tattletale
```

## Equipment Inventory

The detailed category ledgers cover the core product tables, row-level statistics,
and operative prose effects. The weapons/armor-related owner decisions this
inventory previously awaited are now resolved and recorded in
`SR5_RULE_DECISIONS.md` (the `gear.*` and `ware.*` entries). CHAR-812 closed
the final reconciliation pass: weapons, armor, electronics/software, general
gear, augmentations, vehicles/drones, and magical supplies are all reconciled
against the reviewed inventory, with the remaining gaps (Autosoft pricing,
weapon-ammo damage linkage, focus purchase flow, cyberdeck program slots,
focus-formula/spell-formula linkage) recorded as deliberate exclusions in
`SR5_RULE_DECISIONS.md` rather than left unexplained (see the Status table
above). Cyberlimb Customization was excluded at CHAR-812 time but has since
been implemented (`ware.cyberlimb-customization-unmodeled` in
`SR5_RULE_DECISIONS.md` records the resolution) and is no longer a gap.

Electronics, general gear, and magical-supplies items that fit the existing
`gear` catalog shape (commlinks, accessories, RFID tags, communications and
countermeasures, software, skillsofts, credsticks, tools, fixed-capacity
optical devices, security devices, restraints, breaking-and-entering gear,
industrial chemicals, survival gear, biotech, DocWagon contracts, slap
patches, reagents, and magical lodge materials) were added directly to `gear`
rather than as new top-level catalog collections, distinguished by
`categoryId`. Cyberdecks needed fields no existing record type carries (device
Rating, a four-value Attack/Sleaze/Data-Processing/Firewall array, and program
slots), so they are a new typed `cyberdecks` collection with its own loader
validation and evaluator resolution branch. Grapple guns, micro flare
launchers, and monofilament chainsaws are dual-classified as both purchasable
gear and combat-capable weapons in the source; following the existing
`ballistic-shield`/`riot-shield` precedent (armor entries with no separate
weapons-catalog entry — note that unlike these three items, the shields carry
no combat stat fields of their own; their bash/block use is narrative, not
modeled data), they were added once to `weapons` rather than duplicated into
`gear`.

Capacity-scaled host devices were explicitly out of CHAR-809 scope: the core
text describes optical, audio, and sensor host devices (binoculars, cameras,
contacts, glasses, goggles, sensor housings) as purchased at a chosen Capacity
with their cost derived from that Capacity, and CHAR-809A's own scope text
claims "device Capacity for optical, audio, and sensor hosts" verbatim. Only
fixed-cost, non-Capacity-range items from those tables (Binoculars/Optical,
Micro-Camera, Endoscope, Imaging Scope, Periscope, Mage Sight Goggles) were
published under CHAR-809; the remaining variable-Capacity host devices and all
vision/audio enhancements, along with augmentation/cyberlimb Capacity and
vehicle weapon mounts/rigger interfaces, are now published under CHAR-809A,
which the ledger's capacity/mount rule table below already sources to the
same pages.

| Category | Source |
| --- | --- |
| Weapons, accessories, ammunition, explosives | `sr5-core` pp. 422-436 (PDF 424-438) |
| Armor and armor modifications | `sr5-core` pp. 436-438 (PDF 438-440) |
| Commlinks, decks, electronics, sensors | `sr5-core` pp. 438-446 (PDF 440-448) |
| Programs and autosofts | `sr5-core` pp. 243-246, 269-270 (PDF 245-248, 271-272) |
| Security, survival, medical, and general gear | `sr5-core` pp. 442-451 (PDF 444-453) |
| Drugs, BTL, and toxins | `sr5-core` pp. 408-414 (PDF 410-416) |
| Cyberware and bioware | `sr5-core` pp. 451-461 (PDF 453-463) |
| Vehicles and drones | `sr5-core` pp. 461-466 (PDF 463-468) |
| Foci, formulae, lodges, and reagents | `sr5-core` pp. 318-320, 326, 461 (PDF 320-322, 328, 463) |
| Lifestyles | `sr5-core` pp. 373-375 (PDF 375-377) |
| Fake SINs and licenses | `sr5-core` pp. 367-368, 442-443 (PDF 369-370, 444-445) |

Equipment must model included components separately from shop choices. Examples
include integral firearm accessories, an Ares Alpha launcher, commlink baseline
functions, cyberdeck hot-sim, cybereye image link/camera, control-rig connectors,
cybergun smartlinks, and installed vehicle/drone mounts. The cited product row and
description are authoritative for each inclusion.

Hosted attachments are a separate catalog concern from the hosts themselves and
were delivered by CHAR-809A: firearm mounts, armor Capacity, augmentation/
cyberlimb Capacity, optical/audio/sensor device Capacity, and vehicle weapon
mounts are all implemented. The governing capacity and mount rules are:

| Rule | Source |
| --- | --- |
| Capacity defines the slots of accessories an item can hold | `sr5-core` p. 417 (PDF 419) |
| Firearm mounts are top, barrel, and underbarrel; one accessory each; integral accessories consume none | `sr5-core` p. 417 (PDF 419) |
| Firearm accessory table with per-accessory mount, Availability, and cost | `sr5-core` p. 431 (PDF 433) |
| Armor Capacity equals Armor Rating; modifications carry fixed or `[Rating]` Capacity costs | `sr5-core` p. 437 (PDF 439) |
| Optical, audio, and sensor hosts are bought at a chosen Capacity; enhancements consume it | `sr5-core` p. 444 (PDF 446) |
| Bracketed Capacity costs let an item sit in a cyberlimb instead of costing Essence | `sr5-core` pp. 451, 454 (PDF 453, 456) |
| Cyberlimb enhancements consume limb Capacity; one each of Agility, Armor, Strength | `sr5-core` p. 456 (PDF 458) |
| Vehicle weapon mounts equal unaugmented Body ÷ 3; heavy mounts count as two | `sr5-core` p. 461 (PDF 463) |

## Explicit Exclusions

| Entry or family | Classification reason | Source |
| --- | --- | --- |
| Street-Level and Prime Runner creation | Alternate creation tier | `sr5-core` p. 64 (PDF 66) |
| Point Buy and Life Modules | Run Faster method outside approved Sum-to-Ten scope | `run-faster` p. 62 (PDF 64) |
| All Run Faster catalogs | Only allocation rules are approved | `run-faster` pp. 62-63 (PDF 64-65) |
| Herding active skill | Detail chapter treats Herding as an Animal Handling specialization; canonical list omits it | `sr5-core` pp. 90, 143, 151 (PDF 92, 145, 153) |
| Lockpicking active skill | No detail entry; Locksmith covers locks; canonical list omits it | `sr5-core` pp. 90, 145, 151 (PDF 92, 147, 153) |
| Arcane active skill | Typographic label; described skill is Arcana | `sr5-core` pp. 142, 151 (PDF 144, 153) |
| Enchanting active skill | Group/category label; described member is Artificing | `sr5-core` pp. 90, 142, 151, 153 (PDF 92, 144, 153, 155) |
| Custom active skills, traditions, and mentors | GM-authored with no deterministic core construction rules | `sr5-core` pp. 147, 279, 320 (PDF 149, 281, 322) |
| Knowledge/language examples as fixed catalogs | Explicitly open-authored subjects | `sr5-core` pp. 89-91, 147-150 (PDF 91-93, 149-152) |
| Ram spell/preparation | Example references a spell absent from the core spell inventory | `sr5-core` p. 306 (PDF 308) |
| Initiation, metamagics, Submersion, and echoes | Career progression | `sr5-core` pp. 257-258, 324-326 (PDF 259-260, 326-328) |
| Toxic/blood magic paths | Setting material without a core creation path | `sr5-core` pp. 277-278 (PDF 279-280) |
| Similar-model and example loadout rows | Alias/example, not a distinct published mechanical product | `sr5-core` pp. 96-97, 462 (PDF 98-99, 464) |
| Full-body armor and other Availability-over-12 variants | Core item retained as creation-unavailable, not selectable | `sr5-core` pp. 94, 436-438 (PDF 96, 438-440) |

## Reconciliation Report

CHAR-815 added Run & Gun as a fourth approved source and published its 13
new qualities (10 positive, 3 negative) as catalog version `sr5-core`
`1.5.0`. See [`sr5-catalog/RUN_GUN_QUALITIES.md`](sr5-catalog/RUN_GUN_QUALITIES.md).
It is the first slice of a broader, project-owner-approved Run & Gun
expansion; Run & Gun's Martial Arts subsystem and several GM-procedural
chapters were reviewed and are not part of this or any scheduled slice yet
— see [`SR5_CATALOG_DEFERRED_WORK.md`](SR5_CATALOG_DEFERRED_WORK.md).

CHAR-816 published the second slice: Run & Gun's Arsenal chapter weapons
(73 base products across 21 categories, 4 newly introduced — laser weapons,
flamethrowers, harpoon guns, slingshots — plus 7 generated
alternate-configuration profiles, 80 new entries total) as catalog version
`sr5-core` `1.6.0`, bringing the runtime to 157 total weapons. See
[`sr5-catalog/RUN_GUN_WEAPONS.md`](sr5-catalog/RUN_GUN_WEAPONS.md). Weapon
Accessories' new 6-slot mounting system was reviewed and deferred as a
future CHAR-817 candidate (it needs a `WeaponMount` schema change and new
evaluator logic, not just catalog rows). The AMMO section and Arrowheads
were also deferred at the time, on the mistaken belief that no base
ammunition catalog existed in the runtime to extend; that belief was wrong
(CHAR-812 had already published ammunition/arrow/bolt gear in `1.4.0`) and
was corrected before CHAR-818 shipped — see the CHAR-817/CHAR-818 paragraph
below. Improvised Melee Weapons remains reviewed and excluded outright — see
[`SR5_CATALOG_DEFERRED_WORK.md`](SR5_CATALOG_DEFERRED_WORK.md). Armor, gear,
and explosives remain tracked as separate follow-up CHAR-8xx tickets.

CHAR-817 and CHAR-818 published the third slice, correcting the CHAR-816
deferral above: Weapon Accessories (33 new items, plus 17 already-published
`sr5-core` accessories carried forward, 50 total) and the new AMMO/Arrowhead
gear (12 items: 5 AMMO, 7 Arrowheads) as catalog version `sr5-core` `1.7.0`.
The existing `WeaponAccessoryDefinition`/`WeaponMount`/
`GearAttachmentEvaluator` mount system needed only a backward-compatible
extension — three new `WeaponMount` values (Side, Internal, Stock), an
`AdditionalMounts` list generalizing the old two-way `TopOrUnderbarrel`
combinator into an N-way choice, and a `RestrictedToWeaponCategoryIds`
allow-list — not a new subsystem. See
[`sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md`](sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md)
and [`sr5-catalog/RUN_GUN_AMMO.md`](sr5-catalog/RUN_GUN_AMMO.md) for the full
item lists, cost-modeling notes, and exclusions (Underbarrel Weight and
Weapon Commlink, both unpriceable under the current schema).

CHAR-819 added Better Than Bad as a fifth approved source, at the project
owner's direction to port only its qualities ("I want to add the qualities
from better than bad"). It published all 11 headings from the book's "New
Qualities" section — 7 New Positive Qualities, 2 New Mastery Qualities
(modeled with `positive` polarity; the schema has no separate Mastery
concept), and 2 New Negative Qualities — bringing the runtime to 168 total
qualities. This book's printed→PDF page offset (one page) was independently
verified and differs from the two-page offset shared by the other three
approved sources. No other chapter of the book was reviewed; see
[`sr5-catalog/BETTER_THAN_BAD_QUALITIES.md`](sr5-catalog/BETTER_THAN_BAD_QUALITIES.md).

CHAR-820 followed immediately after, at the project owner's direction ("add
in the new adept powers"): both powers in the book's "New Adept Powers"
section, Mystic Aptitude and State of Purity, bringing the runtime to 27
total adept powers. Neither power's stated prerequisite (Essence 6, for
State of Purity) or activation type is code-enforced, consistent with how
every other adept power/quality in this project is modeled. See
[`sr5-catalog/BETTER_THAN_BAD_ADEPT_POWERS.md`](sr5-catalog/BETTER_THAN_BAD_ADEPT_POWERS.md).

A runtime catalog now exists (`sr5-core` `1.4.0`) and is pinned by a semantic
SHA-256 digest that changes whenever any catalog fact changes; this ledger
does not pin that digest value and should not be treated as a substitute for
reading it from the loader (`EmbeddedRulesetCatalogProvider.CurrentVersion`/
`CurrentSemanticDigest` are authoritative). Priority rows/metatypes,
qualities, active skills/groups, knowledge/languages, and Magic and Resonance
are reconciled against the reviewed inventory above (CHAR-806 through
CHAR-808 complete). CHAR-809A closed every CHAR-809 attachment/Capacity gap:
firearm mounts, armor Capacity, optical/audio/sensor device Capacity and
their enhancements, augmentation/cyberlimb Capacity, and vehicle
modifications (rigger interface, weapon mounts) are all implemented, tested,
and manually verified. CHAR-810 is complete: free-form contacts, fake SINs
and linked licenses, and all six core lifestyle tiers with their options and
payment forms are implemented, tested, and manually verified.

CHAR-812 closed the final reconciliation pass across six PDF-vs-catalog
background audits (weapons/armor, electronics/software, general gear/drugs,
augmentations, vehicles/drones, magical equipment). It added ammunition,
arrow/bolt ammo, explosives, grenades, and rockets/missiles to `gear` under
new `categoryId` values (weapons/armor); drugs, toxins, and BTL chips to
`gear` (general gear); Force-scaled cost/Availability/`focusCategoryId` to
all 16 foci (magical equipment); Ocular Drone, External Clip Port, and
cybergun Laser Sight/Silencer plus 7 missing `capacityCost` fields
(augmentations); Leather Jacket/Duster armor and two vehicle citation fixes
(weapons/armor, vehicles/drones). It resolved three reconciliation-report
"mismatches" as non-issues already covered by existing approved decisions
(`vehicle-modification.manual-operation-absolute-values`,
`gear.helmet-availability`, `gear.smoke-area`) and one as a false-positive
gap already present in the base catalog (Biometric Reader). Every remaining
gap the audits found — Autosoft pricing, weapon-ammo damage linkage, the
rocket/missile purchase split, the focus purchase/evaluator flow, Cyberlimb
Customization, and the Gas Grenade's cross-item chemical-payload cost — is
recorded as a deliberate exclusion in `SR5_RULE_DECISIONS.md`, not left as an
unexplained discrepancy. Identity counts elsewhere in this ledger are review
checkpoints, not release counts. CHAR-802's readiness checks (missing
citation, duplicate stable ID, unsupported source, dangling reference)
already run against whatever the catalog currently contains.
