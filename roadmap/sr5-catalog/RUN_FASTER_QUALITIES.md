# Run Faster Qualities Ledger (CHAR-814)

This is the CHAR-814 source ledger for Run Faster's new qualities. It is a
review input for the runtime catalog change it accompanies, not a substitute
for the approved book. It extends [`QUALITIES.md`](QUALITIES.md), which
remains the ledger for the 59 `sr5-core` qualities plus the single Run Faster
quality ("Poor Self Control (Vindictive)") admitted by CHAR-813 to complete
the Hobgoblin metavariant's trait bundle.

CHAR-814 is a project-owner-approved expansion of the `QUALITIES.md`
exclusion row "Run Faster qualities | Excluded in full." Per the project
owner's 2026-08-26 decision, every quality and quality sub-option printed in
Run Faster's "New Quality" (Rank) and "Qualities for Good or Ill" material is
now in scope, including the remaining four Poor Self Control variants that
CHAR-813 deliberately left out as beyond its own ticket's scope.

## Source

Only `run-faster`, pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md), is used. Rank is
printed in the Construction Kits chapter's "New Quality" section
(`run-faster` p. 86, PDF 88); every other entry is printed in "Qualities for
Good or Ill" (`run-faster` pp. 144-159, PDF 146-161), which carries the same
two-page printed/PDF offset as every other reviewed range in that manifest.

## Scope

Included:

- `rank`, the sole entry under Run Faster's own "New Quality" heading. It is
  printed inside the Construction Kits chapter alongside the excluded Point
  Buy and Life Modules creation methods, but the quality itself has no
  Life-Modules-specific mechanic and is usable under Standard Priority or
  Sum-to-Ten exactly like any other quality. Decision:
  `quality.rank-scope`. **Approved** (project owner, 2026-08-26): include it.
- All 42 "Positive Qualities" and 38 "Negative Qualities" headings printed in
  "Qualities for Good or Ill" (`run-faster` pp. 144-159, PDF 146-161).
- The four remaining Poor Self Control variants (Braggart, Thrill-Seeker,
  Compulsive, Combat Monster) that CHAR-813 did not add, published as four
  additional fixed-cost catalog entries alongside the existing
  `poor-self-control-vindictive`. Decision:
  `quality.poor-self-control-family-completion`. **Approved** (project
  owner, 2026-08-26): add all four.

Excluded (unchanged from the manifest's existing Scope section):

- Every Run Faster metagenic quality, Infected quality, and critter power
  from the Changelings/SURGE chapter (`run-faster` pp. 111-141, PDF 113-143).
- Point Buy and Life Modules creation methods and their example "Odd Jobs"
  and background-module content, including the "Celebrity," "Corporate,"
  "Regular Job," and similar occupation entries that a first, uncorrected
  table-of-contents scan can be mistaken for qualities; verification against
  the actual chapter text on `run-faster` pp. 76-84 (PDF 78-86) confirmed
  they are Life Modules career-module options, not quality catalog entries,
  and remain excluded under the existing Life Modules exclusion.

## Cost-Modeling Convention

`RulesetCatalog.QualityDefinition` carries one flat `Cost` integer per
catalog entry, the same shape `sr5-core`'s own tiered/summed qualities
already use (for example `sinner-layered`'s National/Criminal/Corporate
Limited/Corporate Born tiers of 5/10/15/25 publish only `cost: 5`, and
`allergy`'s summed prevalence-plus-severity range of 5-25 publishes only
`cost: 5`). This ledger follows the same established convention for every
multi-tier or summed Run Faster quality: the catalog `Cost` field is the
first Karma value the book lists for that quality's cost/bonus line (which
is also its lowest value in every case reviewed here), and the full tier
table is preserved in this ledger and in the frontend's descriptive text
rather than being separately enforced. This is a pre-existing modeling
simplification of the runtime catalog, not a new decision introduced here.

Where a quality's tiers or per-rating cost are uniform steps of the same
size (for example Fame's 4/8/12/16 or Spike Resistance's 10-per-rating), it
is published as `parameterized` and `repeatable`, matching how `sr5-core`
already models `high-pain-tolerance` and `will-to-live`: taking the quality
multiple times accumulates the rating, each instance billed at the flat
`Cost`.

## New Positive Qualities

