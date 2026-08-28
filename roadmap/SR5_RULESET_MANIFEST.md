# SR5 Ruleset Manifest

This manifest identifies the source revisions and project identities used by
Milestone 8. Its CHAR-801 source contract is approved. CHAR-802 published the
first runtime catalog foundation and its semantic digest.

## Identity

| Field | Value |
| --- | --- |
| Ruleset ID | `sr5-core` |
| Catalog version | `1.0.0` (mutable pre-alpha development schema — see Schema Lifecycle) |
| Manifest status | CHAR-801 approved; CHAR-802 priority foundation published |
| Catalog semantic digest | Computed on every load (`EmbeddedRulesetCatalogProvider.CurrentSemanticDigest`); not enforced pre-alpha — see Schema Lifecycle |
| Review date | 2026-08-28 |
| Reviewer | Project owner |

## Schema Lifecycle

The runtime catalog schema (`backEnd/.../CharacterCreation/Catalog/Resources/
sr5-core-1.0.0.json`) moves through three phases as content is imported from
approved source books:

1. **Pre-alpha / development (current phase).** One mutable canonical schema
   (`sr5-core-1.0.0.json`) holds the full runtime catalog. All schema changes
   — new fields, new content types, new source-book slices — are written
   directly into this file rather than published as a new version. Digest and
   version-pin integrity enforcement is disabled (commented out, not
   deleted, with matching comments) in `RulesetCatalogLoader.Load`,
   `CharacterCreationDraftEvaluator.Evaluate`, and
   `CharacterCreationBaselineReader.Read`, because the schema's content — and
   therefore its digest — is expected to keep changing under
   already-created drafts/sheets during this phase. This trades away the
   "a draft's rules never silently shift" guarantee in exchange for not
   needing an immutable version bump (and full duplicate republish, per the
   old overlay convention) for every content change. Acceptable because
   there are no real players yet; any draft/sheet created pre-lock is
   expected to be discardable.
2. **Schema lock.** Once the approved-book content set for the initial
   release is complete and the schema shape has stopped changing
   structurally, the project owner declares `1.0.0` stable. At that point it
   becomes the first immutable published version: its digest is recorded and
   enforcement is re-enabled by un-commenting the three checks above.
3. **Post-lock / production.** Every subsequent schema change publishes a new
   immutable version (a new resource file plus a new
   `EmbeddedRulesetCatalogProvider.CatalogVersionPin` entry) rather than
   editing a locked file. `RulesetCatalogLoader.LoadOverlay` and the
   `RetainedVersions` append-only-lockfile pattern (both preserved
   unchanged through the pre-alpha phase, just currently holding a single
   entry) resume normal use: legacy versions stay resolvable by
   `EmbeddedRulesetCatalogProvider.Get`, and existing drafts/sheets keep
   resolving against the exact version+digest they were created under.

## Approved Sources

| Source ID | Exact repository-root filename | SHA-256 |
| --- | --- | --- |
| `sr5-core` | `Shadowrun 5th Edition Core Rulebook (Jennifer Brozek, Raymond Croteau etc.) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | `4928B5F45F94C542820D7A7688BD68B7940BF2E9480898CCAFA6111996497F79` |
| `run-faster` | `Shadowrun Run Faster (Catalyst Game Labs) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | `F037FEFADC7FB91EE10180CD0116B55BC5EA825BF4BE56546D80C6F35D555BAF` |
| `run-gun` | `Shadowrun Run Gun (Catalyst Game Labs) (z-library.sk, 1lib.sk, z-lib.sk).pdf` | `D76698DF6652198B340FD62B99F84C8A232C1EF3CAF8390F32DBACB1C93A371B` |
| `better-than-bad` | `pdfcoffee.com_better-than-bad-pdf-free.pdf` | `4BEB7F2620BCA5114AD83E297B92C8337062EBE404974C80E25AE39C11965D07` |

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

Included (CHAR-814, project owner, 2026-08-26):

- Every quality and quality variant printed in Run Faster's "New Quality"
  (Rank, `run-faster` p. 86, PDF 88) and "Qualities for Good or Ill"
  (`run-faster` pp. 144-159, PDF 146-161) material, including the four
  remaining Poor Self Control variants CHAR-813 left out of its own scope.
  See [`sr5-catalog/RUN_FASTER_QUALITIES.md`](sr5-catalog/RUN_FASTER_QUALITIES.md).

Included (CHAR-815, project owner, 2026-08-28):

