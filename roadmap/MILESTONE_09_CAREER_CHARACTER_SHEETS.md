# Milestone 9: Career Character Sheets

**Outcome:** An authenticated player can view a finalized character sheet from
character selection or gameplay, spend Karma on every supported core-rulebook
career advancement, and spend nuyen on eligible catalog purchases. The server
derives every cost and resulting value, records every expenditure, and preserves
the immutable finalized creation sheet as the character's creation baseline.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification
commands. All Shadowrun mechanics and catalog facts remain governed by the approved local
core-rulebook and Run Faster PDFs described in `PROJECT_CONTEXT.md`.

See [`SHEET_901_CAREER_RULES_BASELINE.md`](SHEET_901_CAREER_RULES_BASELINE.md)
for the SHEET-901 rules-freeze deliverable: the cited career advancement cost
table, attribute/skill/quality/magic/resonance rules, the contact-advancement
rejection, the nuyen purchase eligibility ledger, and the candidate rule
decisions pending project-owner approval before SHEET-902 begins.

## Fixed Product Contract

- A finalized character card has separate **Enter World** and **View Character
  Sheet** actions.
- The sheet is available at `/characters/:characterId/sheet` without starting or
  changing a play session.
- `/character` opens the selected character's sheet in a modal without navigating
  away from gameplay or disconnecting SignalR.
- The route and modal use the same sheet query and rendering components.
- The existing finalized `CharacterSheet` remains an immutable creation baseline.
  Career advancement must not rewrite its canonical JSON.
- Current permanent values are composed from the creation baseline and a separate
  mutable career state owned by the server.
- Players never directly edit Karma, nuyen, advancement costs, purchase prices,
  attribute values, skill ratings, or other authoritative mechanical values.
- Players may immediately purchase every career advancement supported by this
  milestone when they have sufficient Karma and satisfy objective mechanical
  prerequisites.
- Advancement ignores training and downtime requirements for this release.
- Advancement does not require Storyteller approval. This is an explicit product
  interpretation for qualities and other rules that normally expect approval or
  appropriate narrative circumstances. Objective conflicts, prerequisites,
  maximums, and creation-only restrictions still apply.
- Nuyen may be spent only through eligible catalog purchases. Arbitrary balance
  edits, player-authored expenses, sales, refunds, trades, and transfers are out of
  scope.
- Availability and acquisition tests do not block initial catalog purchases.
  Availability and legality remain visible information. Price, rating, parameters,
  compatibility, prerequisites, and sufficient funds remain authoritative.
- Ordinary directly priced catalog items are the first purchase surface.
  Augmentation installation, Essence changes, host attachments, Capacity, focus
  bonding, and other purchases that mutate additional mechanical state require
  their dedicated tickets before they become purchasable.
- Resource and advancement history is append-only. Corrections create compensating
  records rather than rewriting or deleting history.
- Legacy character sheets remain readable but cannot use mechanical advancement or
  purchasing until an explicit conversion or bootstrap workflow is approved.

## Rules Contract

The release must reconcile every implemented advancement directly against the
approved PDFs before implementation. At minimum, the contract includes the core
Character Improvement and Training Rate material on core pp. 103-107.

The currently approved cost foundation is:

| Advancement | Karma cost |
| --- | ---: |
| Attribute | New rating x 5 |
| Active skill | New rating x 2 |
| Skill group | New rating x 5 |
| Knowledge or language skill | New rating |
| New Knowledge or language skill | 1 |
| Specialization | 7 |
| Positive quality | Listed cost x 2 |
| Remove negative quality | Listed bonus x 2 |
| Spell | 5 |
| Complex form | 4 |
| Initiation or Submersion | 10 + new grade x 3 |

These values are not a complete implementation contract by themselves. SHEET-901
must additionally record maximums, prerequisites, group-breaking behavior,
creation-only options, Magic and Resonance interactions, Initiation and Submersion
choices, adept progression, contact improvement, and every other supported career
operation with exact citations.

Training-time values must be retained in the rules ledger for accuracy, but the
application does not delay advancement or schedule completion in this milestone.

## Target Architecture

### Creation Baseline

Keep the existing one-to-one `CharacterSheet` unchanged. It remains the immutable
record of:

- Ruleset, catalog version, and semantic digest.
- Creation method and sheet schema version.
- Finalized attributes, skills, qualities, resources, contacts, identities,
  lifestyles, and derived values.
- Creation provenance and source-draft digest.
- Carryover Karma, carryover nuyen, and the once-only starting-cash roll.

Before gameplay depends on canonical sheet data, add an explicit typed baseline
reader keyed by `CharacterSheetKind` and `SheetSchemaVersion`. It must normalize
supported evaluated sheet versions and reject unsupported or malformed combinations
deterministically. Future code must not deserialize every historical row directly
into only the latest canonical record.

### Career State

Add one mutable career-state aggregate per evaluated finalized character. The
relational envelope contains:

- Character ID as primary and restricted foreign key.
- Career document schema version.
- Opaque UUID optimistic-concurrency version.
- Current Karma.
- Current nuyen.
- Lifetime Karma earned.
- Typed progression document serialized to JSONB.
- Created and updated UTC timestamps.

The progression document contains only permanent post-creation changes, including:

- Attribute and special-attribute increases.
- Learned or improved skills and groups.
- New specializations.
- Added positive and removed negative qualities.
- Learned spells, rituals, preparations, powers, and complex forms where allowed.
- Initiation and Submersion progression.
- Other approved permanent career selections that are not inventory instances.

Do not put current damage, current Edge, initiative, ammunition, temporary effects,
or other frequently changing encounter state in the career document. Those values
belong to a later runtime-condition boundary.

### Composed Sheet

Application owns a typed composition operation:

```text
immutable creation baseline
+ permanent career progression
+ acquired inventory
= current permanent character sheet
```

The client must not merge raw creation JSON and progression data or calculate
current ratings. The composed projection returns display-ready names and values,
current balances, derived statistics, and server-calculated legal next actions.

### Reuse Of Character-Creation Functionality

Reuse character-creation functionality selectively. The career sheet should share
catalog infrastructure, calculation primitives, visual language, and small
presentational components, but it must not reopen a finalized character in the
creation wizard or convert the character back into a draft.

Good frontend reuse candidates include:

- Immutable catalog loading, lifetime request deduplication, semantic-digest
  verification, and retained-version lookup.
- Catalog ID indexes, display-name resolution, descriptions, search, filtering, and
  resource normalization.
- Existing accessible UI primitives, readouts, option-detail presentations, and
  responsive section patterns.
- Pure sheet section renderers extracted from creation review where they can accept
  purpose-built display data without depending on draft state.

Shared presentation may render the same current value in both workflows while the
controlling actions remain distinct. For example, an attribute row may be shared,
but creation allocates priority or Karma points across a mutable draft while career
advancement offers one server-priced increase against the current Karma balance.

Do not reuse or add a career mode to:

- `CreatorShellPage` workflow orchestration.
- Creation draft autosave, dirty-generation, impact-preview, or finalization state.
- Whole-document draft replacement.
- Creation step completion and downstream-invalidity navigation.
- Priority, Sum-to-Ten, creation Karma, creation nuyen, Availability-12, or other
  creation-only budgets and caps.

Avoid a broad `mode = creation | career` switch across existing creator components.
If a component requires repeated mode checks for rules, mutation behavior,
diagnostics, or save semantics, keep separate workflow components and extract only
the smaller mode-independent presentation beneath them.

Good backend reuse candidates include:

- Pinned catalog providers, loaders, validation, and stable IDs.
- Typed canonical records used by the version-aware creation-baseline reader.
- Metatype natural-range and Exceptional Attribute resolution.
- Catalog price and rating resolution.
- Attachment compatibility and Capacity calculations when later purchase tickets
  require them.
- Pure derived-statistic formulas after creation-specific inputs are separated from
  them.

Do not run career mutations through `CharacterCreationDraftEvaluator`, reconstruct a
synthetic draft, or call creation-oriented budget evaluators. Creation tolerates an
intermediate invalid aggregate and validates the whole document before one terminal
finalization. Career advancement starts from a valid permanent sheet and applies one
atomic legal operation with an immutable expenditure record.

Application should therefore expose separate career evaluators for intent-oriented
operations such as raising one attribute, raising one skill, acquiring one quality,
or purchasing one catalog item. Shared formulas may sit below both workflows, but
the creator and career handlers, diagnostics, persistence, and transactions remain
separate.