| ID | Display name | Class | Cost | Repeat | Effect summary | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `rank` | Rank | PP | 5 per level (civilian) / 20 per level (military or law enforcement) | yes, up to 3 levels | +1 Social Limit per level toward people inside your organization, or the public you have authority over on the military/law-enforcement track. | `run-faster` p. 86 (PDF 88) |
| `adrenaline-surge` | Adrenaline Surge | P | 12 | no | Act first in the first Initiative Pass of a new combat even without the highest Initiative Score, unless Surprised. | `run-faster` p. 145 (PDF 147) |
| `animal-empathy` | Animal Empathy | P | 3 | no | +2 dice pool modifier influencing or controlling an animal, including riding and Awakened species. | `run-faster` p. 145 (PDF 147) |
| `black-market-pipeline` | Black Market Pipeline | PP | 10 | no | One chosen contact plus merchandise category: 10% price cut and +2 dice pool modifier buying; better sell-back rate. | `run-faster` p. 145 (PDF 147) |
| `born-rich` | Born Rich | P | 5 | no | Trade up to 40 Karma (instead of the normal 10) for starting nuyen at 2,000¥ per point. | `run-faster` p. 145 (PDF 147) |
| `city-slicker` | City Slicker | P | 7 | no | +1 dice pool modifier to Outdoors group skills in urban environments; -1 to Perception/Survival elsewhere. | `run-faster` p. 145 (PDF 147) |
| `college-education` | College Education | P | 4 | no | Academic Knowledge skills at half price during creation; -1 Karma per rank 3+ afterward. | `run-faster` p. 145 (PDF 147) |
| `common-sense` | Common Sense | P | 3 | no | Gamemaster must warn you before a foolish action, up to Edge rating times per session. | `run-faster` p. 145 (PDF 147) |
| `daredevil` | Daredevil | P | 6 | no | Recover 2 Edge instead of 1 for an exceptionally daring action. | `run-faster` p. 146 (PDF 148) |
| `digital-doppelganger` | Digital Doppelganger | P | 7 | no | +2 threshold for Matrix searches/tracking tied to your SIN; requires SINner or a Rating 4+ fake SIN. | `run-faster` p. 146 (PDF 148) |
| `disgraced` | Disgraced | P | 2 | no | +2 dice pool modifier intimidating criminals who remember you; treated as Prejudiced by upright citizens. | `run-faster` p. 146 (PDF 148) |
| `erased` | Erased | P | 8 | no; incompatible with `records-on-file` | No SIN, minimal legwork/Matrix trail, Public Awareness capped at 1; cannot hold a Lifestyle above Middle for long or a fake SIN past three months. | `run-faster` p. 146 (PDF 148) |
| `fame` | Fame | PP | 4 per tier (Local/National/Megacorporate/Global) | yes, up to 4 tiers | Growing Social Limit and dice pool bonus with people who know you; growing Public Awareness and recognition risk; requires SINner or a Rating 3+ fake SIN. | `run-faster` pp. 146-147 (PDF 148-149) |
| `friends-in-high-places` | Friends in High Places | P | 8 | no | Extra Charisma x 4 Karma pool for contacts, none below Connection 8; unspent Karma is lost. | `run-faster` p. 148 (PDF 150) |
| `hawk-eye` | Hawk Eye | P | 3 | no | +1 dice pool modifier to Perception; shifts Range Environmental modifiers one category better; incompatible with cyber/bioware vision replacement. | `run-faster` p. 148 (PDF 150) |
| `inspired` | Inspired | PP | 4 | no, explicitly once | +1 dice pool modifier to Artisan or Performance skills (chosen once) and +2 Street Cred among fellow artists. | `run-faster` p. 148 (PDF 150) |
| `jack-of-all-trades` | Jack of All Trades, Master of None | P | 2 | no | -1 Karma (min 1) learning skills up to Rating 5; +2 Karma per point past Rating 5. Does not apply during creation. | `run-faster` p. 148 (PDF 150) |
| `lightning-reflexes` | Lightning Reflexes | P | 20 | no | +1 Initiative and a bonus Initiative die (not stacking with other Initiative sources); +1 dice pool modifier to Defense Tests. | `run-faster` p. 148 (PDF 150) |
| `linguist` | Linguist | P | 4 | no | Halves language learning time; +1 dice pool modifier to Language tests; 2-for-1 Language points at creation; -1 Karma per rank 3+ afterward. | `run-faster` p. 148 (PDF 150) |
| `made-man` | Made Man | PP | 5 | no | A chosen crime syndicate becomes a free Group Contact at Loyalty 3, usable as fence/black-market source, with real work obligations. | `run-faster` p. 148 (PDF 150) |
| `night-vision` | Night Vision | P | 2 | no | Grants low-light vision with amplified sun-glare penalties. | `run-faster` p. 148 (PDF 150) |
| `outdoorsman` | Outdoorsman | P | 3 | no | +2 dice pool modifier to Outdoors group skills in rural/wild environments; -1 to Perception/Survival in cities. | `run-faster` p. 148 (PDF 150) |
| `overclocker` | Overclocker | P | 5 | no | +1 Rating to one chosen cyberdeck ASDF attribute, reallocatable on reconfiguration. | `run-faster` p. 148 (PDF 150) |
| `perceptive` | Perceptive | PP | 5 per level | yes, up to 2 levels | +1 dice pool modifier to Perception (including Astral/Matrix); a second purchase raises it to +2. | `run-faster` p. 148 (PDF 150) |
| `perfect-time` | Perfect Time | P | 5 | no | Always know the exact time; +1 dice pool modifier to timing-based Performance tests; one extra Free Action per Action Phase. | `run-faster` p. 148 (PDF 150) |
| `poor-link` | Poor Link | P | 8 | no | -2 dice pool modifier on ritual sorcery targeting you (even friendly); +2 dice pool modifier resisting any ritual's test. | `run-faster` pp. 148-149 (PDF 150-151) |
| `privileged-family-name` | Privileged Family Name | P | 7 | no | -2 dice pool modifier for local NPCs on Social tests against you in your home sprawl; requires SINner (National or Corporate). | `run-faster` p. 149 (PDF 151) |
| `restricted-gear` | Restricted Gear | PP | 10 per item | yes, up to 3, only 1 at creation | Buy one item above the normal Availability limit (up to 24 at creation, up to 18 with a 30% markup in play). | `run-faster` p. 149 (PDF 151) |
| `school-of-hard-knocks` | School of Hard Knocks | P | 4 | no | Street Knowledge skills at 2-for-1 during creation; -1 Karma per rank 3+ afterward. | `run-faster` p. 149 (PDF 151) |
| `sense-of-direction` | Sense of Direction | P | 3 | no | Always know true north and retrace your path with 1+ rank of Survival; +1 dice pool modifier to Navigation. | `run-faster` p. 149 (PDF 151) |
| `sensei` | Sensei | PP | 5 | no | A Connection 3+ contact personally mentors one chosen skill or skill group at no charge. | `run-faster` p. 149 (PDF 151) |
| `solid-legendary-rep` | Solid/Legendary Rep | PP | 2 (solid) / 4 (legendary) | no, explicitly once | +1 or +2 Reputation with one specific 1,000-5,000-member group, forgiving later missteps. | `run-faster` p. 150 (PDF 152) |
| `speed-reading` | Speed Reading | P | 2 | no | Read an 800-word page in ~5 seconds or an 800-page book in ~1 hour, gaining a basic (not memorized) understanding. | `run-faster` p. 149 (PDF 151) |
| `spike-resistance` | Spike Resistance | PP | 10 per rating | yes, up to 3 | +1 dice pool modifier per rating resisting harmful biofeedback (black IC, dumpshock, black hammer). | `run-faster` p. 150 (PDF 152) |
| `spirit-whisperer` | Spirit Whisperer | P | 8 | no | Spirits resist Summoning with +1 die, but arrive 1 Force higher than declared and take a curious interest in you. | `run-faster` p. 150 (PDF 152) |
| `steely-eyed-wheelman` | Steely Eyed Wheelman | P | 2 | no | Vehicle Test Terrain Modifiers reduced by 1, minimum 0. | `run-faster` p. 150 (PDF 152) |
| `technical-school-education` | Technical School Education | P | 4 | no | Professional Knowledge skills at 2-for-1 during creation; -1 Karma per rank 3+ afterward. | `run-faster` p. 150 (PDF 152) |
| `tough-as-nails` | Tough as Nails | PP | 5 per purchase | yes, up to 4, max 3 per track | +1 box to a chosen Condition Monitor (Physical or Stun) per purchase. | `run-faster` p. 150 (PDF 152) |
| `trust-fund` | Trust Fund | PP | 5 / 10 / 15 / 20 | no | A managed inheritance covers a chosen Lifestyle tier plus extra monthly nuyen; requires SINner (National or Corporate). | `run-faster` p. 151 (PDF 153) |
| `trustworthy` | Trustworthy | P | 15 | no | +1 dice pool modifier to Influence group skills; +2 Social Limit for trust-dependent situations. | `run-faster` p. 151 (PDF 153) |
| `vehicle-empathy` | Vehicle Empathy | P | 7 | no | +1 dice pool modifier to Pilot tests and +1 Handling Rating while personally driving or jacked in. | `run-faster` p. 151 (PDF 153) |
| `water-sprite` | Water Sprite | P | 6 | no | +2 dice pool modifier to Diving/Swimming and breath-holding/treading-water tests. | `run-faster` p. 151 (PDF 153) |
| `witness-my-hate` | Witness My Hate | P | 7 | no | Single-target Direct Damage spells resolve at +2 DV; Drain Value for those spells also +2. Requires a spellcasting path. | `run-faster` p. 151 (PDF 153) |

