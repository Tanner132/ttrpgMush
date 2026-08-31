# Run & Gun Martial Arts Ledger (CHAR-822)

This is the CHAR-822 source ledger for Run & Gun's Martial Arts subsystem
("Way of the Warrior", `run-gun` pp. 128-142, PDF 130-144). It is a review
input for the runtime catalog and evaluator change it accompanies, not a
substitute for the approved book. This ticket un-defers the "Run & Gun:
Martial Arts (styles + techniques)" item recorded in
[`../SR5_CATALOG_DEFERRED_WORK.md`](../SR5_CATALOG_DEFERRED_WORK.md) on
2026-08-28.

Unlike the plain catalog ports before it, this ticket adds a small subsystem:
two new catalog sections (`martialArtStyles`, `martialArtTechniques`), a new
draft-document field (`martialArts: { styleId, techniqueIds }`), a dedicated
`MartialArtsEvaluator`, Karma costing in `KarmaBudgetEvaluator`, canonical
sheet output, and a new optional `martial-arts` step (index 12) in the
character-creation UI.

## Source

Only `run-gun` is used, already pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md). Citations use the
same two-page printed/PDF offset verified for CHAR-815 through CHAR-818.

## Rules Model (What Is Enforced)

- **One style at creation** (`run-gun` p. 142, PDF 144). The draft document
  holds at most one `martialArts` selection; the whole step is optional.
- **Karma costing:** the style costs **7 Karma and includes the first
  technique**; each additional technique costs **5 Karma**; at most **5
  techniques** may be learned at creation (7 + 5×(n−1), maximum 27 Karma).
  Costs are charged against general Karma by `KarmaBudgetEvaluator`.
- **Technique legality:** every selected technique must appear in the chosen
  style's own six-technique list **or** be universal (`universal: true` —
  Neijia and Strike the Darkness, learnable with any style and deliberately
  not repeated inside style lists).
- **Evaluator diagnostics** (`MartialArtsEvaluator`, step `martial-arts`):
  `martial-arts.style.unknown`, `martial-arts.technique.unknown`,
  `martial-arts.technique.duplicate`, `martial-arts.technique.not-in-style`,
  `martial-arts.technique.required` (a chosen style must take at least its
  included first technique), and `martial-arts.technique.limit-exceeded`.
- **Sheet output:** the finalized canonical sheet records the style and
  technique display names with their source citations.

Technique mechanical effects (bonus dice, Called Shot penalty reductions,
new combat actions) are catalog-display text only — condensed original
paraphrases in the frontend, never code-enforced — consistent with the
project-wide "combat resolution is not modeled" convention and the existing
Killshots and More exclusion.

## Styles

All 42 styles in the chapter, each publishing exactly its six printed
techniques.

