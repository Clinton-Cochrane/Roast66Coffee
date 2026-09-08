# Staff Authentication Operations

This runbook covers named staff accounts introduced by issue #173. ASP.NET Core
Identity owns password hashing, lockout state, roles, and security stamps. JWTs remain
the browser transport, but every authenticated API request also verifies that the
account is active and its security stamp is current.

## Roles and boundaries

- `Admin` can use existing staff workflows, change their own password, and manage
  their own push subscription.
- `Owner` always also has `Admin` and can list, create, enable, disable, and reset
  staff accounts.
- There is no public registration, account deletion, or self-service password email.
- An Owner cannot disable their current account or the final active Owner.

## Initial production rollout

Use a staged rollout so the existing shared login remains available during initial
verification.

1. Back up the database and deploy the additive Identity/audit migration.
2. Keep `Authentication__LegacySharedLoginEnabled=true` and retain the current
   `Admin__Username` and `Admin__Password` for this first release only.
3. Apply migrations, then open a one-off shell on the deployed backend and run:

   ```bash
   Bootstrap__Username=owner \
   Bootstrap__DisplayName="Shop Owner" \
   Bootstrap__Password='replace-with-a-strong-password' \
   dotnet CoffeeShopApi.dll initialize-owner
   ```

4. Remove the `Bootstrap__*` environment values immediately after success. The
   command prints success but never prints the password.
5. Sign in with the named Owner and verify the Staff tab, order status attribution,
   menu edits, notification settings, `/cash`, and push enrollment.
6. Create each staff member with their own initial password. Have each person sign in
   and change it from the Staff screen before normal use.
7. Set `Authentication__LegacySharedLoginEnabled=false` and redeploy. Confirm an old
   legacy token now receives `401`, while an existing named-user token remains valid.
8. Remove `Admin__Username` and `Admin__Password` after the disabled fallback has been
   verified. Do not rotate `Jwt__Key` as part of the normal cutover.

Legacy tokens are deliberately `Admin` only and cannot reach Owner endpoints. Legacy
push subscriptions are unowned and do not receive new-order notifications; a named
user must sign in and enroll the device.

## Routine provisioning and offboarding

Create and reset accounts in `/admin` → **Staff**. Use a unique account per person;
do not create accounts for a shared tablet or post passwords in chat. Account actions
are append-only audit events and never include passwords or reset tokens.

When someone leaves:

1. Disable their account before collecting devices.
2. Confirm an existing session receives `401`.
3. Confirm their push subscriptions no longer receive new-order notifications.
4. Keep the disabled account so historical audit attribution remains readable.

Disabling, password reset/change, and role changes refresh the account security stamp,
revoking all JWTs for that account only.

## Lost device or suspected account compromise

Disable the affected account immediately. If the person still needs access, reset its
password and then re-enable it. Verify another user remains authenticated. Rotate the
global `Jwt__Key` only if the signing key itself might be exposed; rotation revokes all
staff sessions and requires a deployment configuration update.

## Owner recovery

Use this only when no active Owner can sign in. The account must already exist.

```bash
Bootstrap__Username=owner \
Bootstrap__DisplayName="Shop Owner" \
Bootstrap__Password='replace-with-a-new-strong-password' \
dotnet CoffeeShopApi.dll recover-owner
```

The recovery command activates the account, resets its password, restores both roles,
refreshes its security stamp, and writes a system audit event. Remove `Bootstrap__*`
values immediately afterward. If the named account does not exist, restore database
access and initialize a new Owner through the controlled `initialize-owner` command.

## Rollback

The migration is additive, so the first application rollback may continue using the
legacy shared credential while the compatibility switch and settings remain present.
Do not run the migration `Down` in production merely to roll back application code: it
drops identity and audit data. Prefer a forward fix. Once the shared fallback settings
have been removed, restoring an older application version requires an explicit and
reviewed reintroduction of those secrets.
