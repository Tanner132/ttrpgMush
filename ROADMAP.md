# Seattle by Night Feature Roadmap

Realtime presence and consistency cleanup is complete. The remaining accepted
features are split into milestone files so an implementation agent only needs to
load the work it is executing. Shadowrun character creation is now an accepted
multi-slice milestone with a separately versioned rules contract.

## Milestones

| Milestone | File | Outcome |
| --- | --- | --- |
| 2 | [`roadmap/MILESTONE_02_FRONTEND_ROUTING.md`](roadmap/MILESTONE_02_FRONTEND_ROUTING.md) | Routed pages, shared components, and extracted gameplay hooks |
| 2B | [`roadmap/MILESTONE_02B_VISUAL_FOUNDATION.md`](roadmap/MILESTONE_02B_VISUAL_FOUNDATION.md) | Accessible retro-future neon-noir design system and gameplay cockpit |
| 3 | [`roadmap/MILESTONE_03_CORE_COMMANDS.md`](roadmap/MILESTONE_03_CORE_COMMANDS.md) | `/help`, `/who`, `/look`, `/say`, and `/go` |
| 4 | [`roadmap/MILESTONE_04_TYPED_MESSAGES_AND_DICE.md`](roadmap/MILESTONE_04_TYPED_MESSAGES_AND_DICE.md) | Typed speech, `/emote`, and server-authoritative dice |
| 5 | [`roadmap/MILESTONE_05_ADMIN_AUTHORIZATION.md`](roadmap/MILESTONE_05_ADMIN_AUTHORIZATION.md) | Roles, policies, and administrative auditing |
| 6 | [`roadmap/MILESTONE_06_ROOM_EDITOR.md`](roadmap/MILESTONE_06_ROOM_EDITOR.md) | Protected coordinate-and-exit editor without deletion |
| 7 | [`roadmap/MILESTONE_07_CHARACTER_PROFILES.md`](roadmap/MILESTONE_07_CHARACTER_PROFILES.md) | Lightweight, non-mechanical character profiles |
| 8 | [`roadmap/MILESTONE_08_SR5_CHARACTER_CREATION.md`](roadmap/MILESTONE_08_SR5_CHARACTER_CREATION.md) | Core SR5 Standard Priority and Sum-to-Ten character creation |
| 9 | [`roadmap/MILESTONE_09_CAREER_CHARACTER_SHEETS.md`](roadmap/MILESTONE_09_CAREER_CHARACTER_SHEETS.md) | Owner-visible career sheets, advancement, and catalog purchases |

## Delivery Rules

- Complete milestones in order unless a ticket explicitly says it can run in parallel.
- Keep the backend dependency direction documented in `PROJECT_CONTEXT.md`.
- Keep HTTP endpoints and SignalR hubs thin; gameplay and authorization decisions belong in Application.
- Treat command text, identifiers, dice expressions, profile fields, and editor payloads as untrusted input.
- Persist mutations before broadcasting them or reporting success.
- Do not add room or exit deletion endpoints, application commands, or UI controls.
- Each direction remains a separate `RoomExit`. Room creation may seed both
  directed paths for occupied same-layer neighbors; manual exit creation never
  silently creates a reverse path.
- Update `README.md` and `PROJECT_CONTEXT.md` when a milestone changes the implemented surface or an architectural decision is accepted.
- A ticket is complete only when its focused tests and the relevant build/lint checks pass.

Recommended checks:

```powershell
dotnet test backEnd/SeattleByNight.slnx
npm --prefix frontEnd run test -- --run
npm --prefix frontEnd run lint
npm --prefix frontEnd run build
```

## Ruleset Catalog Operations

The SR5 catalog (`backEnd/src/SeattleByNight.Application/CharacterCreation/Catalog/`)
is versioned and pinned, not freely mutable. This section is the operational
reference for its hashes and rollback procedure, written as part of CHAR-812's
release-gate documentation requirement.

