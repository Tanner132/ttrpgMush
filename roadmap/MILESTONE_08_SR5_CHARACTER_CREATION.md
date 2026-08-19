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
7. Augmentations and Essence preview.
8. Active skills, groups, and specializations.
9. Awakening or Emergence, conditionally.
10. Knowledge skills and languages.
11. Contacts.
12. Resources, identities, licenses, vehicles, and drones.
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

## CHAR-810: Implement Contacts, Identities, And Lifestyles

**Depends on:** CHAR-807 and CHAR-809.

**Scope:**

- Implement free-form contacts with Connection/Loyalty and Charisma-derived points.
- Implement identities, Fake SINs, attached licenses, and bounded license subjects.
- Implement all core lifestyles, metatype cost modifiers, duration, and starting cash.

**Acceptance criteria:**

- Contact costs, caps, and free/Karma point provenance are tested.
- Licenses cannot become global character flags or silently legalize forbidden items.
- Lifestyle and starting-cash calculations are server authoritative.
- Open-ended fields remain bounded plain text and are never interpreted as HTML.

## CHAR-811: Final Review And Atomic Finalization

**Depends on:** CHAR-806 through CHAR-810.

**Scope:**

- Implement complete final validation, derived statistics, carryover, and immutable sheet generation.
- Add the review dossier with linked diagnostics and exact budget summaries.
- Replace direct name-only character creation and integrate finalized sheets with selection/play entry.

**Acceptance criteria:**

- Finalization succeeds if and only if no blocking diagnostic remains.
- Duplicate submissions and concurrent edits produce one deterministic committed result.
- Valid Standard Priority and Sum-to-Ten end-to-end flows enter the New Character Room.
- Invalid or interrupted finalization leaves the complete draft resumable.

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
