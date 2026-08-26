# SR5 Core Qualities Ledger

This is the CHAR-801 review ledger for qualities. It is not a runtime catalog.
Only `sr5-core` and approved decisions in `../SR5_RULE_DECISIONS.md` were used.
Unless a row says otherwise, a quality is available during creation, occupies one
quality selection, has no rating, and is not repeatable. A rated quality is one
selection carrying the stated integer rating. A parameterized quality may repeat
only where its row expressly permits it. This closed-world rule prevents an
absence of source permission from creating duplicate benefits.

## Shared Rules

| Rule | Catalog/evaluator requirement | Source |
| --- | --- | --- |
| Creation pools | Begin with 25 Karma. Positive costs and negative awards modify that pool. Enforce separate ceilings of 25 Karma of purchased positive qualities and 25 Karma of awarded negative qualities; generated components with zero cost/award do not enter either ceiling. | `sr5-core` p. 71 (PDF 73); `quality.creation-caps` |
| Career acquisition | After creation, a player may buy a positive quality for twice listed cost. A gamemaster may assign positive or negative qualities from play events. Negative qualities are not player-purchased after creation and may be removed, after stipulated requirements and GM permission, for twice listed award. | `sr5-core` pp. 71, 106-107 (PDF 73, 108-109) |
| Parameters | A field marked `text` is required, trimmed, non-empty bounded plain text. It is never HTML and produces no inferred mechanics. Exact storage bounds are an application constraint for CHAR-802, not a new rules fact. | `quality.open-parameters`; `sr5-core` pp. 76-85 (PDF 78-87) |
| References | Fields ending in `-id` reference the corresponding closed core catalog. Selection must remain valid after all grants, purchases, metatype, Magic/Resonance path, skills, and ware are resolved. | Quality details below |
| Duplicate grants | A generated quality/component has zero Karma and provenance linking it to its parent. It is not separately purchasable and must not satisfy an award cap. Reject a parent selection if a required generated component conflicts with an independently selected quality. | `sr5-core` pp. 73, 322 (PDF 75, 324) |
| Availability metadata | Qualities have no Availability, legality, capacity, or quantity/unit fields; each is `not applicable`. | `sr5-core` pp. 71-87 (PDF 73-89) |

## Positive Qualities

`P` means selectable; `PP` means parameterized. `Repeat` is the complete
creation repeatability rule, not career replacement behavior.