| ID | Display name | Techniques | Source |
| --- | --- | --- | --- |
| `fifty-two-blocks` | 52 Blocks | `called-shot-disarm`, `kick-attack`, `multiple-opponent-defense-defender-has-defended`, `pouncing-dragon`, `randori-dirty-trick`, `rooted-tree` | `run-gun` p. 128 (PDF 130) |
| `aikido` | Aikido | `called-shot-disarm`, `constrictors-crush`, `counterstrike`, `throw-person`, `yielding-force-counter-strike`, `yielding-force-throw` | `run-gun` p. 128 (PDF 130) |
| `arnis-de-mano` | Arnis De Mano | `close-quarter-defense-against-firearms`, `multiple-opponent-combat`, `opposing-force-parry`, `randori-vitals`, `two-weapon-style-attack`, `two-weapon-style-defense` | `run-gun` p. 128 (PDF 130) |
| `bartitsu` | Bartitsu | `ballestra`, `bending-of-the-reed`, `called-shot-disarm`, `kick-attack`, `riposte`, `sweep` | `run-gun` p. 128 (PDF 130) |
| `boxing-brawler` | Boxing (Brawler Style) | `clinch`, `full-offense`, `haymaker`, `opposing-force-block`, `stagger`, `thunder-strike` | `run-gun` p. 129 (PDF 131) |
| `boxing-classic` | Boxing (Classic Style) | `bending-of-the-reed`, `called-shot-feint`, `haymaker`, `oaken-stance-defense-against-being-knocked-down`, `opposing-force-block`, `silken-storm` | `run-gun` p. 129 (PDF 131) |
| `boxing-swarmer` | Boxing (Swarmer Style) | `bending-of-the-reed`, `called-shot-feint`, `clinch`, `haymaker`, `silken-storm`, `two-headed-snake` | `run-gun` p. 129 (PDF 131) |
| `capoeira` | Capoeira | `bending-of-the-reed`, `called-shot-feint`, `kick-attack`, `kip-up`, `sweep`, `tricking` | `run-gun` p. 129 (PDF 131) |
| `carromeleg` | Carromeleg | `counterstrike`, `iaijutsu`, `imposing-stone`, `riposte`, `shadow-block`, `stagger` | `run-gun` p. 129 (PDF 131) |
| `chakram-fighting` | Chakram Fighting | `called-shot-pin`, `close-quarter-firearms-thrown-weapons`, `knucklebreaker-blast-out-of-hands`, `multiple-opponent-defense-friends-in-melee`, `opposing-force-block`, `ti-khao` | `run-gun` p. 129 (PDF 131) |
| `drunken-boxing` | Drunken Boxing | `called-shot-disarm`, `called-shot-feint`, `defiant-dance`, `full-offense`, `karmic-response`, `two-headed-snake` | `run-gun` p. 130 (PDF 132) |
| `fiore-dei-liberi` | Fiore dei Liberi (Two-Weapon Sword Fighting) | `called-shot-break-weapon`, `opposing-force-parry`, `riposte`, `two-weapon-style-attack`, `two-weapon-style-defense`, `yielding-force-riposte` | `run-gun` p. 130 (PDF 132) |
| `firefight` | Firefight | `clinch`, `close-quarter-defense-against-firearms`, `close-quarter-firearms-pistols`, `multiple-opponent-defense-friends-in-melee`, `oaken-stance-defense-against-being-knocked-down`, `oaken-stance-defense-against-being-charged` | `run-gun` p. 130 (PDF 132) |
| `gun-kata` | Gun Kata | `close-quarter-firearms-pistols`, `kip-up`, `multiple-opponent-defense-friends-in-melee`, `opposing-force-block`, `tricking`, `stagger` | `run-gun` p. 130 (PDF 132) |
| `jeet-kune-do` | Jeet Kune Do | `bending-of-the-reed`, `counterstrike`, `kick-attack`, `opposing-force-block`, `randori-vitals`, `yielding-force-counter-strike` | `run-gun` p. 130 (PDF 132) |
| `jogo-du-pau` | Jogo Du Pau | `barbed-hooks`, `herding`, `oaken-stance-defense-against-being-charged`, `oaken-stance-defense-against-being-knocked-down`, `opposing-force-parry`, `pouncing-dragon` | `run-gun` p. 130 (PDF 132) |
| `jujitsu` | Jujitsu | `called-shot-disarm`, `chin-na`, `clinch`, `sacrifice-throw`, `sweep`, `throw-person` | `run-gun` p. 131 (PDF 133) |
| `karate` | Karate | `counterstrike`, `kick-attack`, `kip-up`, `opposing-force-block`, `sweep`, `yielding-force-counter-strike` | `run-gun` p. 131 (PDF 133) |
| `kenjutsu` | Kenjutsu | `bending-of-the-reed`, `finishing-move`, `iaijutsu`, `multiple-opponent-combat`, `multiple-opponent-defense-friends-in-melee`, `opposing-force-parry` | `run-gun` p. 131 (PDF 133) |
| `knight-errant-tactical` | Knight Errant Tactical | `barbed-hooks`, `broken-fang`, `called-shot-break-weapon`, `close-quarter-defense-against-firearms`, `hammer-fist`, `imposing-stone` | `run-gun` p. 132 (PDF 134) |
| `krav-maga` | Krav Maga | `called-shot-disarm`, `clinch`, `constrictors-crush`, `imposing-stone`, `releasing-talons`, `ti-khao` | `run-gun` p. 132 (PDF 134) |
| `kunst-des-fechtens` | Kunst des Fechtens (Longsword Fighting) | `half-sword`, `multiple-opponent-combat`, `opposing-force-parry`, `pouncing-dragon`, `riposte`, `yielding-force-riposte` | `run-gun` p. 132 (PDF 134) |
| `kyujutsu` | Kyujutsu | `called-shot-pin`, `close-quarter-firearms-archery`, `hammer-fist`, `knucklebreaker-blast-out-of-hands`, `soaring-shackles`, `tricking` | `run-gun` p. 132 (PDF 134) |
| `la-verdadera-destreza` | La Verdadera Destreza (Rapier Fighting) | `ballestra`, `multiple-opponent-combat`, `multiple-opponent-defense-friends-in-melee`, `opposing-force-parry`, `riposte`, `yielding-force-riposte` | `run-gun` p. 132 (PDF 134) |
| `lone-star-tactical` | Lone Star Tactical | `called-shot-break-weapon`, `close-quarter-defense-against-firearms`, `herding`, `multiple-opponent-defense-defender-has-defended`, `oaken-stance-defense-against-being-charged`, `rooted-tree` | `run-gun` p. 132 (PDF 134) |
| `muay-thai` | Muay Thai | `clinch`, `crushing-jaws`, `finishing-move`, `kick-attack`, `thunder-strike`, `ti-khao` | `run-gun` p. 133 (PDF 135) |
| `ninjutsu` | Ninjutsu | `counterstrike`, `dim-mak`, `flying-kick`, `kick-attack`, `randori-dirty-trick`, `tricking` | `run-gun` p. 133 (PDF 135) |
| `okichitaw` | Okichitaw | `called-shot-pin`, `counterstrike`, `opposing-force-parry`, `randori-vitals`, `shadow-block`, `sweep` | `run-gun` p. 133 (PDF 135) |
| `parkour` | Parkour | `bending-of-the-reed`, `kip-up`, `leaping-mantis`, `monkey-climb`, `rolling-clouds`, `shadow-block` | `run-gun` p. 133 (PDF 135) |
| `pentjak-silat` | Pentjak-Silat | `called-shot-break-weapon`, `called-shot-disarm`, `dim-mak`, `jiao-di-charge`, `randori-vitals`, `silken-storm` | `run-gun` p. 133 (PDF 135) |
| `quarterstaff-fighting` | Quarterstaff Fighting | `jiao-di-knock-down`, `multiple-opponent-combat`, `opposing-force-parry`, `sweep`, `stagger`, `thunder-strike` | `run-gun` p. 133 (PDF 135) |
| `sangre-y-acero` | Sangre y Acero | `called-shot-break-weapon`, `clinch`, `crushing-jaws`, `finishing-move`, `pouncing-dragon`, `tricking` | `run-gun` p. 133 (PDF 135) |
| `tae-kwon-do` | Tae Kwon Do | `counterstrike`, `flying-kick`, `kick-attack`, `opposing-force-block`, `sweep`, `tricking` | `run-gun` p. 133 (PDF 135) |
| `the-cowboy-way` | The Cowboy Way | `called-shot-entanglement`, `hammer-fist`, `haymaker`, `knucklebreaker-blast-out-of-hands`, `stagger`, `tricking` | `run-gun` p. 133 (PDF 135) |
| `turkish-archery` | Turkish Archery | `called-shot-pin`, `close-quarter-defense-against-firearms`, `hammer-fist`, `silken-storm`, `soaring-shackles`, `thunder-strike` | `run-gun` p. 134 (PDF 136) |
| `whip-fighting` | Whip Fighting | `bending-of-the-reed`, `called-shot-entanglement`, `hammer-fist`, `herding`, `multiple-opponent-defense-friends-in-melee`, `multiple-opponent-combat` | `run-gun` p. 134 (PDF 136) |
| `wildcat` | Wildcat | `clinch`, `counterstrike`, `dim-mak`, `finishing-move`, `multiple-opponent-combat`, `ti-khao` | `run-gun` p. 134 (PDF 136) |
| `wrestling-sport` | Wrestling (Sport Style) | `clinch`, `constrictors-crush`, `jiao-di-knock-down`, `karmic-response`, `sweep`, `throw-person` | `run-gun` p. 134 (PDF 136) |
| `wrestling-sumo` | Wrestling (Sumo Style) | `barbed-hooks`, `clinch`, `herding`, `jiao-di-knock-down`, `rooted-tree`, `throw-person` | `run-gun` p. 134 (PDF 136) |
| `wrestling-professional` | Wrestling (Professional Style) | `clinch`, `jiao-di-charge`, `karmic-response`, `sacrifice-throw`, `tricking`, `yielding-force-throw` | `run-gun` p. 134 (PDF 136) |
| `wrestling-mma` | Wrestling (MMA Style) | `clinch`, `constrictors-crush`, `crushing-jaws`, `jiao-di-knock-down`, `kick-attack`, `pouncing-dragon` | `run-gun` p. 135 (PDF 137) |
| `wudang-sword` | Wudang Sword | `ballestra`, `finishing-move`, `flying-kick`, `hammer-fist`, `iaijutsu`, `riposte` | `run-gun` p. 135 (PDF 137) |

