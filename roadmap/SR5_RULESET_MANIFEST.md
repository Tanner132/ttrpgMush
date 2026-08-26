# SR5 Ruleset Manifest

This manifest identifies the source revisions and project identities used by
Milestone 8. Its CHAR-801 source contract is approved. CHAR-802 published the
first immutable runtime catalog foundation and its semantic digest.

## Identity

| Field | Value |
| --- | --- |
| Ruleset ID | `sr5-core` |
| Catalog version | `1.0.0` |
| Manifest status | CHAR-801 approved; CHAR-802 priority foundation published |
| Catalog semantic digest | `D165B8A649CCEF484D0AAF106289A580205D46380EF6BF7B320DACCCC0003B94` |
| Review date | 2026-08-18 |
| Reviewer | Project owner |

Version `1.0.0` is published and current. Its initial resource contains the
creation methods and complete priority-assignment foundation required by
CHAR-802. Later catalog slices extend immutable versions rather than rewriting a
version referenced by a draft or finalized character.

## Approved Sources

| Source ID | Exact repository-root filename | SHA-256 |
| --- | --- | --- |
| `sr5-core` | `Shadowrun 5th Edition Core Rulebook (Jennifer Brozek, Raymond Croteau etc.) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | `4928B5F45F94C542820D7A7688BD68B7940BF2E9480898CCAFA6111996497F79` |
| `run-faster` | `Shadowrun Run Faster (Catalyst Game Labs) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | `F037FEFADC7FB91EE10180CD0116B55BC5EA825BF4BE56546D80C6F35D555BAF` |

A checksum or filename change creates a new source revision and requires a new
baseline review. External errata, summaries, websites, implementations, and
other books are not approved sources.

## Citation Convention

Catalog and rule citations record both the printed page and physical PDF page:

```text
<source-id> p. <printed-page> (PDF <physical-page>)
```

Example: `sr5-core p. 65 (PDF 67)`. Both approved PDFs have a two-page offset in
the character-creation ranges reviewed for this manifest. Citations must still
store both values rather than assuming that offset elsewhere in either document.

## Scope

Included:

- Default experienced-runner creation from the core rulebook.
- Standard Priority from `sr5-core` pp. 64-65 (PDF 66-67).
- Sum-to-Ten allocation rules from `run-faster` pp. 62-63 (PDF 64-65).
- Run Faster's priority-grant clarification allowing magician and mystic-adept
  grants to be selected as spells, rituals, and/or alchemical preparations from
  `run-faster` p. 63 (PDF 65).
- The five core metatypes and core-only character options.
- The same downstream core creation rules for both allocation methods.

Included (CHAR-813, project owner, 2026-08-26):

- Run Faster's 17 metavariants of the five core metatypes, selectable as a
  parameterized sub-choice of their parent metatype under both Standard
  Priority and Sum-to-Ten. See
  [`sr5-catalog/RUN_FASTER_METATYPES.md`](sr5-catalog/RUN_FASTER_METATYPES.md).
- "Poor Self Control (Vindictive)", the one Run Faster quality that bundle
  requires (`run-faster` p. 158 (PDF 160)).

Excluded:

- Core Street-Level and Prime Runner variants (`sr5-core` p. 64 (PDF 66)).
- Run Faster Point Buy and Life Modules (`run-faster` p. 62 (PDF 64)).
- Every other Run Faster metatype, quality, skill, spell, item, and other
  option: metasapients (Centaur, Naga, Pixie, Sasquatch), shapeshifters, and
  the Changelings/SURGE system, including its Metagenic Qualities catalog.
- Campaign-specific generation variants and custom GM-authored catalogs.
- Rules or corrections whose only authority is outside the approved PDFs.

## Creation Methods

### Standard Priority

Assign A, B, C, D, and E exactly once across Metatype, Attributes,
Magic/Resonance, Skills, and Resources. Duplicate levels are invalid.

Source: `sr5-core` pp. 64-65, 101 (PDF 66-67, 103).

### Sum-to-Ten

Assign one priority level to each of the same five categories and spend exactly
10 points. Priority costs are A = 4, B = 3, C = 2, D = 1, and E = 0. Levels may
repeat, but each category is assigned exactly once.

Source: `run-faster` pp. 62-63 (PDF 64-65).

Run Faster's reproduced priority table expands the core table's `spells` wording
to `spells, rituals, and/or alchemical preparations`. The project owner approved
that wording as the sole non-allocation Run Faster rule on 2026-08-18. It changes
grant composition only and does not admit any Run Faster catalog option. The
remaining core formula-cap conflict is resolved in the decision register.

## Review Artifacts

- [`SR5_CATALOG_LEDGER.md`](SR5_CATALOG_LEDGER.md) records reviewed inventory,
  runtime reconciliation status, and explicit exclusions.
- [`SR5_RULE_DECISIONS.md`](SR5_RULE_DECISIONS.md) records source-resolved issues
  and interpretations that still require project-owner approval.
- [`SR5_RULESET_BASELINE.md`](SR5_RULESET_BASELINE.md) remains the controlling
  scope and release contract.

## Gates

CHAR-801 is not approved until all of the following are true:

- Every ledger category has complete facts and citations, not only identity rows.
- Approved-PDF inventory and explicit exclusions reconcile with no unexplained
  inventory difference. The runtime side remains `not implemented` until CHAR-802
  materializes the approved inventory.
- Every blocking decision is approved with reviewer and date.
- The source-contract reviewer/date replaces the pending values above.

CHAR-802 then publishes catalog version `1.0.0`, computes its semantic digest,
records that digest above, and verifies the runtime resource against the approved
CHAR-801 inventory. This sequencing avoids making CHAR-801 depend on CHAR-802,
which already depends on CHAR-801.
