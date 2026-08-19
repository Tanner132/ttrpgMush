# Tech Debt: Character Creation Review

**Scope:** Correctness, security, and maintainability debt in the SR5 character
creation flow, discovered during a whole-flow review. This is a refactor/hardening
milestone, not a feature milestone — it does not change the modular-monolith
architecture or add new creation sections.

**Outcome:** Finalized SR5 characters carry a complete evaluated sheet, the rules
evaluator enforces the full creation rules it claims to support, the catalog's
integrity guarantees actually cover all catalog data, and the creator no longer
duplicates server-side math.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification
commands. See [`MILESTONE_08_SR5_CHARACTER_CREATION.md`](MILESTONE_08_SR5_CHARACTER_CREATION.md)
for the target architecture this debt sits inside. Rule-interpretation questions
that need owner confirmation are listed in the
[Product Decisions](#product-decisions-to-confirm) section and must be resolved in
[`SR5_RULE_DECISIONS.md`](SR5_RULE_DECISIONS.md) before their work item is built.

> These items are written as a flat, ordered backlog. When exported to GitHub,
> create one issue per work item under a `TechDebt` milestone, with the priority
> label shown in each heading.

---

## Sequencing

Execute in priority order. `P0` and `P1` items are independent and can run in
parallel with each other; `P2` items may depend on the stated predecessor.

1. `TD-01` (P0) — finalized sheet completeness
2. `TD-08` (P0) — structural-safety bypass
3. `TD-02` (P1) — Exceptional Attribute for physical/mental attributes
4. `TD-05` (P1) — client/server Power Point divergence
5. `TD-09` (P1) — priority-cell lookup index
6. `TD-12` (P1) — DI consistency
7. `TD-14` (P1) — shared diagnostic factory
8. `TD-10` (P2) — move catalog data into the pinned resource (depends on `TD-09` shape)
9. `TD-03` (P2) — Edge/special-attribute validation (depends on product decision)
10. `TD-04` (P2) — karma budget consolidation (depends on product decision)
11. `TD-06` (P2) — skill linked-attribute data (depends on `TD-10`)
12. `TD-07` (P2) — citation page-number precision (depends on `TD-10`)
13. `TD-11` (P2) — step-mapping single source of truth
14. `TD-13` (P2) — implement change-preview clearing/refunds

---

## Work Items

### TD-01 — Finalized sheet must contain the full evaluated character (P0)

**Current behavior.** `CharacterCreationDraftSerialization.SerializeCanonicalSheet`
serializes a private `CanonicalCharacterCreationSheet(PriorityAssignmentPreview)`
record that contains only the priority grid
(`CharacterCreationDraftSerialization.cs:27-30`). `CharacterCreationDraftDetails.Preview`
is typed as `PriorityAssignmentPreview?` (`CharacterCreationDraftModels.cs:117-121`),
and finalization writes that preview as the immutable sheet
(`CharacterCreationDraftCommands.cs:251`). A finalized character therefore loses its
metatype, attributes, special attributes, qualities, skills, groups, knowledge,
languages, and Awakening/Emergence selections.

**Change.**

1. Introduce a typed canonical-sheet model in Application that captures the resolved,
   server-derived character state: metatype ID, absolute attribute values (metatype
   minimum plus allocated points), special-attribute values, quality selections,
   skill/group allocations, knowledge/language/native-language selections, and the
   Magic/Resonance selection — each retaining its provenance (priority, special
   points, group points, grant, karma, free points) per the Milestone 8 "Allocation
   provenance is mandatory" requirement.
2. Extend the evaluator to emit that canonical model (not just diagnostics) and add it
   to `CharacterCreationDraftDetails`. The pure evaluators already compute the derived
   values; capture them instead of discarding them.
3. Change `SerializeCanonicalSheet` to serialize the full canonical model, and change
   the finalize handler/store to persist it. Keep `PriorityAssignmentPreview` for the
   existing priority-step UI; do not overload it with full-sheet data.
4. Bump `CharacterCreationDocumentVersions.Sheet` and add a migration-free guard: old
   evaluated sheets without the new fields are recognized by schema version.

**Acceptance.** A finalized character's `GET /api/characters/{id}/sheet` returns a
canonical sheet containing every selected metatype, attribute (absolute), quality,
skill, language, and Awakening/Emergence choice. Domain tests assert the canonical
sheet round-trips and preserves provenance. Existing legacy sheets remain readable.

---

### TD-08 — Close the structural-safety bypass (P0)

**Current behavior.** `CharacterCreationDraftDocumentValidator.IsStructurallySafe`
returns `document is not null` when `PriorityAssignment` is null
(`CharacterCreationDraftCommands.cs:133-135`), skipping every length/count bound.
A client can submit `priorityAssignment: null` with an unbounded `qualities`,
`skills`, `languages`, or parameter strings, and it is persisted verbatim to JSONB.

**Change.**

1. Restructure the validator so collection/text/parameter bounds are enforced
   regardless of whether `PriorityAssignment` is present, and the priority-specific
   checks are an additional layer only when it is non-null.
2. Add a test asserting an oversized quality/skill/text payload with a null priority
   assignment is rejected with `InvalidDocument`.

**Acceptance.** `Replace` and `change-preview` reject oversized documents even when
`priorityAssignment` is null. No unbounded JSONB write is possible.

---

### TD-02 — Honor Exceptional Attribute for physical/mental attributes (P1)

**Current behavior.** `MetatypeAndAttributeEvaluator` flags any attribute above the
racial maximum (`range.Minimum + item.Value > range.Maximum`,
`MetatypeAndAttributeEvaluator.cs:79-89`) and never consults the
`exceptional-attribute` quality. `HasExceptionalAttributeFor` exists only in
`MagicResonanceEvaluator.cs:670`.

**Change.** Move `HasExceptionalAttributeFor` to a shared helper (see `TD-14`) and use
it in `MetatypeAndAttributeEvaluator` to raise the natural maximum by 1 for the
attribute named in the quality's `attribute-id` parameter. Keep the "at most one
attribute at natural maximum" rule operating on the adjusted maximum.

**Acceptance.** A human with Exceptional Attribute (Agility) may allocate Agility to 7
without a `natural-maximum-exceeded` diagnostic, while all other attributes still cap
at their racial maximum. Domain tests cover the raised cap and the unchanged cap for
non-selected attributes.

---

### TD-05 — Remove client/server Power Point divergence (P1)

**Current behavior.** The server computes `Improved Reflexes` cost as
`1.5 / 2.5 / 3.5` (`MagicResonanceEvaluator.cs:483-486`), but the frontend Power Point
total uses flat `powerPointCost * rank` (`CreationSteps.tsx:466`), showing 1.5/3.0/4.5.
The frontend also recomputes karma (`CreationSteps.tsx:285-298`) already computed
server-side.

**Change.** Make the frontend display derived budget numbers from server-provided data
rather than recomputing rules. The smallest correct fix is to have the backend return
per-power effective cost (or a resolved `powerPointCost` including the irregular
reflexes cost) in the catalog, and have `CreationSteps.tsx` consume that. Remove the
client-side karma re-derivation once `TD-04` exposes server budgets.

**Acceptance.** The client-displayed Power Point total for Improved Reflexes ranks
1/2/3 matches the server's 1.5/2.5/3.5. No rules math is duplicated client-side.

---

### TD-09 — Replace linear priority-cell scans with an indexed lookup (P1)

**Current behavior.** `(category, level)` cell lookups use
`catalog.PriorityCells.Values.FirstOrDefault(...)` or `.Single(...)` in
`PriorityAssignmentEvaluator.cs:47`, `MetatypeAndAttributeEvaluator.cs:18-20`,
`QualitiesSkillsKnowledgeEvaluator.cs:23,241`, and `MagicResonanceEvaluator.cs:23`,
re-scanning the 25-cell collection multiple times per evaluation.

**Change.** Add a `(string CategoryId, string LevelId)`-keyed lookup to
`RulesetCatalog` (built once in the loader), and replace the scans with direct lookups.
Return a structured "cell missing" result instead of throwing where `.Single` is used.

**Acceptance.** Evaluators no longer scan `PriorityCells.Values`; the catalog exposes a
single indexed accessor. Existing evaluator tests pass unchanged.

---

### TD-12 — Register evaluators consistently in DI (P1)

**Current behavior.** `PriorityAssignmentEvaluator` and `MetatypeAndAttributeEvaluator`
are registered as singletons (`Application/DependencyInjection.cs:15-16`), while
`QualitiesSkillsKnowledgeEvaluator` and `MagicResonanceEvaluator` are `new`-ed inside
`CharacterCreationDraftEvaluator` (`CharacterCreationDraftEvaluator.cs:23-25`).

**Change.** Register all four evaluators as singletons and remove the inline `new`
fallbacks from `CharacterCreationDraftEvaluator`'s constructor.

**Acceptance.** All four evaluators resolve from DI; the fallback constructor defaults
are removed; tests that construct the evaluator directly are updated to pass instances
explicitly.

---

### TD-14 — Extract a shared diagnostic factory (P1)

**Current behavior.** `Bounded`, `Unknown`, `TextTooLong`, and the `Error` overloads
are duplicated across all four evaluators
(`PriorityAssignmentEvaluator.cs:116-139`, `MetatypeAndAttributeEvaluator.cs:103-108`,
`QualitiesSkillsKnowledgeEvaluator.cs:228`, `MagicResonanceEvaluator.cs:713-745`).

**Change.** Introduce a single internal `CharacterCreationDiagnosticFactory` (or
extension methods) with `Unknown`, `Error`, `Warning`, `TextTooLong`, and a shared
`Bounded`. Also centralize the `HasExceptionalAttributeFor` helper here for `TD-02`.

**Acceptance.** Each diagnostic code's construction exists in one place; evaluators
call the factory. Tests are unaffected in behavior.

---

### TD-10 — Move hardcoded catalog data into the pinned resource (P2)

**Current behavior.** Qualities, skills, skill groups, knowledge categories, creation
paths, aspected values, traditions, spells, rituals, adept powers, mentor spirits,
complex forms, spirit/sprite types, and foci are hardcoded C# arrays in
`RulesetCatalogLoader.cs:236-579`. The semantic digest only covers the JSON subset
(sources, methods, priority levels/categories/cells, metatypes, attributes), so edits
to any of these tables silently change the "immutable" catalog with no digest change.

**Change.** Move these tables into the catalog resource (or a companion pinned
resource with the same digest mechanism). Extend `RulesetCatalogLoader` to validate
them (unique IDs, bounded display names, valid citations, well-formed grants/costs)
and include them in `ComputeSemanticDigest`. Regenerate the semantic digest and update
`EmbeddedRulesetCatalogProvider.CurrentSemanticDigest`. Keep the builder `switch`
logic (skill domain, page maps) as loader code, but make the option facts data.

**Acceptance.** The digest changes whenever any quality cost, spell, adept power, or
other catalog fact changes. `RulesetCatalogLoaderTests` fail on a deliberately mutated
catalog resource. No option facts remain in C# source.

---

### TD-03 — Validate Edge and special attributes fully (P2)

**Current behavior.** Metatype special-attribute points are only checked for overspend
(`MetatypeAndAttributeEvaluator.cs:43-52`). Edge's racial minimum/maximum is never
enforced, and Edge is never required to be allocated.

**Change.** After resolving the product decision (see below): compute absolute Edge
(metatype minimum plus allocated points) and validate it against the metatype's Edge
range; require a complete special-attribute allocation where the priority grants
points; keep Magic/Resonance mutual exclusivity and natural maximum as-is.

**Acceptance.** Overspent, under-allocated, and out-of-range Edge produce specific
diagnostics with source citations.

---

### TD-04 — Consolidate and correct karma accounting (P2)

**Current behavior.** Quality karma is computed twice: in
`QualitiesSkillsKnowledgeEvaluator.EvaluateQualities`
(`QualitiesSkillsKnowledgeEvaluator.cs:57-62`) and in
`MagicResonanceEvaluator.EvaluateKarmaBudget` (`MagicResonanceEvaluator.cs:602-646`).
The `positive - negative > 25` check in the former is unreachable given the two 25-Karma
caps and misstates how negative qualities interact with the creation pool. The full
pool check only runs when `MagicResonance` is non-null.

**Change.** After resolving the product decision (see below): implement one shared
karma-budget computation that computes positive-quality spend, negative-quality bonus,
and formula/power-point/complex-form spend against `25 + negative`, and emit a single
canonical diagnostic set regardless of creation path (including mundane).

**Acceptance.** One diagnostic per karma violation; mundane and awakened characters are
budgeted identically; no unreachable diagnostics remain.

---

### TD-06 — Populate skill linked attributes (P2)

**Current behavior.** `BuildSkills` sets `LinkedAttribute = ""` for every skill
(`RulesetCatalogLoader.cs:255`).

**Change.** As part of `TD-10`, move skills into the catalog resource and set the
correct linked attribute per skill from the approved PDFs.

**Acceptance.** `SkillDefinition.LinkedAttribute` is populated and covered by loader
tests.

---

### TD-07 — Make citation page numbers explicit (P2)

**Current behavior.** `Citation(source, page)` derives `PdfPage = PrintedPage + 2`
(`RulesetCatalogLoader.cs:581`), assuming a uniform offset for the whole book.

**Change.** As part of `TD-10`, carry explicit `printedPage`/`pdfPage` pairs for every
catalog entry, and drop the `+2` assumption.

**Acceptance.** No hardcoded offset; loader tests assert exact citations for a sample
of entries.

---

### TD-11 — Single source of truth for step mapping (P2)

**Current behavior.** Step labels (`CreatorShellPage.tsx:29-59`), the diagnostic→step
switch (`CreatorShellPage.tsx:272-282`), and the `2..15` clamps
(`useDraft.ts:263,278,290`) all encode step indices independently.

**Change.** Define one ordered step model (index, label, availability) in a single
frontend module, and derive labels, navigation bounds, and diagnostic attention
mapping from it. Remove magic numbers.

**Acceptance.** No duplicated step-index literals; changing a step is a one-file edit.

---

### TD-13 — Implement change-preview clearing and refunds (P2)

**Current behavior.** `PreviewCharacterCreationDraftChangeQueryHandler` always returns
empty `ClearedSelections`/`RefundedBudgets` (`CharacterCreationDraftQueries.cs:103-107`)
despite downstream selections existing and being evaluated.

**Change.** Implement the Milestone 8 non-linear navigation contract: comparing the
candidate against the current draft, compute which downstream selections become
invalid (and would be cleared), the budgets that would be refunded, and the earliest
invalidated step, returning them through the existing
`CharacterCreationChangePreview` model.

**Acceptance.** Changing priority/metatype returns a preview with the affected
downstream selections and refunded budgets; tests cover an upstream change that
invalidates skill/knowledge/magic selections.

---

## Product Decisions to Confirm

These require owner confirmation in [`SR5_RULE_DECISIONS.md`](SR5_RULE_DECISIONS.md)
before their work item is built. Do not implement from assumptions.

1. **Karma pool semantics (`TD-04`).** Confirm that negative-quality karma *adds* to
   the 25-Karma creation pool (spendable pool = `25 + negative`), that positive
   qualities and negative qualities each have independent 25-Karma caps, and that
   formula/power-point/complex-form purchases draw from the same pool. Confirm whether
   the `positive - negative > 25` check is dropped entirely.

2. **Special-attribute / Edge allocation (`TD-03`).** Confirm the exact model for
   `SpecialAttributeAllocation.Values` (points-spent deltas vs. absolute values), the
   Edge racial minimum/maximum enforcement, and whether Edge must be allocated during
   creation or may remain at its racial minimum.

3. **Exceptional Attribute scope (`TD-02`).** Confirm that Exceptional Attribute raises
   the natural maximum of exactly one named physical/mental attribute by 1, and
   interacts with the "one attribute at natural maximum" rule as implemented.

4. **Improved Reflexes cost table (`TD-05`).** Confirm the irregular Power Point cost
   (`1.5/2.5/3.5`) is the authoritative core-rulebook value and belongs in catalog data.

---

## Verification

```powershell
dotnet test backEnd/SeattleByNight.slnx
npm --prefix frontEnd run test -- --run
npm --prefix frontEnd run lint
```

Each work item is complete only when its focused tests are added and the relevant
backend/frontend checks pass. Update `PROJECT_CONTEXT.md` "Current Implemented Surface"
when `TD-01` and `TD-10` change the persisted sheet shape or catalog provenance.