### Resource Transactions

Add an append-only `CharacterResourceTransaction` with:

- UUID ID.
- Character ID.
- Resource type (`Karma` or `Nuyen`).
- Signed amount.
- Balance after the transaction.
- Transaction type.
- Bounded description.
- Optional advancement or inventory-purchase ID.
- Server-assigned UTC timestamp.

Positive amounts represent opening balances, later awards, or corrections. Negative
amounts represent advancements or purchases. Player advancement and purchase
requests never supply this amount; Application calculates it from the pinned rules
or catalog.

Transactions are immutable. A correction appends a compensating transaction. The
database must prevent resource transactions from being orphaned from their
character, advancement, or purchase where a reference is present.

### Advancement History

Add an immutable `CharacterAdvancement` with:

- UUID ID and character ID.
- Typed advancement category.
- Stable target ID and bounded typed details.
- Previous and new values where applicable.
- Server-calculated Karma cost.
- Ruleset and catalog version.
- Server-assigned UTC timestamp.

The current career state is the efficient read model. Advancement records provide
the owner-visible history and an operational record for disputed or corrected
changes. This is not an event-sourced aggregate; the application does not rebuild
the state by replaying its full history on every query.

### Acquired Inventory

Creation resources remain part of the immutable baseline. Career purchases create
separate `CharacterInventoryItem` instances with:

- UUID item-instance ID and character ID.
- Catalog item ID and typed catalog collection.
- Ruleset, catalog version, and semantic digest.
- Quantity, rating, and approved parameters.
- Server-calculated purchase price.
- Acquisition source and timestamp.

Two purchases of the same catalog entry remain distinct instances when later
equipment state or attachments may differ. The composed sheet projects creation
resources and acquired inventory together without rewriting either source.

### Idempotency And Concurrency

Every advancement and purchase accepts:

- The expected career-state version.
- A client-generated request ID.
- Only the target and parameters needed to express intent.

Persist a bounded action receipt keyed uniquely by character ID and request ID.
Each mutation must:

1. Resolve the authenticated owner; never accept a user ID from the client.
2. Reject legacy, draft, unowned, or unsupported character sheets without exposing
   another user's character data.
3. Begin a database transaction and lock the career-state row.
4. Return the recorded result for a duplicate request ID without spending again.
5. Reject a stale expected version with a conflict and the information needed to
   reload.
6. Load the typed creation baseline, pinned catalog, career state, and relevant
   inventory.
7. Reconstruct the authoritative current values.
8. Calculate and validate the advancement or purchase.
9. Reject insufficient funds and prevent negative balances.
10. Persist career state, immutable history, resource transaction, inventory where
    applicable, and action receipt atomically.
11. Rotate the career-state version and commit before returning success.

Database constraints must support, but not replace, Application validation. Retried,
concurrent, and stale requests must not duplicate an advancement, item, or charge.

## Balance Initialization

For an evaluated finalized character, initialize:

```text
Initial Karma = DerivedStatistics.CarryoverKarma

Initial nuyen = DerivedStatistics.CarryoverNuyen
              + Lifestyles.StartingCash.Total
```

Initialization creates one opening transaction per resource and is idempotent.
Starting cash is read from the once-only finalized roll and is never rolled again.

New finalizations should create career state and opening transactions in the same
transaction as finalization once SHEET-903 is deployed. Existing evaluated sheets
must be backfilled before mutation endpoints are enabled. Backfill must report
unsupported versions, missing starting cash, digest mismatches, or malformed sheets
rather than invent values.

Legacy sheets have no derivable mechanical baseline and receive no automatic
balances.

## Advancement Surface

### Attributes And Special Attributes

Support permanent advancement of:

- Physical attributes.
- Mental attributes.
- Edge.
- Magic where applicable.
- Resonance where applicable.

The server validates the current value, new-rating cost, metatype ranges, natural
maximum, Exceptional Attribute interaction, Essence-derived restrictions,
Initiation or Submersion maximum changes, path eligibility, and sufficient Karma.
Magic, Resonance, and Edge use dedicated typed operations even where they share a
cost formula with normal attributes.

Every affected derived value is recomputed server-side. Creation-oriented point
budgets and creation caps must not be applied to career advancement.

### Skills, Groups, And Specializations

