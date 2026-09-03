# Production Hosting and Recovery

This runbook is the source of truth for Roast66 production hosting, ownership,
release, backup, and recovery. The portable application contract is Docker,
HTTP, PostgreSQL 17, Git, EF Core migrations, and environment configuration.
Render is the initial production provider, not an application dependency.

## Environment and ownership boundaries

| Concern | Shared development | Production |
| --- | --- | --- |
| Blueprint | `render.dev.yaml` | `render.prod.yaml` |
| Owner and billing | Developer | Client |
| Frontend/API | Existing free Render services | Client-owned Render services |
| Database | Developer Supabase Free PostgreSQL with mock data only | Client-owned paid Render PostgreSQL |
| Data | Disposable; never copied automatically from production | Authoritative current business data |
| Availability | Cold starts and free-tier limits accepted | Paid always-on API; no keep-alive workarounds |
| Secrets | Development-only provider configuration | Client-controlled Render secret configuration |

The client owns the production Render workspace, billing and recovery access,
database, domain/DNS, Stripe account, notification providers, and secrets. The
current operator needs the least access required for setup and incidents. A
support handoff replaces operator access and alert recipients without changing
application code.

## Release progression

Promote the same reviewed commit through the permanent branches:

```text
feature/* -> dev -> testing -> main -> prod -> manual Render deploy
```

- `dev` is active integration.
- `testing` is the build under deliberate staging/hardening review.
- `main` is release-ready and may be ahead of production.
- `prod` identifies the version intended to be running in production.
- Known-good production commits receive immutable Git tags.

Create and protect these branches as a repository administration task before
the workflow is adopted. Require the existing CI and release-contract checks on
each promotion pull request. Do not commit directly to `main` or `prod` and do
not configure Render production to deploy automatically.

## Client account and initial production setup

The client must create the Render account/workspace with its business identity,
billing method, and client-controlled recovery before production resources are
created. Record only non-secret workspace, project, and service names.

Immediately before setup, recheck Render plan IDs, limits, recovery features,
and pricing. Start with the smallest paid always-on API and paid PostgreSQL plans
that satisfy issue 151, keep the static frontend on the appropriate low/no-cost
plan, and record the expected monthly total in the launch record.

Create a Blueprint instance from `render.prod.yaml`. Confirm:

- the API and PostgreSQL are in Oregon;
- PostgreSQL major version is 17 and server-side pooling is disabled;
- the API uses the database's private/internal connection property;
- the API has one instance, a maximum Npgsql pool size of 20, and
  `/api/health/ready` as its health check;
- forwarded-header processing is enabled for the API, uses Render's
  `CF-Connecting-IP` client header, and lists only the proxy addresses or
  networks observed and approved for that Render service;
- both services track `prod` with automatic deploys disabled;
- every `sync: false` value is stored in Render, not Git or documentation;
- platform failure and database-unhealthy alerts reach both the client/business
  owner and current operator; and
- application workflow alerts use the approved client-owned destination.

Render public web-service traffic passes through Cloudflare and Render's load
balancer before reaching the container. The API therefore uses
`CF-Connecting-IP` for client-IP-aware rate limiting and source attribution.
Do not enable unrestricted forwarded-header processing or use arbitrary
`X-Forwarded-For` values. Populate `ForwardedHeaders__KnownProxies` and/or
`ForwardedHeaders__KnownNetworks` in Render from the verified transport peer
addresses for the service; these values are intentionally deployment
configuration, not application defaults. Recheck them after a hosting or
network-topology change.

For a new Render service, populate the trust entries before enabling normal
traffic. If the transport peer address is not already known, deploy a short-
lived diagnostic revision with forwarded-header processing disabled, capture
the transport-level proxy address in the private service logs, then set the
corresponding `sync: false` Render variable and redeploy with processing
enabled. Remove the diagnostic logging before merging the release.

