# Augmentations

This is the CHAR-801 row-level review ledger for core augmentation grades,
cyberware, cyberlimbs, implant weapons, basic bioware, and cultured bioware. It
is a review input, not the runtime catalog. Only the pinned `sr5-core` PDF and
approved decisions are used. Run Faster contributes no augmentation options.

## Shared Rules And Notation

In creation-eligibility cells, `S` means standard grade and `A` means alphaware;
`yes`, a listed rating range, or named variants are the complete eligible set at
that grade. `No` means the item fails the numeric Availability ceiling after the
grade modifier. Restricted and Forbidden suffixes do not themselves bar a
creation purchase: Restricted ware requires an appropriate license, while
Forbidden ware cannot be licensed. Numeric Availability must be at most 12 and
an explicit purchasable Rating must be at most 6. All purchases remain subject
to gamemaster approval. Sources: `sr5-core` pp. 94-95, 416-419 (PDF 96-97,
418-421); decision `gear.legality-at-creation`.

`None` in Availability/legality means the source prints an em dash and assigns
no numeric Availability or legality suffix. `Not applicable` means the field
does not apply. A bracketed number such as `[2]` is Capacity consumed in a host;
an unbracketed Capacity is the host's capacity. Where both Essence and bracketed
Capacity are printed, install directly in the body and pay Essence, or install
in an allowed host and consume Capacity, never both. A zero Essence value is
still the source's explicit value. Sources: `sr5-core` pp. 417, 451, 453-458
(PDF 419, 453, 455-460).

The sole catalog source below is `sr5-core`. For compact row width, a Source
cell beginning with `p.` or `pp.` expands to `sr5-core p.` or `sr5-core pp.`;
the printed and physical PDF pages remain present on every entry. Any different
source ID is written explicitly.

| ID | Display name | Classification | Creation rule/effect | Source |
| --- | --- | --- | --- | --- |
| `augmentation-essence-ledger` | Augmentation Essence ledger | `bookkeeping` | Start at Essence 6. Sum the grade-adjusted Essence of all ware installed directly in the body; capacity-hosted accessories do not also charge Essence. Essence loss is permanent and remaining Essence is rounded up before computing Social limit. | `sr5-core` pp. 52, 95, 417, 451 (PDF 54, 97, 419, 453) |
| `augmentation-attribute-cap` | Augmentation attribute cap | `bookkeeping` | Track natural and augmented Physical/Mental ratings separately. Total augmentation bonus to each attribute is at most +4. Natural ratings still obey natural maxima; augmented ratings affect Initiative, inherent limits, monitors, and other stated derived mechanics but not free Knowledge/Language points or Contact Karma. The wired-reflexes/reaction-enhancers wireless exception may exceed +4 Reaction only while both wireless systems are active. | `sr5-core` pp. 94-95, 100-101, 455 (PDF 96-97, 102-103, 457) |
| `augmentation-magic-resonance-loss` | Essence loss to Magic/Resonance | `bookkeeping` | Total cumulative grade-adjusted Essence loss, then reduce both current and maximum Magic or Resonance by 1 for every point or fraction of total loss before final eligibility checks. Magic 0 disables Magic-linked skills; maximum Magic 0 permanently burns out all magical abilities. Adepts also lose an equal number of Power Points and must remove powers. Resonance 0 removes technomancer/Resonance abilities. Decision: `essence.magic-resonance-order`. | `sr5-core` pp. 52, 95, 249-250, 278-279 (PDF 54, 97, 251-252, 280-281) |
| `augmentation-resource-purchase` | Augmentation resource purchase | `bookkeeping` | Pay ware, host, add-on, and required component costs from Resources plus permitted Karma-to-nuyen conversion. Apply dwarf `x1.10` or troll `x1.50` gear cost to the grade-adjusted total. Ware can replace metatype traits: cybereyes replace natural vision traits and orthoskin replaces troll dermal armor. | `sr5-core` pp. 94-95 (PDF 96-97); decisions `metatype.dwarf-costs`, `metatype.troll-costs` |
| `augmentation-sensitive-system` | Sensitive System interaction | `bookkeeping` | A mundane character with Sensitive System doubles every cyberware Essence loss and may select no bioware. For an Awakened character or technomancer, the quality instead invokes its Drain/Fading test and does not state those mundane ware changes. | `sr5-core` p. 83 (PDF 85) |
| `augmentation-grade-add-ons` | Grade consistency for add-ons | `bookkeeping` | Every accessory/add-on must have the same grade as its host implant. Apply that grade's Essence, Availability, and cost adjustments to the add-on; a capacity-hosted installation still pays no separate Essence. | `sr5-core` p. 451 (PDF 453) |

## Implant Grades

Table values multiply each row's listed standard-grade values. Availability
modifiers change only the numeric part and preserve `R`/`F`; an em dash remains
`none`. Cost multipliers also apply to additive host/component costs. Sources:
`sr5-core` pp. 54, 95, 451 (PDF 56, 97, 453).

| ID | Exact name | Classification | Essence formula | Availability | Cost | Creation eligibility | Citation/decision |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `standard` | Standard | `selectable` | listed Essence `x1.0` | listed numeric value `+0` | listed cost `x1.0` | Eligible subject to final Availability/Rating, Resources, Essence, host, and other rules | `sr5-core` pp. 95, 451 (PDF 97, 453) |
| `alphaware` | Alphaware | `selectable` | listed Essence `x0.8` | listed numeric value `+2` | listed cost `x1.2` | Eligible subject to the adjusted Availability and all other creation rules | `sr5-core` pp. 95, 451 (PDF 97, 453) |
| `betaware` | Betaware | `creation-unavailable` | listed Essence `x0.7` | listed numeric value `+4` | listed cost `x1.5` | Unavailable at creation regardless of adjusted statistics | `sr5-core` pp. 54, 95, 451 (PDF 56, 97, 453) |
| `deltaware` | Deltaware | `creation-unavailable` | listed Essence `x0.5` | listed numeric value `+8` | listed cost `x2.5` | Unavailable at creation regardless of adjusted statistics | `sr5-core` pp. 54, 95, 451 (PDF 56, 97, 453) |
| `used` | Used | `excluded` | listed Essence `x1.25` | listed numeric value `-4` | listed cost `x0.75` | Excluded at creation by `ware.creation-grades`; no used-ware selection may be represented | `sr5-core` p. 451 (PDF 453); decision `ware.creation-grades` |

## Headware

Headware with bracketed Capacity may instead be installed in a cyberlimb. An
implanted commlink/deck selection must reference one separately eligible core
commlink/deck model and pays both costs. Source for all rows and effects:
`sr5-core` pp. 451-453 (PDF 453-455).