Support:

- Learning and raising active skills.
- Learning and raising Knowledge skills.
- Learning and raising non-native languages.
- Adding legal specializations.
- Raising skill groups.
- Breaking and rebuilding groups according to the approved rules.

The career model must represent group integrity explicitly. Raising or specializing
a member skill must not silently preserve a group benefit when the rules say the
group has been broken. Rebuilding must validate member ratings and any other
approved conditions.

The server validates default maximums, Aptitude-adjusted maximums, group membership,
parameterized skills, existing specializations, native-language restrictions, and
sufficient Karma.

### Qualities

Support:

- Acquiring career-eligible positive qualities.
- Removing eligible negative qualities.
- Required parameters and repeatable selections.
- Rating behavior where the approved catalog supports it.

The server validates creation-only restrictions, conflicts, prerequisites,
repeatability, parameters, current selections, and cost. Storyteller approval and
narrative circumstances are intentionally omitted by the fixed product contract,
but objective mechanical requirements are not.

The current creation catalog does not encode every career-quality fact. This surface
cannot ship until SHEET-901 expands and validates the necessary typed catalog data.

### Magic And Resonance

Support all career operations applicable to the character's path that are present in
the approved core rules, including:

- Learning spells and complex forms.
- Initiation and Submersion.
- Raising Magic and Resonance.
- Applicable adept progression.
- Required path-specific selections and prerequisites.

SHEET-901 must explicitly decide and document rituals, preparations, Power Points,
metamagics, echoes, and every other path-specific operation before implementation.
Creation grant evaluators must not be reused as career rules merely because they
work with similar catalog records.

### Remaining Career Operations

SHEET-901 must inventory and explicitly include, defer, or reject every remaining
core advancement category, including contacts and reputation interactions. Lifetime
Karma earned must remain separate from spendable Karma because Street Cred depends
on lifetime earnings under core pp. 372-373.

SHEET-901 resolved contacts as **included**: a player may add a new contact in
career at zero Karma cost (starting at Connection 1 / Loyalty 1), since the
approved core rules define no purchase price for one. This is a product-shape
decision, not RAW, and is recorded pending a future Storyteller-approval gate
(see Deferred Beyond Milestone 9). Raising an existing contact's Connection or
Loyalty rating has no RAW formula and remains excluded from this milestone.
See `SHEET_901_CAREER_RULES_BASELINE.md` Section 5 for the full citation and
reasoning. Reputation (Street Cred/Notoriety/Public Awareness) remains
deferred as stated below; only the lifetime-Karma basis for Street Cred is
tracked in this milestone.

## Nuyen Purchases

### Initial Eligible Surface

The initial direct purchase evaluator may expose fully priced ordinary entries from:

- General gear.
- Weapons.
- Armor.
- Vehicles and drones.
- Cyberdecks.
- Other catalog collections proven to require no installation, host mutation,
  Capacity allocation, bonding, or missing mechanical data.

For each candidate the server resolves:

- Catalog identity and classification.
- Quantity, rating, and required parameters.
- Fixed, per-rating, or by-rating price.
- Current balance and resulting balance.
- Informational Availability and legality.
- Whether the entry is complete and eligible for this purchase surface.

Ignoring Availability does not make incomplete or incompatible entries purchasable.
Items with no definitive server-resolvable price remain unavailable with a bounded
reason.

### Deferred Purchase Behavior

The following require dedicated later support and must not be represented as simple
cash-only purchases:

- Augmentation acquisition and installation, including grade, Essence, and
  Magic/Resonance effects.
- Weapon accessories and mount occupancy.
- Armor modifications and Capacity.
- Device, cyberlimb, and vehicle attachments or modifications.
- Focus Force, purchase, and bonding.
- Ammunition consumption, equipped state, item damage, bricking, and repair.
- Selling, refunds, transfer, theft, loss, and deletion.
- Lifestyle recurring charges.

## Application And API Contract

Application owns purpose-specific commands and queries. Do not add a generic sheet
patch command or expose the persistence document.

The owner-scoped HTTP surface should include:

- `GET /api/characters/{characterId}/career-sheet`
- `GET /api/characters/{characterId}/career-sheet/history` with bounded cursor
  pagination if the initial response does not include a short recent window.
