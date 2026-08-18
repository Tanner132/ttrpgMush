# Milestone 3: Core Text Commands

**Outcome:** The gameplay composer supports `/help`, `/who`, `/look`, `/say`, and
`/go` without weakening server authority. Text without a slash remains normal speech.

**Depends on:** Milestone 2B.

**Command semantics:** `/who` lists all distinct characters currently online on this
application instance and does not reveal their rooms. `/look` describes only the
current room, its visible exits, and the characters in that room. Durable occupants
may be marked online/offline using the current room presence snapshot. `/help`,
`/look`, `/who`, parser errors, and usage errors are private local output and are
never persisted or broadcast. `/go` persists only the resulting movement state and
emits the established movement events; the command text is not a chat message.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## CMD-301: Add A Pure Client Command Parser

**Depends on:** UI-204.

**Scope:**

- Add a gameplay command parser with typed results for plain speech, known commands, unknown commands, and malformed commands.
- Command names are case-insensitive; arguments preserve their user-entered casing.
- Parse only the first token as the command name. Do not parse slash commands on the server as ordinary chat content.
- Keep parser behavior independent from React and SignalR so it can be unit tested.

**Acceptance criteria:**

- Plain text and `/say text` produce a speech action.
- `/help`, `/who`, and `/look` reject unexpected arguments with usage text.
- `/go` requires a non-empty selector.
- Unknown slash commands produce a local error and are never persisted or broadcast.
- Whitespace, mixed-case commands, missing arguments, and leading slash edge cases have unit tests.

## CMD-302: Add Local Command Output To The Transcript

**Depends on:** CMD-301.

**Scope:**

- Model the displayed transcript as server messages plus local informational/error entries.
- Local entries must have stable client IDs and an explicit display kind; do not fake server message IDs or persist local output.
- Ensure pagination and message deduplication continue to operate only on server messages.
- Treat `/help`, `/look`, `/who`, parser errors, and usage guidance as local entries rather than chat messages.

**Acceptance criteria:**

- Command output appears in execution order in the transcript.
- Local output survives ordinary realtime message updates but may reset when the play session ends.
- Local entries cannot collide with or be mistaken for durable chat messages.
- Transcript rendering has focused tests for mixed server and local entries.

## CMD-303: Implement `/help`, `/look`, And `/say`

**Depends on:** CMD-302.

**Scope:**

- `/help` prints supported commands and concise usage from one command metadata source.
- `/look` prints the authoritative current room name and description, visible exits, and current-room occupants only.
- `/say text` and plain text call the existing send-message operation.
- Commands that require a joined connection fail locally with a clear status and do not clear the draft.

**Acceptance criteria:**

- `/look` never includes online characters outside the current room.
- Hidden exits remain absent because the client only receives visible exits.
- `/say` has the same persistence, room isolation, length validation, and activity renewal as plain speech.
- Successful commands clear the draft; rejected or failed commands retain useful input where retry is possible.

## CMD-304: Implement `/go` Against Visible Exits

**Depends on:** CMD-303.

**Scope:**

- Resolve the selector against the current room's visible exits by exact case-insensitive direction.
- If no exact match exists, allow a unique case-insensitive direction prefix.
- Reject ambiguous and missing matches locally with candidate guidance.
- Submit only the resolved exit ID through the existing server-authoritative movement method.

**Acceptance criteria:**

- `/go north` can resolve an available exit.
- Locked exits resolve but are rejected with a locked message; hidden exits cannot resolve.
- Ambiguous prefixes do not move the character.
- The server still validates current room, visibility, lock state, and destination before movement.
- Parser and gameplay tests cover exact, prefix, ambiguous, locked, stale, and failed movement cases.

## CMD-305: Add Global Online Character Query For `/who`

**Depends on:** CMD-301.

**Scope:**

- Extend the in-memory connection registry with a thread-safe snapshot of distinct online characters across all rooms.
- Deduplicate by character ID across multiple connections and sort consistently by name then ID.
- Expose the snapshot through an authenticated, active-session SignalR hub method.
- Return character ID and name only. Do not return room, connection, account, or play-session identifiers.

**Acceptance criteria:**

- Only joined connections count as online.
- Multiple tabs for one play session produce one `/who` entry.
- Disconnect, movement, session expiry, and application restart follow the existing ephemeral presence semantics.
- A stale authenticated cookie without an active play session cannot query `/who`.
- Registry unit tests and SignalR integration tests cover authorization, deduplication, and cross-room results.

## CMD-306: Wire `/who` And Document Commands

**Depends on:** CMD-302 and CMD-305.

**Scope:**

- Add the typed realtime client operation and execute it from the gameplay command dispatcher.
- Render an empty state or the sorted character names as local command output.
- Update `/help`, README hub documentation, and command tests.

**Acceptance criteria:**

- `/who` includes online characters in other rooms but reveals no locations.
- `/look` continues to show only the current room.
- Realtime failures produce local errors and do not disconnect an otherwise healthy gameplay session.