| ID | Exact name | Class | Rating/variant | Essence; capacity/host/location | Availability; legality | Standard cost | Prerequisites, exclusions, included/generated profiles | Mechanical effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `commlink-implanted` | Commlink | `parameterized` | required eligible `commlink-model-id`; implanted commlinks with Device Rating below 5 are generally not installed for security reasons, not a prohibition | 0.2; `[2]` cyberlimb or head | None; legal | 2,000 nuyen + host commlink cost | Generates `commlink-implanted-sim-module`; host model required | Implanted commlink; mentally controlled; built-in sim module | S/A: yes if host also eligible | p. 451 (PDF 453) |
| `control-rig` | Control Rig | `parameterized` | 1-3 | Rating 1/2/3 Essence; head | 5R/10R/15R | 43,000/97,000/208,000 nuyen | Generates built-in sim module, universal data connector, and retractable cable profiles | DNI; while jumped in add Rating to Vehicle tests, Handling, and Speed and reduce Vehicle Test thresholds by Rating, minimum 1 | S: 1-2; A: 1-2 | pp. 451-452 (PDF 453-454) |
| `cortex-bomb-kink` | Cortex Bomb, Kink | `selectable` | kink | 0; `[1]` cyberlimb or head | 12F | 10,000 nuyen | Kink type only | Remote/time/sound trigger; damages selected brain/headware function, or selected cyberlimb components when limb-hosted | S: yes; A: no | p. 452 (PDF 454) |
| `cortex-bomb-microbomb` | Cortex Bomb, Microbomb | `creation-unavailable` | microbomb | 0; `[2]` cyberlimb or head | 16F | 25,000 nuyen | None | Kills bearer; when limb-hosted destroys limb | S/A: no | p. 452 (PDF 454) |
| `cortex-bomb-area-bomb` | Cortex Bomb, Area Bomb | `creation-unavailable` | area bomb | 0; `[3]` cyberlimb or head | 20F | 40,000 nuyen | Uses fragmentation-grenade blast profile | Kills bearer and affects blast area as fragmentation grenade; limb-hosted version blasts area and bearer | S/A: no | p. 452 (PDF 454) |
| `cyberdeck-implanted` | Cyberdeck | `parameterized` | required eligible `cyberdeck-model-id` | 0.4; `[4]` cyberlimb or head | 5R | 5,000 nuyen + host deck cost | Host model required | Fully implanted cyberdeck | S/A: yes if host also eligible | p. 452 (PDF 454) |
| `datajack` | Datajack | `selectable` | not applicable | 0.1; head | 2; legal | 1,000 nuyen | Includes universal connector, retractable one-meter micro-cable, and storage memory | Grants DNI; cabled private mental communication between datajack users; wireless grants Rating 1 noise reduction | S/A: yes | p. 452 (PDF 454) |
| `data-lock` | Data Lock | `parameterized` | 1-12 generally; creation range below | 0.1; head | Rating x 2; legal | Rating x 1,000 nuyen | Only authorized external access through universal connector; never wireless; bearer has no mental access | Device Rating equals Rating; stores protected data | S: 1-6; A: 1-5 | p. 452 (PDF 454) |
| `olfactory-booster` | Olfactory Booster | `parameterized` | 1-6 | 0.2; head | Rating x 3; legal | Rating x 4,000 nuyen | None | Identify/record/play smells, cut off odors, supports VR scent; +Rating dice to scent Perception | S: 1-4; A: 1-3 | p. 452 (PDF 454) |
| `simrig-implanted` | Simrig | `selectable` | not applicable | 0.2; head | 12R | 4,000 nuyen | Imports simrig function; not a separate external simrig purchase | Records sensory data for replay | S: yes; A: no | p. 452 (PDF 454) |
| `skilljack` | Skilljack | `parameterized` | 1-6 | Rating x 0.1; head | Rating x 2; legal | Rating x 20,000 nuyen | Knowsoft/linguasoft required; activesoft acts only as Knowledge unless eligible skillwires also installed; only one skilljack operates at once | Max individual soft Rating = Skilljack Rating; total running Ratings <= Rating x 2, or x3 wireless; start/stop Free Action; no Edge with soft skills | S: 1-6; A: 1-5 | p. 452 (PDF 454) |
| `taste-booster` | Taste Booster | `parameterized` | 1-6 under the general gear-rating range; table omits range beside name | 0.2; head | Rating x 3; legal | Rating x 3,000 nuyen | None | Gustatory AR/VR; +Rating dice to taste Perception | S: 1-4; A: 1-3 | pp. 416, 452-453 (PDF 418, 454-455) |
| `tooth-compartment` | Tooth Compartment | `selectable` | storage or breakable required | None; tooth | 8; legal | 800 nuyen | Required subtype; storage holds datachip/tiny RFID-sized object; breakable links an authored trigger effect but does not include its payload | Wireless/hidden-catch storage retrieval, or wireless/bite activation of linked effect | S: yes; A: yes | p. 452 (PDF 454) |
| `ultrasound-sensor-implanted` | Ultrasound Sensor | `parameterized` | 1-6 | 0.25; `[2]` cyberlimb or head | 10; legal | Rating x 12,000 nuyen | Imports ultrasound sensor profile | Replaces normal vision while active; Free Action switches active sonar/passive sonar/off | S/A: 1-6 | p. 452 (PDF 454) |
| `voice-modulator` | Voice Modulator | `parameterized` | 1-6 | 0.2; head | (Rating x 3)F | Rating x 5,000 nuyen | None | Perfect pitch; up to 100 dB; vocal distortion/imitation/playback; +Rating dice to Impersonation | S: 1-4; A: 1-3 | p. 452 (PDF 454) |

## Eyeware

Except ocular drone, an enhancement is installed bilaterally either in natural
eyes for listed Essence or in a cybereye system for listed Capacity. Cybereyes
replace natural visual traits. Source for all rows: `sr5-core` pp. 453-454
(PDF 455-456); imported enhancement effects: p. 444 (PDF 446).