- `POST /api/characters/{characterId}/advancements/attributes`
- `POST /api/characters/{characterId}/advancements/skills`
- `POST /api/characters/{characterId}/advancements/specializations`
- `POST /api/characters/{characterId}/advancements/qualities`
- `POST /api/characters/{characterId}/advancements/magic-resonance`
- `POST /api/characters/{characterId}/purchases`

All mutations require antiforgery. Non-owned IDs use the project's existing
non-enumerating not-found behavior. Stale versions return conflict. Structurally
valid but mechanically illegal requests return structured field-level diagnostics.
Insufficient funds return a specific bounded result without exposing persistence
details.

The composed sheet response is typed and includes:

- Character, ruleset, catalog, and career-state identity.
- Current Karma, current nuyen, and lifetime Karma earned.
- Current permanent attributes and special attributes.
- Current qualities, skills, groups, Knowledge skills, and languages.
- Current Magic or Resonance selections where applicable.
- Creation and acquired inventory.
- Contacts, identities, and lifestyles.
- Current permanent derived statistics.
- A bounded recent advancement and transaction history.
- Server-derived next actions with cost, eligibility, and blocking diagnostics.

The frontend must not calculate advancement costs, maximums, current ratings,
purchase prices, or resulting balances.

## Character Sheet Experience

Use the existing neon-noir design system. The sheet should read as a live runner
dossier rather than reopening the creation wizard.

### Character Selection

Each finalized slot exposes:

1. **Enter World** as the primary gameplay action.
2. **View Character Sheet** as a secondary route link.

Viewing the sheet must not start a play session, change the selected character,
move the character, or connect SignalR.

### Shared Sheet Components

Use one shared feature surface, with responsibilities equivalent to:

- `CharacterSheetView`: data loading, stale-response protection, errors, retries,
  version handling, and mutation refreshes.
- `CharacterSheetContent`: semantic section rendering and advancement/purchase
  controls.
- `CharacterSheetPage`: owner route wrapper.
- `CharacterSheetModal`: gameplay dialog wrapper around the same view.

Do not maintain separate route and modal sheet implementations.

Suggested sections are:

- Overview and balances.
- Attributes.
- Skills and languages.
- Qualities.
- Magic or Resonance.
- Inventory.
- Contacts, identities, and lifestyles.
- Advancement history.
- Karma and nuyen ledger.

The header keeps current Karma, current nuyen, and save/mutation state visible.
Advanceable rows show the current value, proposed next value, exact cost, and
server-provided blocking reason. Every expenditure requires an explicit
confirmation showing the cost and resulting balance.

Catalog purchasing includes accessible search, category filters, rating and
parameter controls, price, informational Availability and legality, current nuyen,
and a purchase confirmation. The server response remains authoritative if data
changes between preview and confirmation.

### Gameplay Command And Modal

Add the local-only command:

```text
/character
```

It accepts no arguments and opens the current play-session character's sheet. It
does not create a transcript entry, invoke chat, navigate, refresh the room, or
require a joined SignalR connection after the authoritative room session has loaded.
`/character anything` returns a local usage error.

`GameplayPage` remains mounted while the modal is open so the play session,
transcript, presence, and SignalR connection remain intact. Closing restores focus
to the composer. Successful advancement or purchasing refreshes the shared sheet
state without refreshing the room.

The modal must support a wide desktop presentation and an edge-to-edge mobile
presentation. Update the shared primitive as needed to:

- Include links in focus trapping.
- Lock background scrolling and interaction.
- Preserve Escape and backdrop close.
- Restore the invoking element's focus.
- Keep a sticky header and independently scrollable content.
- Retain minimum 44-pixel touch targets.

Avoid nested dialogs for expenditure confirmation. Prefer an inline confirmation
state within the sheet or replace the sheet body temporarily with a confirmation
panel.

## SHEET-901: Freeze Career Rules And Catalog Contract

**Depends on:** CHAR-812 and the accepted product contract above.

**Scope:**

- Inventory every core post-creation advancement and its cost, maximum,
  prerequisite, conflict, and resulting effect.
- Cite the approved PDFs for every implemented mechanic.
- Record the no-downtime, immediate-player-approval, and ignored-Availability
  interpretations in `PROJECT_CONTEXT.md`.