| ID | Display name | Class | Cost/rating | Required parameters; prerequisites; incompatibilities; repeat | Validation-visible grant/effect | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `ambidextrous` | Ambidextrous | P | 4 | none; repeat no | Removes the normal `-2` off-hand-only action modifier. | `sr5-core` p. 71 (PDF 73) |
| `analytical-mind` | Analytical Mind | P | 5 | none; repeat no | `+2` to Logic tests for pattern recognition, evidence analysis, clue hunting, or puzzles; halve problem-solving time. | `sr5-core` p. 72 (PDF 74) |
| `aptitude` | Aptitude | PP | 14 | `skill-id`: one rated Active, Knowledge, or Language skill (not native `N`); repeat no, explicitly once | Selected skill creation natural cap becomes 7 and career cap becomes 13; all final skill sources obey that cap. | `sr5-core` pp. 72, 88, 107 (PDF 74, 90, 109) |
| `astral-chameleon` | Astral Chameleon | P | 10 | Magic rating `> 0` and ability to leave astral signatures; repeat no | Own astral signatures last half normal duration; assensers take `-2` to Assensing tests against them. | `sr5-core` p. 72 (PDF 74) |
| `bilingual` | Bilingual | PP | 5 | `language-id`: a second language distinct from the free native language; creation only; repeat no | Grants that language as a second native language in Step Five. It cannot be acquired in career play. | `sr5-core` p. 72 (PDF 74) |
| `blandness` | Blandness | P | 8 | incompatible with `distinctive-style`; repeat no | Recall threshold `+1`; physical shadow/search/inquiry tests based on appearance `-2`; no magical/Matrix benefit; suppress while visible distinguishing features or circumstances make the character stand out. | `sr5-core` pp. 72, 81 (PDF 74, 83) |
| `catlike` | Catlike | P | 7 | none; repeat no | `+2` Sneaking tests. | `sr5-core` p. 72 (PDF 74) |
| `codeslinger` | Codeslinger | PP | 10 | `matrix-action-id`: one action whose definition contains a test; repeat no | `+2` to the selected Matrix action. Eligibility follows `matrix.quality-action-domain`, including actions whose test can be waived in a particular state. | `sr5-core` pp. 72, 237-244 (PDF 74, 239-246); `matrix.quality-action-domain` |
| `double-jointed` | Double-Jointed | P | 6 | none; repeat no | `+2` Escape Artist tests; permits narrative access to unusually cramped spaces when applicable. | `sr5-core` p. 72 (PDF 74) |
| `exceptional-attribute` | Exceptional Attribute | PP | 14 | `attribute-id`: Body, Agility, Reaction, Strength, Willpower, Logic, Intuition, Charisma, Magic, or Resonance; selected attribute must exist; GM approval; incompatible with `lucky`; repeat no, explicitly once | Raises selected natural maximum by 1 but grants no rating. For Physical/Mental attributes, the selection consumes the one-at-natural-maximum creation allowance. Edge is ineligible. | `sr5-core` pp. 72-74, 101 (PDF 74-76, 103); `attribute.exceptional-maximum-count` |
| `first-impression` | First Impression | P | 11 | none; repeat no | `+2` relevant Social tests during a first meeting only, not later encounters. | `sr5-core` p. 74 (PDF 76) |
| `focused-concentration` | Focused Concentration | PP | 4 per rating | `rating`: integer 1-6; character can cast spells or is a technomancer; repeat no | Sustain one spell/complex form of Force/Level no greater than rating without its sustain penalty; additional sustained effects use normal penalties. | `sr5-core` p. 74 (PDF 76) |
| `gearhead` | Gearhead | P | 11 | none; repeat no | In vehicle/chase combat choose Speed `+20%` or Handling `+1`, plus `+2` difficult maneuvers/stunts, for `1D6` minutes; optional extra `1D6` minutes causes 1 unresisted stress damage per extra minute. | `sr5-core` p. 74 (PDF 76) |
| `guts` | Guts | P | 10 | none; repeat no | `+2` tests resisting fear and intimidation, including magical sources. | `sr5-core` p. 74 (PDF 76) |
| `high-pain-tolerance` | High Pain Tolerance | PP | 7 per rating | `rating`: integer 1-3; incompatible with Pain Resistance adept power, pain editor bioware, and damage compensator bioware; repeat no | Ignore rating boxes of damage when calculating wound modifiers; does not remove damage. | `sr5-core` p. 74 (PDF 76) |
| `home-ground` | Home Ground | PP | 10 each | `profile-id`; `home-ground-subject`: text naming the neighborhood/host/area; repeat yes, but each selection must use a different profile/subject pair | Applies exactly the selected closed profile below only on its Home Ground. | `sr5-core` pp. 74-75 (PDF 76-77) |
| `human-looking` | Human-Looking | P | 6 | metatype elf, dwarf, or ork; repeat no | Human NPC baseline attitude is neutral for Social tests even when biased against metahumans; mistaken identity can cause hostility from anti-human metahumans. | `sr5-core` p. 75 (PDF 77) |
| `indomitable` | Indomitable | PP | 8 per level | `level`: integer 1-3; `limit-allocation`: nonnegative Mental/Physical/Social increments summing to level; repeat no | Adds allocated increments to the corresponding inherent limits; maximum total increase 3. | `sr5-core` p. 75 (PDF 77) |
| `juryrigger` | Juryrigger | P | 10 | none; repeat no | `+2` Mechanical tests when juryrigging and threshold `-1` if GM deems task possible. Results are temporary; examples are not closed purchasable effects. | `sr5-core` pp. 75-76 (PDF 77-78) |
| `lucky` | Lucky | P | 12 | GM approval; incompatible with `exceptional-attribute`; repeat no, explicitly once | Raises metatype Edge maximum by 1 but grants no Edge rating. | `sr5-core` p. 76 (PDF 78) |
| `magic-resistance` | Magic Resistance | PP | 6 per rating | `rating`: integer 1-4; Magic rating must be 0; repeat no | Add rating dice to Spell Resistance. Always on: beneficial spells are resisted and voluntary-subject spells automatically fail. | `sr5-core` p. 76 (PDF 78) |
| `mentor-spirit` | Mentor Spirit | PP | 5 | `mentor-id`: one of 16 profiles; if mystic adept, `advantage-branch`: `magician` or `adept`; all profile-specific choices; Magic rating `> 0`; one mentor at a time | Applies the profile's all-character advantage, selected path advantage, disadvantage, and zero-cost generated grants. Career change requires buying off current quality then repurchasing. | `sr5-core` pp. 76, 320-324 (PDF 78, 322-326) |
| `natural-athlete` | Natural Athlete | P | 7 | none; repeat no | `+2` Running and Gymnastics tests. | `sr5-core` p. 76 (PDF 78) |
| `natural-hardening` | Natural Hardening | P | 10 | none; repeat no | 1 natural biofeedback-filtering point, cumulative with Biofeedback Filter or technomancer firewall. | `sr5-core` p. 76 (PDF 78) |
| `natural-immunity` | Natural Immunity | PP | 4 natural; 10 synthetic | `category`: `natural` or `synthetic`; `subject`: text disease, drug, or poison agreed with GM; repeat no; magical diseases/toxins ineligible | One dose/exposure per 6 hours has no effect; later exposure in that window is normal but recovery time is halved. Disease immunity does not prevent carrying/infecting. | `sr5-core` p. 76 (PDF 78); `quality.open-parameters` |
| `photographic-memory` | Photographic Memory | P | 6 | none; repeat no | `+2` all Memory tests. | `sr5-core` p. 76 (PDF 78) |
| `quick-healer` | Quick Healer | P | 3 | none; repeat no | `+2` all Healing tests made on, for, or by the character, including magical healing. | `sr5-core` p. 77 (PDF 79) |
| `resistance-to-pathogens-toxins` | Resistance to Pathogens/Toxins | PP | 4 one; 8 both | `coverage`: `pathogens`, `toxins`, or `both`; incompatible with `weak-immune-system`; repeat no | `+1` applicable Resistance tests. | `sr5-core` pp. 77, 87 (PDF 79, 89) |
| `spirit-affinity` | Spirit Affinity | PP | 7 | `spirit-type-id`: air, beasts, earth, fire, man, or water; magic user; repeat no | Spirits of that type are favorably disposed; grants 1 additional service for each such spirit and `+1` Binding tests. Type need not belong to tradition; watchers/minions ineligible. | `sr5-core` pp. 77, 303-304 (PDF 79, 305-306) |
| `toughness` | Toughness | P | 9 | none; repeat no | `+1` Body dice on Damage Resistance tests; does not change Body. | `sr5-core` p. 77 (PDF 79) |
| `will-to-live` | Will to Live | PP | 3 per rating | `rating`: integer 1-3; repeat no | Add rating Damage Overflow boxes only; no unconsciousness/incapacitation threshold or wound-modifier change. | `sr5-core` pp. 77, 101 (PDF 79, 103) |

