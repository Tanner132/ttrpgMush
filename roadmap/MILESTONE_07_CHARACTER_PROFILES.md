# Milestone 7: Lightweight Character Profiles

**Outcome:** Characters have a small public roleplay profile that owners can edit and
other authenticated players can view, without beginning the Shadowrun character sheet.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## PROFILE-701: Approve Lightweight Profile Contract

**Depends on:** Milestone 2.

**Decision required before schema work:**

- Approve fields and limits. Proposed initial fields: optional pronouns (80 characters) and optional public description (2,000 characters).
- Confirm profiles are visible to authenticated users and editable only by the owning user.
- Confirm character name changes, portraits/uploads, biography sections, structured Shadowrun attributes, and profile privacy controls are out of scope.
- Decide whether profiles are visible globally by character ID or only when the viewer currently shares a room. Proposed default: authenticated global read, enabling links from `/who` without exposing location.

**Acceptance criteria:**

- Accepted choices are recorded in `PROJECT_CONTEXT.md`.
- The profile contract contains no inferred Shadowrun rules or mechanical statistics.

## PROFILE-702: Persist Profile Fields And Application Use Cases

**Depends on:** PROFILE-701.

**Scope:**

- Add only the approved fields to `Character`, explicit EF mapping, and a migration.
- Add a public-profile query and owner-authorized update command in Application.
- Normalize empty optional values consistently and enforce approved limits server-side.

**Acceptance criteria:**

- Existing characters migrate with empty optional profiles.
- Only the owner can update a profile; ownership is derived from the authenticated user.
- Clients cannot update owner ID, current room, creation data, or future gameplay fields through profile payloads.
- Domain and persistence tests cover normalization, limits, ownership, and round trips.

## PROFILE-703: Add Profile HTTP API

**Depends on:** PROFILE-702.

**Scope:**

- Add authenticated GET and owner-only antiforgery-protected update endpoints.
- Return a purpose-built public profile contract rather than the persistence entity.
- Use 404 behavior that does not expose private ownership information beyond the approved visibility model.

**Acceptance criteria:**

- API tests cover anonymous access, authorized reads, owner updates, non-owner rejection, validation, and missing characters.
- Profile text is returned as plain data and never interpreted as HTML.

## PROFILE-704: Build Profile View And Owner Edit Pages

**Depends on:** PROFILE-703.

**Scope:**

- Add a routed profile page such as `/characters/:characterId/profile`.
- Render owner edit controls only when the account owns the character, while relying on server authorization.
- Add profile links from current-room occupants. Add links from `/who` only if PROFILE-701 approves globally readable profiles.
- Provide accessible loading, missing, forbidden, validation, save, and unsaved-change states.

**Acceptance criteria:**

- Profile text renders safely as text with preserved readable line breaks.
- Direct navigation and refresh work for allowed viewers.
- Mobile and desktop layouts remain usable.
- Frontend tests cover owner/non-owner controls and server rejection.

## PROFILE-705: Profile Documentation And Regression Pass

**Depends on:** PROFILE-702 through PROFILE-704.

**Scope:**

- Update API, route, security, and product documentation.
- Verify profile changes do not alter character naming, ownership limits, room occupancy, play sessions, or movement.
- Run full backend and frontend checks.

**Acceptance criteria:**

- Documentation clearly separates lightweight profiles from future character sheets.
- Full verification passes and no Shadowrun creator/domain code is introduced.
