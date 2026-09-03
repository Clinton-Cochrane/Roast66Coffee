# Supabase Development Database

Supabase is the developer-owned PostgreSQL provider for the shared cloud
development environment. Its data is mock, disposable, and never authoritative
for production. Roast66 uses only its PostgreSQL connection; do not add
Supabase Auth, Realtime, Storage, or SDK dependencies without a separate decision.

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