### Home Ground Profiles

All six are `included-component` children of `home-ground`, cost 0 independently,
and cannot be purchased directly.

| ID | Display name | Effect | Source |
| --- | --- | --- | --- |
| `home-ground.astral-acclimation` | Astral Acclimation | Ignore up to 2 background-count points on the selected Home Ground only. | `sr5-core` pp. 74-75 (PDF 76-77) |
| `home-ground.you-know-a-guy` | You Know a Guy | Neighborhood NPCs are friendly absent changed circumstances; `+2` Street Cred for Negotiation with them. They are not generated contacts. | `sr5-core` p. 75 (PDF 77) |
| `home-ground.digital-turf` | Digital Turf | `+2` Matrix tests in one named host; other quality bonuses stack; lose quality after not frequenting host for over 6 months. | `sr5-core` p. 75 (PDF 77) |
| `home-ground.the-transporter` | The Transporter | `+2` Evasion tests on the selected Home Ground. | `sr5-core` p. 75 (PDF 77) |
| `home-ground.on-the-lam` | On the Lam | `+2` Intuition + appropriate street knowledge to find a quick safe location on the selected Home Ground. | `sr5-core` p. 75 (PDF 77) |
| `home-ground.street-politics` | Street Politics | `+2` Knowledge tests about gangs or their operations on the selected Home Ground. | `sr5-core` p. 75 (PDF 77) |

## Negative Qualities

`N` means selectable; `NP` means parameterized. Awards are positive Karma
credited to the creation pool and counted against the separate negative ceiling.