- Every quality printed in Run & Gun's "New Qualities" (`run-gun` p. 127,
  PDF 129) and the "Qualities" section adjoining the Staying Alive chapter
  (`run-gun` p. 169, PDF 171). See
  [`sr5-catalog/RUN_GUN_QUALITIES.md`](sr5-catalog/RUN_GUN_QUALITIES.md).

Included (CHAR-816, project owner, 2026-08-28):

- Every weapon printed in Run & Gun's "Arsenal" chapter, Blades through
  Flamethrowers (`run-gun` pp. 18-49, PDF 20-51), including four newly
  introduced weapon categories (laser weapons, flamethrowers, harpoon guns,
  slingshots) and seven generated alternate-configuration profiles. See
  [`sr5-catalog/RUN_GUN_WEAPONS.md`](sr5-catalog/RUN_GUN_WEAPONS.md).

Included (CHAR-817/CHAR-818, project owner, 2026-08-28):

- Every weapon accessory printed in Run & Gun's "Weapon Accessories" section
  (`run-gun` pp. 50-53, PDF 52-55) and its 6-slot mounting system (Top,
  Underbarrel, Barrel, Side, Internal, Stock), extending the existing
  `WeaponAccessoryDefinition`/`WeaponMount`/`GearAttachmentEvaluator` system
  rather than introducing a new one. See
  [`sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md`](sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md).
- Every AMMO item (`run-gun` pp. 54-55, PDF 56-57) and Arrowhead
  (`run-gun` pp. 23-24, PDF 25-26), extending `sr5-core`'s existing
  ammunition/arrow gear published under CHAR-812. See
  [`sr5-catalog/RUN_GUN_AMMO.md`](sr5-catalog/RUN_GUN_AMMO.md).

Included (CHAR-819, project owner, 2026-08-28):

- Every quality printed in Better Than Bad's "New Qualities" section — New
  Positive Qualities, New Mastery Qualities, and New Negative Qualities
  (`better-than-bad` pp. 160-162, PDF 161-163). `better-than-bad` is newly
  pinned as a fourth approved source, first used by this ticket. See
  [`sr5-catalog/BETTER_THAN_BAD_QUALITIES.md`](sr5-catalog/BETTER_THAN_BAD_QUALITIES.md).

Included (CHAR-820, project owner, 2026-08-28):

- Both powers printed in Better Than Bad's "New Adept Powers" section, Mystic
  Aptitude and State of Purity (`better-than-bad` pp. 159-160, PDF 160-161).
  See [`sr5-catalog/BETTER_THAN_BAD_ADEPT_POWERS.md`](sr5-catalog/BETTER_THAN_BAD_ADEPT_POWERS.md).

Excluded:

- Core Street-Level and Prime Runner variants (`sr5-core` p. 64 (PDF 66)).
- Run Faster Point Buy and Life Modules (`run-faster` p. 62 (PDF 64)).
- Every other Run Faster metatype, skill, spell, item, and other option not
  listed above as included: metasapients (Centaur, Naga, Pixie, Sasquatch),
  shapeshifters, and the Changelings/SURGE system, including its Metagenic
  Qualities catalog and every Infected quality and critter power.
- Run & Gun's Martial Arts subsystem, Sixth World Combat Tactics, Killshots
  and More combat-resolution rules, Staying Alive's environmental hazard
  rules, equipment repair rules, demolitions test/breach procedures, Gear
  Qualities (GM-only flags), Improvised Melee Weapons, Underbarrel Weight and
  Weapon Commlink (both unpriceable under the current cost schema — see
  [`sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md`](sr5-catalog/RUN_GUN_WEAPON_ACCESSORIES.md)),
  and adventure/fiction content.
- Better Than Bad's New Manipulation Spells, New Armor Modifications, Grey
  Mana Tattoos, New Toxin (Blight), New Cyberware Grade (GreyWare), New Life
  Modules (Nationalities, Formative Years, Further Education, Real Life —
  Life Modules are excluded project-wide, consistent with `run-faster` p. 62
  (PDF 64) above), "New Uses for Karma and Street Cred" (a new spend-Karma
  mechanic, not a catalog entry), and all setting/adventure content (the
  Pretoria-Witwatersrand-Vaal metroplex writeup, NPCs, plot hooks). Only the
  book's New Qualities (CHAR-819) and New Adept Powers (CHAR-820) sections
  have been reviewed for scope; the rest above is unreviewed, not
  reviewed-and-rejected. See
  [`SR5_CATALOG_DEFERRED_WORK.md`](SR5_CATALOG_DEFERRED_WORK.md) for Martial
  Arts (deferred, revisit later) and the rest (reviewed and rejected as out
  of scope).
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
