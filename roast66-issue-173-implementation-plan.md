# Roast66Coffee Issue #173 — Auditable Staff Authentication

## Authority and source material

This is the implementation source of truth for
[issue #173](https://github.com/Clinton-Cochrane/Roast66Coffee/issues/173). It combines
the original ChatGPT guide with repository-specific design and test planning. The
decisions and delivery sequence below take precedence wherever the original guide was
ambiguous or proposed alternatives.

## Outcome

Replace the shared Production admin credential with locally managed named staff
accounts using ASP.NET Core Identity. Preserve the simple username/password login,
existing JWT bearer transport, `/admin` and `/cash` workflows, route guards, and
eight-hour Production session.

The result must make statements such as this trustworthy:

> Mary changed Order #412 from Preparing to Ready for Pickup at 10:43 AM.

It must also let the owner revoke one staff member without rotating every password or
the global JWT signing key.

## Authoritative decisions

- Use ASP.NET Core Identity with locally managed named accounts.
- Do not add Google, Apple, Meta, Microsoft, OAuth, SSO, or public registration.
- Start with `Admin` and `Owner`. Normal staff receive `Admin`; owners receive both.
- Use password-only authentication in V1 with Identity password hashing, login rate
  limiting, and account lockout. MFA is an explicitly accepted V1 deferral.
- Revoke all sessions for one staff identity when that account is disabled, its
  password is reset, or its roles change. Per-device JWT revocation is a non-goal.
- Use Identity's security stamp as the account session version. Include it in JWTs and
  validate it, together with `IsActive`, on every authenticated API request.
- Support more than one Owner, prohibit self-disable, and prohibit disabling the final
  active Owner.
- Owner-created/reset passwords are initial passwords. Staff can change their own
  password while authenticated. Forced first-login password replacement is deferred.
- Keep audit history indefinitely in V1. Do not expose audit update/delete behavior.
- Use a staged shared-credential cutover; do not rely on a one-shot migration.

## Scope boundaries

Do not add:

- customer accounts;
- email invitations;
- employee numbers, addresses, schedules, payroll, or HR fields;
- a permission-builder or arbitrary permission JSON;
- custom password hashing;
- home-grown MFA or SSO;
- refresh tokens;
- a detailed login-history or general Activity dashboard;
- IP/device fingerprint tracking;
- changes to the current frontend session architecture;
- changes to the eight-hour Production session without a demonstrated need.

## Repository-specific corrections to the original guide

### Temporary legacy-auth switch

Release A may retain the old shared login only when
`Authentication__LegacySharedLoginEnabled=true`. The switch defaults to false.
While enabled, Production still requires `Admin__Username` and `Admin__Password`.

Legacy tokens:

- receive `Admin`, never `Owner`;
- identify the actor as `legacy-shared-admin`;
- are rejected immediately after the switch is disabled;
- cannot manage staff accounts.

After named-account verification, disable the switch before removing the old settings.
Do not rotate the JWT signing key during this cutover; named users should remain signed
in.

### Identity security stamps replace a custom session-version field

JWTs contain the user's Identity security stamp. Token validation loads the user and
rejects the token when the user is missing, inactive, or has a different stamp.
Disable, password reset/change, and role change operations explicitly refresh the
stamp. This invalidates that user's tokens without affecting other staff.

### Push subscriptions belong to stable staff IDs

The existing implementation stores `User.Identity.Name` and sends new-order pushes to
every saved endpoint. Named-account offboarding is incomplete unless this changes.

- Store `StaffUserId`, not a display name, on new/upserted subscriptions.
- Deliver only to subscriptions whose owning staff account remains active.
- Remove a user's subscriptions when the account is disabled.
- Leave legacy subscriptions unowned until a named user signs in and re-registers;
  never guess ownership during migration.

### Audit writes share the business transaction

The audit helper adds a tracked `AuditEvent`; it never calls `SaveChanges` itself.
Business services add the event before their existing save. Identity operations, which
may save more than once through `UserManager`, run inside an explicit relational
transaction together with their audit event.

A failed mutation, concurrency conflict, terminal status request, or replay must not
produce a false or duplicate audit event.

## Data model

### StaffUser

```csharp
public sealed class StaffUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
```

Identity owns IDs, normalized usernames, password hashes, password validation,
security stamps, lockout state, roles, and reset tokens.

### AuditEvent

```text
Id
OccurredUtc
ActorUserId
ActorDisplayName
Action
EntityType
EntityId
DetailsJson
```

Store the user ID for stable attribution and a display-name snapshot for readable
history after later name changes. Transitional/system actors may have no Identity FK.
The model is append-only in application code.

### StaffPushSubscription

Add nullable `StaffUserId` for migration compatibility. New registrations require the
authenticated user ID. Delivery ignores null owners and inactive owners. The same push
endpoint may be reassigned when a different named user signs in on a shared device.

## Target code structure

```text
Models/StaffUser.cs                 Identity user extension
Models/AuditEvent.cs                append-only audit row
Security/StaffRoles.cs              Admin and Owner constants
Security/StaffActor.cs              validated internal actor value
Security/StaffClaimsPrincipal.cs    claim-to-actor boundary
Services/StaffTokenService.cs       JWT creation
Services/StaffAccountService.cs     owner/self account operations
Services/AuditEventFactory.cs       safe tracked audit creation
Controllers/StaffController.cs      /api/admin/staff and /me/password
```

Authentication may be extracted from `AdminController`, but these contracts remain:

```text
POST /api/admin/login  -> { "token": "..." }
GET  /api/admin/me     -> safe identity and roles
```

`ApplicationDbContext` becomes `IdentityDbContext<StaffUser>` and retains all existing
application mappings. Identity and audit tables receive explicit PostgreSQL RLS deny
policies for Supabase `anon` and `authenticated` roles.

## JWT and authorization contract

Named-user JWTs contain:

```text
sub / nameidentifier = Identity user ID
name                 = display name
username             = normalized login name for display
role                 = one claim per role
security_stamp       = current Identity security stamp
```

The API derives every actor from the authenticated principal. React must never send
`changedBy`, `staffName`, `actorUserId`, or equivalent trusted identity data.

All existing `[Authorize(Roles = "Admin")]` routes remain valid. Owner accounts also
carry `Admin`. Staff management uses `[Authorize(Roles = "Owner")]`; hiding the Staff
tab is only a UI convenience.

## Staff-management contract

```text
GET  /api/admin/staff
POST /api/admin/staff
POST /api/admin/staff/{id}/enable
POST /api/admin/staff/{id}/disable
POST /api/admin/staff/{id}/reset-password
POST /api/admin/me/change-password
```

V1 UI fields:

```text
Display name
Username
Initial password
Owner access (Owner-only choice)
```

Responses use explicit DTOs and never serialize `StaffUser`. Safety rules:

- only Owner can list or manage staff;
- username uniqueness uses Identity normalization;
- Owner cannot disable their current account;
- final active Owner cannot be disabled or demoted;
- disabled users cannot log in or use existing JWTs;
- password reset/change and role changes revoke existing JWTs;
- no staff deletion endpoint;
- password hashes, security stamps, reset tokens, and passwords never appear in API
  responses, audit details, or logs.

## Sensitive audit scope

Audit the authenticated actor and UTC timestamp for:

1. Order status changes and general staff order edits.
2. Menu create/edit/archive/restore/delete.
3. Homepage/menu special and promotion changes.
4. Menu import and development/test default reset.
5. Notification settings changes and notification-log purge.
6. Staff create, enable, disable, password reset/change, and role changes.
7. Staff push subscription enrollment/removal.

Do not audit reads, searches, tab changes, heartbeat traffic, public ordering, or menu
exports. Details are structured and allow-listed. They never contain credentials,
customer contact values, provider payloads, or raw request bodies. For settings holding
contact information, record changed field names rather than old/new values.

## Order-status attribution

The existing state machine remains authoritative:

```text
Received -> Preparing -> ReadyForPickup -> Completed
```

The controller obtains a `StaffActor` from validated claims and passes it to
`AdvanceStatusAsync`. When a transition advances, the service updates the order and
adds one `order.status.changed` event before the same `SaveChangesAsync` call.

- Invalid or missing orders write no event.
- Terminal requests write no event.
- Conflicts write no event.
- Replayed requests write no second event.
- Competing PostgreSQL writers produce one transition and one event.

The bounded admin-order projection includes the latest status actor/time. The order
card shows:

```text
Last changed by Mary · 10:43 AM
```

Pre-migration orders have no attribution and render without that line. A full timeline
or Activity page is deferred.

## Surgical implementation slices

### Slice 0 — Isolation and characterization

- Work from `feature/173-auditable-staff-authentication`, based on completed #176 work.
- Preserve login response, admin/cash gates, rate limiting, JWT validation, existing
  authorization, order-state concurrency, and frontend 401 handling in tests first.

### Slice 1 — Additive Identity schema and composition

- Add the Identity EF package at the existing .NET 8 patch family.
- Configure Identity stores, password policy, normalization, lockout, and roles without
  cookies or public registration.
- Add Identity, audit, and push-ownership schema through a normal additive migration.
- Add RLS deny policies and migration/model contract coverage.
- Seed Testing through `WebAppFactory`; Development through explicit local
  initialization; never auto-seed Production credentials.

### Slice 2 — Bootstrap/recovery and named JWT authentication

- Add idempotent, fail-closed `initialize-owner` and constrained `recover-owner`
  commands. Read secrets from explicit bootstrap configuration and never log them.
- Convert login to Identity and issue the named claims.
- Validate active state, security stamp, and legacy switch in JWT bearer events.
- Add `/api/admin/me`.
- Retain shared authentication only through the temporary explicit switch.

### Slice 3 — Owner-only staff management

- Implement list/create/enable/disable/reset and authenticated self-password change.
- Enforce transactions, role rules, normalized uniqueness, self-disable protection,
  final-Owner protection, stamp refresh, and safe DTOs.
- Add an accessible bilingual Owner-only Staff tab.

### Slice 4 — Push-subscription ownership

- Associate registrations with `ClaimTypes.NameIdentifier`.
- Filter fan-out by active ownership.
- Remove subscriptions during account disable.
- Cover reassignment of a shared browser endpoint.

### Slice 5 — Audit foundation and order attribution

- Add the audit entity, indexes, safe factory, and update/delete guard.
- Derive actors only from authenticated principals.
- Add status audit to the existing concurrency-safe save.
- Project and display the latest status actor/time.

### Slice 6 — Remaining sensitive mutations

- Thread the internal actor into menu, notification settings, staff, push, and general
  staff-order mutation services.
- Add the safe event before the mutation's successful save/commit.

### Slice 7 — Production cutover and cleanup

1. Deploy the additive release with legacy fallback enabled.
2. Apply migrations and initialize the first Owner.
3. Verify named Owner login and create remaining staff.
4. Verify targeted revocation and push removal without disturbing other users.
5. Disable legacy fallback and prove the old password and an issued legacy token fail.
6. Retain old Render values briefly only as an application-image rollback aid.
7. Remove fallback code, `Admin__Username`, `Admin__Password`, credential-settings API/UI,
   and obsolete shared-device instructions after the observation window.

## Test plan

Tests are written before each behavior change.

### Backend unit/service tests

- Valid named credentials succeed; wrong, unknown, inactive, and locked-out accounts
  fail without username disclosure.
- JWTs contain expected identity, roles, stamp, issuer, audience, and configured expiry.
- Staff rules cover duplicates, self-disable, final Owner, role changes, password reset,
  and stamp refresh.
- Audit details omit secrets and audit rows reject update/delete attempts.
- Successful status transitions write one event; invalid, terminal, conflicting, and
  replayed requests write none.

### HTTP integration tests

- Anonymous staff routes return 401; Admin requests to Owner routes return 403.
- Admin can use normal operations and Owner can additionally manage staff.
- Disabling/resetting one user rejects their old token while another token remains valid.
- Legacy credentials and legacy tokens obey the temporary switch.
- `/api/admin/me`, staff DTOs, responses, and logs expose no authentication secrets.
- Disabled users' push endpoints do not receive delivery.

### PostgreSQL tests

- Full migrations are repeatable, model-aligned, and leave no pending migration.
- Identity, audit, indexes, relationships, and RLS policies exist in PostgreSQL.
- Granted Supabase client roles still cannot read Identity or audit rows.
- Concurrent status advances yield one state change and one audit event.
- Failed writes cannot commit orphan audit rows.
- Multi-save Identity operations and audit roll back together.
- The additive migration remains compatible with the previous application image.

### Frontend tests

- Login continues storing the JWT and both gates continue working.
- Only Owner sees Staff; backend denial remains authoritative.
- Staff management covers loading, validation, success, failure, enable/disable, reset,
  and self-protection states.
- Revoked-session 401 handling clears `/admin` and `/cash` sessions.
- Order cards render attribution and tolerate older orders without it.
- English/es-MX strings remain in parity and controls remain keyboard accessible.

### Full verification

- Run focused backend/frontend tests after each slice.
- Run PostgreSQL integration tests for schema, RLS, transactions, and concurrency.
- Run frontend tests, lint, and Production build.
- Finish with `scripts/ci/full-local-smoke.sh` from #176.
- Perform the staged Render smoke and rollback checks before deleting shared credentials.

## Definition of done

- Every staff member has a unique named identity.
- Shared Production authentication is disabled and ultimately removed.
- Identity owns password hashing and validation.
- Existing login, JWT, `/admin`, `/cash`, and eight-hour Production sessions still work.
- Only Owner can manage staff, and final/self lockout is prevented.
- One user's access and push delivery can be revoked without affecting other users.
- Important mutations record the authenticated actor and UTC timestamp atomically.
- Order cards surface useful status attribution.
- Audit rows are append-only and contain no secrets.
- Concurrency and replay behavior cannot create duplicate/false audit events.
- Authentication, authorization, account management, audit, migration, RLS, frontend,
  and rollout tests pass.
- Runbooks cover provisioning, offboarding, lost devices, reset, owner recovery, staged
  cutover, rollback, and final legacy-secret removal.

## Implementation record

Implemented on `feature/173-auditable-staff-authentication`. The code and additive
migration complete Slices 0–6 and provide the guarded Release A path for Slice 7.
Disabling the compatibility switch and later removing its code/settings are deliberate
production rollout actions; they cannot be performed by a source commit alone.

Verification completed on 2026-09-01:

- 180 backend tests passed, including PostgreSQL migration, RLS, concurrency, named
  authentication, role authorization, targeted disable/reset/change revocation, and
  append-only audit coverage.
- Backend coverage gates passed: 74.40% application line / 57.71% branch; security
  paths reached 96.59% line / 87.93% branch.
- 141 frontend tests passed; ESLint and the Production TypeScript/Vite build passed.
- The high-severity dependency-audit gate passed (one pre-existing low-severity
  development dependency advisory remains).
- Render migration/startup, Chromium menu, and repeated-migration release smoke passed.
- EF reports no model changes pending after `AddStaffIdentityAndAudit`.