- Identify every creation-only quality and option.
- Define skill-group breaking and rebuilding behavior.
- Define Edge, Magic, Resonance, Initiation, Submersion, adept, spell, ritual,
  preparation, complex-form, contact, and reputation advancement behavior.
- Expand the typed catalog only where career evaluation needs facts that are not
  already represented.
- Identify creation formulas that are genuinely rules-neutral and may be extracted
  behind shared pure helpers without changing creation behavior.
- Identify creation evaluators, budgets, diagnostics, and persistence behavior that
  must remain creation-only.
- Produce an explicit purchase eligibility ledger for every catalog collection and
  identify entries with incomplete price or career data.
- Define supported sheet-schema and legacy behavior.

**Acceptance criteria:**

- Every career operation is explicitly included, deferred, or rejected.
- Every included cost and legality rule cites an approved PDF page.
- Missing or ambiguous rules have approved product decisions before evaluator work.
- No creation cap, budget, or grant behavior is silently reused as a career rule.
- Shared rule helpers have creation regression tests and contain no draft,
  finalization, or career transaction orchestration.
- The catalog loader rejects invalid career metadata and digest changes cover every
  new semantic fact.
- The purchase ledger proves that every exposed item has a deterministic price and
  supported parameters.

## SHEET-902: Add Typed Creation-Baseline Reading

**Depends on:** SHEET-901.

**Scope:**

- Add explicit readers for evaluated sheet schema versions 1, 2, and 3 and for
  legacy sheet kind.
- Normalize supported evaluated sheets into one typed career baseline.
- Validate ruleset, catalog version, and semantic digest during normalization.
- Add retained fixtures for every supported historical shape.
- Preserve creator identity and profile fields through finalization if CHAR-812 has
  not already corrected their current loss.

**Acceptance criteria:**

- Supported evaluated sheets normalize deterministically.
- Legacy sheets render as legacy without invented statistics or balances.
- Unknown kind/version combinations, malformed JSON, missing required values, and
  digest mismatches fail safely.
- Historical fixtures prevent latest-record changes from silently redefining old
  sheets.
- The existing immutable sheet row remains unchanged by every read.

## SHEET-903: Persist Career State, Balances, And History

**Depends on:** SHEET-902.

**Scope:**

- Add career state, resource transactions, advancement history, acquired inventory,
  and idempotent action receipts with explicit EF mappings and a migration.
- Add a deterministic backfill for supported evaluated sheets.
- Create career state and opening transactions atomically during new finalization.
- Add Application persistence boundaries; do not add a generic repository.
- Enforce nonnegative balances, optimistic concurrency, bounded details, and
  restrictive foreign keys.

**Acceptance criteria:**

- Opening Karma and nuyen exactly match finalized carryover and starting-cash
  values and are created once.
- Starting cash is never rerolled.
- Backfill is idempotent and reports unsupported rows without partial initialization.
- Legacy sheets receive no invented mechanical state.
- Career state and history cannot outlive their character.
- Concurrent initialization cannot create duplicate state or opening transactions.
- No operation mutates `character_sheets.canonical_sheet`.

## SHEET-904: Expose The Composed Career Sheet

**Depends on:** SHEET-903.

**Scope:**

- Add an owner-scoped composed-sheet query and typed HTTP response.
- Resolve canonical IDs against the pinned catalog.
- Reuse the retained catalog provider and semantic-digest validation rather than
  introducing a separate career catalog cache or lookup implementation.
- Calculate current permanent values and derived statistics.
- Return current balances, bounded recent history, and server-derived advancement
  eligibility.
- Add cursor-paginated history if histories are separate from the main response.

**Acceptance criteria:**

- Owners can read their evaluated sheets; other users receive non-enumerating
  not-found responses.
- The response contains no raw mutable persistence document.
- Catalog display names and mechanics come from the pinned digest.
- Eligibility includes exact cost and bounded blocking diagnostics.
- Legacy and unsupported sheets return an intentional readable state.
- Queries do not initialize or mutate already initialized career state.

## SHEET-905: Build The Routed Character Sheet

**Depends on:** SHEET-904.

**Scope:**

- Add typed frontend contracts and a shared character-sheet feature surface.
- Reuse catalog loading, indexing, descriptions, resource normalization, and UI
  primitives from character creation where their contracts are workflow-neutral.