## Techniques

All 70 distinct techniques referenced by the styles above or published as
universal, including every parameterized variant split into its own stable
ID (see interpretation notes below).

| ID | Display name | Universal | Source |
| --- | --- | --- | --- |
| `ballestra` | Ballestra | no | `run-gun` p. 135 (PDF 137) |
| `barbed-hooks` | Barbed Hooks | no | `run-gun` p. 135 (PDF 137) |
| `bending-of-the-reed` | Bending of the Reed | no | `run-gun` p. 135 (PDF 137) |
| `broken-fang` | Broken Fang | no | `run-gun` p. 135 (PDF 137) |
| `called-shot-break-weapon` | Called Shot (Break Weapon) | no | `run-gun` p. 136 (PDF 138) |
| `called-shot-disarm` | Called Shot (Disarm) | no | `run-gun` p. 136 (PDF 138) |
| `called-shot-entanglement` | Called Shot (Entanglement) | no | `run-gun` p. 136 (PDF 138) |
| `called-shot-feint` | Called Shot (Feint) | no | `run-gun` p. 136 (PDF 138) |
| `called-shot-pin` | Called Shot (Pin) | no | `run-gun` p. 136 (PDF 138) |
| `chin-na` | Chin Na | no | `run-gun` p. 136 (PDF 138) |
| `clinch` | Clinch | no | `run-gun` p. 136 (PDF 138) |
| `close-quarter-firearms-archery` | Close Quarter Firearms (Archery) | no | `run-gun` p. 136 (PDF 138) |
| `close-quarter-firearms-pistols` | Close Quarter Firearms (Pistols) | no | `run-gun` p. 136 (PDF 138) |
| `close-quarter-firearms-thrown-weapons` | Close Quarter Firearms (Thrown Weapons) | no | `run-gun` p. 136 (PDF 138) |
| `close-quarter-defense-against-firearms` | Close Quarter Defense Against Firearms | no | `run-gun` p. 137 (PDF 139) |
| `constrictors-crush` | Constrictor's Crush | no | `run-gun` p. 137 (PDF 139) |
| `counterstrike` | Counterstrike | no | `run-gun` p. 137 (PDF 139) |
| `crushing-jaws` | Crushing Jaws | no | `run-gun` p. 137 (PDF 139) |
| `defiant-dance` | Defiant Dance | no | `run-gun` p. 137 (PDF 139) |
| `dim-mak` | Dim Mak | no | `run-gun` p. 137 (PDF 139) |
| `finishing-move` | Finishing Move | no | `run-gun` p. 137 (PDF 139) |
| `flying-kick` | Flying Kick | no | `run-gun` p. 137 (PDF 139) |
| `full-offense` | Full Offense | no | `run-gun` p. 137 (PDF 139) |
| `grasping-vines` | Grasping Vines | no | `run-gun` p. 137 (PDF 139) |
| `half-sword` | Half-Sword | no | `run-gun` p. 137 (PDF 139) |
| `hammer-fist` | Hammer Fist | no | `run-gun` p. 138 (PDF 140) |
| `haymaker` | Haymaker | no | `run-gun` p. 138 (PDF 140) |
| `herding` | Herding | no | `run-gun` p. 138 (PDF 140) |
| `iaijutsu` | Iaijutsu | no | `run-gun` p. 138 (PDF 140) |
| `imposing-stone` | Imposing Stone | no | `run-gun` p. 138 (PDF 140) |
| `jiao-di-charge` | Jiao Di (Charge) | no | `run-gun` p. 138 (PDF 140) |
| `jiao-di-knock-down` | Jiao Di (Knock Down) | no | `run-gun` p. 138 (PDF 140) |
| `karmic-response` | Karmic Response | no | `run-gun` p. 138 (PDF 140) |
| `kick-attack` | Kick Attack | no | `run-gun` p. 138 (PDF 140) |
| `kip-up` | Kip-Up | no | `run-gun` p. 139 (PDF 141) |
| `knucklebreaker-blast-out-of-hands` | Knucklebreaker (Blast Out of Hands) | no | `run-gun` p. 139 (PDF 141) |
| `leaping-mantis` | Leaping Mantis | no | `run-gun` p. 139 (PDF 141) |
| `monkey-climb` | Monkey Climb | no | `run-gun` p. 139 (PDF 141) |
| `multiple-opponent-combat` | Multiple Opponent Combat | no | `run-gun` p. 139 (PDF 141) |
| `multiple-opponent-defense-defender-has-defended` | Multiple Opponent Defense (Defender Has Defended) | no | `run-gun` p. 139 (PDF 141) |
| `multiple-opponent-defense-friends-in-melee` | Multiple Opponent Defense (Friends in Melee) | no | `run-gun` p. 139 (PDF 141) |
| `oaken-stance-defense-against-being-charged` | Oaken Stance (Defense Against Being Charged) | no | `run-gun` p. 139 (PDF 141) |
| `oaken-stance-defense-against-being-knocked-down` | Oaken Stance (Defense Against Being Knocked Down) | no | `run-gun` p. 139 (PDF 141) |
| `opposing-force-block` | Opposing Force (Block) | no | `run-gun` p. 139 (PDF 141) |
| `opposing-force-parry` | Opposing Force (Parry) | no | `run-gun` p. 139 (PDF 141) |
| `releasing-talons` | Releasing Talons | no | `run-gun` p. 139 (PDF 141) |
| `neijia` | Neijia | yes | `run-gun` p. 140 (PDF 142) |
| `randori-dirty-trick` | Randori (Dirty Trick) | no | `run-gun` p. 140 (PDF 142) |
| `randori-vitals` | Randori (Vitals) | no | `run-gun` p. 140 (PDF 142) |
| `riposte` | Riposte | no | `run-gun` p. 140 (PDF 142) |
| `rooted-tree` | Rooted Tree | no | `run-gun` p. 140 (PDF 142) |
| `sacrifice-throw` | Sacrifice Throw | no | `run-gun` p. 140 (PDF 142) |
| `shadow-block` | Shadow Block | no | `run-gun` p. 140 (PDF 142) |
| `silken-storm` | Silken Storm | no | `run-gun` p. 140 (PDF 142) |
| `pouncing-dragon` | Pouncing Dragon | no | `run-gun` p. 141 (PDF 143) |
| `rolling-clouds` | Rolling Clouds | no | `run-gun` p. 141 (PDF 143) |
| `soaring-shackles` | Soaring Shackles | no | `run-gun` p. 141 (PDF 143) |
| `stagger` | Stagger | no | `run-gun` p. 141 (PDF 143) |
| `strike-the-darkness` | Strike the Darkness | yes | `run-gun` p. 141 (PDF 143) |
| `sweep` | Sweep | no | `run-gun` p. 141 (PDF 143) |
| `throw-person` | Throw Person | no | `run-gun` p. 141 (PDF 143) |
| `thunder-strike` | Thunder Strike | no | `run-gun` p. 141 (PDF 143) |
| `ti-khao` | Ti Khao | no | `run-gun` p. 141 (PDF 143) |
| `tricking` | Tricking | no | `run-gun` p. 141 (PDF 143) |
| `two-headed-snake` | Two-Headed Snake | no | `run-gun` p. 141 (PDF 143) |
| `two-weapon-style-attack` | Two-Weapon Style Attack | no | `run-gun` p. 141 (PDF 143) |
| `two-weapon-style-defense` | Two-Weapon Style Defense | no | `run-gun` p. 141 (PDF 143) |
| `yielding-force-counter-strike` | Yielding Force (Counter Strike) | no | `run-gun` p. 141 (PDF 143) |
| `yielding-force-riposte` | Yielding Force (Riposte) | no | `run-gun` p. 141 (PDF 143) |
| `yielding-force-throw` | Yielding Force (Throw) | no | `run-gun` p. 141 (PDF 143) |

