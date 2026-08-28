# Run & Gun Qualities Ledger (CHAR-815)

This is the CHAR-815 source ledger for Run & Gun's new qualities. It is a
review input for the runtime catalog change it accompanies, not a substitute
for the approved book. It extends [`QUALITIES.md`](QUALITIES.md) and
[`RUN_FASTER_QUALITIES.md`](RUN_FASTER_QUALITIES.md), which cover the
`sr5-core` and `run-faster` quality catalogs respectively.

CHAR-815 is the first slice of a project-owner-approved Run & Gun expansion
(2026-08-28): every quality, weapon, and armor/gear item in the book that
fits the existing catalog shape without requiring a new gameplay mechanic.
Martial Arts and several GM-procedural chapters were reviewed and are
deliberately not part of this or any other CHAR-8xx slice yet; see
[`../SR5_CATALOG_DEFERRED_WORK.md`](../SR5_CATALOG_DEFERRED_WORK.md).

## Source

Only `run-gun`, newly pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md), is used. Every
citation in this ledger carries the same two-page printed/PDF offset
verified across both other approved sources (confirmed here by cross-checking
the Table of Contents' printed page numbers against the physical PDF page
each section's own footer/header actually lands on).

## Scope

Included:

- All 7 "Positive Qualities" and the 1 "Negative Quality" printed in the
  "Killshots and More" chapter's "New Qualities" section (`run-gun` p. 127,
  PDF 129).
- All 3 positive and 2 negative qualities printed in the "Qualities" section
  immediately following the Staying Alive chapter (`run-gun` p. 169, PDF
  171). These are tied narratively to Staying Alive's environmental hazard
  rules (space, radiation), but the qualities themselves are ordinary
  creation-time catalog entries with textual effects, consistent with how
  most `sr5-core`/`run-faster` quality mechanics are documentation-only. The
  underlying Staying Alive environmental system itself remains excluded; see
  `../SR5_CATALOG_DEFERRED_WORK.md`.

Excluded (see `../SR5_CATALOG_DEFERRED_WORK.md` for the full reasoning):

- Martial Arts styles/techniques (`run-gun` pp. 128-141, PDF 130-143) —
  deferred, not a plain catalog port.
- Gear Qualities (Counterfeit, Defective, Hot; `run-gun` p. 197, PDF 199) —
  confirmed by reading the full section to be GM-secret flags assigned to a
  piece of gear during play, never a player choice at character creation, so
  these are not quality catalog entries at all despite the name.

## Cost-Modeling Convention

Follows the same convention established in `RUN_FASTER_QUALITIES.md`: a
multi-tier or summed quality publishes only its first-listed (and, in every
case reviewed here, lowest) Karma value as the catalog `Cost`, with the full
tier breakdown preserved in this ledger and the frontend description text.
Positive Qualities cost Karma to take; Negative Qualities award Karma. The
book renders the Karma-cost header for every Positive Quality in this
section with a decorative leading glyph that this ledger reads simply as a
plain positive Karma value (Negative Qualities render the same header with
an explicit "+", e.g. "Combat Junkie (+7 Karma)", confirming the sign
convention).

## New Positive Qualities

