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

| Category | Approved-PDF identity review | Detailed fact review | Runtime catalog | Reconciliation |
| --- | --- | --- | --- | --- |
| Priority rows and metatypes | Complete | Complete | Methods, levels, categories, and 25 cell identities published | Assignment reconciled; grants and metatypes pending CHAR-806 |
| Qualities | Complete: 31 positive, 28 negative | Complete | Not implemented | Pending |
| Active skills and groups | Complete: 75 skills, 15 groups | Complete | Not implemented | Pending |
| Knowledge and languages | Complete as open-authored categories | Complete | Not implemented | Pending |
| Magic and Resonance | Complete identity pass | Complete | Not implemented | Pending |
| Weapons and armor | Complete identity pass | Complete | Not implemented | Pending |
| Electronics and software | Complete identity pass | Complete | Not implemented | Pending |
| General gear and consumables | Complete identity pass | Complete | Not implemented | Pending |
| Augmentations | Complete identity pass | Complete | Not implemented | Pending |
| Vehicles and drones | Complete identity pass | Complete | Not implemented | Pending |
| Magical equipment | Complete identity pass | Complete | Not implemented | Pending |
| Contacts, identities, lifestyles | Complete identity pass | Complete | Not implemented | Pending |

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
and operative prose effects. Runtime reconciliation remains pending, and the
weapons/armor facts listed in its review footer await owner decisions.

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

No runtime catalog exists yet, so project counts, missing entries, unexpected
entries, and the semantic digest cannot be reported. Identity counts above are
review checkpoints, not release counts. CHAR-802 must fail readiness for any
missing citation, duplicate stable ID, unsupported source, dangling reference,
or mismatch between this reviewed inventory and the immutable catalog resource.
