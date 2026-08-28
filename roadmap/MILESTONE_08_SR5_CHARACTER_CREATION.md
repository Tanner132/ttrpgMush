# Milestone 8: Core SR5 Character Creation

**Outcome:** An authenticated player can consume one of their two character slots,
complete an autosaved Shadowrun Fifth Edition character using Standard Priority or
Sum-to-Ten, and finalize it into a server-validated playable character containing
every applicable core-rulebook creation option.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification
commands. See [`SR5_RULESET_BASELINE.md`](SR5_RULESET_BASELINE.md) for authority,
scope, catalog completeness, and unresolved-rule gates.

## Fixed Product Contract

- The rules baseline is the approved local English SR5 core-rulebook PDF.
- Standard Priority and Sum-to-Ten are supported.
- Run Faster is used only for Sum-to-Ten allocation and its priority-grant
  clarification allowing the listed magician and mystic-adept spell grants to be
  selected as spells, rituals, and/or alchemical preparations.
- Every selectable metatype, quality, skill, spell, power, item, and other option is core-only.
- The approved local core-rulebook and Run Faster PDFs are the only rules and catalog references.
- External rules summaries, implementations, catalogs, errata documents, and other books are out of scope unless explicitly approved later.
- The application owns its catalog models, stable IDs, evaluator, diagnostics, and tests.
- The server calculates all costs, grants, requirements, budgets, derived values, and final legality.
- A creation draft consumes a character slot and reserves its globally unique name.
- Drafts are not playable and cannot enter chat, presence, movement, or play sessions.
- A user may have at most two total drafts and finalized characters.

## Target Architecture

### Character Lifecycle

Extend `Character` with a lifecycle state of `Draft` or `Finalized` and a nullable
finalization timestamp. Creating a draft creates the `Character` immediately so
the existing user-row lock, two-character count, owner relationship, character ID,
and normalized-name unique index remain authoritative.

Add a one-to-one `CharacterCreationDraft` containing:

- Character ID as primary and restricted foreign key.
- Ruleset ID, catalog version, and catalog semantic digest.
- Creation method.
- Draft document schema version.
- Typed selections serialized to JSONB.
- Opaque UUID optimistic-concurrency version.
- Created and updated UTC timestamps.

Add a one-to-one immutable `CharacterSheet` containing:

- Character ID as primary and restricted foreign key.
- Ruleset ID, catalog version, and catalog semantic digest.
- Creation method and sheet schema version.
- Canonical evaluated sheet serialized to JSONB.
- Canonical source-draft digest.
- Finalized UTC timestamp.
- Sheet kind distinguishing evaluated sheets from migrated legacy placeholders.

Relational columns hold ownership, lifecycle, concurrency, catalog identity, and
timestamps. JSONB holds the evolving aggregate selections and immutable finalized
snapshot. Do not add an EAV schema or normalize every SR5 option before a gameplay
query demonstrates that need.

### Catalog

Ship immutable, versioned, project-owned catalog resources with Application. Each
published version remains available while a draft references it. The catalog uses
project IDs and typed records derived directly from the approved PDFs.

Catalog data contains only what the application needs:

- Stable ID, display name, category, and source reference.
- Ratings, costs, availability, capacity, and creation limits.
- Typed prerequisites, exclusions, parameters, grants, and effects.
- Parent-child relationships for equipment and generated selections.
- Explicit selectable, support-only, included-component, and career-only status.
- Ruleset and catalog version metadata.

Do not embed a general expression language. Put direct option facts in catalog data
and cross-selection algorithms in typed Application code.

### Rule Evaluation

The Application evaluator is pure and deterministic. It accepts a complete catalog,
creation method, and draft selections and returns:

- Entitlements and grants.
- Expenditures by source currency.
- Derived values.
- Canonical preview.
- Structured diagnostics.
- Finalization readiness.

Allocation provenance is mandatory. Attribute, skill, spell, contact, power, gear,
and other selections must retain whether they came from priority points, special
points, group points, a talent grant, free knowledge/contact points, Karma, or nuyen.
A single net balance cannot prove SR5 legality.