## Interpretation Notes

Content decisions made while reconciling the chapter's style lists against
its technique write-ups, recorded for project-owner review:

1. **La Verdadera Destreza's bare "Multiple Opponent Defense"** — the
   style's list names the technique without a variant; the write-up defines
   two. Published as the `friends-in-melee` variant, the reading most
   consistent with a fencing style built around circling multiple opponents.
2. **Okichitaw's "Hard Technique (Parry)"** — no technique of that name
   exists in the chapter's write-ups. Published as `opposing-force-parry`,
   the only parry-flavored "meet force with force" technique defined.
3. **"Kick" in style lists** — style lists that print "Kick" are published
   as `kick-attack`, the chapter's only kick technique.
4. **Name normalizations** — style-list spellings are normalized to the
   technique write-up headings: Counterstrike, Thunder Strike, Hammer Fist,
   Sacrifice Throw, Rolling Clouds, and the Close-Quarter(s) Defense naming
   are unified to one ID each rather than duplicated per spelling.
5. **Knucklebreaker** — the write-up defines it only as a Called Shot
   (Blast Out of Hands) rider, so only the
   `knucklebreaker-blast-out-of-hands` variant is published.
6. **`grasping-vines` is deliberately orphaned** — no published style lists
   it (its styles — whip/chain arts — reference `called-shot-entanglement`
   directly), but it is kept in the catalog so the (already-published)
   `one-trick-pony` quality and future post-creation learning have a
   complete technique inventory.
