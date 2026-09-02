# Milestone 7 — World Forge Implementation Review

Review of the uncommitted Milestone 7 working tree against
`GAME_ENGINE_MILESTONE_7_WORLD_BUILDER.MD`. Status at review time: backend
build clean, 876 backend + 496 frontend tests passing.

**Verdict:** the architecture is right and the pipeline core is genuinely
solid — one shared validation path (loader = publish gate = delete gate),
atomic document swaps, audit-in-transaction writes, drafts invisible to
players, retirement layered as store metadata. But the milestone's own
Definition of Done is not yet met: **there is no encounter editor**, so the
acceptance scenario ("author a second complete mission … a new instanced
encounter with rooms … a retrievable mission item") cannot be executed
through the UI; and the publish gate validates references but **not effect
targets**, which the spec explicitly promises — one shipped trigger is
already silently broken because of it.

Findings are numbered by priority. CONFIRMED = the failing path was traced;
SUSPECTED = mechanism identified, not fully traced.

---

## Resolution (2026-09-02)

Every P1 and P2 finding is fixed and covered by a test; the P3 advisories are
fixed except the ones that are scale trades, which are recorded in the
milestone doc instead. Backend 895 passing, frontend 505.

| # | What changed |
|---|---|
| 1 | **Encounter editor built** (`EncounterEditor.tsx`) — rooms, exits, items, interactables, entry room, inline reachability. Renaming a room key carries every reference, including the NPC placements and triggers this screen does not own. `Encounter` joins `EditableKinds`, so every content kind now reaches a real screen. |
| 2 | `ValidateEffect` gained an NPC-target arm covering `alertNpc`/`pacifyNpc`/`startCombat`: an unnamed NPC is refused outside an NPC-bound scene, a named one must be declared, and a room-scoped effect must name someone in the room the trigger fires in. `giveItem` inside a scene no encounter can open is refused; an unguarded `turnInMission` choice is refused; opposed tests are refused in a trigger and on an unbound scene's choice. |
| 3 | **Decided: `alertNpc` resolves encounter-wide, `pacifyNpc`/`startCombat` stay room-scoped.** An alarm carries through a building; a fight cannot start through a wall, so a cross-room alert is a direct awareness flip rather than a queued combat reaction. The shipped ledger trigger now works as written, and its failure branch has a playthrough test. |
| 4 | `TriggerEngine` throws on an opposed `runTest` instead of rolling it unopposed, and the publish gate refuses it first. |
| 5 | `CheckRoomDeletionAsync` counts chat messages and room visits; `DeleteRoomAsync` deletes them in the same transaction and names the counts in the audit record. A relocated character gets an open visit where they now stand. |
| 6 | NPC speech is buffered with narration and sent after the commit, so authored order is preserved and a lost fire-once race leaves nobody having spoken. |
| 7 | `SceneEffectResolver` threads a running damage tally, so two `dealDamage` effects stack. |
| 8 | The trigger audit envelope carries every roll; the row columns describe the first (the one that chose the branch). |
| 9 | Every raised event stamps the room it happened in, and the trigger engine drops an event whose subject has already walked away. |
| 10 | `SceneEffectResolver.EventsFor` is the one derivation of "what did these effects do", shared by the scene and trigger engines; the admin assign endpoint raises `MissionAccepted` onto the character's room. |
| 11 | `FindOfferableScene` gates trigger `openScene` and branch scenes on retirement. |
| 12 | `GameCommandQueue` logs the action id, request id, scope and user on every engine fault. |
| 13 | `useContentDraft` resets when its kind changes. |
| 14 | Index keys on every in-place-edited id field (scene nodes and choices, mission objectives, pool terms, placements, triggers). |
| 15 | Renaming a scene node rewrites `startNodeId` and every `nextNodeId`. |
| 16 | Placements can be removed; the save row moved off the selection so removing the last one does not take it with them. |
| 17 | Room deletion runs inside `mutate` — busy state, no double-fire, and the server's refusal in the same banner as any other error. |
| 18 | Every editor has a Validate button. |

P3: picker fallbacks removed (a mission trigger narrows to its own encounter);
reference caches key on each definition's `updatedAtUtc`; draft delete takes two
clicks; the scene-NPC placeholder only appears where a scene NPC exists; a
trigger `openScene` no longer clobbers a conversation in progress; `NumberField`
no longer emits `NaN`; retired-key sets match each kind's own comparer;
`StampVersion` uses publish time; the module rail is a real tablist and
selection rows carry `aria-current`; failed checks say so instead of saying
"Checking…" forever; and `GameCommandQueue` reclaims idle scopes, which had
grown per encounter instance forever.

Blank spots and scale trades are recorded under "Known gaps after the
implementation review" in the milestone doc rather than built.

---

## P1 — Blocks the Definition of Done

### 1. No encounter editor (CONFIRMED — blank spot, spec "Builder UI" screen 3, "Creation" §2)
`Encounter` is excluded from `EditableKinds`
([WorldForgePage.tsx:89](../frontEnd/src/pages/admin/WorldForgePage.tsx)) and no
screen creates or edits encounters: rooms, descriptions, exits, item
placements, or interactables. Three spec requirements fall with it:
- **Mission items** (§2): items live inside encounter fragments; with no
  encounter editor there is no way to author the key/name/description of an
  objective or delivery item, so `AcquireItem`/`DeliverItem` objectives can
  only bind to pre-existing items.
- **Per-placement interactions** (§2): `EncounterInteractableDefinition`
  exists in the model and trigger pickers use interactable names, but nothing
  can create or edit one.
- The World Map module is an acknowledged stub linking to `/admin/world`.

### 2. Publish gate does not validate effect targets (CONFIRMED — spec §3: "publish validation checks trigger references (events, scenes, tests, effect targets)")
`GameContentLoader.ValidateEffect` has no `AlertNpc`/`PacifyNpc` arm, and no
NPC-effect checks for null/unknown `npcName` in trigger context:
- `alertNpc` naming a nonexistent NPC publishes fine and silently no-ops.
- `startCombat`/`pacifyNpc`/`alertNpc` with `npcName: null` inside a trigger
  (or unbound scene) always resolves no NPC and no-ops; the loader accepts it.
- A scene `giveItem` validates if *any* encounter declares the item, but at
  runtime requires a live encounter declaring it
  (SceneEffectResolver.cs:192–202, throws) — a Johnson-dialogue `giveItem`
  outside an encounter throws every time.
- An opposed `testId` on a choice of an *unbound* scene
  (`NpcTemplateId == null`) is statically guaranteed to throw at runtime
  (SceneEngine.cs:326–329) yet passes publish.
- An unconditioned `turnInMission` choice (no `missionReadyToTurnIn` /
  `carryingItem` conditions) is offered, picked, and throws
  (SceneEffectResolver.cs:117–123).

### 3. Shipped content is already broken by #2 (CONFIRMED)
The `ledger-terminal-records` trigger (encounters.json): failing the
`read-shipping-records` test narrates "a chime answers it," then the
`onFailure` `alertNpc` names "Warehouse Ganger" — who is in
`warehouse-floor`, not `storage-room`. `SceneEffectResolver.ResolveNpcAsync`
(SceneEffectResolver.cs:288–299) only resolves NPCs in the session's current
room, so the alert is dropped and the escalation never happens. Even if
resolved, the `NpcAlert` reaction would fail target resolution
(GameActionExecutor.cs:245 requires same-room). No test covers the failure
branch. Decide: should NPC effects resolve encounter-wide rather than
room-wide? (The ambush design brief says yes.)

### 4. Trigger `runTest` ignores opposed pools (CONFIRMED)
`TriggerEngine.RunAsync` RunTest (TriggerEngine.cs:251–265) builds and
resolves the test without ever inspecting `OpposedPoolId`. An authored
opposed test in a trigger silently rolls unopposed (threshold null ⇒ trivial
success); `SceneEngine.RollChoiceTestAsync` throws loudly for the same case.
Either oppose it (against whom?) or refuse opposed tests in trigger `runTest`
at publish.

### 5. Room deletion crashes on any room with history (CONFIRMED — my trace)
`ChatMessage.RoomId` and `RoomVisit.RoomId` are both `DeleteBehavior.Restrict`
FKs. `CheckRoomDeletionAsync` (WorldEditorStore.cs) counts exits, occupants,
entry links, and active return links — but not chat messages or room visits —
and `DeleteRoomAsync` doesn't clean them up. Deleting any room that has ever
been visited or spoken in passes the check, then throws `DbUpdateException`
at `SaveChangesAsync` — an unhandled 500, not a refusal with a reason. In
practice room deletion only works on never-used rooms. `RoomDeletionTests`
seeds neither chat nor visits, which is why it's green. Decide: chat/visit
rows are room-scoped history, not ledger/receipts — either delete them with
the room (in the same transaction) or count them and refuse with a reason.

---

## P2 — Real defects to fix before building more on top

### Engine
6. **CONFIRMED** — Trigger NPC speech broadcasts *before* commit while
   narration is buffered until after (TriggerEngine.cs:222–231 vs 308–311):
   authored `[narrate, npcSpeaks]` arrives reversed, and a failed commit
   (fire-once race) leaves an NPC having spoken for a trigger that never
   fired — against the file's own §24 comment.
7. **CONFIRMED** — Two `dealDamage` effects in one trigger under-apply:
   damage is computed from the runtime snapshot taken at executor start and
   emitted as an absolute `SetCharacterDamageChange`; the second Set
   overwrites the first (SceneEffectResolver.cs:155–171). Spec promises
   reactions "composable in sequence."
8. **CONFIRMED** — Multi-`runTest` triggers audit only the last roll
   (single `resolution` local, one audit entry — TriggerEngine.cs:257,
   396–421). Every dice test is supposed to be audited.
9. **CONFIRMED** — Movement race: `PlayerEnteredRoom` is enqueued
   fire-and-forget from a MediatR command that never rides the serialized
   queue (MoveCharacterCommand.cs:84–94); the trigger later reads
   `Session.CurrentRoomId` at processing time for scene anchoring and
   narration. Move-and-step-back anchors the ambush scene (and its
   narration) to the wrong room; trigger runs on the destination scope while
   the player acts on another.
10. **CONFIRMED** — Event asymmetry: `SceneEngine.ChooseAsync` raises
    `MissionAccepted`/`NpcPacified` from effects; identical effects in
    trigger reactions raise nothing. And `MissionAssignmentStore.AssignAsync`
    (admin assignment — the path both playthrough tests use) never raises
    `MissionAccepted`, so the shipped `advance-cleared` mission trigger
    never fires on an admin-assigned run; nothing tests it firing at all.
11. **CONFIRMED** — Retire is not honored by triggers: `OpenScene`/`runTest`
    branch scenes use bare `FindScene`, which resolves retired content
    (TriggerEngine.cs:238, 276). Retiring the ambush scene while its
    encounter trigger stays published keeps serving it to new players —
    against spec §5 "stops being offered."
12. **CONFIRMED (pre-existing, now urgent)** — `GameCommandQueue.ConsumeAsync`
    has a bare `catch` that maps every engine exception to a generic
    ActionFailed with no logging (GameCommandQueue.cs:126–131). Acceptable
    when all content was repo-authored and CI-proven; now that admins author
    content, every authored-content fault in #2 is invisible. At minimum,
    log the exception with the action id and scope.

### Frontend
13. **CONFIRMED** — Trigger editor kind-switch keeps the stale draft:
    `useContentDraft` never resets on `kind` change
    (useContentDraft.ts:44–58); switching Encounters → Missions with
    `gang-warehouse` open lets "Save draft" write the *encounter fragment*
    as a **Mission** definition (server accepts: payload id matches route).
14. **CONFIRMED** — Focus lost every keystroke in id/key fields used as React
    keys: SceneEditor node panels (SceneEditor.tsx:157) and choice rows
    (:189), MissionEditor objective keys (:238) — each character remounts
    the component.
15. **CONFIRMED** — Renaming a scene node leaves `startNodeId` and every
    `nextNodeId` pointing at the old id; the selects then *display* a
    different value than the state carries until publish refuses.
16. **CONFIRMED** — Placed NPCs can be added but never removed
    (PlacedNpcEditor.tsx — no remove control).
17. **CONFIRMED** — Room-delete errors are swallowed in WorldEditorPage (the
    delete runs outside `mutate`'s try/catch; no banner, button not
    disabled during the request, double-click fires twice). This is also the
    UI half of #5.
18. **CONFIRMED** — `POST …/validate` endpoint + client function exist with
    zero callers; the intended per-editor "Validate" button is missing.

---

## P3 — Advisories and polish

- **SUSPECTED** — Trigger reference pickers fall back to globally-pooled
  rooms/NPCs/items exactly when the owner declares none, offering references
  the publish gate will reject (TriggerEditor.tsx:72–87).
- **SUSPECTED** — Reference caches only refresh when the definition key list
  changes; a node added to a scene this session isn't offered to
  `advanceScene` pickers until some definition is added/removed
  (useContentReferences.ts:47–124).
- **SUSPECTED** — Draft hard delete is one un-confirmed click
  (ContentDashboard.tsx:394–401); the server rightly allows it for drafts,
  so hours of authoring can vanish on a mis-click. Add a confirm.
- **SUSPECTED** — Effect editor offers "— the scene's own NPC —" (empty
  npcName) in trigger context where no bound NPC can exist.
- **SUSPECTED** — A trigger `openScene` firing mid-negotiation reuses the
  character's single scene row and nulls `PendingNegotiatedNuyen` — clobbers
  the Johnson conversation with no guard or test. Same mechanism: `openScene`
  on the `npcSpokenTo` event replaces the dialogue that just opened.
- **SUSPECTED** — Duplicate pool terms (legit in SR5) create duplicate React
  keys in TestDefinitionEditor (:255) and index-based removal misbehaves.
- **SUSPECTED** — `NumberField` passes transient `NaN` through, serialized
  as `null` → server parse error the author didn't visibly cause (fields.tsx:65).
- Accessibility: World Forge module rail uses `role="tab"` with no
  tabpanel/arrow-key contract; selection rows convey state by CSS class only;
  a few fetches swallow errors into permanent "Checking…" states.
- TOCTOU races (advisory at current single-admin scale): publish validates
  then writes non-atomically; delete's check races a concurrent publish that
  adds a reference.
- Dashboard inventory runs a full corpus compose+parse per pending-edit
  definition plus one for the corpus — O(N) loader runs per GET; fine at
  today's corpus size, worth a cache when it grows.
- Case-sensitivity is mixed: payload/route agreement and loader ids are
  Ordinal, retired-key sets and template-dependent counts are
  OrdinalIgnoreCase — two ids differing only in case would cross-match
  retirement flags.
- `StampVersion` uses max `UpdatedAtUtc`, so saving a *draft* on a published
  row changes the served revision string without a publish (cosmetic).
- Pre-existing scope-map leak (`GameCommandQueue` never prunes ScopeStates)
  now grows with every room any player walks through, since movement events
  ride the queue per-room.

---

## Blank spots vs the design doc (beyond #1)

- **World-room triggers are unpublishable.** Spec §3 attaches triggers to
  "encounters, rooms, and placements" and headlines "player enters room";
  but mission-level triggers may not carry roomKeys while
  `playerEnteredRoom` requires one — so room-entry triggers only exist
  inside encounters. Public-world ambience (the mockup's Downtown narrate
  trigger) cannot be authored.
- **"Object interact" is inspect-only.** `InteractableInspected` is raised
  solely by the inspect action; sneak-past and other placement interactions
  raise no events.
- **`giveItem` raises no `ItemPickedUp`** and never advances an
  `AcquireItem` objective — content must pair it with `completeObjective`
  by hand, and no trigger can react to the acquisition.
- **Blast radius is a count, not a list** (spec §4): "used by 3 placed NPCs"
  never names which encounters/placements.
- **Placed-NPC override coverage is partial:** schema supports hostile and
  weapon overrides; the editor only exposes armor, monitors, initiative
  base, and pools.
- **Test gaps:** repeatable triggers, trigger conditions, the
  `NpcDefeated`/`EncounterEntered`/`MissionAccepted`/`ItemPickedUp` events
  end-to-end, the fire-once concurrent race, `runTest` failure branches
  (would have caught #3), and scene-replaces-dialogue.

## Doc corrections (implementation diverged sensibly — update the milestone doc)

- **Return room** is not authored: the engine records "where you entered
  from" at encounter entry (StateChangeApplier.cs:365). Drop it from the
  mission editor requirements.
- **Negotiable pay** is not a mission flag: negotiability is authored as a
  `setNegotiatedPay` choice in the Johnson scene. Reword §2.
- **Mission items** are declared inside encounter fragments, not standalone
  content — fold their authoring into the encounter editor requirement.

## Suggested fix order

1. #12 (log queue exceptions) — makes everything else diagnosable.
2. #2 + #4 + #11 (publish-gate effect-target validation, opposed-test
   refusal, retire honored by triggers) — closes the spec §3/§5 promises.
3. #3 (decide cross-room NPC effect resolution; fix the ledger trigger).
4. #5 + #17 (room deletion FK handling + UI error surfacing).
5. #13–#16 (frontend correctness).
6. #6–#10 (trigger ordering/composability/audit/movement race/events).
7. #1 (encounter editor — the remaining DoD feature work).
