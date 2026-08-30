# Supabase database operations runbook

This runbook applies while Supabase project `thfmvhqwlskvfgejpzwz` is the production PostgreSQL provider. Never commit database URLs, passwords, access tokens, or dump files.

## Prerequisites

- Supabase CLI 2.x
- Docker
- PostgreSQL 17 client tools
- An empty PostgreSQL 17 staging database
- Enough protected local storage for the dump

Authenticate and link a temporary work directory:

```bash
supabase login
mkdir -p /tmp/roast66-supabase-workdir
supabase init --workdir /tmp/roast66-supabase-workdir --yes
supabase link \
  --workdir /tmp/roast66-supabase-workdir \
  --project-ref thfmvhqwlskvfgejpzwz
```

The link command can request the Supabase database password. Enter it interactively or use the operating system's credential storage; do not put it in shell history.

## Create a backup

Set `BACKUP_DATE` to the UTC date used in the filenames:

```bash
BACKUP_DATE=YYYYMMDD

supabase db dump \
  --workdir /tmp/roast66-supabase-workdir \
  --linked --role-only \
  -f "/tmp/roast66-production-${BACKUP_DATE}-roles.sql"

supabase db dump \
  --workdir /tmp/roast66-supabase-workdir \
  --linked \
  -f "/tmp/roast66-production-${BACKUP_DATE}-schema.sql"

supabase db dump \
  --workdir /tmp/roast66-supabase-workdir \
  --linked --data-only --use-copy --schema public \
  -f "/tmp/roast66-production-${BACKUP_DATE}-public-data.sql"
```

Record the project ref, PostgreSQL version, UTC completion times, byte sizes, and SHA-256 checksums in a dated drill record. Supabase free-tier recovery depends on exports retained outside Supabase, so copy the verified files to approved encrypted backup storage before deleting the temporary copies.

## Restore into vanilla PostgreSQL staging

The Supabase schema dump contains platform-managed extensions, ownership, publication, and role grants that vanilla PostgreSQL does not provide. Preserve the original dump and create a filtered staging copy:

```bash
sed -E \
  -e '/^CREATE EXTENSION IF NOT EXISTS /d' \
  -e '/^ALTER TABLE .* OWNER TO "postgres";$/d' \
  -e '/^ALTER PUBLICATION "supabase_realtime" OWNER TO "postgres";$/d' \
  -e '/^GRANT .* TO "service_role";$/d' \
  -e '/^GRANT .* TO "postgres";$/d' \
  -e '/^SET SESSION AUTHORIZATION "postgres";$/d' \
  -e '/^RESET SESSION AUTHORIZATION;$/d' \
  -e '/^ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" /d' \
  "/tmp/roast66-production-${BACKUP_DATE}-schema.sql" \
  > "/tmp/roast66-production-${BACKUP_DATE}-schema-local-staging.sql"
```

Restore only into a confirmed empty target. `ON_ERROR_STOP` makes either command fail immediately on the first SQL error:

```bash
psql -X -v ON_ERROR_STOP=1 \
  "$STAGING_DATABASE_URL" \
  -f "/tmp/roast66-production-${BACKUP_DATE}-schema-local-staging.sql"

psql -X -v ON_ERROR_STOP=1 \
  "$STAGING_DATABASE_URL" \
  -f "/tmp/roast66-production-${BACKUP_DATE}-public-data.sql"
```

Run application migrations once against staging:

```bash
ConnectionStrings__DefaultConnection="$STAGING_DATABASE_URL" \
  dotnet CoffeeShopApi.dll migrate
```

Verify migration history, table row counts, menu reads, admin login, private order tracking, payment identifiers when present, and `GET /api/health/ready`.

## Restore into a replacement Supabase project

Use a new, empty Supabase project on the same PostgreSQL major version. Obtain its direct database URL from the Supabase dashboard and keep it out of shell history.

Apply files in this order:

```bash
psql -X -v ON_ERROR_STOP=1 "$REPLACEMENT_DATABASE_URL" \
  -f "/secure/backup/roast66-production-${BACKUP_DATE}-roles.sql"

psql -X -v ON_ERROR_STOP=1 "$REPLACEMENT_DATABASE_URL" \
  -f "/secure/backup/roast66-production-${BACKUP_DATE}-schema.sql"

psql -X -v ON_ERROR_STOP=1 "$REPLACEMENT_DATABASE_URL" \
  -f "/secure/backup/roast66-production-${BACKUP_DATE}-public-data.sql"
```

Do not point the production API at the replacement project until all staging verification checks pass. Update the API connection string in the deployment provider, deploy one API instance, run smoke tests, and only then resume normal traffic.

## Deployment and rollback decision

Before deploying a schema migration:

1. Capture and checksum a fresh backup.
2. Restore and migrate it in staging.
3. Prefer additive, backward-compatible schema changes.
4. Run the controlled migration step before new API instances receive traffic.

After a migration has run, prefer a forward fix when any of these are true:

- The migration changed or deleted data.
- The previous application version is incompatible with the current schema.
- Reversing the migration would require guessing or reconstructing values.
- Production has accepted writes using the new schema.

Roll back only the application image when the current database schema remains backward compatible with that image. Run an EF migration `Down` only when it has been tested against a restored backup and proven not to lose data.

Use full database recovery into a replacement Supabase project when the production database is corrupted, important data was deleted, or a safe forward fix is impossible. Never restore destructively over the only production copy.

## Cleanup and credential handling

After the drill is accepted:

1. Stop the isolated staging API.
2. Drop only the explicitly named staging database.
3. Securely delete temporary SQL dumps, filtered copies, logs, and response files under `/tmp`.
4. Remove `/tmp/roast66-supabase-workdir` and the temporary CLI installation if it is no longer needed.
5. Keep the checksum record, but never the production data, in Git.
6. Run `supabase logout` only if the workstation should no longer retain Supabase access.
7. Rotate the Supabase database password or access token if it was pasted into chat, written to an unprotected file, exposed in logs, or used on an untrusted machine. Routine CLI use through protected credential storage does not by itself require rotation.

Record every removed target and whether an encrypted off-site backup remains available.
