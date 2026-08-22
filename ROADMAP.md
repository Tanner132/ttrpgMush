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

## Next Build Ticket

Continue the accepted character-creation feature with **CHAR-810: Implement
Contacts, Identities, And Lifestyles** in the Milestone 8 file. CHAR-801
through CHAR-809A are complete.

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

CHAR-810 (Contacts, Identities, and Lifestyles) through CHAR-812 (release
gate) have not been started.
