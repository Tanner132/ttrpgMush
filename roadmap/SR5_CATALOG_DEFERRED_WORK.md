# SR5 Catalog Deferred Work

This file tracks character-option content that an approved sourcebook
contains but that the project owner has chosen not to port into the runtime
catalog yet, because doing so would mean adding a new gameplay mechanic or
subsystem rather than extending the existing catalog/evaluator shape. It is
a backlog for future project-owner-approved tickets, not a scope decision by
itself — nothing here is in scope until a future CHAR-8xx (or later) ticket
explicitly admits it, the same way CHAR-813/814 admitted pieces of Run
Faster.

This is distinct from the permanent "Excluded" rows in
[`SR5_RULESET_MANIFEST.md`](SR5_RULESET_MANIFEST.md) and each ledger's own
"Explicit Exclusions" section, which record content that was reviewed and
rejected as out of scope entirely (GM-only procedures, alternate creation
tiers, adventure/fiction content, etc.) rather than content worth reconsidering
later.

## Open Items

_None currently._

## Resolved (Admitted By A Later Ticket)

### Run & Gun: Martial Arts (styles + techniques) — admitted by CHAR-822, 2026-08-31

- **Source:** `run-gun` pp. 128-142 (PDF 130-144).
- **Why it was deferred (2026-08-28):** Not a plain catalog port. Martial
  Arts is a progression subsystem: a character buys a style for 7 Karma
  (each style has a fixed list of 6 available techniques), then buys
  individual techniques for 5 Karma each (up to 5 techniques at character
  creation, 27 Karma total), so it needed a style/technique picker UI and
  new evaluator logic, not just new catalog rows. The techniques also
  mechanically reference the "Killshots and More" combat rules this project
  does not model.
- **Resolution:** CHAR-822 ("add in the rules and entries for martial
  arts... add a new optional martial arts step") built exactly that: 42
  styles and 70 techniques as new `martialArtStyles`/`martialArtTechniques`
  catalog sections, a `MartialArtsEvaluator` plus Karma costing, and an
  optional `martial-arts` creator step. Technique combat effects remain
  display text only — the underlying Killshots and More exclusion below is
  unchanged. See
  [`sr5-catalog/RUN_GUN_MARTIAL_ARTS.md`](sr5-catalog/RUN_GUN_MARTIAL_ARTS.md).

## Excluded (Reviewed, Not Candidates For Later)

Recorded here for a single running index across sourcebooks; the
authoritative exclusion reasoning lives in each book's own ledger file.

| Item | Source | Reason |
| --- | --- | --- |
| Sixth World Combat Tactics (unit/team maneuvers) | `run-gun` pp. 89-102 (PDF 91-104) | GM/team tactical procedures, not a character-creation option. |
| Killshots and More (called shots, combat interrupts, Combat Edge uses) | `run-gun` pp. 106-126 (PDF 108-128) | Combat-resolution rules; this project does not model combat resolution. |
| Staying Alive (environmental hazard rules: heat, cold, radiation, pollution, underwater, space) | `run-gun` pp. 144-168 (PDF 146-170) | GM narrative/environmental system. The three positive and two negative qualities tied to this material (Radiation Sponge, Rad-Tolerant, Spacer, Blighted, Earther) are still in scope as ordinary catalog qualities, consistent with the existing "most quality mechanical effects are not code-enforced" convention. |
| Fixin' All the Broken Drek (equipment repair rules) | `run-gun` p. 143 (PDF 145) | GM/mechanical procedure, not a purchasable option. |
| Advanced Demolitions test/building-breach procedures | `run-gun` pp. 175-192 (PDF 177-194) | Procedural GM rules. The explosive compounds, detonators, and explosive accessories themselves remain in scope as gear. |
| Gear Qualities (Counterfeit, Defective, Hot) | `run-gun` p. 197 (PDF 199) | Confirmed by reading the full section: these are GM-secret flags the gamemaster may assign to a piece of gear during play ("Gamemasters can secretly assign this quality..."), never a player choice at character creation. |
| Hostile Extraction | `run-gun` p. 198 (PDF 200) | Adventure/scenario content, not rules content. |
| Catspaw, Fight for Your Life, What You Don't Know Kills You, Sensei's Thoughts | `run-gun` pp. 6-17 (PDF 8-19) | Fiction and flavor essays, not rules content. |
| Improvised Melee Weapons | `run-gun` Exotic Melee section prose | GM-adjudicated narrative-damage rule, not a purchasable catalog product. |