7. **Universal techniques are excluded from style lists** — Neijia and
   Strike the Darkness are learnable with any style per their write-ups, so
   they are flagged `universal: true` and never repeated inside a style's
   six-technique list; the UI and evaluator add them to every style's
   available pool.
8. **Called Shot technique semantics** — where a Called Shot is normally
   available to everyone, the technique reduces its modifier by 1; where a
   Called Shot requires martial-arts training, the technique unlocks it.
   Either way this is display text only (see Rules Model above).

## Explicit Exclusions

| Item | Treatment | Source |
| --- | --- | --- |
| Cross-style +2 stacking cap | "No more than a +2 bonus... from purchasing the same technique from two different martial art styles" governs owning multiple styles, which creation does not allow (one style at creation); nothing to enforce. | `run-gun` p. 142 (PDF 144) |
| Post-creation learning times, instruction costs, and lifestyle add-ons | Advancement/downtime rules, out of scope for character creation. | `run-gun` p. 142 (PDF 144) |
| Technique combat mechanics as enforced rules | All technique effects reference the Killshots and More combat rules this project does not model; published as display text only. | `run-gun` pp. 135-141 (PDF 137-143) |

## Review Footer

- Reviewed Martial Arts chapter: `run-gun` pp. 128-142 (PDF 130-144).
- Approved-PDF entries in scope: 42 styles, 70 distinct techniques (2
  universal), each style publishing exactly 6 techniques.
- Reconciliation: 42 catalog `martialArtStyles` and 70
  `martialArtTechniques` entries account for every style and every
  technique write-up in the chapter, with variant splits and the one
  deliberate orphan (`grasping-vines`) explained in the Interpretation
  Notes above; no unexplained inventory difference.
- Backend verification: 12 `MartialArtsEvaluatorTests` plus 6
  `RunGunMartialArtsTests` catalog-content tests; full Application test
  suite green (425 passed, 0 failed, 2 skipped).
