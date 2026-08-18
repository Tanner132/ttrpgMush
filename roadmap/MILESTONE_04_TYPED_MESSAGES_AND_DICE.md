# Milestone 4: Typed Messages, Emotes, And Dice

**Outcome:** Durable room communication carries an explicit type, `/emote` renders as
an action with optional quoted dialogue, and server-generated dice results are saved
as chat entries without introducing a separate dice-roll record.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## MSG-401: Approve Message Contract And Evaluate Dice Libraries

**Depends on:** Milestone 3.

**Scope and decisions required before MSG-405:**

- Approve persisted message types. Proposed initial set: `Say`, `Emote`, and `Roll`.
- Confirm that only room communication is persisted: speech, emotes, and the server-rendered result of a public roll. `/help`, `/look`, `/who`, parser output, and usage errors remain local-only; `/go` persists movement rather than command text.
- Decide whether movement arrivals/departures remain ephemeral events or become durable typed messages. Proposed default: remain ephemeral.
- Approve dice grammar, limits, and output. Proposed non-Shadowrun default: `NdS`, optional signed modifier, maximum 100 dice, maximum 1,000 sides, and no exploding dice, pools, limits, glitches, or opposed tests.
- Evaluate maintained .NET dice packages before designing a parser. Compare license, maintenance, .NET 10 compatibility, security history, expression complexity limits, server-controlled randomness, deterministic testing support, and suitability for future Shadowrun dice pools.
- Record the selected package and pinned version, or document why no candidate is suitable before approving a minimal internal parser.
- Do not add a `DiceRoll` entity/table or structured roll-result columns in this milestone. Persist one canonical server-rendered `Roll` chat message containing the normalized expression and result. Future mechanics must introduce a separately approved structured model if they need rolls as gameplay inputs.

**Acceptance criteria:**

- Accepted choices are recorded in `PROJECT_CONTEXT.md` before dice implementation begins.
- No Shadowrun-specific dice behavior is inferred from the setting.
- The dependency decision is supported by a small spike covering valid input, invalid input, configured limits, deterministic testing, and production randomness.

## MSG-402: Persist And Return Typed Messages

**Depends on:** MSG-401 message-type decision only.

**Scope:**

- Add a domain message-type enum and required `ChatMessage` type property.
- Map the enum explicitly and add an EF migration that backfills every existing message as `Say`.
- Carry type through Application models, room history, SignalR contracts, and frontend API types.

**Acceptance criteria:**

- Existing transcripts retain all messages as speech after migration.
- Unknown or unsupported type values fail safely rather than rendering as another type.
- Persistence and API integration tests cover round trips and history pagination.

## MSG-403: Authorize Typed Message Creation

**Depends on:** MSG-402.

**Scope:**

- Extend the send-message application use case with a requested supported user-authored type.
- Permit `Say` and `Emote` from the authenticated active character.
- Do not permit clients to submit `Roll` content or results through the generic send path.
- Retain session locking, content validation, timestamp authority, persistence-before-broadcast, and activity renewal.

**Acceptance criteria:**

- A client cannot forge another character, room, timestamp, message type, or dice result.
- Invalid types and content are rejected without persistence or broadcast.
- Concurrency and room-isolation guarantees remain covered by integration tests.

## MSG-404: Implement `/emote` And Typed Rendering

**Depends on:** MSG-403.

**Scope:**

- Parse everything after `/emote` as the emote body and send it through the typed message path.
- Preserve quotes as ordinary text; no interpolation language or special quoted-string parser is needed. For example, `/emote leans against a wall "how are you?"` stores `leans against a wall "how are you?"`.
- Render speech, emotes, and future rolls with explicit accessible markup and established visual styling.
- Keep raw user content as text; never render message content as HTML.
- Continue using EF Core parameterized writes and never concatenate emote content into raw SQL.

**Acceptance criteria:**

- `/emote leans against a wall "how are you?"` renders as `Character Name leans against a wall "how are you?"` without duplicating the name in stored content.
- Empty and oversized emotes are rejected consistently with speech.
- Quotes, apostrophes, SQL-like text, and HTML-like text are stored and rendered as inert plain text.
- History and realtime delivery render a given message type identically.

## MSG-405: Integrate Server-Authoritative Dice Rolling

**Depends on:** MSG-401 dice decision and MSG-402.

**Scope:**

- Integrate the library selected by MSG-401 behind an Application-facing dice engine boundary so package-specific contracts do not leak into transport or Domain. Implement a minimal internal parser only if MSG-401 documents why no package is suitable.
- Parse and enforce the approved grammar, expression length, dice count, side count, and computational limits on the server.
- Resolve the active character and room under the same gameplay session lock ordering used by chat and movement.
- Generate the result on the server, persist one canonical `Roll` chat message, renew activity, then broadcast it.
- Do not create a separate dice-roll persistence model. The durable chat entry is the room transcript record for the roll.
- Use an appropriate unbiased system random source in production and deterministic behavior in tests through the selected package or adapter boundary.

**Acceptance criteria:**

- Clients send an expression, never authoritative outcomes.
- Invalid or abusive expressions are rejected without persistence, broadcast, or excessive allocation.
- Roll messages participate in current play-session room-visit visibility exactly like speech.
- Reload, reconnect, and history pagination retain the same authoritative roll result because the rendered roll chat entry is durable.
- Tests cover the package adapter, grammar boundaries, deterministic outcomes, authorization, transaction ordering, and persistence-before-broadcast.

## MSG-406: Implement `/roll` And Complete Typed Message UX

**Depends on:** MSG-404 and MSG-405.

**Scope:**

- Add `/roll expression` to the parser, realtime client, dispatcher, and `/help` output.
- Render the normalized expression, individual results when approved, and total without trusting client calculations.
- Add only filters supported by real typed messages, initially `All`, roleplay (`Say` and `Emote`), and rolls. Keep `All` as the default and indicate when an active filter hides new entries.
- Add useful pending and error states without blocking unrelated incoming messages.

**Acceptance criteria:**

- A successful roll clears the draft and appears once through normal realtime delivery.
- A rejected roll retains the expression for correction.
- Local command output remains visible regardless of the durable-message filter.
- Typed message tests cover history, realtime delivery, reconnect deduplication, and mobile rendering.