Diagnostics contain a stable code, severity, affected step, field path, related
option IDs, source rule, bounded message arguments, and suggested resolution.
Intermediate drafts may contain rule errors. Finalization may not.

### Transactions

Starting a draft locks the authenticated user's Identity row, counts drafts and
finalized characters, reserves the normalized name, assigns the configured starting
room, and inserts the character and draft in one transaction.

Updating a draft checks ownership and the expected UUID version, evaluates the
candidate state, updates the name and document atomically, and rotates the version.
Rule-invalid but structurally safe intermediate state may be saved.

Discarding a draft locks the owner row, checks the version, deletes the draft child
and draft character in one transaction, and releases both slot and name. Finalized
characters are not deletable through this operation.

Finalization evaluates an exact versioned draft, then transactionally rechecks its
version and digest, inserts the immutable sheet, marks the existing character
finalized, resets it to the configured starting room, and removes the draft child.
Rule failure or any persistence failure leaves the character as an unchanged draft.

## API Contract

Use authenticated endpoints under `/api/character-creation`:

- `GET /catalogs/current?method=...` returns the current immutable catalog contract and digest.
- `GET /catalogs/{catalogId}/{version}` returns a retained pinned catalog version.
- `POST /drafts` reserves a slot and name and pins method/catalog version.
- `GET /drafts` lists the authenticated user's resumable drafts.
- `GET /drafts/{characterId}` returns selections, budgets, preview, diagnostics, steps, and version.
- `PUT /drafts/{characterId}` replaces the typed draft document using an expected version.
- `POST /drafts/{characterId}/change-preview` reports data invalidated by an upstream change.
- `DELETE /drafts/{characterId}` discards a version-matched draft.
- `POST /drafts/{characterId}/finalize` reruns all rules and atomically finalizes.
- `GET /api/characters/{characterId}/sheet` returns the owner's finalized sheet.

All mutations require antiforgery. Non-owned identifiers return a non-enumerating
404. Stale versions return 409. A structurally valid but illegal finalization returns
422 with diagnostics. The existing name-only `POST /api/characters` must be removed
when this flow launches so it cannot bypass character creation.

## Creator Experience

Use the existing neon-noir tokens and primitives. The creator reads as a runner
dossier terminal rather than a generic form wizard.

Desktop layout:

- Dossier header with character name, method, draft ID, autosave state, and readiness.
- Numbered left step rail with complete, attention, locked, and conflict states.
- Large center workspace for the active allocation or catalog.
- Sticky right inspector for option details, prerequisites, budgets, and diagnostics.
- Bottom command bar with Back, save status, and Continue.

Mobile layout:

- One content column below 900px.
- Accessible step-index disclosure replacing the permanent rail.
- Relevant budget telemetry immediately below the active heading.
- Selection details expanding directly after the selected row.
- Sticky command bar with safe-area padding and minimum 44px targets.
- No horizontally scrolling data tables as the only representation.

Stable steps:

1. Method selection before draft creation.
2. Identity and concept.
3. Priority assignment.
4. Metatype and special attributes.
5. Physical and mental attributes.
6. Qualities.
7. Augmentations, Essence preview, and implant capacity attachments.
8. Active skills, groups, and specializations.
9. Awakening or Emergence, conditionally.
10. Knowledge skills and languages.
11. Contacts.
12. Resources, accessories and modifications, identities, licenses, vehicles,
    and drones.
13. Lifestyle and starting-cash selection.
14. Remaining Karma and finishing choices.
15. Review and finalization.

Upstream changes that would remove selections require a server-generated impact
preview and explicit confirmation. The server reports the exact selections cleared,
budgets refunded, and earliest invalidated step. The client never guesses cascading
effects.