| ID | Display name | Class | Award/rating | Required parameters; prerequisites; incompatibilities; repeat | Validation-visible grant/effect | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `addiction` | Addiction | NP | Mild 4; Moderate 9; Severe 20; Burnout 25 | `severity`; `dependency`: `physiological` or `psychological`; `subject`: text substance/device/activity; repeat no | Apply the closed severity profile below. Addiction tests, withdrawal, and progression use the substance-abuse rules. | `sr5-core` pp. 77-78, 414 (PDF 79-80, 416); `quality.open-parameters` |
| `allergy` | Allergy | NP | sum prevalence and severity: 5-25 | `prevalence`: `uncommon` (2) or `common` (7); `severity`: Mild (3), Moderate (8), Severe (13), Extreme (18); `allergen`: text; repeat no | Apply both closed components below. Resistance loses one die per severity stage when attacked with allergen. | `sr5-core` p. 78 (PDF 80); `quality.open-parameters` |
| `astral-beacon` | Astral Beacon | N | 10 | Magic rating `> 0`; repeat no | Astral signatures last twice normal; threshold to assense information about them `-1`. | `sr5-core` pp. 78-79 (PDF 80-81) |
| `bad-luck` | Bad Luck | N | 12 | repeat no | On Edge use roll `1D6`; on 1 Edge is spent with opposite intended effect; can trigger only once per game session. | `sr5-core` p. 79 (PDF 81) |
| `bad-rep` | Bad Rep | N | 7 | repeat no | Starts play with 3 Notoriety. It can decrease only after confronting/resolving the reputation source, then quality may be bought off. | `sr5-core` p. 79 (PDF 81) |
| `code-of-honor` | Code of Honor | NP | 15 | `code-profile`: `protected-group`, `assassins-creed`, or `warriors-code`; for protected group, `protected-group`: text and GM approval; repeat no | Apply selected closed profile below. Other authored codes have no source construction rules and are excluded. | `sr5-core` pp. 79-80 (PDF 81-82); `quality.open-parameters` |
| `codeblock` | Codeblock | NP | 10 | `matrix-action-id`: one action whose definition contains a test and is likely to be used; repeat no | `-2` selected Matrix action. Eligibility follows `matrix.quality-action-domain`. | `sr5-core` pp. 80, 237-244 (PDF 82, 239-246); `matrix.quality-action-domain` |
| `combat-paralysis` | Combat Paralysis | N | 12 | repeat no | First Initiative score each combat round is halved, round up; later phases normal; `-3` Surprise; combat Composure threshold `+1`. | `sr5-core` p. 80 (PDF 82) |
| `dependents` | Dependents | NP | Occasional 3; Regular 6; Close 9 | `tier`; `dependent-description`: text describing one or more dependents; repeat no | All tiers increase skill learning/improvement and long-term-project base time by 50%; lifestyle cost `+10%`, `+20%`, or `+30%` by tier. | `sr5-core` p. 80 (PDF 82); `quality.open-parameters` |
| `distinctive-style` | Distinctive Style | NP | 5 | `distinctive-feature`: text physical appearance/mannerism/personality; incompatible with `blandness`; repeat no, explicitly once | Tests to identify, trace, physically locate, or obtain legwork information `+2`; NPC Memory threshold `-1`, minimum 1; no astral-search effect. | `sr5-core` pp. 80-81 (PDF 82-83); `quality.open-parameters` |
| `elf-poser` | Elf Poser | N | 6 | metatype human; repeat no | Can cosmetically pass as elf and avoid non-elf Social modifiers; discovery can cause hostile/contemptuous attitudes. | `sr5-core` p. 81 (PDF 83) |
| `gremlins` | Gremlins | NP | 4 per level | `level`: integer 1-4; repeat no | For moderately sophisticated external devices, reduce rolled 1s needed for glitch by level; GM may require otherwise automatic operation tests. Does not affect implants or sabotage opponents by touch. | `sr5-core` p. 81 (PDF 83) |
| `incompetent` | Incompetent | NP | 5 | `active-skill-group-id`; group must be physically/path-usable and campaign-relevant; Language/Knowledge ineligible; repeat no, explicitly once | Character is unaware in every group skill, cannot possess the group, and receives no gear benefit tied to an affected skill. | `sr5-core` p. 81 (PDF 83) |
| `insomnia` | Insomnia | NP | 10 or 15 | `severity`: `ten-karma` or `fifteen-karma`; repeat no | Before Stun recovery, Intuition + Willpower (4). At 10: failure doubles recovery interval and delays Edge refresh up to 24 hours. At 15: failure negates that recovery attempt and Edge refresh waits 24 hours. Success permits normal recovery and Edge after 8 restful hours. | `sr5-core` pp. 81-82 (PDF 83-84) |
| `loss-of-confidence` | Loss of Confidence | NP | 10 | `skill-id`: one rated skill with final natural rating at least 4 and in which character has invested/prides themself; repeat no | Selected-skill tests `-2`; specialization cannot apply and Edge cannot be used on those tests. | `sr5-core` p. 82 (PDF 84) |
| `low-pain-tolerance` | Low Pain Tolerance | N | 9 | repeat no | Wound modifier advances per 2 cumulative boxes rather than 3, across Physical and Stun tracks. | `sr5-core` p. 82 (PDF 84) |
| `ork-poser` | Ork Poser | N | 6 | metatype human or elf; repeat no | Can cosmetically pass as ork; discovery controls possible ork acceptance/hostility and poser-metatype stigma. | `sr5-core` p. 82 (PDF 84) |
| `prejudiced` | Prejudiced | NP | sum prevalence and degree: 3-10 | `prevalence`: `common` (5) or `specific` (3); `degree`: `biased` (0), `outspoken` (2), or `radical` (5); `target-group`: text; repeat no | Against target, character Social tests `-2` per degree stage (1/2/3); target receives `+2` per stage in negotiations. | `sr5-core` p. 82 (PDF 84); `quality.open-parameters` |
| `scorched` | Scorched | NP | 10 | `cause-profile`: `btl` or `ic`; for IC, `ic-types`: non-empty subset of `black` and `psychotropic`; `effect-profile`: one of five below; BTL requires at least Mild BTL Addiction and BTL gear; IC requires decker or technomancer; repeat no | On entering VR/slotting BTL, Body + Willpower (4): failure applies effect 6 hours; glitch/critical glitch 24 hours. Against causal IC/BTL, Willpower (3) to confront; `-2` Damage Resistance against its damage. Medical repair plus buyoff removes it; later exposure may restore it. | `sr5-core` p. 83 (PDF 85) |
| `sensitive-system` | Sensitive System | N | 12 | repeat no | Mundane: double cyberware Essence loss and reject all bioware. Awakened/technomancer: before each Drain/Fading test, Willpower (2); failure raises that Drain/Fading Value by 2. | `sr5-core` p. 83 (PDF 85) |
| `simsense-vertigo` | Simsense Vertigo | N | 5 | repeat no | `-2` all tests while interacting with AR, VR, or simsense, including smartlinks, simrigs, and image links. | `sr5-core` p. 83 (PDF 85) |
| `sinner-layered` | SINner (Layered) | NP | National 5; Criminal 10; Corporate Limited 15; Corporate Born 25 | `sin-profile`; `issuer`: text nation/corporation; Criminal also requires `issuer-kind`: `national` or `corporate`; repeat no; profile replaces conflicting prior legal SIN as described | Generates one legal SIN profile. Taxes on gross income: National 15%, Criminal 15%, Corporate Limited 20%, Corporate Born 10%; apply profile access, registry, tracking, prejudice, and broadcast obligations below. | `sr5-core` pp. 84-85 (PDF 86-87); `quality.open-parameters` |
| `social-stress` | Social Stress | NP | 8 | `cause`: text; `trigger`: text; repeat no | For Leadership or Etiquette reduce 1s needed to glitch by 1; GM may call for additional Social tests, especially around trigger. | `sr5-core` p. 85 (PDF 87); `quality.open-parameters` |
| `spirit-bane` | Spirit Bane | NP | 7 | `spirit-type-id`: air, beasts, earth, fire, man, or water; magic user; repeat no | Spirits of type target character first/use lethal force; character `-2` summoning/binding type; spirit `+2` resisting banishment. Type need not be in tradition; watchers/minions ineligible. | `sr5-core` pp. 85, 303-304 (PDF 87, 305-306) |
| `uncouth` | Uncouth | N | 14 | repeat no | `-2` Social tests resisting improper/impulsive action; double Karma cost for Social skills including creation; cannot learn Social skill groups; unaware in unowned Social skills below rating 1. | `sr5-core` p. 85 (PDF 87) |
| `uneducated` | Uneducated | N | 8 | repeat no | Unaware and cannot default in unowned Technical, Academic Knowledge, and Professional Knowledge skills; double Karma cost to learn/improve those categories including creation; GM may restrict groups and call for ordinary-task tests. | `sr5-core` p. 87 (PDF 89) |
| `unsteady-hands` | Unsteady Hands | N | 7 | repeat no | When manifest, `-2` all Agility tests. After stressful encounter, Agility + Body (4); failure manifests for remainder of run, success avoids it that time. | `sr5-core` p. 87 (PDF 89) |
| `weak-immune-system` | Weak Immune System | N | 10 | incompatible with `natural-immunity` and `resistance-to-pathogens-toxins`; repeat no | Disease Power `+2` for every Resistance test. | `sr5-core` p. 87 (PDF 89) |