Before accepting production traffic, verify from two separate client networks
that one client's configured login window returns `429` only for that client,
while the other client's first request remains below the limit. Repeat with
forged `CF-Connecting-IP` and `X-Forwarded-For` headers; changing those caller
supplied values must not evade the real client partition.

Production starts fresh. Do not transfer customer or order history from the
development Supabase project. Apply EF migrations, load the current approved
menu/configuration, and bootstrap the first named Owner with one-time
`Bootstrap__*` inputs. Remove those inputs immediately after the command succeeds.

## Manual production deployment

1. Confirm the intended commit passed the full test and release-contract suite.
2. Confirm `prod` points to that exact commit and record the commit SHA.
3. For a risky/destructive migration, create and verify a fresh one-off portable
   dump before deployment when practical.
4. In the client Render workspace, manually deploy the production API and static
   site from `prod`.
5. Confirm migration completion and `GET /api/health/ready` before normal traffic.
6. Smoke-test menu loading, staff sign-in, one test order, private tracking, and
   authorization failures for anonymous staff routes.
7. Record the outcome and tag a known-good release only after verification.

Application rollback is safe only when the previous image is compatible with
the current schema. Prefer a forward fix after a data-changing migration. Run an
EF migration `Down` only after proving it against a restored copy without data
loss.

## Render-managed recovery

For an ordinary database or platform failure, use Render's best available paid
PostgreSQL recovery point. The operational target is same-business-day recovery,
aiming for approximately four hours when an operator is available; this is not a
contractual SLA.

Never restore over the only production database:

1. Create a Render recovery database as a replacement target.
2. Keep customer traffic pointed away from the replacement.
3. Configure an isolated API instance with the replacement's direct/private
   PostgreSQL connection.
4. Apply and verify all EF migrations.
5. Invoke `POST /api/admin/retention/purge` and verify the count-only queries in
   `logging-and-data-retention.md` report zero expired rows.
6. Verify readiness, migration history, menu reads, staff authentication, order
   creation/tracking, payment identifiers when present, and notification settings.
7. Redirect production configuration to the verified replacement database.
8. Recheck production health and core workflows before retiring the old target.

Record the selected recovery timestamp, start/end UTC times, elapsed operator
time, elapsed recovery time, checks performed, cutover result, rollback target,
and cleanup. Never record connection strings, credentials, tokens, or customer data.

## Catastrophic rebuild without a usable recovery point

The initial launch has no guaranteed off-provider recovery point. If Render and
its managed recovery are unavailable or unrecoverable, current production-only
data may be lost; that risk is accepted for this short-lived ordering data.

1. Create client-owned PostgreSQL 17 on Render or another compatible provider.
2. Deploy the production Docker image using a normal PostgreSQL connection string.
3. Apply the repository's EF migrations.
4. Restore or re-enter the current approved menu, operational configuration, and
   named staff accounts from their non-database sources.
5. Run the retention purge even if the target is expected to be empty.
6. Verify readiness and all core workflows.
7. Redirect DNS/application configuration and resume traffic.
8. Record known data loss, recovery time, new ownership details, and follow-up work.

Supabase is a known-compatible degraded recovery target, not an automatic
replica or failover system.

## Launch and recovery-drill record

The live Render-managed recovery drill is one of the final launch tasks and
requires the paid client-owned database. Before launch, record completion of:

- client workspace, billing, recovery ownership, and operator access;
- current API/database/static plan IDs and expected monthly cost;
- domains, CORS, frontend/API URLs, and client-owned secret configuration;
- Render health checks, platform notifications, and alert recipients;
- fresh production migrations, menu/configuration load, and Owner bootstrap;
- PostgreSQL 17, Oregon colocation, private networking, pool limit 20, and no
  PgBouncer;
- full CI/release smoke and secret scan;
- replacement database -> migrations -> 48-hour purge -> smoke -> cutover drill;
- catastrophic empty-database rebuild drill; and
- measured recovery timing, cleanup, known-good commit SHA, and release tag.
