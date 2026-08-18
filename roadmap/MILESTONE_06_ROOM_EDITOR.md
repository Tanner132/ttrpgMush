# Milestone 6: Coordinate-And-Exit Room Editor Without Deletion

**Outcome:** Authorized world builders can create and update rooms and directed exits
on a coordinate map. No room or exit can be deleted through this application.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## WORLD-601: Add Authorized World Graph Queries

**Depends on:** SEC-501.

**Scope:**

- Add Application queries for the complete room graph and individual room editor details.
- Include room metadata, nullable coordinates/layer, and all outgoing/incoming exits including hidden and locked state.
- Expose policy-protected HTTP GET endpoints for world builders.
- Keep player room-session queries unchanged so hidden exits remain hidden from players.

**Acceptance criteria:**

- Unauthorized callers cannot enumerate hidden world data.
- The graph response is bounded for the initial single-world deployment and avoids per-room query loops.
- Integration tests cover policy enforcement and directed one-way, loop, branch, hidden, and locked exits.

## WORLD-602: Add Audited Room Create And Update Use Cases

**Depends on:** SEC-502 and WORLD-601.

**Scope:**

- Add commands to create and update room name, description, access type, `MapX`, `MapY`, and `MapLayer`.
- Validate existing database length limits and coordinate integer bounds.
- Allow nullable coordinates for rooms not yet placed. Coordinates affect presentation only and never graph connectivity.
- Write the room mutation and audit record atomically.
- Do not add a room deletion command.

**Acceptance criteria:**

- Clients cannot set IDs, creation timestamps, or unsupported access types arbitrarily.
- Overlapping coordinates have explicitly documented behavior. Initial recommendation: allow them and surface an editor warning rather than creating a connectivity rule.
- Tests cover validation, authorization at transport, audit details, and rollback.

## WORLD-603: Add Audited Directed Exit Create And Update Use Cases

**Depends on:** SEC-502 and WORLD-601.

**Scope:**

- Add commands to create and update source, destination, name, direction, hidden state, and locked state.
- Validate both rooms exist and preserve directed-edge semantics.
- Permit one-way exits, loops, branches, hidden paths, and locked paths.
- Write the exit mutation and audit record atomically.
- Do not infer or create a reverse exit and do not add an exit deletion command.

**Acceptance criteria:**

- Creating a reverse path requires a separate explicit create request.
- Invalid room IDs and failed exit updates produce no audit success records.
- Existing player movement immediately uses committed exit changes on its next operation.
- Tests cover all supported graph shapes and policy enforcement.

## WORLD-604: Expose Protected World Mutation Endpoints

**Depends on:** WORLD-602 and WORLD-603.

**Scope:**

- Add thin antiforgery-protected HTTP endpoints under an administrative world route.
- Require the world-editor policy on every endpoint.
- Use explicit request/response contracts and consistent validation problem responses.
- Do not map HTTP DELETE endpoints.

**Acceptance criteria:**

- API tests cover 401, 403, antiforgery rejection, validation, create, update, and audit creation.
- OpenAPI exposes no room or exit deletion operation.
- Payloads cannot bind domain authority fields that are not editable.

## WORLD-605: Build Coordinate Room Editor Page

**Depends on:** FE-206, SEC-505, and WORLD-604.

**Scope:**

- Add a protected `/admin/world` page with layer selection and a coordinate-based room view.
- Support selecting, creating, positioning, and editing rooms through accessible forms.
- Represent unplaced rooms in a separate list so nullable coordinates remain usable.
- Provide a non-graphical list/table fallback for keyboard and narrow-screen use.
- Preserve the established application visual language; do not introduce a component framework.

**Acceptance criteria:**

- Coordinates are presentation metadata only; moving a room does not create, remove, or redirect exits.
- Overlapping rooms are visibly warned about and remain editable.
- The editor is usable with keyboard controls and on a narrow mobile viewport.
- No deletion action appears in menus, forms, keyboard shortcuts, or context controls.

## WORLD-606: Add Directed Exit Editing UX

**Depends on:** WORLD-605.

**Scope:**

- Display outgoing and incoming directed exits distinctly for the selected room.
- Add forms to create and update exit direction, name, source, destination, hidden state, and locked state.
- Offer an explicit convenience action to create a separate reverse exit, with its own editable fields and confirmation.
- Refresh only affected graph state after successful changes where practical.

**Acceptance criteria:**

- The UI never implies that one exit is bidirectional.
- Creating or editing one direction does not silently mutate another exit.
- Hidden and locked states are visible to builders but remain protected from player queries.
- Component and end-to-end-style frontend tests cover one-way and explicit reverse creation flows.

## WORLD-607: World Editor Safety And Regression Pass

**Depends on:** WORLD-601 through WORLD-606.

**Scope:**

- Verify editor mutations against active player movement and room-session reads.
- Add concurrency handling for two editors updating the same record. Prefer an explicit concurrency token rather than silent last-write-wins if conflicts can lose work.
- Document editor permissions, no-deletion constraint, coordinate semantics, and recovery through edits/locking/hiding.

**Acceptance criteria:**

- Stale edits produce a conflict response and preserve the newer committed value.
- No backend, frontend, or documented path deletes rooms or exits.
- Full backend and frontend verification passes.