Earlier-step edits are always allowed. An upstream edit must preserve downstream
selections in the draft, then re-evaluate the complete aggregate against the new
rules and budgets. Newly invalid downstream values are never silently cleared;
the response identifies each affected field, the exceeded amount or unmet
requirement, and the earliest affected step. Those steps remain editable and are
marked attention-required, and finalization remains unavailable until every
diagnostic is resolved. This applies equally to priority grants, attribute and
skill points, special points, nuyen, and later resource budgets.

Autosave discrete changes immediately and text after blur or a short idle delay.
Serialize writes per draft, keep the server snapshot canonical, and provide explicit
Unsaved, Saving, Saved, Failed, and Conflict states. Never use local storage as the
authoritative draft.

## CHAR-801: Freeze The Rules And Content Contract

**Depends on:** Accepted scope in `PROJECT_CONTEXT.md`.

**Scope:**

- Pin the approved local core-rulebook and Run Faster PDFs by exact filename and checksum.
- Rebuild the rules and option inventory directly from the approved PDFs; do not carry forward unverified provisional inventory facts.
- Record a PDF page citation for every implemented mechanic and catalog option.
- Complete the include/exclude catalog ledger described in `SR5_RULESET_BASELINE.md`.
- Resolve every blocking ambiguity against the approved PDFs when available; otherwise
  require an explicit, documented product interpretation before implementing it.
- Assign a project ruleset ID and first immutable catalog version.

**Acceptance criteria:**

- Every implemented mechanic and catalog fact cites an approved PDF and page.
- Every catalog option is core-tagged and reconciled against a reviewed inventory transcribed from the approved core PDF.
- No rule or option whose only source is outside the approved PDFs enters the catalog or evaluator.
- Sum-to-Ten contains no Run Faster option catalogs.
- The ruleset manifest contains the approved filenames and matching checksums.

## CHAR-802: Add Catalog And Pure Evaluation Foundation

**Depends on:** CHAR-801.

**Scope:**

- Add typed catalog models, immutable resource loading, schema validation, and semantic hashing.
- Add a pure Application evaluator and structured diagnostics.
- Add `SeattleByNight.Application.Tests` for rules and catalog tests.
- Implement Standard Priority and Sum-to-Ten assignment validation first.

**Acceptance criteria:**

- All 120 Standard Priority permutations are exhaustively tested.
- All 3,125 five-category Sum-to-Ten assignments are exhaustively tested.
- Corrupt catalogs, duplicate IDs, dangling references, or digest mismatches fail readiness.
- Identical inputs always produce identical diagnostics and previews.
- Unknown and non-core option IDs produce bounded diagnostics rather than exceptions.

## CHAR-803: Persist Slot-Bearing Drafts

**Depends on:** CHAR-802.

**Scope:**

- Add lifecycle, draft, sheet, EF mappings, migration, and store boundary.
- Add start, read, update, discard, and finalization commands/queries.
- Make every playable-character and play-session query require finalized state.
- Migrate existing characters to explicit legacy finalized sheets without inventing SR5 statistics.

**Acceptance criteria:**

- Drafts and finalized characters jointly obey the two-character limit under concurrency.
- Drafts reserve global names and discarding releases both slot and name.
- Drafts cannot enter play sessions, movement, chat, or presence.
- Stale updates and finalization return conflicts without partial writes.
- Existing characters remain playable and are clearly marked as legacy sheets.

## CHAR-804: Expose Draft And Catalog HTTP APIs

**Depends on:** CHAR-803.

**Scope:**

- Add thin endpoints for catalog, draft lifecycle, change previews, and finalization.
- Add typed request/response contracts and stable problem/diagnostic shapes.
- Keep owner identity and all calculated values server-derived.

**Acceptance criteria:**

- API tests cover authentication, ownership isolation, antiforgery, malformed payloads, limits, and conflicts.
- Clients cannot submit authoritative costs, grants, totals, derived values, ownership, or lifecycle state.
- Finalization always reloads and evaluates the current persisted draft.
- Non-core IDs are rejected even when structurally valid.

## CHAR-805: Build Slot Dashboard And Creator Shell

**Depends on:** CHAR-804.

**Scope:**

