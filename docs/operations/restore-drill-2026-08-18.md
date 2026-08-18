# Restore drill: 2026-08-18

## Scope

- Production provider: Supabase
- Source project: `Roast66Coffee` (`thfmvhqwlskvfgejpzwz`)
- Source status at capture: `ACTIVE_HEALTHY`
- Source PostgreSQL version: `17.6.1.054`
- Restore target: local PostgreSQL database `roast66_restore_staging_20260818`
- Target server: the isolated development Docker PostgreSQL service on `localhost:5432`
- Operator: Codex, authenticated by the project owner
- Status: restored and migrated; waiting for verification review

The restore target is a dedicated database and is not used by the running API, which remains connected to `coffeedb`.

## Backup record

- Backup method: Supabase CLI `db dump`, split into roles, schema, and data
- Backup completion window (UTC): 2026-08-18 16:15:29–16:16:10
- Source recovery point: separate transaction-consistent snapshots taken by the three sequential dump operations
- Roles file: `/tmp/roast66-production-20260818-roles.sql`
  - Size: 297 bytes
  - SHA-256: `25873cec56a2cc6514e204f420231777f85c03da818caa7090cdcdfa89776ecd`
- Schema file: `/tmp/roast66-production-20260818-schema.sql`
  - Size: 14,606 bytes
  - SHA-256: `07336b8f83e77621c19d0a7164755c762a58a5827c0d06719440eb16674f8c15`
- Data file: `/tmp/roast66-production-20260818-data.sql`
  - Size: 15,539 bytes
  - SHA-256: `43179a3b030b2d99a1a7a3101a1943148ef77fd4c6aed1b0fb8c5a4078e98066`
- Application data file: `/tmp/roast66-production-20260818-public-data.sql`
  - Completed (UTC): 2026-08-18 16:23:12
  - Size: 6,696 bytes
  - SHA-256: `12e655afdfe1b11dc797dbd53c219280fd31ab436885ac28a3294fd23f74cac8`

Do not add a database URL, password, backup archive, or customer data to this repository.

## Procedure

1. Authenticate the Supabase CLI and link its temporary work directory to project `thfmvhqwlskvfgejpzwz`.
2. Capture the roles, schema, and data as separate SQL files outside the repository:

   ```bash
   supabase db dump --linked --role-only -f /tmp/roast66-production-20260818-roles.sql
   supabase db dump --linked -f /tmp/roast66-production-20260818-schema.sql
   supabase db dump --linked --data-only --use-copy -f /tmp/roast66-production-20260818-data.sql
   ```

3. Record the timestamps, source PostgreSQL version, archive size, and SHA-256 above.
4. Restore the archive into the empty staging target. This will be performed interactively after reviewing the backup record.
5. Run the application migration command against the restored target only after restore verification.

## Restore and migration execution

The original backup files remained unchanged. For the vanilla PostgreSQL staging target, a temporary schema copy excluded Supabase-managed extensions, ownership, publication, and platform-role grants. Application data was re-exported with `--schema public` so Supabase-managed `auth` and `storage` schemas were not required locally.

Final restore inputs:

- Schema: `/tmp/roast66-production-20260818-schema-local-staging-v2.sql`
- Data: `/tmp/roast66-production-20260818-public-data.sql`

Final commands:

```bash
psql -X -v ON_ERROR_STOP=1 \
  -d roast66_restore_staging_20260818 \
  -f /tmp/roast66-production-20260818-schema-local-staging-v2.sql

psql -X -v ON_ERROR_STOP=1 \
  -d roast66_restore_staging_20260818 \
  -f /tmp/roast66-production-20260818-public-data.sql

docker compose run --rm --no-deps --entrypoint dotnet \
  -e 'ConnectionStrings__DefaultConnection=<local-staging-connection>' \
  backend CoffeeShopApi.dll migrate
```

Timings:

- Schema restore: 0.42 seconds
- Public data restore: 0.10 seconds
- Successful application migration: 2.52 seconds

Problems encountered:

1. The unmodified schema requested Supabase-only `pgsodium` and other platform extensions unavailable in vanilla PostgreSQL.
2. The full data dump contained Supabase-managed `auth` and `storage` sections; an application-only `public` data dump was captured instead.
3. The floating `postgres:latest` Docker tag advanced to PostgreSQL 18 and rejected the existing PostgreSQL 17 volume. The Compose service was pinned to `postgres:17`, matching production's major version.
4. Passing `dotnet CoffeeShopApi.dll migrate` as a Compose service command duplicated the image entrypoint and started the API. Overriding the entrypoint executed the intended command.
5. The successful migration logged advisory-lock acquisition before applying migrations and exited normally.

## Verification

- Migration history: passed; staging advanced from 12 restored entries to all 13 repository migrations, ending with `20260813002904_AddOrderTrackingTokens`.
- Source/staging row counts: passed.
  - Menu items: 38 / 38
  - Orders: 2 / 2
  - Order items: 2 / 2
  - Add-ons: 1 / 1
  - Notification messages, notification settings, payment checkout drafts, and staff push subscriptions: 0 / 0
- Menu integrity: passed; all 38 entries were readable, including the four `DRINKS` entries Energy Drink, Lemonade, Refresher, and Tesla.
- Tracking-token migration: passed; both restored orders received distinct non-null tokens.
- Stripe identifiers: not applicable; the backup contained no non-null order payment-intent identifiers.
- Staging API health: passed; `GET /api/health` returned `200`.
- Public menu smoke test: passed; `GET /api/menu` returned `200`.
- Admin login smoke test: passed; valid local credentials returned `200` and a non-empty token. Credentials and token were not logged.
- Private order tracking smoke test: passed; a restored order's private tracking endpoint returned `200` and an order DTO. The token was not logged and its temporary file was deleted immediately.
- Verification API: isolated container `roast66-staging-verification` on `http://localhost:5002`, connected only to `roast66_restore_staging_20260818`.
- Concurrent migration lock: passed. With a controlled session holding advisory lock `7266677001`, PostgreSQL reported one granted exclusive advisory lock and two ungranted exclusive waiters. After release, both concurrent migration commands acquired the lock in turn and exited `0`.
- Automated lock coverage: passed. `PostgresMigrationLockTests.MigrationLock_BlocksAConcurrentMigrationConnection` verified against PostgreSQL 17 in required mode (689 ms).
- Provider runbook: added at `docs/operations/supabase-database-runbook.md` with exact Supabase CLI backup, staging restore, replacement-project recovery, rollback, and cleanup procedures.
- Cleanup: passed. The staging API container and `roast66_restore_staging_20260818` database were removed; temporary dumps, filtered schema copies, logs, and response files were securely deleted; temporary Supabase CLI/work directories were removed; and the local Supabase access token was deleted with `supabase logout`.
- Backup retention after drill: no SQL dump was retained locally or in the repository. The checksums and drill evidence remain in this record; a future operational backup should be copied to approved encrypted off-site storage before local cleanup.

## Results

- Backup captured: passed
- Restore completed: passed for application-owned schema and data
- Restore verification: passed
- Migration completed: passed
- Concurrent migration-lock verification: passed
- Runbook and cleanup documentation: passed
- Final outcome: passed and cleaned up