| ID | Exact name | Class | Rating | Essence; capacity/host | Availability; legality | Standard cost | Prerequisites/included/generated | Mechanical creation effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cybereyes` | Cybereyes basic system | `parameterized` | 1-4 | 0.2/0.3/0.4/0.5; Capacity 4/8/12/16 | 3/6/9/12; legal | 4,000/6,000/10,000/14,000 nuyen | Replaces both eyes and natural vision traits; generates included image-link and camera profiles | Normal 20/20 vision plus host capacity | S: 1-4; A: 1-3 | pp. 453-454 (PDF 455-456) |
| `flare-compensation-implanted` | Flare compensation | `selectable` | not applicable | 0.1; `[1]` cybereyes or natural eyes | 4; legal | 1,000 nuyen | Bilateral | Mitigates glare modifiers and flashing-light penalties | S/A: yes | pp. 453-454 (PDF 455-456) |
| `image-link-implanted` | Image link | `selectable` | not applicable | 0.1; included at no capacity cost in cybereyes, or natural eyes | 4; legal | 1,000 nuyen when independent | Generated free by `cybereyes`; independent purchase only for natural eyes | Displays visual data/AR | S/A: yes | pp. 453-454 (PDF 455-456) |
| `low-light-vision-implanted` | Low-light vision | `selectable` | not applicable | 0.1; `[2]` cybereyes or natural eyes | 4; legal | 1,500 nuyen | Bilateral | See normally down to starlight, not total darkness | S/A: yes | pp. 453-454 (PDF 455-456) |
| `ocular-drone` | Ocular drone | `selectable` | one eye per purchase | None; `[6]` cybereye only | 6; legal | 6,000 nuyen | Requires cybereye host; cannot be retinal; generates deployed ocular-drone profile | Functions as host cybereye while seated; removed unit is controlled as Horizon Flying Eye; one missing eye gives -3 all tasks, both removed cause blindness | S/A: yes | p. 453 (PDF 455) |
| `retinal-duplication` | Retinal duplication | `creation-unavailable` | 1-6 | 0.1; `[1]` cybereyes or natural eyes | 16F | Rating x 20,000 nuyen | Requires captured retina recording | Opposed Rating vs retinal-scanner Rating to spoof retina | S/A: no | pp. 453-454 (PDF 455-456) |
| `smartlink-implanted` | Smartlink | `selectable` | not applicable | 0.2; `[3]` cybereyes or natural eyes | 8R | 4,000 nuyen | Requires smartgun system for benefit; bilateral implant is more effective than external smartlink | Receives smartgun data and enables implanted-smartlink smartgun benefits | S/A: yes | pp. 453-454 (PDF 455-456) |
| `thermographic-vision-implanted` | Thermographic vision | `selectable` | not applicable | 0.1; `[2]` cybereyes or natural eyes | 4; legal | 1,500 nuyen | Bilateral | Infrared heat-pattern vision, including living targets in total darkness | S/A: yes | pp. 453-454 (PDF 455-456) |
| `vision-enhancement-implanted` | Vision enhancement | `parameterized` | 1-3 | 0.1; `[Rating]` cybereyes or natural eyes | Rating x 3; legal | Rating x 4,000 nuyen | Bilateral | +Rating visual-Perception limit; wireless also +Rating dice | S: 1-3; A: 1-3 | pp. 453-454 (PDF 455-456) |
| `vision-magnification-implanted` | Vision magnification | `selectable` | not applicable | 0.1; `[2]` cybereyes or natural eyes | 4; legal | 2,000 nuyen | Bilateral | Digital zoom up to x50; applies ranged-combat magnification rules | S/A: yes | pp. 453-454 (PDF 455-456) |

## Earware

Enhancements are bilateral and installed either directly for Essence or in a
cyberear host for Capacity. Source for all rows: `sr5-core` pp. 453-454
(PDF 455-456); imported audio effects: p. 445 (PDF 447).

| ID | Exact name | Class | Rating | Essence; capacity/host | Availability; legality | Standard cost | Prerequisites/included | Mechanical creation effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cyberears` | Cyberears | `parameterized` | 1-4 | 0.2/0.3/0.4/0.5; Capacity 4/8/12/16 | 3/6/9/12; legal | 3,000/4,500/7,500/11,000 nuyen | Replaces inner ears; generates included sound-link profile | Normal omnidirectional hearing plus host capacity | S: 1-4; A: 1-3 | pp. 453-454 (PDF 455-456) |
| `audio-enhancement-implanted` | Audio Enhancement | `parameterized` | 1-3 | 0.1; `[Rating]` cyberears or natural ears | Rating x 3; legal | Rating x 4,000 nuyen | Bilateral | +Rating audio-Perception limit; wireless also +Rating dice | S/A: 1-3 | pp. 453-454 (PDF 455-456) |
| `balance-augmenter` | Balance Augmenter | `selectable` | not applicable | 0.1; `[4]` cyberears or inner ear | 8; legal | 8,000 nuyen | None | +1 die to tests involving balance | S/A: yes | pp. 453-454 (PDF 455-456) |
| `damper-implanted` | Damper | `selectable` | not applicable | 0.1; `[1]` cyberears or inner ear | 6; legal | 2,250 nuyen | None | +2 dice to resist sonic attacks, including flashbangs | S/A: yes | pp. 453-454 (PDF 455-456) |
| `select-sound-filter-implanted` | Select Sound Filter | `parameterized` | 1-6 | 0.1; `[Rating]` cyberears or inner ear | Rating x 3; legal | Rating x 3,500 nuyen | Bilateral; implanted maximum 6 replaces external maximum 3 | Select/recognize one sound group per Rating; one listened to actively, others recorded/trigger-monitored | S: 1-4; A: 1-3 | pp. 453-454 (PDF 455-456) |
| `sound-link-implanted` | Sound Link | `selectable` | not applicable | 0.1; included at no capacity cost in cyberears, or natural ears | 4; legal | 1,000 nuyen when independent | Generated free by `cyberears`; independent purchase only for natural ears | Plays linked PAN/headware audio directly | S/A: yes | pp. 453-454 (PDF 455-456) |
| `spatial-recognizer-implanted` | Spatial Recognizer | `selectable` | not applicable | 0.1; `[2]` cyberears or inner ear | 8; legal | 4,000 nuyen | None | +2 Perception limit to locate sound source; wireless also gives +2 dice | S/A: yes | pp. 453-454 (PDF 455-456) |

## Bodyware

Only bracketed items may be cyberlimb-hosted. Source for all rows:
`sr5-core` pp. 454-456 (PDF 456-458).