### Addiction Profiles

These are `included-component` profiles, never direct purchases.

| ID | Award | Dose/activity | Craving interval | Withdrawal and persistent effects | Source |
| --- | ---: | --- | --- | --- | --- |
| `addiction.mild` | 4 | 1 dose or 1 hour | monthly | Failed withdrawal: `-2` Mental-attribute tests if psychological, or Physical-attribute tests if physiological, until satisfied. | `sr5-core` pp. 77-78 (PDF 79-80) |
| `addiction.moderate` | 9 | 1 dose or 1 hour | about 2 weeks | Failed withdrawal: corresponding Mental/Physical tests `-4` until satisfied. | `sr5-core` p. 78 (PDF 80) |
| `addiction.severe` | 20 | 2 doses or 2 hours | weekly | Failed withdrawal: corresponding tests `-4`; all Social tests always `-2`. | `sr5-core` p. 78 (PDF 80) |
| `addiction.burnout` | 25 | minimum 3 doses or 3 hours | daily | Until satisfied: corresponding tests `-6`; all Social tests always `-3`. | `sr5-core` p. 78 (PDF 80) |

### Allergy Components

Prevalence and severity are `included-component` values whose awards sum.

| ID | Award | Effect | Source |
| --- | ---: | --- | --- |
| `allergy.uncommon` | 2 | Allergen is rare in local environment. | `sr5-core` p. 78 (PDF 80) |
| `allergy.common` | 7 | Allergen is prevalent in local environment. | `sr5-core` p. 78 (PDF 80) |
| `allergy.mild` | 3 | While affected, `-2` Physical tests. | `sr5-core` p. 78 (PDF 80) |
| `allergy.moderate` | 8 | While affected, `-4` Physical tests. | `sr5-core` p. 78 (PDF 80) |
| `allergy.severe` | 13 | While affected, `-4` all tests and 1 unresisted Physical box per minute exposed. | `sr5-core` p. 78 (PDF 80) |
| `allergy.extreme` | 18 | While affected, `-6` anything and 1 unresisted Physical box per 30 seconds; First Aid, Medicine, or magic can stop shock damage. | `sr5-core` p. 78 (PDF 80) |