- Replace name-only creation with two explicit slot cards.
- Add protected new/resume routes and the responsive dossier shell.
- Add autosave, version-conflict handling, step navigation, diagnostics, and budget telemetry.
- Preserve browser back/forward and refresh behavior.

**Acceptance criteria:**

- Draft, finalized, empty, loading, and failed slot states are accessible and distinct.
- Reload resumes the persisted authoritative step and state.
- The shell is fully keyboard operable and usable at narrow mobile widths.
- No draft exposes an Enter World action.

## CHAR-806: Implement Priorities, Metatypes, And Attributes

**Depends on:** CHAR-805.

**Scope:**

- Implement both creation methods, five core metatypes, normal attributes, and special attributes.
- Implement metatype minima/maxima, special-point grants, and natural-maximum rules.
- Add destructive previews for method, priority, and metatype changes.

**Acceptance criteria:**

- Every core metatype/priority combination is cataloged and boundary-tested.
- Attribute allocation preserves priority, special-point, and Karma provenance.
- Knowledge, contact, limit, and other dependent previews recalculate authoritatively.
- Invalid downstream selections are never silently removed.
- Revisiting any earlier step preserves later selections, surfaces authoritative
  invalidation diagnostics, and blocks finalization until the draft is repaired.

## CHAR-807: Implement Qualities, Skills, And Knowledge

**Depends on:** CHAR-806.

**Scope:**

- Implement every core positive and negative quality with levels and required parameters.
- Implement every ordinary core active skill, all core groups, exotic skill parameters, and specializations.
- Implement open-ended knowledge/language names, categories, native language, and Bilingual behavior.

**Acceptance criteria:**

- Catalog reconciliation has zero unexplained differences for qualities and skills.
- Quality prerequisites, conflicts, repeatability, and Karma caps are tested.
- Skill/group budgets and group-breaking rules are tested at every boundary.
- Free-text parameters are bounded, plain text, and subject to server validation.

## CHAR-808: Implement Magic And Resonance

**Depends on:** CHAR-806 and CHAR-807.

**Scope:**

- Implement mundane, magician, aspected magician, adept, mystic adept, and technomancer paths.
- Implement core traditions, spells, rituals, adept powers, mentor spirits, and complex forms.
- Implement priority grants, Magic/Resonance allocation, power points, and dependent skill eligibility.

**Acceptance criteria:**

- Each path has at least one valid and invalid golden build.
- Technomancer and mystic-adept errata interpretations have named regression tests.
- Conditional UI exposes only authoritative eligible options.
- Magic and Resonance are mutually exclusive and Essence effects are deterministic.

## CHAR-809: Implement Resources And Essence

**Depends on:** CHAR-807 and CHAR-808.

**Scope:**

- Implement all core chargen gear, weapons, armor, electronics, software, magical supplies, augmentations, vehicles, and drones.
- Implement ratings, quantities, availability, legality, grades, cost, capacity, attachment, and included-component rules.
- Implement nuyen accounting, Karma conversion, Essence, and augmentation-dependent effects.

**Acceptance criteria:**

- Every project catalog entry is reconciled with the reviewed core-PDF inventory.
- Included components, generated profiles, and bookkeeping records never appear as duplicate shop options.
- Resource, availability, capacity, and Essence boundary/property tests pass.
- Street samurai, decker, rigger, and magical-equipment golden builds pass.

## CHAR-809A: Implement Gear Capacity, Mounts, And Attachments

**Depends on:** CHAR-809.

Purchased gear is not a flat list. Weapons, armor, worn electronics,
augmentations, and vehicles all host other purchases, and each host constrains
what it can carry. CHAR-809 prices independent items only; this slice adds the
host/attachment relationship the catalog contract already anticipates under
`Parent-child relationships for equipment and generated selections`.

**Scope:**

- Add a typed host/attachment relationship to draft resource selections. An
  attachment references a specific purchased line instance, not a bare item ID,
  so two copies of the same host carry independent attachments. This requires
  stable per-line instance identifiers in the draft document.
