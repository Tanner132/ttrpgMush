# Better Than Bad Adept Powers Ledger (CHAR-820)

This is the CHAR-820 source ledger for Better Than Bad's new adept powers. It
is a review input for the runtime catalog change it accompanies, not a
substitute for the approved book. It extends the existing `adeptPowers`
catalog (`sr5-core` pp. 308-309, PDF 310-311) with the two new powers this
book adds.

CHAR-820 is a project-owner-directed, adept-powers-only follow-up to CHAR-819
(2026-08-28): "add in the new adept powers." Like CHAR-819, no other chapter
of the book was reviewed for scope beyond this one section.

## Source

`better-than-bad`, already pinned as this project's fourth approved source
under CHAR-819 (see
[`BETTER_THAN_BAD_QUALITIES.md`](BETTER_THAN_BAD_QUALITIES.md) for the
independently-verified one-page printed→PDF offset this book uses, distinct
from the two-page offset of the other three approved sources).

## Scope

Included:

- Both powers printed in the "New Adept Powers" section, immediately
  following "New Manipulation Spells" under the book's "New Spells and Adept
  Powers" chapter heading: Mystic Aptitude (`better-than-bad` p. 159, PDF
  160) and State of Purity (`better-than-bad` p. 160, PDF 161).

Excluded (not reviewed for this ticket; see
[`BETTER_THAN_BAD_QUALITIES.md`](BETTER_THAN_BAD_QUALITIES.md) for the
running list of everything else in the book still unreviewed):

- New Manipulation Spells (Astral Disruption, Mass Astral Disruption;
  `better-than-bad` p. 158, PDF 159) — a different catalog collection
  (`spells`), out of scope for this adept-powers-only ticket.

## Cost-Modeling Convention

Follows the existing `AdeptPowerDefinition` shape used by every `sr5-core`
adept power: a flat `powerPointCost`, a `ranked` flag, and no `maxRank`
unless the book states an explicit numeric cap (neither of these two powers
does). Neither power's "Prerequisite" or "Activation" line is code-enforced
— consistent with existing project precedent (`QualityDefinition` and
`AdeptPowerDefinition` both carry no prerequisite/activation fields; most
adept power and quality mechanics are catalog-and-description-only). Both
are preserved in this ledger and the frontend description text.

## New Adept Powers

| ID | Display name | Cost | Ranked | Prerequisite | Activation | Effect summary | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `mystic-aptitude` | Mystic Aptitude | 0.75 PP per level | yes (no stated max) | — | Simple Action | Substitute Magic + Rank for any one Physical Attribute (chosen when activated) for a number of Combat Turns equal to Magic; take Drain equal to (rank x 2) when the boost ends. | `better-than-bad` p. 159 (PDF 160) |
| `state-of-purity` | State of Purity | 1.5 PP | no | Essence 6 | Complex Action | While sustained, unarmed combat attacks deal (Magic + Strength) damage (Physical or Stun, adept's choice) with AP -(Magic x 0.5); on deactivation, resist Drain of [(Magic x 0.5) + Combat Rounds active]. | `better-than-bad` p. 160 (PDF 161) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| Mystic Aptitude's rank cap | The book states no numeric maximum rank (unlike, e.g., `improved-reflexes`'s explicit rank-3 cap elsewhere in the core catalog); modeled as `ranked: true` with no `maxRank`, consistent with several existing open-ended `sr5-core` powers (Adrenaline Boost, Attribute Boost, Combat Sense). | `better-than-bad` p. 159 (PDF 160) |
| Mystic Aptitude's "choose when activated" attribute substitution | Not modeled as `parameterized` — the choice is a play-time activation choice per the book's own text, not a character-creation-time catalog parameter, so it's treated like other powers with in-play choices (e.g. `combat-sense`) rather than `attribute-boost`, whose analogous choice is fixed at purchase. | `better-than-bad` p. 159 (PDF 160) |
| State of Purity's Essence 6 prerequisite | Not code-enforced; recorded here and in the frontend description only, matching how every other quality/power prerequisite in this project is handled. | `better-than-bad` p. 160 (PDF 161) |

## Review Footer

- Reviewed adept power rules: `better-than-bad` pp. 159-160 (PDF 160-161).
- Approved-PDF adept power headings in scope: 2 (Mystic Aptitude, State of
  Purity).
- Reconciliation: 2 new catalog entries account for both in-scope headings
  with no unexplained inventory difference.
- Remaining unknown facts: None.
- Runtime reconciliation status: Implemented (CHAR-820) in the current
  mutable pre-alpha `sr5-core` `1.0.0` schema (see
  [[sr5-schema-lifecycle]]), bringing the runtime to 27 total adept powers.