## New Negative Qualities

| ID | Display name | Class | Award | Repeat | Effect summary | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `albinism` | Albinism | N | 4 (8 without cybereyes) | no | Worse Glare penalties, faster sunburn; award drops to 4 only if the character starts play with cybereyes. | `run-faster` p. 151 (PDF 153) |
| `amnesia` | Amnesia | NP | 4 (surface) / 8 (neural deletion) | no | Surface memory loss keeps skills but hides their origin; neural deletion rebuilds the character's knowledge from near nothing with the gamemaster. | `run-faster` p. 152 (PDF 154) |
| `asthma` | Asthma | N | 8 | no | Fatigue damage effects trigger twice as often and escalate per the Asthma Effects table. | `run-faster` p. 152 (PDF 154) |
| `bi-polar` | Bi-Polar | N | 7 | no | Manic/depressive/stable states (re-rolled roughly daily) with opposing Physical/Mental dice pool swings. | `run-faster` p. 152 (PDF 154) |
| `big-regret` | Big Regret | N | 5 | no | -3 Social Limit against anyone who knows a serious past mistake; blackmail risk. | `run-faster` p. 153 (PDF 155) |
| `blind` | Blind | N | 15 (5 with astral sight) | no | Automatic failure on vision-based Perception and heavy combat/Perception/Surprise penalties; cybereyes cannot correct it. | `run-faster` p. 153 (PDF 155) |
| `borrowed-time` | Borrowed Time | N | 20 | no; cannot be bought off | Death arrives at an unknown gamemaster-rolled moment; surviving it costs all current Edge. | `run-faster` p. 153 (PDF 155) |
| `computer-illiterate` | Computer Illiterate | N | 7 | no | -4 dice pool modifier on any computer/electronic/Matrix-connected test. | `run-faster` p. 153 (PDF 155) |
| `creature-of-comfort` | Creature of Comfort | NP | 10 / 17 / 25 | no | Accustomed to Middle/High/Luxury Lifestyle; -1 dice pool modifier per tier below it to Social/Healing tests while "slumming." | `run-faster` p. 153 (PDF 155) |
| `day-job` | Day Job | NP | 5 / 10 / 15 | no | A real job with real hours; requires a valid SIN; missing shifts risks losing the job (2 Street Cred and a month's pay). | `run-faster` p. 154 (PDF 156) |
| `deaf` | Deaf | N | 15 | no | Automatic failure on audio Perception; general Perception/Surprise penalties. | `run-faster` p. 154 (PDF 156) |
| `did-you-just-call-me-dumb` | Did You Just Call Me Dumb? | N | 3 | no | Every Glitch on a Social test automatically counts as a Critical Glitch. | `run-faster` p. 154 (PDF 156) |
| `dimmer-bulb` | Dimmer Bulb | NP | 5 per level | yes, up to 3 | -1 dice pool modifier per level to Logic/Intuition tests. | `run-faster` p. 154 (PDF 156) |
| `driven` | Driven | N | 2 | no | Must test Willpower + Logic (4) to resist dropping everything for a lead; +1 Willpower while pursuing one. | `run-faster` p. 154 (PDF 156) |
| `emotional-attachment` | Emotional Attachment | N | 5 | no | An irrationally beloved item you'll risk everything to keep or recover. | `run-faster` p. 154 (PDF 156) |
| `ex-con` | Ex-Con | N | 15 | no | Fresh parole: detailed police/prison file, mandatory check-ins, no Restricted/Forbidden augmentations, limited corp/law-enforcement contacts. | `run-faster` p. 155 (PDF 157) |
| `flashbacks` | Flashbacks | NP | 7 / 15 | no | A specific trigger forces a Composure (5) Test or incapacitates you with hallucinations; rarer triggers cost less. | `run-faster` p. 155 (PDF 157) |
| `hobo-with-a-shotgun` | Hobo with a Shotgun | N | 10 | no | Refuses lodging above Squatter; forced otherwise, -2 Mental attributes until a day back on the street. | `run-faster` p. 155 (PDF 157) |
| `hung-out-to-dry` | Hung Out to Dry | N | 8 | no | Contacts inexplicably go cold; a mystery for the character to unravel. | `run-faster` p. 155 (PDF 157) |
| `illiterate` | Illiterate | N | 5 | no | Cannot read: -1 Social Limit once known, -2 dice pool modifier on unfamiliar interfaces, no reading-dependent Knowledge skills at creation, double Karma for them until literate. | `run-faster` p. 155 (PDF 157) |
| `in-debt` | In Debt | NP | 1 per point (max 15) | yes, up to 15 | Each point trades for 5,000¥ extra starting funds; debt grows 10%/month and unpaid interest costs Physical damage. | `run-faster` p. 156 (PDF 158) |
| `incomplete-deprogramming` | Incomplete Deprogramming | N | 10 | no | A poorly deprogrammed cover identity can seize control under stress (Composure 4 Test) for several minutes. | `run-faster` p. 156 (PDF 158) |
| `infirm` | Infirm | NP | 5 per purchase | yes, up to 5 | -1 to every Physical attribute maximum per purchase (never below 1); no source may exceed the lowered maximum. | `run-faster` p. 156 (PDF 158) |
| `liar` | Liar | N | 7 | no | -1 dice pool modifier to Social skills; 1-in-6 chance per conversation the listener assumes you're lying outright. | `run-faster` p. 156 (PDF 158) |
| `night-blindness` | Night Blindness | N | 6 | no; incompatible with other eye-affecting qualities (narrative, not encoded) | Every Light/Glare Environmental modifier is one category worse; must be bought off if corrected with cyber-/bioware. | `run-faster` p. 156 (PDF 158) |
| `oblivious` | Oblivious | NP | 6 / 10 | no | -2 dice pool modifier to all Perception; the higher tier also raises every Perception threshold by 1. | `run-faster` p. 157 (PDF 159) |
| `pacifist` | Pacifist | NP | 10 / 15 | no | Refuses (and discourages) harm outside self-defense; the stricter tier adds guilt penalties and permanent Willpower/Charisma loss for any violence. | `run-faster` p. 157 (PDF 159) |
| `paranoia` | Paranoia | N | 7 | no | -2 dice pool modifier on Social tests with unfamiliar people or low-Loyalty contacts; hides address, moves often. | `run-faster` p. 157 (PDF 159) |
| `paraplegic` | Paraplegic | N | 10 | no | Paralyzed from the waist down; fast wheelchair movement (Agility x 3/x 4) but struggles with stairs/curbs; +10% Lifestyle and vehicle-modification cost. | `run-faster` p. 157 (PDF 159) |
| `phobia` | Phobia | NP | 5 (Uncommon+Mild) up to 15 (Common+Severe) | no | A visceral fear: dice pool penalty scaling with severity (Mild -1/Moderate -3/Severe -6) and how often the trigger occurs (Uncommon/Common). | `run-faster` p. 157 (PDF 159) |
| `pie-iesu-domine` | Pie Iesu Domine, Dona Eis Requiem | N | 2 | no | A flagellant: gains High Pain Tolerance 1 but starts each day with 1 box of self-inflicted Physical damage. | `run-faster` p. 158 (PDF 160) |
| `poor-self-control-braggart` | Poor Self Control (Braggart) | N | 5 | no | Compulsively one-ups others' claims of success absent a Composure (3) Test. | `run-faster` p. 158 (PDF 160) |
| `poor-self-control-thrill-seeker` | Poor Self Control (Thrill-Seeker) | N | 4 | no | Always takes the riskiest option absent a Composure (2) Test; +1 Initiative Score for 5 Combat Turns when it wins. | `run-faster` p. 158 (PDF 160) |
| `poor-self-control-compulsive` | Poor Self Control (Compulsive) | NP | 4 (threshold 1, personal scope) up to 12 (threshold 4, broad public scope) | no | A compulsive need for order in a chosen sphere; resisted with a Composure Test whose threshold sets the award. | `run-faster` p. 158 (PDF 160) |
| `poor-self-control-vindictive` *(existing, CHAR-813)* | Poor Self Control (Vindictive) | N | 5 | no | No slight goes unanswered absent a Composure (2) Test; escalates and remembers every offender. | `run-faster` p. 158 (PDF 160) |
| `poor-self-control-combat-monster` | Poor Self Control (Combat Monster) | N | 10 | no | Fights until every opponent is down absent a Composure (3) Test to disengage. | `run-faster` p. 158 (PDF 160) |
| `records-on-file` | Records on File | NP | 1 per rating (max 10) | yes, up to 10; incompatible with `erased` | Per rating, one Big Ten megacorp holds an up-to-date file, granting it +2 dice pool modifier to identify/track you in its territory. | `run-faster` pp. 158-159 (PDF 160-161) |
| `reduced-sense` | Reduced (Sense) | NP | 2 (smell/taste) to 10 (touch) | yes, one per distinct sense | -2 dice pool modifier on tests using the chosen sense; stacks across senses for tests drawing on more than one. | `run-faster` p. 159 (PDF 161) |
| `sensory-overload-syndrome` | Sensory Overload Syndrome | N | 15 | no | High-ARO areas or sensory enhancement risk a Willpower + Edge (4) Test or a multi-minute epileptic seizure. | `run-faster` p. 159 (PDF 161) |
| `signature` | Signature | N | 10 | no | Compulsively leaves a calling card; anyone tracing your handiwork gets a dice pool bonus equal to Street Cred plus Public Awareness. | `run-faster` p. 159 (PDF 161) |
| `vendetta` | Vendetta | N | 7 | no | A blood feud: encountering the foe forces a Composure (3) Test or a compelled confrontation. | `run-faster` p. 159 (PDF 161) |
| `wanted` | Wanted | N | 10 | no | A 25,000¥+ bounty on your head, temptation even for former friends. | `run-faster` p. 159 (PDF 161) |

## Unenforced Prerequisites And Nuances (Documentation-Only)

Consistent with how `sr5-core`'s own quality prerequisites (Magic rating,
GM approval, and similar) are recorded in `QUALITIES.md` without runtime
enforcement beyond the generic `Parameterized`/`Conflicts` fields, the
following Run Faster prerequisites and cross-quality nuances are recorded
here for GM reference only and are not evaluator-enforced:

| Quality | Unenforced condition | Source |
| --- | --- | --- |
| `digital-doppelganger` | Requires the `sinner-layered` quality or a Rating 4+ fake SIN. | `run-faster` p. 146 (PDF 148) |
| `fame` | Requires the `sinner-layered` quality or a Rating 3+ fake SIN. | `run-faster` p. 146 (PDF 148) |
| `privileged-family-name` | Requires the `sinner-layered` quality (National or Corporate tier). | `run-faster` p. 149 (PDF 151) |
| `trust-fund` | Requires the `sinner-layered` quality (National or Corporate tier). | `run-faster` p. 151 (PDF 153) |
| `day-job` | Requires the `sinner-layered` quality or a Rating 4+ fake SIN. | `run-faster` p. 154 (PDF 156) |
| `witness-my-hate` | Requires a spellcasting Magic/Resonance path. | `run-faster` p. 151 (PDF 153) |
| `hawk-eye` | Incompatible with cyber/bioware vision replacement (narrative, not a catalog conflict). | `run-faster` p. 148 (PDF 150) |
| `night-blindness` | "Incompatible with any other quality that affects the eyes" is prose-level, not a closed list, so no `conflicts` entries were added. | `run-faster` p. 156 (PDF 158) |
| `reduced-sense` / `blind` / `deaf` | The book bars Reduced (sight) with Blind and Reduced (hearing) with Deaf specifically, not Reduced (any sense) with either. Because `reduced-sense`'s catalog entry covers every sense through one parameterized quality, this pairing cannot be expressed as a blanket `conflicts` entry without wrongly blocking, for example, Reduced (smell) plus Blind. Left unenforced and recorded here. | `run-faster` p. 159 (PDF 161) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| Life Modules "Odd Jobs" and career-module entries (Celebrity, Corporate, Regular Job, Shadow Work, Terrorist, Tours of Duty, and similar) | Excluded. These are Life Modules background options, not qualities, despite sitting in the same table-of-contents column as page numbers near the Qualities chapter. | `run-faster` pp. 76-84 (PDF 78-86); unchanged Life Modules exclusion |
| Poor Self Control Karma-cost header ("4 TO 12 KARMA") | The umbrella quality is published here as five independent fixed-cost catalog entries (one per named variant: Braggart 5, Thrill-Seeker 4, Compulsive 4-12, Vindictive 5, Combat Monster 10) rather than one parameterized quality, because each variant has its own distinct mechanical effect text, matching how the existing `poor-self-control-vindictive` entry was already modeled under CHAR-813. | `run-faster` p. 158 (PDF 160) |
| Poor Self Control (Compulsive) Karma formula | "Base Karma value is (2 x threshold needed for Composure Test; must be from 1 to 4); then add 2/3/4" for personal/single-public/broad-public scope. Catalog `Cost` publishes the minimum (threshold 1, personal scope: 2x1+2=4); the full formula is preserved here rather than modeled as a computed field. | `run-faster` p. 158 (PDF 160) |
| Phobia table OCR/typeset ambiguity | The printed Karma table's row labels (Mild/Moderate/Severe) visually collide with its Karma-value column in a way that could misattribute Moderate's -3 dice pool prose effect to a "3 Karma" cell. This ledger cross-checked the table's three Karma values (3/5/10) against the chapter's own unambiguous prose ("Mild ... -1 ... Moderate ... -3 ... Severe ... -6") and against the header's stated total range ("BONUS: 5 TO 15 KARMA"): Uncommon(2)+Mild(3)=5 and Common(5)+Severe(10)=15 both reconcile exactly, confirming Mild=3, Moderate=5, Severe=10. | `run-faster` pp. 157-158 (PDF 159-160) |
| Albinism, Amnesia, Blind, Flashbacks, Creature of Comfort, Day Job, Oblivious, Pacifist tier ordering | Where a quality's header lists Karma values in an order that is not simple ascending/descending (for example Albinism's "4 OR 8," where 8 is the default and 4 is the cybereyes exception), this ledger and the catalog `Cost` field both follow the existing project convention of using the first-listed value, matching how `sr5-core`'s own `natural-immunity` and `sinner-layered` entries were modeled. | `run-faster` pp. 151-157 (PDF 153-159) |