### Code Of Honor Profiles

These are `included-component` profiles, never direct purchases.

| ID | Required parameter/effect | Source |
| --- | --- | --- |
| `code-of-honor.protected-group` | One approved likely-to-occur protected group; GM may permit two, at least one likely. Sapient paracritters and a mentor's favored spirit type are eligible. Charisma + Willpower (4) to allow attempted killing; failure requires intervention; character uses nonlethal methods. Each surviving witness raises Public Awareness by 1. Each violent action allowed/taken causes secret `1D6`; on 1 complication, then secret Perception (4) to notice. Lose 1 adventure Karma per protected person killed. | `sr5-core` p. 79 (PDF 81) |
| `code-of-honor.assassins-creed` | Never kill anyone not paid to kill; lose 1 Karma and gain 1 Public Awareness per unintentional/unpaid murder. | `sr5-core` pp. 79-80 (PDF 81-82) |
| `code-of-honor.warriors-code` | Do not kill unarmed, unaware/unprepared, or defenseless people or knowingly risk doing so; lose 1 Karma per such person killed or allowed to be killed. | `sr5-core` p. 80 (PDF 82) |

### Scorched Effect Profiles

These are `included-component` profiles, never direct purchases.

| ID | Effect while active | Source |
| --- | --- | --- |
| `scorched.short-term-memory-loss` | BTL: forget slotting and immediately make another Withdrawal test; failure restores craving/withdrawal and requires another chip. IC: Memory threshold `+1`; failure creates gaps/disorientation. | `sr5-core` p. 83 (PDF 85) |
| `scorched.long-term-memory-loss` | Short-term effects plus loss of access to one active skill, treated unaware, for duration. | `sr5-core` p. 83 (PDF 85) |
| `scorched.blackout` | Retain no memories of the period; technology and magic cannot restore them. | `sr5-core` p. 83 (PDF 85) |
| `scorched.migraines` | `-2` all Physical and Mental tests, light sensitivity, and nausea. | `sr5-core` p. 83 (PDF 85) |
| `scorched.paranoia-anxiety` | Even basic interactions require Social Success test threshold 5; if no skill applies default Charisma `-1`; failure causes paranoia/anxiety for duration. | `sr5-core` p. 83 (PDF 85) |

### SINner Profiles

These are `included-component` profiles, never direct purchases. Each requires
the issuer text and generates one legal identity, not a purchasable fake SIN.

| ID | Award | Registry/access and evaluator-visible obligations | Source |
| --- | ---: | --- | --- |
| `sinner.national` | 5 | National citizen rights; national security/military eligibility; biometric data in Global SIN Registry and shared with law enforcement; always broadcast; 15% gross tax; no megacorp connection. | `sr5-core` p. 84 (PDF 86) |
| `sinner.criminal` | 10 | Corporate or national issuer; replaces prior SIN; always broadcast; felony not to; criminal status/biometrics tracked, frequent questioning and social/access penalties; Awakened registered with law enforcement; 15% gross tax. | `sr5-core` p. 84 (PDF 86) |
| `sinner.corporate-limited` | 15 | Usually replaces National SIN; Global SIN Registry; megacorp employment and possible secret clearance but no leadership/officer/special-forces path; extraction/prejudice liability; 20% gross tax. | `sr5-core` pp. 84-85 (PDF 86-87) |
| `sinner.corporate-born` | 25 | Corporate-born identity; registry confirms validity but additional record is corporation-limited; shadow-community hostility/liability; 10% gross tax. | `sr5-core` p. 85 (PDF 87) |