- Extract small pure display sections from creation review only when doing so keeps
  both callers free of creation/career mode branches.
- Add `/characters/:characterId/sheet` as an authenticated lazy route.
- Add **Enter World** and **View Character Sheet** to finalized slot cards.
- Render every composed-sheet section read-only before enabling expenditure
  controls.
- Handle loading, retry, missing, stale response, legacy, and unsupported states.

**Acceptance criteria:**

- Viewing a sheet does not start or alter a play session.
- Direct navigation and browser refresh work for the owner.
- All catalog-backed values render readable names with safe fallbacks.
- The finalized sheet does not mount `CreatorShellPage`, create or update a draft,
  invoke autosave, or expose finalization behavior.
- Shared components contain presentation only; career eligibility and costs come
  from the composed-sheet response rather than frontend creation calculations.
- Desktop and mobile layouts remain usable without a horizontal table being the
  only representation.
- Slot and route tests prove ownership remains server-authoritative.

## SHEET-906: Implement Attribute Advancement

**Depends on:** SHEET-904 and the attribute contract from SHEET-901.

**Scope:**

- Add commands and endpoints for normal attributes, Edge, Magic, and Resonance.
- Calculate cost and all maximums server-side.
- Recompute affected derived values.
- Add idempotency, concurrency, resource history, and UI confirmation.

**Acceptance criteria:**

- Legal advancement charges the exact Karma once and rotates the state version.
- Insufficient Karma, stale versions, maximum violations, forged costs, and
  inapplicable special attributes are rejected without mutation.
- Exceptional Attribute, Essence, Initiation, Submersion, and path restrictions are
  covered where applicable.
- Concurrent requests cannot overspend.

## SHEET-907: Implement Skill And Group Advancement

**Depends on:** SHEET-906 only where shared progression infrastructure is reused;
rules depend on SHEET-901.

**Scope:**

- Learn and raise active, Knowledge, and language skills.
- Add specializations.
- Raise, break, and rebuild skill groups.
- Validate parameters, native languages, Aptitude, maximums, and group state.
- Add exact previews, confirmations, history, and focused UI controls.

**Acceptance criteria:**

- Every marginal cost is calculated from the authoritative current rating.
- Group/member overlap cannot double-count ratings or preserve an invalid group.
- Native languages and specializations obey their approved semantics.
- Retried or concurrent commands cannot duplicate charges or selections.

## SHEET-908: Implement Quality Advancement

**Depends on:** SHEET-901 and SHEET-907 where quality effects reference skills.

**Scope:**

- Acquire career-eligible positive qualities and remove eligible negative qualities.
- Add required career metadata to the catalog and loader.
- Validate parameters, conflicts, prerequisites, repeatability, ratings, and
  creation-only restrictions.
- Recompute affected values and eligibility.

**Acceptance criteria:**

- Costs use the approved career multiplier rather than creation cost directly.
- Creation-only and mechanically ineligible qualities remain unavailable.
- Removing or adding a quality cannot leave an illegal composed state.
- Immediate player application is documented as the approved interpretation.

## SHEET-909: Implement Magic And Resonance Advancement

**Depends on:** SHEET-901, SHEET-906, and relevant quality/skill behavior.

**Scope:**

- Implement the approved path-specific career operations.
- Support spells, complex forms, Initiation, Submersion, Magic, Resonance, and adept
  progression as defined by the frozen contract.
- Add required typed catalog facts, selections, prerequisites, and derived effects.

**Acceptance criteria:**

- Mundane and inapplicable paths cannot submit magical or Resonance advancement.
- Every path-specific selection remains within its approved prerequisites and caps.
- Grade, attribute maximum, and acquired-option changes compose deterministically.
- No creation grant is reapplied during career advancement.

## SHEET-910: Implement Catalog Purchases And Inventory

**Depends on:** SHEET-903, SHEET-904, and the purchase ledger from SHEET-901.

**Scope:**

- Add the eligible catalog query and purchase command.
- Resolve quantity, rating, parameters, and price server-side.
- Create acquired inventory instances and nuyen transactions atomically.
- Add searchable, filterable purchase UI with informational Availability and
  legality.
- Explicitly exclude installation, attachment, bonding, and incomplete item types.

**Acceptance criteria:**