- `EmbeddedRulesetCatalogProvider.CurrentRulesetId`/`CurrentVersion`/
  `CurrentSemanticDigest` are the single source of truth for which catalog
  version is live. Never hardcode a digest value in prose elsewhere (this
  roadmap and `SR5_CATALOG_LEDGER.md` deliberately don't) — read it from the
  provider.
- `RetainedVersions` is an append-only lockfile of every published version
  (`1.0.0` through the current version). A released version's resource file
  and pinned digest are never edited after release; new content always
  becomes a new resource file (e.g. `sr5-core-1.4.0.json`) plus a new pin.
- Every overlay's `BaseResourceName` points at the standalone `sr5-core-1.0.0.json`
  base, not at the previous overlay — `LoadOverlay` reads the base as raw
  bytes rather than resolving its own overlay chain, so each new overlay must
  carry forward, in full, any additive content from every earlier overlay it
  still needs (this is why `1.4.0.json` republishes `1.1.0`'s
  Knowledge/Language suggestions, `1.2.0`'s metavariants, and `1.3.0`'s Run
  Faster qualities alongside its own new content).
- **Computing a new digest**: pin a placeholder digest string on the new
  `CatalogVersionPin`, run `dotnet test --filter FullyQualifiedName~RulesetCatalogLoaderTests`,
  and read the real SHA-256 digest out of the resulting
  `RulesetCatalogException` message (`"Catalog semantic digest mismatch.
  Expected <placeholder>, calculated <real digest>"`). Never guess a digest.
- **Rollback / disable procedure**: revert `EmbeddedRulesetCatalogProvider.CurrentVersion`
  (and `CurrentSemanticDigest`) to the prior pinned version and redeploy. No
  data migration is needed — a player's draft or finalized character records
  the catalog version it was created against, and `RulesetCatalogLoader`
  already rejects a sheet whose pinned version/digest no longer resolves
  (`RulesetCatalogException`, confirmed in practice during CHAR-814 when a
  stale local draft correctly failed this check). Because `RetainedVersions`
  never removes an entry, rolling `CurrentVersion` back does not orphan any
  character created under a newer version — it simply stops offering that
  version for new drafts.

## Release Sequence

1. Release Milestone 2 as a behavior-preserving frontend foundation.
2. Release Milestone 2B as the visual foundation before adding command UI.
3. Release Milestone 3 with the core command set.
4. Complete MSG-401, then release Milestone 4 in one schema-compatible deployment.
5. Release Milestone 5 before exposing any administrative editor route.
6. Release Milestone 6 behind the world-editor policy.
7. Complete PROFILE-701, then release Milestone 7 independently of the future Shadowrun character domain.
8. Complete CHAR-801 and approve the rules baseline before implementing Milestone 8 slices.
9. Complete the Milestone 8 release gate, then freeze the career rules contract in SHEET-901 before implementing mutable character state.

## Next Build Ticket

**CHAR-813** (Run Faster Metavariants) is a project-owner-approved catalog
expansion (2026-08-25/26) started ahead of CHAR-812/Milestone 9 sequencing, the
same kind of recorded process exception as Milestone 9's early start below. Its
ledger (`roadmap/sr5-catalog/RUN_FASTER_METATYPES.md`) and all four gating
decisions are approved. Backend is complete: `RulesetCatalog`/
`RulesetCatalogLoader` gained a `Metavariants` collection (a metavariant is a
parameterized sub-choice of its parent metatype, not an independent priority
option), catalog version `sr5-core` `1.2.0` adds the 17 metavariants plus the
new "Poor Self Control (Vindictive)" quality, `MetatypeAndAttributeEvaluator`
resolves the selection (attribute-range override, priority-level-specific
special-attribute-point grant, and flat Karma surcharge from the Extended
Priority Charts), and `ResourcesEssenceEvaluator`/`LifestyleEvaluator` no
longer apply a Dwarf/Troll gear or lifestyle multiplier when a metavariant is
selected, using the metavariant's own lifestyle multiplier instead where the
book specifies one. Verified via 195 Application-layer tests (4 new).

The creator UI is also complete: the Metatype step offers an optional
metavariant picker (a card row below the parent metatype's own card,
disabled when unavailable at the assigned priority level) showing each
option's special-attribute grant and Karma cost, the dossier index and
finalized character sheet resolve the metavariant's display name in place of
the parent metatype's, and every other step/preview helper that reads
metatype attribute ranges (Attributes, Knowledge, Contacts, the header Karma
totals, gear/lifestyle cost multipliers) now resolves through a shared
`effectiveMetatypeAttributes` helper instead of the base metatype directly.
Verified via 284 frontend tests (9 new plus updated fixtures), a clean
`tsc -b` build, and a live manual walkthrough in the browser (logged in as
`devuser`, an existing Elf draft correctly rendered all four Elf metavariants
with their exact trait text and Karma costs, and correctly rejected selecting
one because that draft is pinned to a catalog version published before
CHAR-813 — confirming catalog-version immutability holds).

**CHAR-814** (Run Faster Qualities) is a project-owner-approved catalog
expansion (2026-08-26) in the same recorded-exception lane as CHAR-813. Its
ledger (`roadmap/sr5-catalog/RUN_FASTER_QUALITIES.md`) and both gating
decisions (including it and Rank, and finishing the Poor Self Control
family CHAR-813 deliberately left out) are approved. It publishes catalog
version `sr5-core` `1.3.0` (an overlay on `1.0.0` republishing `1.1.0`'s
Knowledge/Language suggestions and `1.2.0`'s metavariants, so nothing from
either intermediate overlay is lost) with 84 new quality entries: Rank, all
42 Run Faster positive qualities, all 37 Run Faster negative qualities minus
the already-published Poor Self Control heading, and its four remaining
variants (Braggart, Thrill-Seeker, Compulsive, Combat Monster) alongside the
existing Vindictive. No new evaluator logic was needed — like most
`sr5-core` qualities, their mechanical prose is descriptive rather than
code-enforced — except one new bidirectional `conflicts` link between
`erased` and `records-on-file`. The frontend's `catalogDescriptions.ts`
gained a paraphrased description for every new entry (plus the
previously-undescribed `poor-self-control-vindictive`); `QualitiesStep.tsx`
needed no changes, since it already renders generically from
`catalog.qualities`. Verified via 6 new backend tests (`RunFasterQualitiesTests`,
covering total/by-source counts, Rank, Fame's flat-step tiering, all five
Poor Self Control variants, the `erased`/`records-on-file` conflict, and the
Spike Resistance/Dimmer Bulb repeatable-rating pattern) — full suite 443
backend tests and 284 frontend tests all pass — and a live authenticated
browser check against the running app confirmed the `/catalogs/current`
endpoint now serves version `1.3.0` with 144 total qualities (85 sourced to
`run-faster`) and the expected `rank`/`fame`/conflict shapes. A pre-existing
player draft pinned to an earlier, uncommitted local catalog state failed to
evaluate (`RulesetCatalogException`, digest mismatch against its own pinned
version); this is unrelated to CHAR-814 — every currently-committed catalog
version's digest still resolves exactly as pinned per
`Retained_catalog_digests_match_the_committed_pins` — and was not
investigated further as out of scope for this ticket. Fixed in passing: a
stale `"1.1.0"` version assertion in
`CharacterCreationEndpointTests.Catalogs_require_authentication_and_return_the_pinned_contract`
that CHAR-813 left unbumped when it moved `CurrentVersion` to `1.2.0`,
which meant `SeattleByNight.Api.Tests` had been failing outright since that
change (a locked build output from a stale running dev server had been
masking this before now).

**CHAR-812 (Milestone 8's completeness/accessibility/release gate) is
complete — Milestone 8 is fully complete (CHAR-801 through CHAR-812).** Six
parallel background audits reconciled every equipment domain against the
approved core PDF; the four genuine content gaps they found (ammunition/
explosives/grenades/rockets, drugs/toxins/BTL, unpriced foci, unfindable
Autosoft pricing) were all closed per project-owner direction to implement
everything now, published as catalog version `sr5-core` `1.4.0` (see
`roadmap/SR5_CATALOG_LEDGER.md` and the new entries in
`roadmap/SR5_RULE_DECISIONS.md`). The accessibility half (keyboard/
screen-reader/reduced-motion/zoom/narrow-screen review) was completed and
live-verified earlier in the same session. Full detail in
`roadmap/MILESTONE_08_SR5_CHARACTER_CREATION.md`'s CHAR-812 section. Milestone
9 rules work had already started ahead of CHAR-812 on 2026-08-25 as a
recorded process exception (see below); that exception is now moot since
CHAR-812 has landed.

**SHEET-901** (`roadmap/SHEET_901_CAREER_RULES_BASELINE.md`) is complete and
approved: the full core Character Improvement Table (attributes, skills,
groups, specializations, qualities, spells/rituals/preparations, complex
forms, Initiation, Submersion) is cited and reconciled against the existing
CHAR-801 creation ledgers, plus new catalog material for the 9 core
metamagics and 9 core echoes (previously out of creation scope and
undocumented). All seven candidate rule decisions in `SR5_RULE_DECISIONS.md`
"Milestone 9 Career Decisions" were reviewed and resolved by the project
owner on 2026-08-25 (three overrode the original recommendation, one changed
from rejected to included, three approved as recommended) — notably, the
spell/ritual/preparation/complex-form `Magic x 2`-style caps and the mystic
adept's Karma-per-Power-Point purchase are **creation-only**, not
career-continuing, and new-contact acquisition is **included** at zero Karma
cost pending a future Storyteller-approval gate. The nuyen purchase
eligibility ledger (SHEET-910) resolves every catalog collection to
eligible/excluded but still needs a mechanical per-SKU audit pass before
implementation, per that document's Section 6.4.

**SHEET-902 through SHEET-906 are complete**, carrying Milestone 9 through its
first Karma-spending release (Recommended Delivery Sequence items 1-3). In
order: SHEET-902 added `CharacterCreationBaselineReader`, a typed reader that
normalizes the current evaluated sheet schema version into a
`CharacterCreationBaseline` and rejects unsupported/malformed/digest-mismatched
sheets deterministically (legacy sheet support was dropped rather than built,
per the 2026-08-25 project-owner scope note also recorded in
`SHEET_901_CAREER_RULES_BASELINE.md` §7). SHEET-903 added the mutable career
layer: `CharacterCareerState` (current Karma/nuyen, lifetime Karma earned, a
JSONB `CareerProgressionDocument`), append-only `CharacterResourceTransaction`/
`CharacterAdvancement` history, `CharacterInventoryItem`, and
`CharacterActionReceipt` for request-id idempotency, plus an idempotent
backfill for already-finalized characters and atomic opening-balance seeding
(`CarryoverKarma`/`CarryoverNuyen` + starting cash) — verified with real
PostgreSQL integration tests (Testcontainers), never invented values for
malformed/legacy rows. SHEET-904 added the owner-scoped composed-sheet query
(`GET /api/characters/{characterId}/career-sheet`), which overlays progression
onto the baseline without ever mutating or reinitializing career state on
read. SHEET-905 added the routed, read-only `/characters/:characterId/sheet`
frontend page and a "View Character Sheet" link on finalized character slots,
reusing character-creation catalog indexing/description helpers while never
mounting the creator or touching draft/autosave/finalization state. SHEET-906
added the milestone's first mutation — `POST
/api/characters/{characterId}/advancements/attributes` raises one Physical/
Mental attribute, Edge, Magic, or Resonance by exactly one rating per request
at `new rating x 5` Karma, capped at each attribute's natural maximum (+1 for
`exceptional-attribute`; Edge also +1 for `lucky`; Magic/Resonance flat 6/7
since Initiation isn't implemented yet) — version-checked via an EF
concurrency token and idempotent via the SHEET-903 action-receipt table, with
derived statistics recomputed on every read (never persisted) through
formulas now shared with creation's `DerivedStatisticsEvaluator`. The routed
sheet's Attributes tab is now interactive (current value, cost, inline
non-modal spend confirmation); every other section remains the SHEET-905
read-only presentation. Verified via 472 backend tests (up from 443) including
new PostgreSQL concurrency/idempotency coverage, 287 frontend tests (up from
284), and both a `dotnet build`/`tsc -b` clean build.

**Next**: SHEET-907 (Skill And Group Advancement), completing the milestone's
"first broad Karma-spending release" alongside SHEET-906, per the milestone
file's Recommended Delivery Sequence item 3. SHEET-911 (`/character` command
and gameplay modal) can also start now that SHEET-905 is stable, per sequence
item 4.

CHAR-811 (Final Review And Atomic Finalization) is **complete**: finalization
now genuinely requires a complete character. Previously, Metatype/Attributes,
Skills/Knowledge (including the one-required-free-native-language rule), Magic
or Resonance, and Lifestyle were only validated when their document field
happened to be non-null, so a draft could reach `isReadyToFinalize: true`
having never touched most of the sheet; each of those evaluators now always
runs once the priority assignment is resolved, closing that gap (Contacts and
Identities/Licenses remain deliberately optional, per CHAR-810). A new
`DerivedStatisticsEvaluator` computes the sr5-core p. 101 final-calculations
block — Essence, Physical/Mental/Social Limits, Initiative (base + 1D6),
Physical/Stun Condition Monitor boxes and Overflow, and Karma/nuyen
carryover (capped at 7/5,000) — deterministically on every preview, not just
at finalize, and exposes it through the draft response. The new Review &
Finalize creator step shows this final-calculations block alongside every
diagnostic in the draft; the underlying Finalize action, atomic
draft-to-sheet commit, optimistic-concurrency conflict handling, and
New-Character-Room routing already existed from earlier tickets and needed no
changes. Both Standard Priority and Sum-to-Ten end-to-end flows are verified
to finalize successfully. There is no separate "Karma & Finishing" step: the
creator header already carries a running Karma total, so a dedicated summary
step added nothing. Instead, Knowledge/Language points beyond the free pool
(previously a hard block) now draw extra Karma directly, per the sr5-core
p. 107 Karma Advancement Table (a rank's marginal cost is its own rank
number; a specialization beyond the pool costs a flat 7), folded into the
same shared creation Karma pool as contacts' free-Karma overflow. Verified
via 384 backend tests (15 new) and 233 frontend tests (8 new).

CHAR-809 is substantially complete: weapons, armor, and augmentations are
substantially cataloged; general gear, electronics/software, and magical
supplies (reagents and lodge materials) are populated using the existing gear
catalog schema; vehicles/drones cover the full core
groundcraft/watercraft/aircraft/drone tables; and cyberdecks are a typed
catalog category with their own evaluator wiring. Street samurai, decker,
rigger, and magical-equipment golden builds all pass. Still open before
CHAR-809 can be called fully reconciled: a full CHAR-812-style reconciliation
pass (the current catalog is "substantially populated," not exhaustively
cross-checked line-by-line against the core PDF), spell-formula-to-known-spell
linkage, and focus formulae.

CHAR-809A (Gear Capacity, Mounts, And Attachments) is **complete**: firearm
mounts, armor Capacity, device Capacity, augmentation/cyberlimb Capacity, and
vehicle weapon mounts are all implemented. Draft resource selections carry a
stable per-line instance ID; an independent `GearAttachmentEvaluator`
(deliberately separate from `ResourcesEssenceEvaluator`) enforces mount slots
(17 cataloged accessories; top/barrel/underbarrel by weapon category, with
integral/no-mount accessories), armor Capacity pools (7 cataloged
modifications), device Capacity (optical/audio/sensor hosts bought at a
chosen Rating-as-Capacity, with vision/audio/sensor enhancements consuming
it), augmentation/cyberlimb Capacity (cybereyes/cyberears/cyberlimbs; 3
cataloged cyberlimb enhancements capped at one per type per limb; bracketed-
Capacity-cost bodyware/cyberguns install in a cyberlimb instead of costing
Essence), and vehicle weapon mounts (4 cataloged modifications; `floor(Body /
3)` mount slots; heavy mounts cost 2 slots and are creation-unavailable;
Manual Operation requires an existing mount). The creator UI shows
attachments as sub-items under their host, with a modal (opened from a small
"+" control on the host line) that lists remaining Capacity/mount slots and
available options, across all five host kinds (weapon, armor, gear,
augmentation, vehicle). Verified via 241 Domain/Application/Api backend tests,
210 frontend tests, and live manual exercise of every attachment flow through
the creator UI. Cyberdeck program slots remain a separately tracked gap
outside CHAR-809A's written scope — not implemented here.

CHAR-810 (Contacts, Identities, and Lifestyles) is **complete**: free-form
contacts (Connection/Loyalty, priced against a Charisma x3 free Karma pool
with overflow drawn from the general creation Karma pool), fake SINs and
linked licenses (rating-scaled `catalog.gear` purchases sharing the Resources
nuyen budget with gear/attachments), and all six core lifestyle tiers
(payment forms of standard/permanent/team, lifestyle options, one required
primary, metatype cost multipliers) are implemented via three new evaluators
(`ContactEvaluator`, `IdentityEvaluator`, `LifestyleEvaluator`) chained into
the existing Resources/GearAttachment budget pipeline. Starting cash is
rolled once, server-side, only at finalize (`CanonicalStartingCash`), staying
out of the deterministic preview evaluators. The Contacts and Lifestyle
creator steps are new; fake SIN/license purchases are folded into the
existing Resources & Vehicles step rather than a dedicated one, since both
draw from the same catalog.gear/nuyen mechanism. Verified via 147
Application-layer backend tests (25 new) and 225 frontend tests (15 new), and
live manual exercise of contacts, fake SIN + license linkage, and lifestyle
selection through the creator UI.
