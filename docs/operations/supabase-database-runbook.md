# Supabase Development Database

Supabase is the developer-owned PostgreSQL provider for the shared cloud
development environment. Its data is mock, disposable, and never authoritative
for production. Roast66 uses only its PostgreSQL connection; do not add
Supabase Auth, Realtime, Storage, or SDK dependencies without a separate decision.

The database security contract is provider-neutral. Every API-owned table in
`public` has PostgreSQL row-level security enabled with no permissive policies.
Consequently Supabase `anon` and `authenticated` PostgREST roles receive no rows
and cannot write even if table privileges are granted. The backend connection
owns the application tables and remains the only application data path.

## Configuration

The existing developer-owned Render services `roast66-api` and `roast66-web`
are managed with `render.dev.yaml`. Store the Supabase direct PostgreSQL URL as
the API's `ConnectionStrings__DefaultConnection` secret. Never commit the URL,
database password, access token, or service-role key.

Keep the development project on PostgreSQL 17 whenever Supabase supports it.
The API uses normal Npgsql client pooling with a maximum of 20 connections. Do
not configure PgBouncer for migrations; EF migrations require a direct database
connection.

Cold starts and free-tier pauses are accepted in development. Do not add
scheduled pings, browser heartbeats, service-role requests, or other keep-alive
workarounds.

## Data boundary and reset

- Use generated/mock customers, orders, payments, and notification destinations.
- Never copy production data, backups, secrets, or provider exports into development.
- Treat all existing Supabase data as disposable.
- Git and EF migrations are the schema source of truth.
- The approved menu snapshot may be loaded explicitly after migrations.

Before resetting the project, confirm the target is the developer-owned
development project and contains no production data. Apply EF migrations to the
empty PostgreSQL 17 target, seed the approved menu explicitly, bootstrap a
development Owner, and verify `GET /api/health/ready`.

Development data does not need a recurring backup. If a disposable export is
created for a portability test, keep it out of Git and delete it immediately
after the test.

## Verification

After a dev deployment or reset, verify migrations, readiness, menu reads, staff
sign-in, order submission, and private tracking. Free-tier latency or cold starts
are not production incidents.

The PostgreSQL release tests inventory every regular or partitioned `public`
table owned by the migration role and fail if RLS is disabled. Tables owned by a
Supabase service or extension role are outside the application's ownership and
must not be altered by Roast66 migrations. The same inventory and denial tests
run against stock PostgreSQL, so Render receives the same protection without
Supabase-specific policies or roles.

When adding a table, enable RLS in the same EF migration. Run the complete
PostgreSQL contract before deployment:

```bash
scripts/ci/with-postgres.sh \
  dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj --no-restore
```

If PostgreSQL is replaced, these migrations and tests no longer provide the
boundary. The replacement design must preserve backend-only data access using
that database's permissions and must add an equivalent automated inventory and
client-denial contract before deployment.