| ID | Exact name | Class | Rating/variant | Essence; capacity/location | Availability; legality | Standard cost | Prerequisites/exclusions | Mechanical creation effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `bone-lacing-plastic` | Bone Lacing, Plastic | `selectable` | plastic | 0.5; skeleton | 8R | 8,000 nuyen | Exactly one bone lacing; incompatible with bone-density augmentation/other bone-altering ware | +1 Body for Physical damage resistance, +1 cumulative non-encumbering Armor, unarmed `(STR + 1)P` | S/A: yes | pp. 454-456 (PDF 456-458) |
| `bone-lacing-aluminum` | Bone Lacing, Aluminum | `selectable` | aluminum | 1; skeleton | 12R | 18,000 nuyen | Same exclusions as plastic | +2 Body for Physical damage resistance, +2 Armor, unarmed `(STR + 2)P` | S: yes; A: no | pp. 454-456 (PDF 456-458) |
| `bone-lacing-titanium` | Bone Lacing, Titanium | `creation-unavailable` | titanium | 1.5; skeleton | 16R | 30,000 nuyen | Same exclusions as plastic | +3 Body for Physical damage resistance, +3 Armor, unarmed `(STR + 3)P` | S/A: no | pp. 454-456 (PDF 456-458) |
| `dermal-plating` | Dermal Plating | `parameterized` | 1-6 | Rating x 0.5; skin | (Rating x 4)R | Rating x 3,000 nuyen | Incompatible with orthoskin/other skin Armor augmentation | +Rating cumulative non-encumbering Armor; visibly/tactually obvious | S: 1-3; A: 1-2 | pp. 454, 456 (PDF 456, 458) |
| `fingertip-compartment` | Fingertip Compartment | `selectable` | not applicable | 0.1; `[1]` cyberlimb or fingertip | 4; legal | 3,000 nuyen | Holds one micro-sized item; monofilament whip remains separate purchase | Concealability -10; insert/remove Complex Action, Simple wireless; whip extend Simple/retract Complex | S/A: yes | pp. 454-455 (PDF 456-457) |
| `grapple-gun-implanted` | Grapple Gun | `selectable` | not applicable | 0.5; `[4]` cyberlimb or body | 8; legal | 5,000 nuyen | Imports grapple-gun profile; rope not included and must attach externally | Implanted grapple gun | S/A: yes | pp. 455-456 (PDF 457-458) |
| `internal-air-tank` | Internal Air Tank | `parameterized` | 1-3 | 0.25; `[3]` cyberlimb or one lung | Rating; legal | Rating x 4,500 nuyen | Replaces part of one lung | Hold breath Rating hours and gain complete inhalation-toxin protection while doing so; activate/deactivate Simple, refill 5 minutes pressurized or 6 hours breathing; wireless activation Free and reports level/purity | S/A: 1-3 | pp. 455-456 (PDF 457-458) |
| `muscle-replacement` | Muscle Replacement | `parameterized` | 1-4 | Rating x 1; muscles | (Rating x 5)R | Rating x 25,000 nuyen | Incompatible with muscle augmentation, muscle toner, and other muscle augmentation | +Rating Strength and +Rating Agility, each subject to +4 cap | S: 1-2; A: 1-2 | pp. 455-456 (PDF 457-458) |
| `reaction-enhancers` | Reaction Enhancers | `parameterized` | 1-3 | Rating x 0.3; spine | (Rating x 5)R | Rating x 13,000 nuyen | Incompatible with other Reaction enhancements except wireless wired reflexes | +Rating Reaction; adjusts Initiative and Physical limit; wireless paired wired-reflex exception can exceed +4 | S: 1-2; A: 1-2 | pp. 455-456 (PDF 457-458) |
| `skillwires` | Skillwires | `parameterized` | 1-6 | Rating x 0.1; nervous system | Rating x 4; legal | Rating x 20,000 nuyen | Requires implanted skilljack and running activesoft; incompatible with reflex recorder; skillwire addiction Rating 5/Threshold 2 is a runtime risk | Use activesoft up to Skillwire Rating; wireless gives +1 relevant inherent limit to soft-driven skills | S: 1-3; A: 1-2 | `sr5-core` pp. 414, 455-456 (PDF 416, 457-458) |
| `smuggling-compartment` | Smuggling Compartment | `selectable` | not applicable | 0.2; `[2]` cyberlimb or hollowed body location | 6; legal | 7,500 nuyen | Small/mini item, typically no larger than light pistol; GM size adjudication | Concealability -10; insert/retrieve Complex, Simple wireless | S/A: yes | pp. 455-456 (PDF 457-458) |
| `wired-reflexes` | Wired Reflexes | `parameterized` | 1-3 | 2/3/5; nervous system | 8R/12R/20R | 39,000/149,000/217,000 nuyen | Incompatible with Reaction or Initiative augmentation except wireless reaction enhancers | While active +Rating Reaction and +Rating D6 Initiative Dice; manual toggle Complex, wireless Simple; paired wireless exception can exceed +4 Reaction | S: 1-2; A: 1 | pp. 455-456 (PDF 457-458) |

## Cyberlimbs, Customization, Enhancements, And Accessories

Every limb starts at Strength 3 and Agility 3. Customization is chosen only with
the limb and cannot later change; enhancements may be added/replaced. A limb's
customized attribute cannot exceed the character's natural maximum, while a
Rating 1-3 enhancement may then raise that limb subject to the global +4
augmentation cap. Limb attributes replace the natural rating for a limb-only
test; use the average for multiple limbs or weakest for careful coordination.
Partial-limb ratings apply only to tests directly using that part. Cyberlimbs
hold no bioware and no cyberware that charges Essence rather than Capacity.
Each full limb adds 1 Physical Condition Monitor box; partial arm/leg adds 0.5
before the final total, while hands/feet add none. Unarmed damage is `(limb
STR)P`. Sources: `sr5-core` pp. 455-457 (PDF 457-459).