| ID | Display name | Cost | Repeat | Effect summary | Source |
| --- | --- | --- | --- | --- | --- |
| `acrobatic-defender` | Acrobatic Defender | 4 | no | Use Gymnastics instead of Willpower for Full Defense; Physical Limit applies instead of Mental Limit. | `run-gun` p. 127 (PDF 129) |
| `agile-defender` | Agile Defender | 3 | no | Use Agility instead of Willpower for Full Defense. | `run-gun` p. 127 (PDF 129) |
| `brand-loyalty` | Brand Loyalty | 3 per level | yes, 2 levels (Manufacturer, then Product) | +1 dice pool modifier with gear from your chosen manufacturer (or, at the Product tier, one specific product); -1 dice pool modifier with other gear of the same type. | `run-gun` p. 127 (PDF 129) |
| `one-trick-pony` | One Trick Pony | 7 | no | Use one Martial Arts technique without first learning its style. | `run-gun` p. 127 (PDF 129) |
| `perceptive-defender` | Perceptive Defender | 4 | no | Use Perception instead of Willpower for Full Defense; Mental Limit applies. | `run-gun` p. 127 (PDF 129) |
| `sharpshooter` | Sharpshooter | 4 | no | Called Shot penalties reduced by 2; -1 dice pool modifier to all other Ranged Attack actions. | `run-gun` p. 127 (PDF 129) |
| `too-pretty-to-hit` | Too Pretty to Hit | 3 | no | Use Charisma instead of Willpower for Full Defense. | `run-gun` p. 127 (PDF 129) |
| `radiation-sponge` | Radiation Sponge | 5 | no; incompatible with `rad-tolerant` | One less Fatigue step in Radiation environments; never faces a Deadly environment directly, but a would-be Deadly exposure lingers afterward as a doubled-duration Extreme environment. | `run-gun` p. 169 (PDF 171) |
| `rad-tolerant` | Rad-Tolerant | 3 | no; incompatible with `radiation-sponge` | Doubles time to gain `blighted`; also gets Radiation Sponge's one-less-Fatigue-step benefit in Radiation environments. | `run-gun` p. 169 (PDF 171) |
| `spacer` | Spacer | 3 | no | +1 dice pool modifier to Physical actions in non-Earth-norm gravity. | `run-gun` p. 169 (PDF 171) |

## New Negative Qualities

| ID | Display name | Award | Repeat | Effect summary | Source |
| --- | --- | --- | --- | --- | --- |
| `combat-junkie` | Combat Junkie | 7 | no | In a stressful situation, must pass Composure (4) to avoid attacking first; on an unexpected complication, defaults to violence unless Intuition + Logic (4) succeeds. | `run-gun` p. 127 (PDF 129) |
| `blighted` | Blighted | 5 (6 months) / 10 (12 months) / 15 (24 months) | no; the tier is chosen once | Test Edge (3) each game session; failure imposes a dice pool penalty scaled to the chosen duration tier (-1 Physical at 6 months; -1 to all actions at 12 months; -2 Physical and -1 to all other actions at 24 months). | `run-gun` p. 169 (PDF 171) |
| `earther` | Earther | 3 | no | -2 dice pool modifier to Physical actions in non-Earth-norm gravity. | `run-gun` p. 169 (PDF 171) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| Both quality headings use a decorative glyph before the Karma header (rendered as a replacement character during PDF text extraction) rather than a literal `+`/`-` sign for Positive Qualities. | Read as a plain positive Karma cost, cross-checked against the one Negative Quality on the same page (Combat Junkie) whose header renders with an explicit `+`, confirming Positive Qualities cost Karma and Negative Qualities award it, matching every other reviewed quality section in this project. | `run-gun` p. 127 (PDF 129) |
| Gear Qualities (Counterfeit, Defective, Hot) | Excluded — GM-secret flags applied to gear during play, not a character-creation choice; recorded in `../SR5_CATALOG_DEFERRED_WORK.md` as a reviewed-and-rejected item, not a deferred one. | `run-gun` p. 197 (PDF 199) |
| Martial Arts (`One Trick Pony`'s only mechanical hook) | `one-trick-pony` is cataloged with its plain textual effect; it presupposes the (excluded) Martial Arts subsystem to have any in-play effect, same treatment as any other quality whose effect references mechanics this project doesn't enforce. | `run-gun` p. 127 (PDF 129) |

## Review Footer

- Reviewed quality rules: `run-gun` p. 127 (PDF 129) and p. 169 (PDF 171).
- Approved-PDF quality headings in scope: 7 positive + 1 negative (Killshots
  chapter) + 3 positive + 2 negative (Qualities section) = 13 headings.
- Reconciliation: 13 new catalog entries (10 positive, 3 negative) account
  for all 13 in-scope headings with no unexplained inventory difference.
- Remaining unknown facts: None.
- Runtime reconciliation status: Implemented (CHAR-815) as catalog version
  `sr5-core` `1.5.0`, an overlay on `1.0.0` republishing `1.1.0` through
  `1.4.0`'s additive content plus this ledger's 13 new qualities and the new
  `run-gun` source registration.