## Review Footer

- Reviewed quality rules: `run-faster` p. 86 (PDF 88) and pp. 144-159 (PDF
  146-161).
- Reviewed cross-references: `sr5-core` quality mechanics referenced by name
  (Photographic Memory, Pain Resistance adept power) were not re-verified
  beyond confirming they already exist in the CHAR-807 core catalog.
- Approved-PDF quality headings in scope: 1 (Rank) + 42 positive + 38
  negative = 81 headings. Poor Self Control's single heading expands to 5
  catalog entries (1 already published under CHAR-813, 4 new here), so the
  runtime catalog gains 84 new entries from the 80 headings not already
  published (81 minus the already-published Poor Self Control heading),
  plus Poor Self Control's 4 remaining variants.
- Other classifications: `excluded` 1 category (Life Modules career-module
  content occupying adjacent table-of-contents space).
- Reconciliation: 84 new catalog entries (43 positive including `rank`, 41
  negative including 4 new Poor Self Control variants) plus the
  already-published `poor-self-control-vindictive` account for all 81
  in-scope headings with no unexplained inventory difference.
- Remaining unknown facts: None. The Phobia table ambiguity and the
  tier-ordering convention are resolved above by cross-checking prose,
  header totals, and existing project precedent rather than left open.
- Runtime reconciliation status: Implemented (CHAR-814) as catalog version
  `sr5-core` `1.3.0`, an overlay on `1.0.0` republishing `1.1.0`/`1.2.0`'s
  additive content plus this ledger's 84 new qualities.