| ID | Exact name | Class | Rating | Essence; capacity/location | Availability; legality | Standard cost | Prerequisites/exclusions/effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `obvious-cyberlimb-full-arm` | Obvious Full Arm | `selectable` | STR 3, AGI 3 | 1; Capacity 15 | 4; legal | 15,000 nuyen | Replaces full arm; obvious | S/A: yes | p. 457 (PDF 459) |
| `obvious-cyberlimb-full-leg` | Obvious Full Leg | `selectable` | STR 3, AGI 3 | 1; Capacity 20 | 4; legal | 15,000 nuyen | Replaces full leg; obvious | S/A: yes | p. 457 (PDF 459) |
| `obvious-cyberlimb-hand-foot` | Obvious Hand/Foot | `parameterized` | required `hand` or `foot`; STR 3, AGI 3 | 0.25; Capacity 4 | 2; legal | 5,000 nuyen | Partial extremity; obvious; no monitor box | S/A: yes | p. 457 (PDF 459) |
| `obvious-cyberlimb-lower-arm` | Obvious Lower Arm | `selectable` | STR 3, AGI 3 | 0.45; Capacity 10 | 4; legal | 10,000 nuyen | Partial limb; obvious; 0.5 monitor-limb contribution | S/A: yes | p. 457 (PDF 459) |
| `obvious-cyberlimb-lower-leg` | Obvious Lower Leg | `selectable` | STR 3, AGI 3 | 0.45; Capacity 12 | 4; legal | 10,000 nuyen | Partial limb; obvious; 0.5 monitor-limb contribution | S/A: yes | p. 457 (PDF 459) |
| `obvious-cyberlimb-torso` | Obvious Torso | `selectable` | shell; STR/AGI limb tests not applicable | 1.5; Capacity 10 | 12; legal | 20,000 nuyen | Torso shell; obvious | S: yes; A: no | p. 457 (PDF 459) |
| `obvious-cyberlimb-skull` | Obvious Skull | `creation-unavailable` | shell; STR/AGI limb tests not applicable | 0.75; Capacity 4 | 16; legal | 10,000 nuyen | Skull shell; obvious | S/A: no | p. 457 (PDF 459) |
| `synthetic-cyberlimb-full-arm` | Synthetic Full Arm | `selectable` | STR 3, AGI 3 | 1; Capacity 8 | 4; legal | 20,000 nuyen | Replaces full arm; Concealability -8 visually, obvious by touch | S/A: yes | p. 457 (PDF 459) |
| `synthetic-cyberlimb-full-leg` | Synthetic Full Leg | `selectable` | STR 3, AGI 3 | 1; Capacity 10 | 4; legal | 20,000 nuyen | Replaces full leg; Concealability -8 visually, obvious by touch | S/A: yes | p. 457 (PDF 459) |
| `synthetic-cyberlimb-hand-foot` | Synthetic Hand/Foot | `parameterized` | required `hand` or `foot`; STR 3, AGI 3 | 0.25; Capacity 2 | 2; legal | 6,000 nuyen | Partial extremity; Concealability -8; no monitor box | S/A: yes | p. 457 (PDF 459) |
| `synthetic-cyberlimb-lower-arm` | Synthetic Lower Arm | `selectable` | STR 3, AGI 3 | 0.45; Capacity 5 | 4; legal | 12,000 nuyen | Partial limb; Concealability -8; 0.5 monitor-limb contribution | S/A: yes | p. 457 (PDF 459) |
| `synthetic-cyberlimb-lower-leg` | Synthetic Lower Leg | `selectable` | STR 3, AGI 3 | 0.45; Capacity 6 | 4; legal | 12,000 nuyen | Partial limb; Concealability -8; 0.5 monitor-limb contribution | S/A: yes | p. 457 (PDF 459) |
| `synthetic-cyberlimb-torso` | Synthetic Torso | `selectable` | shell; STR/AGI limb tests not applicable | 1.5; Capacity 5 | 12; legal | 25,000 nuyen | Torso shell; Concealability -8 visually | S: yes; A: no | p. 457 (PDF 459) |
| `synthetic-cyberlimb-skull` | Synthetic Skull | `creation-unavailable` | shell; STR/AGI limb tests not applicable | 0.75; Capacity 2 | 16; legal | 15,000 nuyen | Skull shell; Concealability -8 visually | S/A: no | p. 457 (PDF 459) |
| `cyberlimb-customization` | Customization, each STR or AGI point above 3 | `parameterized` | required host, attribute `Strength` or `Agility`, and integer points; final customized value <= natural maximum | no added Essence/Capacity; purchased with host | host Availability +1 per point; host legality | +5,000 nuyen per point | Cyberlimb host required; purchase-time only; customization is not a separately installed enhancement | Sets host's base limb attribute before enhancements; eligible only while final host Availability <=12 | pp. 456-457 (PDF 458-459) |
| `cyberlimb-enhancement-agility` | Cyberlimb Enhancement, Agility | `parameterized` | 1-3 | no Essence; `[Rating]` in target cyberlimb | (Rating x 3)R | Rating x 6,500 nuyen | One Agility enhancement per limb; host Capacity; replaces rather than stacks with same type | +Rating target-limb Agility | S/A: 1-3 | pp. 456-457 (PDF 458-459) |
| `cyberlimb-enhancement-armor` | Cyberlimb Enhancement, Armor | `parameterized` | 1-3 | no Essence; `[Rating]` in target cyberlimb | Rating x 5; legal | Rating x 3,000 nuyen | One Armor enhancement per limb; host Capacity | +Rating cumulative non-encumbering Armor | S/A: 1-2 | pp. 456-457 (PDF 458-459) |
| `cyberlimb-enhancement-strength` | Cyberlimb Enhancement, Strength | `parameterized` | 1-3 | no Essence; `[Rating]` in target cyberlimb | (Rating x 3)R | Rating x 6,500 nuyen | One Strength enhancement per limb; host Capacity; replaces rather than stacks with same type | +Rating target-limb Strength | S/A: 1-3 | pp. 456-457 (PDF 458-459) |
| `cyberarm-gyromount` | Cyberarm gyromount | `selectable` | fixed Rating 3 effect | no Essence; `[8]` full/partial cyberarm | 12F | 6,000 nuyen | Full or partial cyberarm required; not cumulative with worn gyro stabilization | Rating 3 gyro-stabilization effect; toggle Simple, Free wireless | S: yes; A: no | pp. 456-457 (PDF 458-459) |
| `cyberarm-slide` | Cyberarm slide | `selectable` | not applicable | no Essence; `[3]` cyberarm | 12R | 3,000 nuyen | Cyberarm required; holds hold-out, taser, or light pistol; weapon separate | Conceals weapon; ready as Free Action | S: yes; A: no | pp. 456-457 (PDF 458-459) |
| `cyber-holster` | Cyber holster | `selectable` | not applicable | no Essence; `[5]` cyberlimb | 8R | 2,000 nuyen | Cyberlimb required; taser/pistol-or-smaller weapon separate | Fully encloses weapon or acts as pistol-sized compartment; insert/retrieve Simple, ready Free wireless | S/A: yes | pp. 456-457 (PDF 458-459) |
| `hydraulic-jacks` | Hydraulic jacks | `parameterized` | 1-6 | no Essence; `[Rating]` in each of two cyberlegs | 9; legal | Rating x 2,500 nuyen each | Exactly two cyberlegs with identical Rating jacks; cost/capacity paid in each leg | Per Rating: +1 Physical limit for jumping/sprinting, +20% max jump, -2 m effective fall; wireless +1 die to jumping/sprinting/leg lifting tests | S/A: 1-6 | pp. 456-457 (PDF 458-459) |
| `large-smuggling-compartment` | Large smuggling compartment | `selectable` | not applicable | no Essence; `[5]` cyberlimb | 6; legal | 8,000 nuyen | Cyberlimb required; heavy-pistol/small-SMG/breadbox size subject to GM | Insert/retrieve Complex, Simple wireless | S/A: yes | pp. 456-457 (PDF 458-459) |

## Cyber Implant Weapons

Each weapon is installed directly for Essence or in a cyberlimb for Capacity.
Cyberguns use internal magazines and generate both the listed weapon profile and
an included smartgun-system component; only the three listed cybergun accessories
are allowed. Melee attacks use Unarmed Combat and Physical limit. Source for all
rows/profiles: `sr5-core` p. 458 (PDF 460).

