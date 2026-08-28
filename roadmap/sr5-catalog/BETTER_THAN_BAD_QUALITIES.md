# Better Than Bad Qualities Ledger (CHAR-819)

This is the CHAR-819 source ledger for Better Than Bad's new qualities. It is
a review input for the runtime catalog change it accompanies, not a
substitute for the approved book. It extends [`QUALITIES.md`](QUALITIES.md),
[`RUN_FASTER_QUALITIES.md`](RUN_FASTER_QUALITIES.md), and
[`RUN_GUN_QUALITIES.md`](RUN_GUN_QUALITIES.md), which cover the `sr5-core`,
`run-faster`, and `run-gun` quality catalogs respectively.

CHAR-819 is a project-owner-directed, qualities-only pass through Better Than
Bad (2026-08-28): "I want to add the qualities from better than bad." Unlike
the three-book Run & Gun pass, no other chapter of this book was reviewed for
scope — only its "New Qualities" section.

## Source

`better-than-bad` (repository-root filename
`pdfcoffee.com_better-than-bad-pdf-free.pdf`) is newly pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md) as this project's
fourth approved source. Its printed/PDF page offset was independently
verified (not assumed from the other three sources' offset) by cross-checking
consecutive page footers ("159 << BUILDING A HOODER" / "160 BUILDING A HOODER
>>") against the physical PDF page each one actually lands on: this book's
production carries a **one-page** printed→PDF offset (physical PDF page N is
printed page N-1), not the two-page offset shared by `sr5-core`, `run-faster`,
and `run-gun`. Every citation in this ledger uses this book's own offset.

## Scope

Included:

- All 7 "New Positive Qualities" (`better-than-bad` p. 160, PDF 161).
- Both "New Mastery Qualities" (`better-than-bad` p. 161, PDF 162).
- Both "New Negative Qualities" (`better-than-bad` p. 162, PDF 163).

Excluded (out of scope for this ticket; not reviewed for a future ticket):

- Everything else in the book — New Adept Powers, New Complex Forms/Echoes,
  New Life Modules (Nationalities, Formative Years, Further Education, Real
  Life; Life Modules are excluded project-wide, consistent with the
  `run-faster` Point Buy/Life Modules exclusion), "New Uses for Karma and
  Street Cred" (a new spend-Karma mechanic, not a catalog port), and all
  setting/adventure content (the Pretoria-Witwatersrand-Vaal metroplex
  writeup, NPCs, plot hooks, the "Hooding" lifestyle chapters). The project
  owner's instruction was scoped to qualities only; nothing else in this book
  has been reviewed for inclusion or exclusion.

## Cost-Modeling Convention

Follows the same convention established in `RUN_FASTER_QUALITIES.md` and
`RUN_GUN_QUALITIES.md`. Positive Qualities cost Karma to take; Negative
Qualities award Karma (rendered in the book as "Bonus: N Karma"). Special
Modifications is the one repeatable/tiered quality in this slice (max rank
2); per the established convention it publishes its flat per-rank Karma cost
(5) as the catalog `Cost` with `repeatable: true`, matching how
`brand-loyalty` (CHAR-815) models the same shape — the full per-rank benefit
menu is preserved in this ledger and the frontend description text, not in
the schema.

Three qualities in this slice — Prototype Materials, Special Modifications,
and Elemental Attunement/Resonant Discordance (Mastery Qualities) — carry a
book-printed "Prerequisite" line (Mundane; Mundane; Adept Powers Killing
Hands/Elemental Strike/Elemental Body; Submersion grade 1, respectively). None
of these prerequisites are code-enforced, consistent with existing project
precedent: most quality mechanical effects, including prerequisites, are
catalog-and-description-only (see [[sr5-catalog-conventions]] /
`RulesetCatalog.cs`'s `QualityDefinition`, which has no prerequisite field).
The prerequisite text is preserved here and in the frontend description for
player/GM reference.

## New Positive Qualities

| ID | Display name | Cost | Repeat | Prerequisite | Effect summary | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `hair-trigger` | Hair Trigger | 2 | no | Technomancer | Enter hot-sim as a Free Action (cold-sim with the appropriate Echo); with a control rig, jump into a drone as a Simple Action. | `better-than-bad` p. 160 (PDF 161) |
| `hi-rez` | Hi-Rez | 4 | no | Technomancer | Technomancer Matrix Perception bonus increases from +2 to +4; may make a Matrix Perception test to detect hidden icons as a Free Action once per Combat Turn. | `better-than-bad` p. 160 (PDF 161) |
| `instinctive-hack` | Instinctive Hack | 2 | no | — | Unless surprised, take one of Brute Force, Data Spike, Hack on the Fly, or Matrix Perception before Initiative is rolled for the first time in a combat. | `better-than-bad` p. 160 (PDF 161) |
| `prototype-materials` | Prototype Materials | 5 | no | Mundane; Gamemaster approval | Take another rating of the Special Modifications quality beyond its normal rank-2 maximum. | `better-than-bad` p. 160 (PDF 161) |
| `rabble-rouser` | Rabble Rouser | 6 | no | — | +2 dice pool bonus to social skill tests influencing a crowd of ten or more people. | `better-than-bad` p. 161 (PDF 162) |
| `shoot-first-dont-ask-questions` | Shoot First, Don't Ask Questions | 2 | no | — | On a successful Surprise test, increase initiative score by total hits rolled (first turn only); reduces the threshold for quick-drawing a weapon by 1. | `better-than-bad` p. 161 (PDF 162) |
| `special-modifications` | Special Modifications | 5 per rating (max rank 2) | yes, 2 levels | Mundane | Per rating, add +1 damage to a weapon or choose two of: +1 AP, +1 Accuracy, +1 Recoil Compensation, +½ capacity, -1 Concealability, or +1 Reach. Only usable by the payer; 1 lifestyle cycle to replicate onto a replacement weapon. | `better-than-bad` p. 161 (PDF 162) |

## New Mastery Qualities

| ID | Display name | Cost | Repeat | Prerequisite | Effect summary | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `elemental-attunement` | Elemental Attunement | 5 | no | Adept Powers: Killing Hands, Elemental Strike, Elemental Body | Changes Elemental Body's Drain to 1 unresisted box (Physical or Stun, chooser's choice) at the start of the Combat Turn after activation. | `better-than-bad` p. 161 (PDF 162) |
| `resonant-discordance` | Resonant Discordance | 13 | no | Submersion grade 1 | May enter cold-sim while retaining the +2 Matrix dice pool bonus; hot-sim grants a further +2 to Compiling, Decompiling, and Threading complex forms. | `better-than-bad` p. 161 (PDF 162) |

## New Negative Qualities

| ID | Display name | Award | Repeat | Effect summary | Source |
| --- | --- | --- | --- | --- | --- |
| `dead-sin` | Dead Sin | 20 | no | Grants a Rating 3 Fake SIN plus four Rating 3 fake licenses (you are presumed dead); if ever flagged fake, must immediately buy off the quality, going into Karmic debt if short; a SIN scanner flags it fake on a tied roll vs. threshold. | `better-than-bad` p. 162 (PDF 163) |
| `hard-luck` | Hard Luck | 5 | no | Must pay lifestyle costs one level above the one chosen. | `better-than-bad` p. 162 (PDF 163) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| One-page printed→PDF offset (not the two-page offset shared by the other three approved sources) | Independently verified via consecutive page-footer cross-check rather than assumed; recorded above and used for every citation in this ledger. | `better-than-bad` pp. 159-160 (PDF 160-161) |
| Special Modifications' tiered per-rank benefit menu | Modeled per the established multi-tier convention: flat per-rank Karma cost (5) with `repeatable: true`, full menu preserved in this ledger and the frontend description only. | `better-than-bad` p. 161 (PDF 162) |
| Prerequisite lines (Technomancer; Mundane; Mundane + GM approval; Adept Powers; Submersion grade 1) | Not code-enforced, consistent with existing project-wide precedent that `QualityDefinition` has no prerequisite field; preserved in this ledger and the frontend description text for reference. | `better-than-bad` pp. 160-161 (PDF 161-162) |
| Every other chapter of the book (setting, Life Modules, New Adept Powers, New Uses for Karma/Street Cred, etc.) | Out of scope for CHAR-819 by explicit project-owner instruction (qualities only); not reviewed, not recorded as excluded-forever — a future ticket could review the rest of the book from scratch. | — |

## Review Footer

- Reviewed quality rules: `better-than-bad` pp. 160-162 (PDF 161-163).
- Approved-PDF quality headings in scope: 7 New Positive Qualities + 2 New
  Mastery Qualities + 2 New Negative Qualities = 11 headings.
- Reconciliation: 11 new catalog entries (9 positive — including the 2
  Mastery Qualities, which the schema has no separate polarity for — and 2
  negative) account for all 11 in-scope headings with no unexplained
  inventory difference.
- Remaining unknown facts: None.
- Runtime reconciliation status: Implemented (CHAR-819) in the current
  mutable pre-alpha `sr5-core` `1.0.0` schema (see [[sr5-schema-lifecycle]]),
  alongside the new `better-than-bad` source registration.