## Mentor Spirit Components

The following 16 profiles are `included-component` children of `mentor-spirit`,
not separate qualities. All advantages/disadvantages are always active. Magician
and adept branches are mutually exclusive for a mystic adept. Power grants are
zero-cost generated selections with parent provenance and still require every
listed choice; they do not spend Power Points. Custom archetypes are excluded by
`mentor.custom-archetypes`.

| ID | All advantage | Magician advantage | Adept advantage/grant and required choice | Disadvantage | Source |
| --- | --- | --- | --- | --- | --- |
| `mentor.bear` | `+2` resist damage except Drain | `+2` Health spells, preparations, and spell rituals | Generate Rapid Healing rating 1 | On character Physical damage or protected-person serious injury, Simple Charisma + Willpower; berserk `3 - hits` turns, 3 hits avoids; attack aggressors without safety regard | `sr5-core` p. 321 (PDF 323) |
| `mentor.cat` | Choose Gymnastics or Sneaking: `+2` tests | `+2` Illusion spells, preparations, and spell rituals | Generate Light Body rating 2 | At combat start Charisma + Willpower (3), or cannot make incapacitating attack until character takes Physical damage | `sr5-core` p. 321 (PDF 323); `mentor.cat-infiltration` |
| `mentor.dog` | `+2` Tracking | `+2` Detection spells, preparations, and rituals | Generate two Improved Sense selections, each choosing Direction Sense, Improved Tactile, Perfect Pitch, or Human Scale | Charisma + Willpower (3) to leave someone, betray comrades, or let another sacrifice themself | `sr5-core` p. 321 (PDF 323); `power.improved-sense-domain` |
| `mentor.dragonslayer` | Choose one Social skill: `+2` | `+2` Combat spells, preparations, and rituals | Generate Enhanced Accuracy for one eligible skill and Danger Sense rating 1 | Breaking a promise gives `-1` all actions until made good | `sr5-core` pp. 321-322 (PDF 323-324) |
| `mentor.eagle` | `+2` Perception | `+2` summon air spirits | Generate Combat Sense rating 1 | Generate `allergy` with common pollutants/mild profile and 0 award | `sr5-core` p. 322 (PDF 324) |
| `mentor.fire-bringer` | Choose Artisan or Alchemy: `+2` | `+2` Manipulation spells, preparations, and spell rituals | Generate Improved Ability rating 1 for one non-combat skill | Charisma + Willpower (3) to refuse sincere request for help | `sr5-core` p. 322 (PDF 324) |
| `mentor.mountain` | `+2` Survival | `+2` Counterspelling and anchored rituals | Generate Mystic Armor rating 1 | Charisma + Willpower (3) to abandon plan or to proceed without one | `sr5-core` pp. 322-323 (PDF 324-325) |
| `mentor.rat` | `+2` Sneaking | `+2` Alchemy tests harvesting reagents; may use any tradition's reagents | Generate Natural Immunity adept power rating 2, not the positive quality | Charisma + Willpower (3) to avoid immediately fleeing/seeking cover in combat; fight if no escape | `sr5-core` p. 323 (PDF 325) |
| `mentor.raven` | `+2` Con | `+2` Manipulation spells, preparations, and spell rituals | Generate Traceless Walk and Voice Control rating 1 | Charisma + Willpower (3) to avoid exploiting misfortune or pulling a trick/prank | `sr5-core` p. 323 (PDF 325) |
| `mentor.sea` | `+2` Swimming | `+2` summon water spirits | Generate Improved Ability rating 1 for one Athletics-group skill | Charisma + Willpower (3) to give away owned property or be charitable | `sr5-core` p. 323 (PDF 325) |
| `mentor.seducer` | `+2` Con | `+2` Illusion spells, preparations, and spell rituals | Generate Improved Ability rating 1 for one Acting- or Influence-group skill | Charisma + Willpower (3) to avoid available vice/indulgence | `sr5-core` p. 323 (PDF 325) |
| `mentor.shark` | `+2` Unarmed Combat | `+2` Combat spells, preparations, and spell rituals | Generate Killing Hands | On Physical damage, Simple Charisma + Willpower; berserk `3 - hits` turns, 3 hits avoids; attack aggressor and then body without safety regard | `sr5-core` pp. 323-324 (PDF 325-326) |
| `mentor.snake` | `+2` Arcana | `+2` Detection spells, preparations, and spell rituals | Generate Kinesics rating 2 | Charisma + Willpower (3) to avoid pursuing hinted rare secrets/knowledge | `sr5-core` p. 324 (PDF 326) |
| `mentor.thunderbird` | `+2` Intimidation | `+2` summon air spirits | Generate one Critical Strike selection with required combat-skill parameter | Charisma + Willpower (3) to avoid responding in kind to insult | `sr5-core` p. 324 (PDF 326); `mentor.thunderbird-critical-strike` |
| `mentor.wise-warrior` | Choose Leadership or Instruction: `+2` | `+2` Combat spells, preparations, and spell rituals | Generate Improved Ability rating 1 for one Combat skill | Dishonorable/discourteous act gives `-1` all actions until atoned | `sr5-core` p. 324 (PDF 326) |
| `mentor.wolf` | `+2` Tracking | `+2` Combat spells, preparations, and rituals | Generate Attribute Boost (Agility) rating 2 | Charisma + Willpower (3) to retreat from fight | `sr5-core` p. 324 (PDF 326) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| Run Faster qualities | Excluded from this ledger's own review scope (`sr5-core` and this book's approved decisions only). Run Faster's own qualities are now in scope under CHAR-814; see [`RUN_FASTER_QUALITIES.md`](RUN_FASTER_QUALITIES.md). | `../SR5_RULESET_MANIFEST.md` Scope |
| Street-Level/Prime Runner quality budgets | Excluded generation variants; only experienced-runner separate 25/25 ceilings are cataloged. | `sr5-core` p. 64 (PDF 66); manifest Scope |
| Custom mentor archetypes | Excluded because the core allows GM creation but supplies no construction rules; only 16 printed profiles are closed options. | `sr5-core` p. 320 (PDF 322); `mentor.custom-archetypes` |
| Custom Code of Honor mechanics | Excluded. The protected-group rule and two fully defined alternative examples are cataloged; prose says other forms can exist but gives no construction/valuation rule. A text field cannot invent mechanical effects. | `sr5-core` pp. 79-80 (PDF 81-82); `quality.open-parameters` |
| Quality prose examples | Addiction subjects, allergens, dependent types, distinctive features, prejudice targets, and similar examples are not exhaustive catalog entries. They remain typed text parameters. | `sr5-core` pp. 77-85 (PDF 79-87); `quality.open-parameters` |
| Cat mentor `Infiltration` | Source names a nonexistent core skill. Canonical target is Sneaking. | `sr5-core` p. 321 (PDF 323); `mentor.cat-infiltration` |
| Eagle generated Allergy page reference | Eagle says Allergy is on p. 322, but the quality is on p. 78. Preserve the bad internal citation as provenance; use the reviewed Allergy mechanics above. | `sr5-core` pp. 78, 322 (PDF 80, 324) |
| Sensitive System path split | The same quality has mutually exclusive mundane versus Awakened/technomancer effects; do not apply both merely because ware remains technically selectable. | `sr5-core` p. 83 (PDF 85) |
| SINner title wording | Heading/table say `SINner (Layered)`; fourth tier is introduced as both `Corporate Born` and `Corporate SIN`. Canonical sub-option is `Corporate Born`, matching the operative tier text. | `sr5-core` pp. 73, 84-85 (PDF 75, 86-87) |
| Source typo `Combat Paralyisis` | Summary table misspells the quality; detail heading and canonical display name are `Combat Paralysis`. | `sr5-core` pp. 73, 80 (PDF 75, 82) |