| ID | Exact name | Class | Essence; capacity/location | Availability; legality | Standard cost | Prerequisites/included/generated profile | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cybergun-hold-out-pistol` | Cybergun, Hold-out pistol | `selectable` | 0.1; `[2]` limb or body | 8R | 2,000 nuyen | Generates `cyber-hold-out`; includes smartgun system | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `cybergun-light-pistol` | Cybergun, Light pistol | `selectable` | 0.25; `[4]` limb or body | 10R | 3,900 nuyen | Generates `light-cyber-pistol`; includes smartgun system | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `cybergun-machine-pistol` | Cybergun, Machine pistol | `selectable` | 0.5; `[6]` limb or body | 12R | 3,500 nuyen | Generates `cyber-machine-pistol`; includes smartgun system | S: yes; A: no | `sr5-core` p. 458 (PDF 460) |
| `cybergun-heavy-pistol` | Cybergun, Heavy pistol | `selectable` | 0.5; `[6]` limb or body | 12R | 4,300 nuyen | Generates `heavy-cyber-pistol`; includes smartgun system | S: yes; A: no | `sr5-core` p. 458 (PDF 460) |
| `cybergun-submachine-gun` | Cybergun, Submachine gun | `selectable` | 1; `[8]` limb or body | 12R | 4,800 nuyen | Generates `cyber-submachine-gun`; includes smartgun system | S: yes; A: no | `sr5-core` p. 458 (PDF 460) |
| `cybergun-shotgun` | Cybergun, Shotgun | `selectable` | 1.25; `[10]` limb or body | 12R | 8,500 nuyen | Generates `cyber-shotgun`; includes smartgun system | S: yes; A: no | `sr5-core` p. 458 (PDF 460) |
| `cybergun-grenade-launcher` | Cybergun, Grenade launcher | `creation-unavailable` | 1.5; `[15]` limb or body | 20F | 30,000 nuyen | Generates `cyber-microgrenade-launcher`; includes smartgun system | S/A: no | `sr5-core` p. 458 (PDF 460) |
| `cybergun-external-clip-port` | External clip port | `selectable` | 0.1; `[1]` cybergun host | None; legal | +1,000 nuyen | Cybergun required; external clip and ammunition separate; visible clip can reveal gun | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `cybergun-laser-sight` | Laser sight | `selectable` | no Essence; `[1]` cybergun host | None; legal | +1,000 nuyen | Cybergun required; imports laser-sight effect | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `cybergun-silencer-suppressor` | Silencer/suppressor | `selectable` | no Essence; `[2]` cybergun host | None; legal | +1,000 nuyen | Cybergun required; imports silencer/suppressor effect | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `hand-blade-retractable` | Hand blade (retractable) | `selectable` | 0.25; `[2]` limb or flesh | 10F | 2,500 nuyen | Generates melee profile: Reach none, `(STR + 2)P`, AP -2 | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `hand-razors-retractable` | Hand razors (retractable) | `selectable` | 0.2; `[2]` limb or flesh | 8F | 1,250 nuyen | Generates melee profile: Reach none, `(STR + 1)P`, AP -3 | S/A: yes | `sr5-core` p. 458 (PDF 460) |
| `spurs-retractable` | Spurs (retractable) | `selectable` | 0.3; `[3]` limb or flesh | 12F | 5,000 nuyen | Generates melee profile: Reach none, `(STR + 3)P`, AP -2 | S: yes; A: no | `sr5-core` p. 458 (PDF 460) |
| `shock-hand` | Shock hand | `selectable` | 0.25; `[4]` limb or flesh | 8R | 5,000 nuyen | Generates melee profile: Reach none, `9S(e)`, AP -5; ten charges per hand | S/A: yes | `sr5-core` p. 458 (PDF 460) |

### Generated And Included Profiles

These records are produced by their parent and cannot be purchased again in
that instance. Imported ordinary-gear behavior remains a reference to that
catalog entry rather than a duplicate augmentation profile.

| ID | Display name | Classification | Parent | Generated facts/effect | Source |
| --- | --- | --- | --- | --- | --- |
| `commlink-implanted-sim-module` | Implanted commlink sim module | `included-component` | `commlink-implanted` | Sim module included at zero additional cost | `sr5-core` p. 451 (PDF 453) |
| `control-rig-sim-module` | Control rig sim module | `included-component` | `control-rig` | Built-in sim module enabling DNI with other devices | `sr5-core` p. 452 (PDF 454) |
| `control-rig-universal-data-connector` | Control rig universal data connector | `included-component` | `control-rig` | Built-in universal data connector | `sr5-core` p. 452 (PDF 454) |
| `control-rig-retractable-cable` | Control rig retractable cable | `included-component` | `control-rig` | Approximately one meter; connector/cable are datajack-like but do not state the datajack wireless noise bonus | `sr5-core` p. 452 (PDF 454) |
| `cybereyes-image-link` | Cybereyes image link | `included-component` | `cybereyes` | Bilateral system includes image-link function at no extra cost/capacity | `sr5-core` pp. 453-454 (PDF 455-456) |
| `cybereyes-camera` | Cybereyes camera | `included-component` | `cybereyes` | Built-in camera at no extra cost; source assigns no separate Rating | `sr5-core` p. 453 (PDF 455) |
| `cyberears-sound-link` | Cyberears sound link | `included-component` | `cyberears` | Included sound-link function at no extra cost/capacity | `sr5-core` p. 454 (PDF 456) |
| `cybergun-smartgun-system` | Cybergun smartgun system | `included-component` | any `cybergun-*` weapon parent | Pre-equipped smartgun system; no separate purchase/capacity charge | `sr5-core` p. 458 (PDF 460) |
| `ocular-drone-deployed` | Deployed ocular drone | `generated` | `ocular-drone` | Uses Horizon Flying Eye profile plus all enhancements in its cybereye host; remains the same purchased eye | `sr5-core` pp. 453, 465 (PDF 455, 467) |
| `cyber-hold-out` | Cyber hold-out | `generated` | `cybergun-hold-out-pistol` | Accuracy `4 (6)`, Damage `6P`, AP none, SA, RC none, Ammo `2 (m) / 6 (c)` | `sr5-core` p. 458 (PDF 460) |
| `light-cyber-pistol` | Light cyber pistol | `generated` | `cybergun-light-pistol` | Accuracy `6 (8)`, Damage `7P`, AP none, SA, RC none, Ammo `10 (m) / 15 (c)` | `sr5-core` p. 458 (PDF 460) |
| `cyber-machine-pistol` | Cyber machine pistol | `generated` | `cybergun-machine-pistol` | Accuracy `4 (6)`, Damage `6P`, AP none, SA/BF, RC 1, Ammo `18 (m) / 32 (c)` | `sr5-core` p. 458 (PDF 460) |
| `heavy-cyber-pistol` | Heavy cyber pistol | `generated` | `cybergun-heavy-pistol` | Accuracy `4 (6)`, Damage `7P`, AP -1, SA, RC none, Ammo `8 (m) / 12 (c)` | `sr5-core` p. 458 (PDF 460) |
| `cyber-submachine-gun` | Cyber submachine gun | `generated` | `cybergun-submachine-gun` | Accuracy `4 (6)`, Damage `7P`, AP none, SA/BF, RC 2, Ammo `18 (m) / 32 (c)` | `sr5-core` p. 458 (PDF 460) |
| `cyber-shotgun` | Cyber shotgun | `generated` | `cybergun-shotgun` | Accuracy `4 (6)`, Damage `10P`, AP -1, SS, RC none, Ammo `4 (m) / 10 (c)` | `sr5-core` p. 458 (PDF 460) |
| `cyber-microgrenade-launcher` | Cyber microgrenade launcher | `generated` | `cybergun-grenade-launcher` | Accuracy `4 (6)`, Damage/AP as grenade, SS, RC none, Ammo `2 (m) / 6 (c)`; parent remains creation-unavailable | `sr5-core` p. 458 (PDF 460) |

## Basic Bioware

Bioware has no wireless functionality. Source for all rows: `sr5-core`
pp. 459-460 (PDF 461-462).

| ID | Exact name | Class | Rating | Essence; location/host | Availability; legality | Standard cost | Prerequisites/exclusions | Mechanical creation effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `adrenaline-pump` | Adrenaline pump | `parameterized` | 1-3 | Rating x 0.75; lower abdomen/adrenal glands; no host | (Rating x 6)F | Rating x 55,000 nuyen | Forced activation on failed Composure under stress, otherwise Free Action; cannot stop early; 1-hour regeneration | For Rating x 1D6 Combat Turns: ignore injury modifiers/Stun unconsciousness and +Rating STR, AGI, REA, WIL; then resist Stun equal to active turns with natural Body + Willpower | S: 1-2; A: 1 | p. 459 (PDF 461) |
| `bone-density-augmentation` | Bone density augmentation | `parameterized` | 1-4 | Rating x 0.3; bones; no host | (Rating x 4); legal | Rating x 5,000 nuyen | Incompatible with bone lacing/other bone augmentation; corrected by `ware.bone-density-cost` | +Rating Body only for damage resistance; unarmed R1 `(STR)P`, R2 `(STR + 1)P`, R3 `(STR + 2)P`, R4 `(STR + 3)P`, AP none | S: 1-3; A: 1-2 | pp. 459-460 (PDF 461-462); decision `ware.bone-density-cost` |
| `cats-eye` | Cat's eye | `selectable` | not applicable | 0.1; eyes; no host | 4; legal | 4,000 nuyen | Incompatible with cyberware eye replacement/enhancement | Grants low-light vision; visibly slit/reflective | S/A: yes | pp. 459-460 (PDF 461-462) |
| `enhanced-articulation` | Enhanced articulation | `selectable` | not applicable | 0.3; joints/tendons; no host | 12; legal | 24,000 nuyen | None | +1 die Escape Artist and +1 Physical limit | S: yes; A: no | pp. 459-460 (PDF 461-462) |
| `muscle-augmentation` | Muscle augmentation | `parameterized` | 1-4 | Rating x 0.2; muscles; no host | (Rating x 5)R | Rating x 31,000 nuyen | Incompatible with muscle replacement and other Strength-increasing augmentation | +Rating Strength, subject to +4 cap | S/A: 1-2 | pp. 459-460 (PDF 461-462) |
| `muscle-toner` | Muscle toner | `parameterized` | 1-4 | Rating x 0.2; muscles; no host | (Rating x 5)R | Rating x 32,000 nuyen | Incompatible with muscle replacement and other Agility-increasing augmentation | +Rating Agility, subject to +4 cap | S/A: 1-2 | pp. 459-460 (PDF 461-462) |
| `orthoskin` | Orthoskin | `parameterized` | 1-4 | Rating x 0.25; skin; no host | (Rating x 4)R | Rating x 6,000 nuyen | Incompatible with dermal plating/other skin Armor augmentation; replaces troll natural dermal deposit bonus | +Rating cumulative Armor | S: 1-3; A: 1-2 | pp. 459-460 (PDF 461-462) |
| `pathogenic-defense` | Pathogenic Defense | `parameterized` | 1-6 | Rating x 0.1; spleen; no host | Rating x 2; legal | Rating x 4,500 nuyen | None | +Rating dice to Disease Resistance | S: 1-6; A: 1-5 | pp. 459-460 (PDF 461-462) |
| `platelet-factories` | Platelet factories | `selectable` | not applicable | 0.2; bone marrow/blood; no host | 12; legal | 17,000 nuyen | None | Whenever 2+ Physical boxes would be taken, reduce by 1 box | S: yes; A: no | pp. 459-460 (PDF 461-462) |
| `skin-pocket` | Skin pocket | `selectable` | not applicable | 0.1; authored body location; no host | 4; legal | 12,000 nuyen | Holds one small object | Concealability -10; insert/remove Complex Action | S/A: yes | pp. 459-460 (PDF 461-462) |
| `suprathyroid-gland` | Suprathyroid gland | `creation-unavailable` | not applicable | 0.7; thyroid; no host | 20R | 140,000 nuyen | Requires twice normal food; +25% lifestyle cost | +1 Agility, Body, Reaction, Strength, each subject to +4 cap | S/A: no | pp. 459-460 (PDF 461-462) |
| `symbiotes` | Symbiotes | `parameterized` | 1-4 | Rating x 0.2; bloodstream; no host | Rating x 5; legal | Rating x 3,500 nuyen | Monthly food Rating x 200 nuyen, included by High-or-better lifestyle | +Rating dice to Physical and Stun healing tests | S/A: 1-2 | pp. 459-460 (PDF 461-462) |
| `synthacardium` | Synthacardium | `parameterized` | 1-3 | Rating x 0.1; heart; no host | Rating x 4; legal | Rating x 30,000 nuyen | None | +Rating dice to tests using Athletics skill-group skills | S: 1-3; A: 1-2 | pp. 459-460 (PDF 461-462) |
| `tailored-pheromones` | Tailored pheromones | `parameterized` | 1-3 | Rating x 0.2; glands; no host | Rating x 4R | Rating x 31,000 nuyen | Target must smell user within comfortable conversation range; no effect on magical abilities/tests | +Rating dice to Acting/Influence skill tests against eligible target and +Rating Social limit | S: 1-3; A: 1-2 | pp. 459-460 (PDF 461-462) |
| `toxin-extractor` | Toxin extractor | `parameterized` | 1-6 | Rating x 0.2; liver; no host | Rating x 3; legal | Rating x 4,800 nuyen | None | +Rating dice to all Toxin Resistance | S: 1-4; A: 1-3 | pp. 459-460 (PDF 461-462) |
| `tracheal-filter` | Tracheal Filter | `parameterized` | 1-6 | Rating x 0.1; trachea; no host | Rating x 3; legal | Rating x 4,500 nuyen | Inhalation vector only | +Rating dice to Toxin Resistance against inhalation toxins | S: 1-4; A: 1-3 | pp. 459-460 (PDF 461-462) |

## Cultured Bioware

Cultured bioware is tailor-made for its recipient but uses the same grade
formulas. Source for all rows: `sr5-core` pp. 460-461 (PDF 462-463).

| ID | Exact name | Class | Rating/parameter | Essence; location/host | Availability; legality | Standard cost | Prerequisites/exclusions | Mechanical creation effect | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cerebral-booster` | Cerebral booster | `parameterized` | 1-3 | Rating x 0.2; cerebrum; no host | Rating x 6; legal | Rating x 31,500 nuyen | None | +Rating Logic, subject to +4 cap | S: 1-2; A: 1 | pp. 460-461 (PDF 462-463) |
| `damage-compensators` | Damage compensators | `parameterized` | 1-12 generally; creation range below | Rating x 0.1; nervous pathways; no host | (Rating x 3)F | Rating x 2,000 nuyen | Incompatible with High Pain Tolerance quality; chosen ignored boxes may be Physical, Stun, or combined | Ignore Rating damage boxes before determining injury modifiers | S: 1-4; A: 1-3 | pp. 460-461 (PDF 462-463); incompatibility `sr5-core` p. 74 (PDF 76) |
| `mnemonic-enhancer` | Mnemonic enhancer | `parameterized` | 1-3 | Rating x 0.1; brain memory centers; no host | Rating x 5; legal | Rating x 9,000 nuyen | None | +Rating dice to Knowledge, Language, and memory tests; +Rating Mental limit | S/A: 1-2 | pp. 460-461 (PDF 462-463) |
| `pain-editor` | Pain editor | `creation-unavailable` | not applicable | 0.3; nervous tissue; no host | 18F | 48,000 nuyen | Incompatible with High Pain Tolerance quality; activation state required in play | Active: ignore all injury modifiers and Stun unconsciousness, +1 Willpower, -1 Intuition, -4 tactile Perception; damage awareness requires Observe in Detail or biomonitor | S/A: no | pp. 460-461 (PDF 462-463); incompatibility `sr5-core` p. 74 (PDF 76) |
| `reflex-recorder` | Reflex recorder (Skill) | `parameterized` | required one `active-skill-id` linked to a Physical attribute | 0.1; motor-reflex nerves; no host | 10; legal | 14,000 nuyen | Skill required; one recorder per skill; multiple different skills allowed; incompatible with skillwires | +1 augmented Rating to selected Physical-linked skill, subject to final skill effects rather than natural creation ranks | S/A: yes | pp. 460-461 (PDF 462-463) |
| `sleep-regulator` | Sleep regulator | `selectable` | not applicable | 0.1; hypothalamus; no host | 6; legal | 12,000 nuyen | None | Need 3 hours sleep/night; stay awake twice normal before sleep-deprivation fatigue; healing rest unchanged; sleep is deeper/harder to interrupt | S/A: yes | pp. 460-461 (PDF 462-463) |
| `synaptic-booster` | Synaptic booster | `parameterized` | 1-3 | Rating x 0.5; spinal nerve cells; no host | (Rating x 6)R | Rating x 95,000 nuyen | Incompatible with every other Reaction or Initiative enhancement | +Rating Reaction and +Rating D6 Initiative Dice; adjust Initiative/Physical limit; subject to +4 cap | S: 1-2; A: 1 | pp. 460-461 (PDF 462-463) |

