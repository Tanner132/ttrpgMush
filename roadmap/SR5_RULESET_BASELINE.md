# SR5 Character Creation Ruleset Baseline

This document controls Milestone 8 rules and catalog work. It is a planning and
verification contract, not a substitute for the approved books.

CHAR-801 review artifacts:

- [`SR5_RULESET_MANIFEST.md`](SR5_RULESET_MANIFEST.md)
- [`SR5_CATALOG_LEDGER.md`](SR5_CATALOG_LEDGER.md)
- [`SR5_RULE_DECISIONS.md`](SR5_RULE_DECISIONS.md)

## Authority And Scope

The only approved rules references are these local PDFs at the repository root:

1. `Shadowrun 5th Edition Core Rulebook (Jennifer Brozek, Raymond Croteau etc.) (z-library.sk, 1lib.sk, z-lib.sk).pdf`
2. `Shadowrun Run Faster (Catalyst Game Labs) (z-library.sk, 1lib.sk, z-lib.sk).pdf`

Do not use external rules summaries, websites, implementations, catalogs, errata
documents, or other books to define behavior or establish catalog completeness.
If the project owner later approves another source, update this contract and the
ruleset manifest before using it.

Current approved scope:

- Core-rulebook selectable content only.
- Standard Priority from the core rulebook.
- Sum-to-Ten allocation rules from Run Faster.
- Run Faster's reproduced priority-table clarification allowing magician and
  mystic-adept spell grants to be selected as spells, rituals, and/or alchemical
  preparations. This is the only approved Run Faster rule outside allocation.
- No other Run Faster options, supplements, house rules, or career-only systems.
- The contents of the pinned PDFs control; do not silently apply corrections or
  interpretations from another revision or separate errata document.

If the approved PDFs are ambiguous or conflict, record and approve a product
decision before implementing the affected rule. Never fill a gap from memory or
an unapproved reference.

## Approved Source Pins

| Source | SHA-256 |
| --- | --- |
| SR5 core-rulebook PDF | `4928B5F45F94C542820D7A7688BD68B7940BF2E9480898CCAFA6111996497F79` |
| Run Faster PDF | `F037FEFADC7FB91EE10180CD0116B55BC5EA825BF4BE56546D80C6F35D555BAF` |

The exact filenames above and these checksums identify the approved revisions.
A changed file is a new source revision and requires an explicit baseline review;
do not silently regenerate a catalog against it.

## Ruleset Manifest Required Fields

- Project ruleset ID and immutable catalog version.
- Exact approved source filenames and SHA-256 checksums.
- PDF page convention used by citations: printed page, PDF page index, or both.
- Exact Run Faster pages used for Sum-to-Ten.
- Approved ambiguity decisions, reviewer, and date.
- Catalog semantic digest.

## Creation Methods

Standard Priority assigns A, B, C, D, and E exactly once across Metatype,
Attributes, Magic/Resonance, Skills, and Resources.

Sum-to-Ten uses the priority scores, total, and repeated-priority restrictions
stated in the approved Run Faster PDF. CHAR-801 must record their exact page
citations before implementation.

Both methods use the same priority rows and downstream core rules, with the one
approved Run Faster formula-grant clarification above. The method selection never
enables a Run Faster metatype, quality, item, or other catalog entry.

## Source Review And Inventory

The previous provisional inventory is not an approved implementation input.
CHAR-801 must build a fresh reviewed inventory directly from the approved PDFs.
Do not preserve an entry, count, cost, prerequisite, or inferred interaction
unless a reviewer can cite it to an approved PDF page.

Review the complete core character-creation surface, including:

- Priority rows, metatypes, attributes, derived values, and creation limits.
- Mundane, magical, adept, and technomancer creation paths.
- Active skills, skill groups, specializations, knowledge skills, and languages.
- Positive and negative qualities, including ranks and parameters.
- Traditions, spells, rituals, adept powers, mentor spirits, and complex forms.
- Weapons, armor, electronics, software, general gear, augmentations, vehicles,
  drones, magical supplies, lifestyles, and starting resources.
- Contacts, identities, licenses, availability, legality, Essence, Karma, nuyen,
  grants, and all other finalization constraints.

For each catalog entry or implemented rule, record:

- Stable project ID and display name.
- Approved source PDF and page citation.
- Whether it is selectable, included, generated, bookkeeping-only, unavailable
  during creation, or explicitly excluded.
- Costs, ratings, limits, parameters, prerequisites, exclusions, grants, and
  effects needed by the application.
- Any ambiguity decision that affects its interpretation.

Names for user-authored knowledge skills, languages, contacts, identity details,
and license subjects remain bounded text wherever the approved PDFs define them
as open-ended rather than closed catalogs.

## Catalog Completeness Ledger

For every category, maintain three reviewed sets:

- Approved-PDF inventory with page citations.
- Project runtime catalog.
- Explicit exclusions with reason and page citation.

The release report must show approved-PDF count, project count, missing entries,
unexpected entries, and adjudicated differences. Release requires zero
unexplained differences. Only the approved PDFs can establish completeness.

## Blocking Ambiguity Register

During the fresh PDF review, record every unclear or conflicting rule before its
affected slice is implemented. At minimum, explicitly verify and cite:

- Technomancer priority skill and complex-form grants.
- Mystic Adept power-point purchase and cap.
- Sum-to-Ten total and repeated-priority restrictions.
- Natural-maximum restrictions and attribute allocation limits.
- Maximum skills at creation and interactions with Aptitude and free grants.
- Whether and when skill groups may be broken during creation.
- Priority-grant collisions with already purchased skills.
- Negative-quality selection and awarded-Karma limits.
- Restricted and Forbidden gear under the creation availability ceiling.
- Ware grades, availability, and Essence-loss timing and rounding.
- Knowledge specializations and native-language edge cases.
- Contact rating and combined-rating caps.
- Fake-license ratings and identity relationships.
- Lifestyle customization and starting-cash selection.
- Preparation eligibility, focus restrictions, and magical prerequisites.
- Derived-stat rounding, movement, sprinting, and unused allocation points.

Each resolution records the PDF citation, chosen behavior, impacted catalog or
evaluator IDs, and named regression tests. If the PDFs do not resolve the issue,
the project owner must approve a documented interpretation before finalization
can permit the affected selection.

## Rules Test Contract

At minimum, the completed milestone includes:

- Exhaustive Standard Priority and Sum-to-Ten assignment tests.
- Boundary tests at minimum, maximum, one below, and one above every rating and budget.
- Catalog schema, stable-ID, source-scope, citation-integrity, and semantic-hash tests.
- Reordering/property tests proving allocation order does not alter results.
- Golden valid builds covering every supported mundane, magical, adept, and
  technomancer path plus multiple Sum-to-Ten patterns.
- Golden invalid builds for every budget, eligibility, availability, Essence,
  quality, skill, identity, contact, and finalization class.
- PostgreSQL races for two slots, duplicate names, stale updates, discard/start, and finalization.
- HTTP authentication, ownership, antiforgery, malformed payload, non-core injection, and concurrency tests.
- Frontend keyboard, diagnostics, autosave, destructive preview, conflict, responsive, and accessibility tests.
- Real-browser Standard Priority and Sum-to-Ten finalization flows before release.

Rule defects always add a regression test at the lowest deterministic layer.
Catalog changes require a source citation, provenance note, and reconciliation
report; never bulk-update golden results merely to make tests pass.
