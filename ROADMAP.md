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

Continue the accepted character-creation feature with **CHAR-812:
Completeness, Accessibility, And Release Gate** in the Milestone 8 file.
CHAR-801 through CHAR-811 are complete.

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

CHAR-811 (Final Review And Atomic Finalization) and CHAR-812 (release gate)
have not been started.
