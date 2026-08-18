# SR5 Detailed Catalog Review

These files are the row-level CHAR-801 transcription of the approved PDFs. They
are review inputs for the typed runtime catalog introduced by CHAR-802, not the
runtime catalog itself and not a substitute for the books.

## Sources

Only the source IDs and revisions pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md) may be used.
Citations use printed page and physical PDF page, for example:
`sr5-core p. 65 (PDF 67)`.

## Required Entry Fields

Every closed-catalog entry records:

- Stable lowercase ASCII project ID and exact display name.
- Classification: `selectable`, `parameterized`, `included-component`,
  `generated`, `bookkeeping`, `creation-unavailable`, or `excluded`.
- Source citation on the same row or immediately adjacent detail block.
- Every applicable creation fact: cost, rating range, availability, legality,
  capacity, quantity/unit, prerequisite, exclusion, grant, effect, limit, and
  required parameter.
- Parent/child or generated-profile relationships needed to prevent duplicate
  purchases.
- The approved decision ID when interpretation changes literal source text.

Use `none` when a field is applicable but the approved source explicitly has no
value. Use `not applicable` when the concept does not apply. Never leave a blank
that could mean either unknown or none.

Open-authored options record the bounded typed fields and closed categories that
the application validates. Examples in the PDF are support material, not an
exhaustive catalog.

## Review Footer

Each category file ends with:

- Reviewed printed/PDF page ranges.
- Approved-PDF entry count by classification.
- Explicit exclusions and source discrepancies.
- Remaining unknown facts. CHAR-801 requires `None` here.
- Runtime reconciliation status. This remains `Not implemented` until CHAR-802.

## Category Files

| File | Scope |
| --- | --- |
| `PRIORITIES_METATYPES.md` | Methods, priority cells, metatypes, attributes, global creation budgets |
| `QUALITIES.md` | Positive/negative qualities and closed sub-options |
| `SKILLS.md` | Active skills, groups, specializations, knowledge, languages |
| `MAGIC_RESONANCE.md` | Paths, traditions, spells, rituals, preparations, powers, mentors, forms, spirits, sprites |
| `WEAPONS_ARMOR.md` | Weapons, ammunition, accessories, explosives, armor |
| `ELECTRONICS_GEAR.md` | Electronics, software, sensors, security, survival, medical, drugs, toxins, general gear |
| `AUGMENTATIONS.md` | Grades, cyberware, bioware, cyberlimbs, implant weapons |
| `VEHICLES_RESOURCES.md` | Vehicles, drones, magical equipment, lifestyles, identities, licenses, contacts, final resources |