## Explicit Exclusions

| ID | Display name/family | Classification | Reason | Source |
| --- | --- | --- | --- | --- |
| `used-ware-at-creation` | Used ware at creation | `excluded` | The augmentation chapter lists used ware, but the approved creation decision follows Step Six and excludes it. Use the grade row above as the sole grade identity; this row records the behavioral exclusion and is not an additional grade option. | `sr5-core` pp. 95, 451 (PDF 97, 453); `ware.creation-grades` |
| `betaware-at-creation` | Betaware at creation | `excluded` | Retained in the grade inventory as `creation-unavailable`; no creation purchase is generated. | `sr5-core` pp. 54, 95, 451 (PDF 56, 97, 453) |
| `deltaware-at-creation` | Deltaware at creation | `excluded` | Retained in the grade inventory as `creation-unavailable`; no creation purchase is generated. | `sr5-core` pp. 54, 95, 451 (PDF 56, 97, 453) |
| `run-faster-augmentations` | Run Faster augmentations | `excluded` | Every Run Faster catalog option is outside approved scope; only Sum-to-Ten and the approved formula-grant clarification enter the ruleset. | `run-faster` pp. 62-63 (PDF 64-65); manifest scope |
| `augmentation-similar-models-loadouts` | Similar models and example loadouts | `excluded` | Examples and similar-model references are not distinct published augmentation rows. | `sr5-core` pp. 96-97, 451-461 (PDF 98-99, 453-463) |