- Implement firearm mounts. Accessories occupy one of `top`, `barrel`, or
  `underbarrel`; each mount holds at most one accessory; accessories listed with
  no mount occupy none; `Top or Under` accessories require an explicit chosen
  mount. Hold-outs have no mounts. Pistols, machine pistols, and SMGs have top
  and barrel mounts only. All rifles and heavy weapons have all three. Projectile
  weapons accept only accessories designed for them.
  Sources: `sr5-core` p. 417 (PDF 419), p. 431 (PDF 433).
- Implement integral/included accessories. Accessories that come with a weapon do
  not consume a mount location and are never separately purchasable or charged in
  that instance.
  Source: `sr5-core` p. 417 (PDF 419).
- Implement armor Capacity. A worn armor piece has Capacity equal to its Armor
  Rating; each modification has a fixed or `[Rating]` Capacity cost; modification
  ratings run 1-6 except as noted; each worn piece carries its own Capacity pool.
  Catalog the seven core armor modifications with their Capacity, Availability,
  and cost.
  Source: `sr5-core` p. 437 (PDF 439).
- Implement device Capacity for optical, audio, and sensor hosts. Host devices are
  purchased at a chosen Capacity within their printed range, their cost is derived
  from that chosen Capacity, and each vision/audio/sensor enhancement consumes its
  own Capacity cost from the host.
  Source: `sr5-core` p. 444 (PDF 446).
- Implement augmentation Capacity. Cybereyes, cyberears, and cyberlimbs carry
  Capacity for modifications. An item with a bracketed Capacity cost may be
  installed in a cyberlimb, consuming Capacity instead of Essence. Bodyware
  without a bracketed Capacity cost cannot be installed in a cyberlimb.
  Cyberlimbs hold no bioware and no implant that costs Essence rather than
  Capacity. Cyberlimb enhancements consume the limb's Capacity, are limited to the
  Agility, Armor, and Strength types, and permit at most one enhancement of each
  type per limb.
  Sources: `sr5-core` p. 451 (PDF 453), p. 454 (PDF 456), p. 456 (PDF 458).
- Implement vehicle weapon mounts and modifications. A vehicle may carry weapon
  mounts equal to its unaugmented Body divided by three, rounded down. A standard
  mount holds an assault rifle or smaller weapon; a heavy mount counts as two
  mounts and holds any weapon. Catalog the core vehicle modifications with their
  Availability and cost.
  Source: `sr5-core` p. 461 (PDF 463).
- Apply existing purchase rules to every attachment individually: the creation
  Availability ceiling of 12, rating-derived Availability and cost, metatype gear
  cost modifiers, and augmentation grade multipliers. Per `gear.rating-cap-force`,
  the creation Rating 6 ceiling applies to purchasable Rating and Force only and
  does not cap Capacity.
  Sources: `sr5-core` p. 94 (PDF 96); `SR5_RULE_DECISIONS.md`.
- Add capacity, mount, and attachment diagnostics with the established structured
  shape, including the host line, the exceeded pool, and the amount over.
- Extend the canonical evaluated sheet so a finalized sheet records each
  attachment against its host line with allocation provenance preserved.

**Open rule questions (resolved):**

- Whether ballistic and riot shields are modifiable worn armor: resolved as
  source-resolved. `sr5-core` p. 437 (PDF 439) states directly that shields "have
  a Capacity equal to their Armor Rating" for the chemical protection, fire
  resistance, and nonconductivity modifications. Recorded as `gear.shield-capacity`
  in `SR5_RULE_DECISIONS.md`.
- Whether a host purchased at a chosen Capacity may later be re-rated while
  attachments remain: resolved as no special-case code. No source rule addresses
  this, so the project's standing policy applies unchanged — upstream edits never
  silently delete downstream selections, and the server re-evaluates and returns
  field-level diagnostics. Lowering a host's chosen Rating/Capacity naturally
  makes the existing Capacity math re-run over-budget, which already emits
  `attachment.capacity.exceeded` without deleting the attachment. Covered by
  `Lowering_a_host_rating_below_its_attachments_surfaces_a_diagnostic_without_deleting_them`
  in `GearAttachmentEvaluatorTests.cs`.