- Clients cannot choose price, resulting balance, catalog version, or item owner.
- Insufficient funds, unsupported parameters, incomplete entries, stale versions,
  and forged IDs fail without inventory or ledger changes.
- Availability never blocks an otherwise eligible initial purchase.
- Purchases survive reload and compose with creation inventory.
- Duplicate and concurrent requests cannot duplicate an item or charge.

## SHEET-911: Add The Gameplay Sheet Modal And `/character`

**Depends on:** SHEET-905. It may run before SHEET-906 through SHEET-910 and gain
their controls automatically through the shared component.

**Scope:**

- Add `/character` to the parser, help registry, composer hints, and gameplay
  command dispatcher.
- Open the current session character in a shared wide character-sheet modal.
- Keep gameplay, room session, transcript, presence, and SignalR mounted.
- Improve the shared modal's focus trap, scrolling, sizing, and mobile behavior.

**Acceptance criteria:**

- `/character` is case-insensitive, takes no arguments, and remains local-only.
- The command works while SignalR is temporarily disconnected if the authoritative
  room session is still loaded.
- Opening and closing does not navigate, start a session, send chat, move, or refresh
  the room.
- Closing restores focus to the composer.
- Route and modal render the same authoritative sheet implementation.
- Modal accessibility tests cover focus containment, Escape, backdrop close, focus
  restoration, and mobile scrolling.

## SHEET-912: Completeness, Accessibility, And Release Gate

**Depends on:** SHEET-902 through SHEET-911.

**Scope:**

- Reconcile every included advancement and purchasable catalog entry against the
  frozen contract.
- Verify schema upgrades, backfill, concurrency, idempotency, and ownership using
  real PostgreSQL integration tests.
- Complete responsive and accessibility review for route, modal, confirmations,
  catalog search, histories, and diagnostics.
- Update `README.md`, `PROJECT_CONTEXT.md`, and the root roadmap.
- Run all backend and frontend checks.

**Acceptance criteria:**

- Every included advancement is available from the composed sheet and has approved
  citations and focused rule tests.
- Players can never directly set a balance, cost, price, rating, or resulting value.
- Every successful expenditure creates exactly one permanent change and one matching
  resource transaction.
- Retried, stale, concurrent, forged, and unauthorized requests cannot overspend or
  duplicate state.
- The immutable creation baseline is byte-for-byte unchanged by advancement and
  purchasing.
- Route and modal remain usable with keyboard, touch, narrow viewports, reduced
  motion, loading failures, and validation failures.
- Legacy sheets remain readable and mechanically blocked without invented data.
- Full backend tests, frontend tests, lint, and production build pass.

## Recommended Delivery Sequence

1. Complete SHEET-901 before changing persistence or adding mutable controls.
2. Complete SHEET-902 through SHEET-905 to ship a useful read-only owner sheet.
3. Complete SHEET-906 and SHEET-907 for the first broad Karma-spending release.
4. Complete SHEET-911 as soon as SHEET-905 is stable so `/character` is available;
   later controls appear through the shared view.
5. Complete SHEET-908 and SHEET-909 after their expanded career catalogs pass
   semantic validation.
6. Complete SHEET-910 independently of SHEET-908 and SHEET-909 once the purchase
   eligibility ledger is frozen.
7. Complete SHEET-912 before declaring career sheets released.

## Deferred Beyond Milestone 9

- Storyteller approval queues and training-time scheduling. **When a
  Storyteller-approval-gate feature is built, add zero-Karma contact
  acquisition (Section "Remaining Career Operations" above) to its gated-action
  list** — Milestone 9 allows it without approval only because no such gate
  exists yet.
- Karma and nuyen award administration; this milestone preserves ledger support for
  those later sources but implements player expenditures first.
- Runtime current Edge, damage, initiative, temporary modifiers, and effects.
- Equipment loadouts, ammunition, consumption, damage, bricking, and repair.
- Augmentation installation and post-creation Essence changes.
- Attachment, mount, Capacity, and focus-bonding workflows.
- Selling, refunds, trading, theft, transfer, loss, and item deletion.
- Lifestyle billing and other recurring expenses.
- Availability and acquisition tests.
- Street Cred, Notoriety, and Public Awareness mutation workflows beyond preserving
  the required lifetime-Karma basis.