## Source Discrepancies And Decisions

| Subject | Approved ledger behavior | Discrepancy/provenance |
| --- | --- | --- |
| Creation grades | Standard and alphaware only; used excluded; beta/delta creation-unavailable. Decision `ware.creation-grades`. | Step Six says only standard/alphaware, while the augmentation chapter says standard/alphaware/used. `sr5-core` pp. 95, 451 (PDF 97, 453). |
| Bone Density cost | `Rating x 5,000` nuyen. Decision `ware.bone-density-cost`. | Product table prints malformed `Raxing x 5,000`. `sr5-core` p. 460 (PDF 462). |
| Cumulative Essence | Sum all grade-adjusted Essence and apply one current/maximum Magic or Resonance loss per point or fraction before final eligibility. Decision `essence.magic-resonance-order`. | Creation and subsystem chapters phrase fractional/current/maximum reductions differently. `sr5-core` pp. 52, 95, 249-250, 278 (PDF 54, 97, 251-252, 280). |
| Taste Booster range | Parameterized Rating 1-6, then creation limits narrow availability by grade. | The table uses Rating in Availability/cost but omits a range beside the name; the chapter's general gear rule says ratings are usually 1-6. `sr5-core` pp. 416, 453 (PDF 418, 455). |
| Damage compensator range | General catalog range remains 1-12; creation is further capped by Rating 6 and grade-adjusted Availability, yielding the row's narrower eligible ranges. | The product's explicit range exceeds the general starting-gear Rating ceiling. `sr5-core` pp. 94, 418, 461 (PDF 96, 420, 463). |
| Wireless Reaction exception | Preserve the explicit reaction-enhancers/wired-reflexes wireless exception above +4; no other augmentation may exceed the +4 attribute-bonus cap. | Step Six gives a universal +4 cap while both ware descriptions expressly permit their combined wireless Reaction bonus above +4. `sr5-core` pp. 94, 455 (PDF 96, 457). |

## Review Footer

### Reviewed Ranges

- `sr5-core` pp. 52, 54-55, 74, 83, 94-101 (PDF 54, 56-57, 76,
  85, 96-103): Essence, ware overview, quality incompatibilities, creation
  budgets, grade eligibility, attribute cap, Magic/Resonance loss, replacement
  of metatype traits, and derived values.
- `sr5-core` pp. 168-172, 249-250, 278-279 (PDF 170-174, 251-252,
  280-281): Armor/damage/electricity, Resonance loss, Magic loss, burnout, and
  adept Power Point loss.
- `sr5-core` pp. 414, 416-419, 444-445 (PDF 416, 418-421, 446-447):
  skillwire addiction, ratings, Capacity, creation Availability/Rating limits,
  legality, and imported vision/audio effects.
- `sr5-core` pp. 451-461 (PDF 453-463): complete implant-grade,
  headware, eyeware, earware, bodyware, cyberlimb, implant-weapon, basic
  bioware, and cultured-bioware prose and product tables.
- `sr5-core` p. 465 (PDF 467): Horizon Flying Eye reference used only by the
  generated deployed ocular-drone profile.
- `run-faster` pp. 62-63 (PDF 64-65): complete approved Run Faster range,
  reviewed to confirm it contributes no augmentation entry.

### Entry Counts

Counts include each stable-ID table row in this file once. The behavioral
exclusion rows for beta/delta/used creation do not duplicate the grade identities
in the approved-PDF total; they are reported separately as explicit exclusions.

| Category | Catalog entries |
| --- | ---: |
| Grades | 5 |
| Shared bookkeeping | 6 |
| Headware | 15 |
| Eyeware | 10 |
| Earware | 7 |
| Bodyware | 12 |
| Cyberlimbs/customization/enhancements/accessories | 23 |
| Implant weapons and add-ons | 14 |
| Included/generated profiles | 16 |
| Basic bioware | 16 |
| Cultured bioware | 7 |
| **Approved-PDF total** | **131** |

| Classification | Approved-PDF entries |
| --- | ---: |
| `selectable` | 54 |
| `parameterized` | 43 |
| `included-component` | 8 |
| `generated` | 8 |
| `bookkeeping` | 6 |
| `creation-unavailable` | 11 |
| `excluded` | 1 |
| **Total** | **131** |

Explicit exclusion records not added to the approved-PDF total: 5. The eleven
completely creation-unavailable grade/item identities are betaware, deltaware,
Cortex Bomb Microbomb, Cortex Bomb Area Bomb, Retinal Duplication, Bone Lacing
Titanium, Obvious Skull, Synthetic Skull, Cybergun Grenade Launcher,
Suprathyroid Gland, and Pain Editor. Partially eligible parameterized families
remain `parameterized` and state exact grade/rating eligibility on-row.

### Remaining Unknown Facts

None.

### Runtime Reconciliation Status

Not implemented. CHAR-802 must materialize these identities and relationships,
then verify costs, grade formulas, eligible ranges, citations, host/capacity
constraints, generated profiles, exclusions, and counts against this ledger.