## Review Footer

- Reviewed quality rules: `sr5-core` pp. 71-87 (PDF 73-89).
- Reviewed creation/career interactions: `sr5-core` pp. 88, 101, 103, 106-107 (PDF 90, 103, 105, 108-109).
- Reviewed referenced closed domains: Matrix action eligibility pp. 237-244 (PDF 239-246), core spirit types pp. 303-304 (PDF 305-306), and all mentor profiles pp. 320-324 (PDF 322-326).
- Approved-PDF quality headings: 59 total: 31 positive (`selectable` 17, `parameterized` 14) and 28 negative (`selectable` 13, `parameterized` 15).
- Included components: 6 Home Ground profiles, 4 Addiction profiles, 6 Allergy components, 3 Code of Honor profiles, 5 Scorched profiles, 4 SINner profiles, and 16 Mentor Spirit profiles: 44 total.
- Other classifications: `generated` 0 top-level entries (generated child selections are described on parent/profile rows), `bookkeeping` 0, `creation-unavailable` 0, `excluded` 4 option families in the exclusions table.
- Reconciliation: the 31/28 headings exactly match the core summary table on `sr5-core` p. 73 (PDF 75); no unexplained inventory difference.
- Remaining unknown facts: None. Source omissions that intentionally admit no custom mechanics are explicit exclusions, while application text-length bounds remain CHAR-802 implementation constraints rather than rules facts.
- Runtime reconciliation status: Not implemented (CHAR-802).