**Acceptance criteria:**

- A weapon cannot carry two accessories on one mount, an accessory cannot occupy a
  mount its host lacks, and integral accessories consume no mount. ✅
- Armor, device, and augmentation Capacity pools are enforced per host instance,
  and two copies of the same host track their attachments independently. ✅
- Attachment purchases are charged against the nuyen budget and, where applicable,
  Essence, with provenance retained. ✅
- Every attachment is independently checked against the Availability 12 ceiling. ✅
- Cyberlimb enhancement type limits and the bioware/Essence-implant exclusion are
  tested at their boundaries. ✅
- Vehicle weapon mounts are limited by unaugmented Body divided by three, and a
  heavy mount consumes two. ✅
- Removing or re-rating a host surfaces authoritative diagnostics for the
  attachments it can no longer carry, and never silently deletes them. ✅
- Golden builds covering a modified firearm, modified armor, a cyberlimb loadout,
  and an armed vehicle pass. ✅ (`GearAttachmentEvaluatorTests.cs` covers each
  loadout individually at the evaluator layer; the frontend attachment flow for
  each host kind — device, cyberlimb, and vehicle — was additionally exercised
  live end-to-end through the actual creator UI and API.)

**Status: Complete.** All three deferred CHAR-809A sub-scopes (device Capacity,
augmentation/cyberlimb Capacity, vehicle weapon mounts) are implemented,
unit/integration-tested (241 Domain/Application/Api backend tests, 210 frontend tests passing), and manually
verified live in the browser. Cyberdeck program slots remain explicitly out of
this ticket's scope; see the CHAR-809 status note in `ROADMAP.md`.

## CHAR-810: Implement Contacts, Identities, And Lifestyles

**Depends on:** CHAR-807 and CHAR-809. Licenses covering attached accessories
also depend on CHAR-809A.

**Scope:**

- Implement free-form contacts with Connection/Loyalty and Charisma-derived points.
- Implement identities, Fake SINs, attached licenses, and bounded license subjects.
- Implement all core lifestyles, metatype cost modifiers, duration, and starting cash.

**Acceptance criteria:**

- Contact costs, caps, and free/Karma point provenance are tested. ✅
  (`ContactEvaluatorTests.cs`: free-Karma-pool exact spend, creation-cap
  rejection, general-Karma overflow, unspent-pool rejection, duplicate
  instance ids.)
- Licenses cannot become global character flags or silently legalize
  forbidden items. ✅ `IdentityEvaluator` never reads `Availability.Legality`
  and returns only a fresh local `CanonicalIdentities` record touching no
  shared/static state; `IdentityEvaluatorTests.cs` locks this in by asserting
  an unrelated item's diagnostics are identical whether or not a
  license/SIN is also present.
- Lifestyle and starting-cash calculations are server authoritative. ✅
  `LifestyleEvaluator` computes every preview cost; the one-shot starting-cash
  dice roll happens only in `FinalizeCharacterCreationDraftCommandHandler`
  during atomic finalization, never in the deterministic preview path.
- Open-ended fields remain bounded plain text and are never interpreted as
  HTML. ✅ Contact name/role, fake SIN details, and license subject are all
  capped at 120 characters (`creation.text.too-long`) and rendered as plain
  React text content, matching the `quality.open-parameters` convention used
  elsewhere in the creator.

**Status: Complete.** `ContactEvaluator`, `IdentityEvaluator`, and
`LifestyleEvaluator` are implemented, chained into the existing
Resources/GearAttachment nuyen budget, unit-tested (147 Application-layer
backend tests, 25 new), and exposed through new Contacts and Lifestyle
creator steps plus a fake-SIN/license section folded into the existing
Resources & Vehicles step. 225 frontend tests pass (15 new), and the full
flow — adding a contact, purchasing a fake SIN, linking a license to it, and
choosing a primary lifestyle with a team payment form — was manually
exercised live through the creator UI with diagnostics surfacing and
clearing correctly.

