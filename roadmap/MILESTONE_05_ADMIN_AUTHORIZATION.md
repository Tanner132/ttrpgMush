# Milestone 5: Roles, Policies, And Administrative Auditing

**Outcome:** Administrative capabilities use named authorization policies and every
administrative mutation creates an append-only audit record.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## SEC-501: Define Roles, Policies, And Bootstrap Procedure

**Depends on:** Milestone 2.

**Scope:**

- Define centralized role names and named policies. Proposed roles are `Administrator`, `WorldBuilder`, and `Moderator`.
- Define least-privilege policies for role management, world editing, moderation access, and audit-log reading.
- Seed role definitions idempotently, but never assign elevated production access to a normal registered account automatically.
- Document a safe operator bootstrap procedure for the first administrator. Development-only assignment may be deterministic and environment-gated.

**Acceptance criteria:**

- Policies are used at endpoint boundaries; role-name checks are not scattered through handlers.
- New registrations receive no elevated role.
- Production startup does not contain default administrative credentials or silent elevation.
- Policy mapping and bootstrap behavior are documented and tested.

## SEC-502: Add Append-Only Administrative Audit Records

**Depends on:** SEC-501.

**Scope:**

- Add an audit entity containing ID, UTC timestamp, authenticated actor user ID, action, target type, target ID, and bounded structured details.
- Add explicit EF mapping, indexes for timestamp/actor/target queries, restrictive foreign-key behavior, and a migration.
- Expose an Application audit writer suitable for use inside the same database transaction as an administrative mutation.
- Do not add update or delete operations for audit records.

**Acceptance criteria:**

- Server time and authenticated actor identity are authoritative.
- Sensitive values such as password data, cookies, tokens, and connection strings cannot be included in details.
- Failed or rolled-back mutations do not leave a success audit record.
- Persistence tests cover append, query ordering, bounds, and transaction rollback.

## SEC-503: Add Role Administration Use Cases And API

**Depends on:** SEC-502.

**Scope:**

- Add administrator-only user lookup and role assignment/removal use cases.
- Prevent removal of the last effective administrator unless a separately approved recovery procedure exists.
- Audit successful role changes with actor, target user, role, and operation.
- Return minimal account data and never expose password or security-stamp fields.

**Acceptance criteria:**

- Unauthenticated requests return 401 and unauthorized authenticated requests return 403.
- Role changes require antiforgery protection and the role-management policy.
- Invalid users/roles and no-op changes have explicit behavior and tests.
- Concurrent role changes preserve at least one administrator.

## SEC-504: Add Audit Log Query API

**Depends on:** SEC-502.

**Scope:**

- Add an audit-reader policy and cursor-paginated Application query.
- Support bounded filters for actor, action, target type/ID, and UTC time range.
- Return newest first with a deterministic ID tie-breaker.

**Acceptance criteria:**

- Only authorized administrators can read audit records.
- Pagination does not skip or duplicate records with equal timestamps.
- Filters are parameterized and bounded.
- API tests cover 401, 403, authorized reads, filters, and cursor behavior.

## SEC-505: Add Administrative Pages And Navigation

**Depends on:** SEC-503 and SEC-504.

**Scope:**

- Add protected routes for role management and audit-log browsing under `/admin`.
- Show navigation only when account capabilities allow it, while keeping server authorization authoritative.
- Extend the account response with effective capabilities or roles needed for navigation; do not infer access from usernames.
- Provide accessible loading, empty, error, confirmation, and pagination states.

**Acceptance criteria:**

- Direct URL navigation by an unauthorized user renders an access-denied state and cannot retrieve protected data.
- Role changes and audit filters work on desktop and mobile.
- Frontend route and authorization tests cover hidden links, direct access, and server rejection.
