# Run & Gun AMMO and Arrowheads Ledger (CHAR-818)

This is the CHAR-818 source ledger for Run & Gun's AMMO section and
Arrowheads sidebar. It is a review input for the runtime catalog change it
accompanies, not a substitute for the approved book. It extends
`sr5-core`'s existing ammunition/arrow gear, published under CHAR-812 in
catalog version `1.4.0` (`gear` `categoryId: "ammunition"`).

CHAR-816 originally excluded this material, believing no base ammunition
catalog existed in the runtime to extend. That belief was wrong: CHAR-812
had already published `sr5-core`'s ammunition, arrow/bolt, explosive,
grenade, and rocket/missile line items in `gear`. The error was in checking
`sr5-core-1.0.0.json` (the immutable, never-updated base resource) instead
of the current runtime catalog, which layers CHAR-812's additions in via the
`1.4.0` overlay and carries them forward through every later version. This
ledger corrects that record and publishes the new items as ordinary
additions to the existing `gear` catalog — no schema or evaluator change was
needed. (Ammo damage/AP modifier fields are catalog-display-only and are
never wired into evaluator logic, per the existing "weapon-ammo damage
linkage" exclusion in `SR5_RULE_DECISIONS.md`, so these entries carry no
special evaluation behavior beyond ordinary `gear` purchase.)

## Source

Only `run-gun` is used, already pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md). Citations use the
same two-page printed/PDF offset verified for CHAR-815/816/817.

## Scope

Included: all 5 items in the AMMO section (`run-gun` pp. 54-55, PDF 56-57)
and all 7 items in the Arrowheads sidebar (`run-gun` pp. 23-24, PDF 25-26),
published as 12 new `gear` entries under `categoryId: "ammunition"`.

## Cost-Modeling Convention

Every entry publishes `Availability` and `Cost` exactly as printed, following
the same convention as `sr5-core`'s existing ammunition entries (CHAR-812).
Static Shaft is the one parameterized item (`RatingRange` 1-6, `PerRating`
cost); every other item is a flat-stat, fixed-price product sold in a
printed bundle quantity (e.g. "(10)" rounds), matching how `sr5-core`
ammunition is already modeled.

## New AMMO

| ID | Display name | Damage | AP | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `ammo-ex-explosive-run-gun` | EX-Explosive Rounds (10) | +2 | -1 | 14F / Forbidden | 120¥ | `run-gun` p. 54 (PDF 56) |
| `ammo-frangible` | Frangible Rounds (10) | -1 | +4 | 2R / Restricted | 10¥ | `run-gun` p. 54 (PDF 56) |
| `ammo-flare` | Flare Rounds (10) | -2 / +2 | +2 / -3 | 6R / Restricted | 20¥ | `run-gun` p. 54 (PDF 56) |
| `ammo-tracker-round` | Tracker Rounds (10) | -2 | -2 | 8R / Restricted | 150¥ | `run-gun` p. 54 (PDF 56) |
| `ammo-capsule-round` | Capsule Rounds (10) | -4 | +4 | 2 / Legal | 5¥ | `run-gun` p. 54 (PDF 56) |

## New Arrowheads

| ID | Display name | Acc | Damage | AP | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `arrowhead-barbed` | Barbed Head | -- | +1 | -- | 5R / Restricted | 10¥ | `run-gun` p. 23 (PDF 25) |
| `arrowhead-explosive` | Explosive Head | -1 | +2 | -1 | 9F / Forbidden | 15¥ | `run-gun` p. 23 (PDF 25) |
| `arrowhead-hammerhead` | Hammerhead | -1 | +1S | +2 | 5 / Legal | 5¥ | `run-gun` p. 24 (PDF 26) |
| `arrowhead-incendiary` | Incendiary Head | -1 | 8P (see source) | -6 | 12F / Forbidden | 100¥ | `run-gun` p. 24 (PDF 26) |
| `arrowhead-screamer` | Screamer Head | -2 | -2S | +6 | 2 / Legal | 5¥ | `run-gun` p. 24 (PDF 26) |
| `arrowhead-stick-n-shock` | Stick-n-Shock Head | -1 | 8S(e) | -5 | 6R / Restricted | 25¥ | `run-gun` p. 24 (PDF 26) |
| `arrowhead-static-shaft` (Rating 1-6) | Static Shaft | -- | +4S(e) | -- | 6R / Restricted | 25¥/rating | `run-gun` p. 24 (PDF 26) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| EX-Explosive Rounds naming/stat overlap with `sr5-core`'s existing `ammo-explosive-rounds` | Run & Gun's "EX-Explosive Rounds" (120¥/14F/+2 damage/-1 AP) has a different name and different stats than `sr5-core`'s existing "Explosive Rounds" (`ammo-explosive-rounds`, 80¥/9F/+1 damage/-1 AP, CHAR-812). Attempted to reconcile against `sr5-core`'s own ammo price table (p. 435) but that page's `pdftotext` extraction was column-scrambled and unreliable. Published as a distinct, separately-ID'd item (`ammo-ex-explosive-run-gun`) rather than silently overwriting or merging with the existing entry; flagged here as an unresolved cross-book discrepancy for project-owner review, consistent with this project's convention of recording PDF-vs-PDF conflicts rather than deciding them unilaterally. | `run-gun` p. 54 (PDF 56); `sr5-core` p. 435 (unreliable extraction) |
| Flare Rounds' and Incendiary Head's dual-value damage/AP strings | Preserved verbatim as printed (e.g. "-2 / +2", "8P (see source)") rather than resolved to a single number, following the same "encode complex stat strings as-is" convention used elsewhere in this catalog (e.g. the Gauss Rifle's `"10 (c) + Energy"` ammo field). | `run-gun` pp. 24, 54 (PDF 26, 56) |

## Review Footer

- Reviewed AMMO/Arrowhead rules: `run-gun` pp. 23-24, 54-55 (PDF 25-26,
  56-57).
- Approved-PDF products in scope: 5 AMMO items, 7 Arrowhead items (12
  total).
- Reconciliation: 12 new catalog `gear` entries account for all 12 in-scope
  products with no unexplained inventory difference. Combined with the
  existing 196 `gear` entries (including `sr5-core`'s own CHAR-812
  ammunition), the runtime catalog now publishes 208 total `gear` entries as
  of version `sr5-core` `1.7.0`.