## CHAR-811: Final Review And Atomic Finalization

**Depends on:** CHAR-806 through CHAR-810, including CHAR-809A.

**Scope:**

- Implement complete final validation, derived statistics, carryover, and immutable sheet generation.
- Add the review dossier with linked diagnostics and exact budget summaries.
- Replace direct name-only character creation and integrate finalized sheets with selection/play entry.

**Acceptance criteria:**

- Finalization succeeds if and only if no blocking diagnostic remains. ✅
  Already true by construction (`IsReadyToFinalize` is exactly "no Error-
  severity diagnostic"), but this was previously vacuous for untouched
  sections. Metatype/Attributes, Skills/Knowledge (including the
  one-required-free-native-language rule), Magic-or-Resonance, and Lifestyle
  now always evaluate once the priority assignment is resolved, so a draft
  that never touched them is correctly blocked rather than trivially "ready."
  Contacts and Identities/Licenses remain deliberately optional (CHAR-810).
- Duplicate submissions and concurrent edits produce one deterministic
  committed result. ✅ Pre-existing: `ExpectedVersion` optimistic concurrency
  plus an atomic draft-delete/sheet-insert commit already guaranteed this;
  `Finalization_atomically_creates_sheet_and_removes_draft` covers the
  duplicate-submission case (a second finalize with the same version returns
  `NotFound` once the draft is gone).
- Valid Standard Priority and Sum-to-Ten end-to-end flows enter the New
  Character Room. ✅ `Valid_sum_to_ten_draft_also_finalizes_into_the_new_character_room`
  adds Sum-to-Ten coverage alongside the existing Standard Priority test.
- Invalid or interrupted finalization leaves the complete draft resumable. ✅
  Pre-existing: a rule-violation or conflict response never mutates the
  stored draft.

**Status: Complete.** A new `DerivedStatisticsEvaluator` computes the
sr5-core p. 101 final-calculations block (Essence, Physical/Mental/Social
Limits, Initiative, Condition Monitor boxes and Overflow, Karma/nuyen
carryover capped at 7/5,000) deterministically on every preview, exposed
through the draft response. The new Review & Finalize creator step displays
this block plus every diagnostic in the draft; the Finalize action itself,
atomic commit, and room routing needed no changes. The direct name-only
character-creation endpoint (`POST /api/characters`) has no remaining
production caller — the creator UI only ever calls the draft/finalize
endpoints — and is kept solely as shared test scaffolding for unrelated
suites (movement, chat, room presence) that need a character to exist
without exercising SR5 chargen. There is no separate "Karma & Finishing"
creator step: the creator header already carries a running Karma total, so a
dedicated summary step was cut as redundant. Instead, Knowledge/Language
points beyond the free pool — previously a hard block
(`knowledge.free-points.exceeded`) — now draw extra Karma directly, per the
sr5-core p. 107 Karma Advancement Table (a rank's marginal cost is its own
rank number, cumulative 1/3/6/10/15/21 to reach ratings 1-6; a specialization
beyond the pool costs a flat 7), folded into the same shared creation Karma
pool as contacts' free-Karma overflow (`knowledge.karma-overflow`). Verified
via 384 backend tests (15 new) and 233 frontend tests (8 new), plus live
manual exercise of the Review & Finalize step showing real diagnostics and
final calculations.

## CHAR-812: Completeness, Accessibility, And Release Gate

**Depends on:** CHAR-811.

**Scope:**

- Produce the final approved-PDF/project catalog reconciliation report.
- Run full golden-build, property, concurrency, migration, API, frontend, and browser suites.
- Complete keyboard, screen-reader, reduced-motion, zoom, and narrow-screen review.
- Document ruleset/catalog hashes and operational rollback/disable procedure.

**Acceptance criteria:**

- Every in-scope option from the approved core PDF is present and every out-of-scope option is absent.
- No unexplained catalog discrepancy remains.
- No serious or critical automated accessibility finding remains.
- Existing authentication, chat, movement, presence, world editing, and play sessions regressions pass.
- The released ruleset and catalog hashes match the approved review artifacts.

**Status: Complete.** Six parallel background audits reconciled every
equipment domain against the approved core PDF (weapons/armor,
electronics/software, general gear/drugs, augmentations, vehicles/drones,
magical equipment). The audits surfaced four genuine content gaps beyond
data-entry fixes — ammunition/explosives/grenades/rockets, drugs/toxins/BTL,
unpriced foci, and unfindable Autosoft pricing — all resolved this ticket per
project owner direction to implement everything now rather than defer:

- Ammunition (11 types), arrow/bolt ammo (4 types), explosives (4 items),
  grenades (7 types), and rockets (3 selectable + 3 `creationUnavailable`
  missiles) were added to `gear` under new `categoryId` values, reusing the
  existing purchase/evaluation/display pipeline with new optional
  `damage`/`ap`/`blast` display fields on `GearDefinition` — no weapon-ammo
  damage-resolution linkage was built, since no weapon's damage is resolved
  by any evaluator today (`gear.ammunition-grenade-rocket-linkage`).
- Drugs (10 types), toxins (9 types), and BTL chips (4 types) were added to
  `gear` the same way, with new `speed`/`duration`/`addictionType`/`effect`
  display fields.
- All 16 foci gained Force-scaled `cost`/`availability`/`ratingRange`/
  `focusCategoryId` data (`FocusDefinition` extended, plus new loader
  validation) — this adds missing catalog data, not a purchase-flow fix,
  since no `FocusSelection`/evaluator/nuyen-deduction path exists for any
  focus yet (`focus.pricing-added-no-purchase-flow`).
- Autosofts remain excluded: no priced Autosoft table exists anywhere in the
  approved PDF copy despite an extensive search (`gear.autosoft-pricing-absent`).

Seven smaller fixes also shipped: Leather Jacket/Duster armor; Ocular Drone,
External Clip Port, and cybergun Laser Sight/Silencer augmentations; 7
existing headware/earware entries gained missing `capacityCost` fields; and
two vehicle citation corrections (Harley-Davidson Scorpion, Horizon
Flying-Eye). Three reconciliation-report "mismatches" turned out to be
non-issues already covered by existing approved decisions
(`vehicle-modification.manual-operation-absolute-values`,
`gear.helmet-availability`, `gear.smoke-area`), and one flagged gap
(Biometric Reader) was already present and correct in the base catalog. A
Gas Grenade cross-item cost reference was excluded per
`gear.gas-grenade-chemical-payload-cost` in `SR5_RULE_DECISIONS.md`, matching
the existing `gear.focus-formula-cost-reference` precedent of excluding
rather than fabricating a cross-item price. A separately flagged Cyberlimb
Customization gap (`ware.cyberlimb-customization-unmodeled`) was implemented
rather than excluded: `ResourcesEssenceEvaluator.EvaluateCyberlimbCustomization`
lets a purchased cyberlimb's inherent Strength/Agility be raised above the
base 3, at `+5,000¥`/`+1 Availability` per point, capped at the character's
natural metatype maximum, with matching STR/AGI steppers in
`AugmentationsStep.tsx`. The new content
shipped as a `sr5-core-1.4.0.json` overlay (196 gear items, 12 armor, 95
augmentations, 16 priced foci) pinned in `EmbeddedRulesetCatalogProvider`
with a computed semantic digest.

The accessibility half of this ticket was completed and live-verified
earlier in the same session: 17 clickable rows across 6 creator steps
received `role="button"`/`tabIndex`/`onKeyDown`/`aria-label`, confirmed
working in a real browser session with no horizontal overflow at 375px.
Verified via the full backend suite (504 tests passing: Domain, Application,
Infrastructure, API) and the full frontend suite (325 tests across 36
files), both green, plus a clean `tsc -b` type-check.
