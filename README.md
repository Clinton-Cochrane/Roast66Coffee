# Roast 66 Coffee

Roast 66 Coffee is the ordering and shop-operations application for Roast 66's mobile coffee business. Customers can browse the menu, build drinks, place pickup orders, and follow preparation through a private tracking link. Staff use dedicated admin and cash views to manage the menu, process the order queue, and configure notifications.

The application is a React single-page frontend backed by an ASP.NET Core API and PostgreSQL. It is designed to run locally with Docker Compose and deploy to Render from environment-specific Blueprints.

## What the Application Does

### Customer experience

- Browse a categorized menu in English or Spanish.
- Build drinks with quantities, flavors, add-ons, and notes.
- Place pickup orders with optional email notifications.
- Replay accidental duplicate submissions without creating a second order.
- Receive a cryptographically random private tracking token after ordering.
- Follow an order from received through preparing, ready for pickup, and completed.
- Return to an active tracked order from the navigation bar on the same device.
- Optionally download an order summary.

### Staff experience

- Use `/admin` for the order queue, menu management, bulk menu operations, and notification settings.
- Use `/cash` for a streamlined shop-device workflow.
- Sign in with an individual staff account; Owners can create, disable, and reset staff accounts.
- Advance order status and trigger customer-ready notifications.
- Import, export, or explicitly seed menu data.
- Register staff devices for web-push notifications.

### Production safeguards

- Production refuses to start without a stable JWT signing key. Shared credentials are required only while the temporary legacy-login switch is enabled.
- Named staff sessions expire after eight hours, reject malformed, expired, disabled-account, and stale-security-stamp tokens, and synchronize logout across browser tabs.
- Staff mutations are attributed to a stable account and recorded in an append-only audit trail.
- Public order APIs use 256-bit tracking tokens instead of sequential IDs and customer identity.
- Public tracking responses omit phone numbers, email addresses, provider IDs, and internal database fields.
- Public order creation, tracking, login, and password-support endpoints are rate limited.
- PostgreSQL enforces one durable order per client-generated idempotency key.
- Production migrations take a PostgreSQL advisory lock and finish before the API starts serving traffic.

Online payments and external SMS are feature-gated and disabled by default until their production integrations are approved and hardened. Payments use a provider-neutral application service; Stripe is the included gateway. SMS uses a provider-neutral sender contract with a disabled default implementation, so no SMS vendor is installed or contacted by default.

## Architecture

| Area | Technology |
| --- | --- |
| Frontend | React 18, TypeScript, React Router 7, Axios, Tailwind CSS, Vite |
| Backend | .NET 8, ASP.NET Core Web API, Entity Framework Core |
| Database | PostgreSQL |
| Authentication | ASP.NET Core Identity accounts and JWT bearer tokens for staff routes |
| Testing | xUnit, ASP.NET integration tests, Vitest, Testing Library |
| Deployment | Docker and Render Blueprint |
| Automation | GitHub Actions, Dependabot, CodeQL |

Repository layout:

```text
CoffeeShopApi/        ASP.NET Core API, EF models, migrations, and services
CoffeeShopApi.Tests/  Backend unit and API integration tests
roast66/              React frontend and frontend tests
docs/operations/      Testing, release, deployment, and incident runbooks
scripts/ci/            Coverage and production-release smoke automation
scripts/ops/          Health-check helpers
coverlet.runsettings   Meaningful backend coverage exclusions
render.*.yaml         Development and production Render Blueprints
docker-compose.yml    Local frontend, backend, and PostgreSQL stack
```

Most code is intentionally documented through focused types, descriptive names,
and small workflows. Comments are reserved for behavior that the code cannot
explain by itself: security boundaries, concurrency guarantees, destructive
operations, provider constraints, and the reason a surprising choice is safe.
The testing and release rationale is maintained in
[`docs/operations/testing-and-release-readiness.md`](docs/operations/testing-and-release-readiness.md).

### Where the important rules live

| Workflow | Entry points and source of truth | Non-obvious rule |
| --- | --- | --- |
| Public ordering | `OrderController` → `OrderService` | The browser supplies menu IDs, but the service validates live availability, snapshots names/prices, and resolves concurrent idempotency retries through a database unique constraint. |
| Order tracking | `OrderController`, `PublicOrderDto`, `OrderStatusPage.tsx` | A 256-bit URL-safe token is the public credential. Numeric IDs and customer identity are not public lookup credentials. |
| Staff order queue | `AdminController`, `OrderService`, `ViewOrders.tsx` | Status changes include the caller's expected state and use a concurrency token so retries replay safely without skipping workflow states. |
| Menu management | `MenuService`, `ManageMenu.tsx` | PostgreSQL serializes homepage-special selection across rows; order history remains intact because order lines contain immutable menu snapshots. |
| Authentication | `StaffAccountService`, `StaffTokenService`, `SecurityConfiguration`, `Startup` | Owners manage named accounts. Every request validates account activity and the Identity security stamp, so disabling or resetting one account revokes only that account's sessions. |
| Customer notifications | `NotificationService`, `DataRetentionService` | A deduplicated, destination-free audit row is saved before delivery. Logs retain safe classifications, not provider bodies, destinations, or customer message content. |
| Staff push | `StaffPushNotificationQueue`, `StaffPushNotificationWorker`, `StaffPushNotificationService` | Delivery is bounded and best-effort after order commit; one dead or failing browser subscription cannot fail the order or block other devices. |
| Online payment | `PaymentService`, `IPaymentGateway`, provider adapters | Checkout totals come from the stored order snapshot. Verified webhook replays converge through optimistic concurrency. |
| Database release | `Program`, `docker/backend-entrypoint.sh`, `DatabaseReadinessHealthCheck` | Migration takes an advisory lock and must finish before startup; readiness requires both connectivity and zero pending migrations. |

Large React screens keep their state orchestration close to the rendered workflow,
while reusable session, status, idempotency, and transport rules live in `src/lib`,
`src/constants`, `authSession.ts`, and `axiosConfig.ts`. If a screen grows new
independent behavior, extract that behavior behind a named hook or module rather
than extending the component with another cross-cutting effect.

## Local Development

### Prerequisites

- Docker with Docker Compose for the recommended setup
- Node.js 20+ and npm for frontend-only development
- .NET 8 SDK for backend development and EF migrations
- PostgreSQL when running the backend without Docker
- Python 3 and ripgrep (`rg`) for the coverage summary and full local smoke

### Recommended: Docker Compose

1. Create the local environment files:

   ```bash
   cp env.example .env
   cp CoffeeShopApi/.env.example CoffeeShopApi/.env
   cp roast66/.env.example roast66/.env
   ```

2. Start PostgreSQL, initialize the local database, and start the API and frontend:

   ```bash
   docker compose up --build -d
   ```

   Compose applies pending migrations and seeds the default menu only when the
   local database is empty. Existing local menu and order data are preserved.

3. Open the services:

   - Frontend: `http://localhost:3000`
   - API: `http://localhost:5001`
   - API liveness: `http://localhost:5001/api/health`
   - API readiness: `http://localhost:5001/api/health/ready`

The Docker connection string must use `Host=postgres-db`, which is already shown in `CoffeeShopApi/.env.example`.

To discard all local orders and menu edits and recreate a clean snapshot:

```bash
docker compose down --volumes
docker compose up --build -d
```

The first command permanently removes only this Compose project's local database
volume. The initializer recreates the schema and default menu on the next start.

### Run without Docker

Create `CoffeeShopApi/appsettings.json` from `CoffeeShopApi/appsettings.Example.json` and set a local PostgreSQL connection string. Then run:

```bash
dotnet ef database update --project CoffeeShopApi
dotnet run --project CoffeeShopApi
```

In another terminal:

```bash
cd roast66
npm install
npm run dev
```

The API listens on port 80 unless `PORT` is set. Ensure `VITE_API_URL` points to the actual API URL.

### Local menu snapshot

The frontend includes a snapshot of the local menu at
`roast66/public/data/menu.json`. Vite development on localhost and loopback uses
that snapshot for public menu reads by default. Docker Compose explicitly uses
the initialized API menu so displayed item IDs always match order submissions.

To run menu and layout work without an API, database, or Docker:

```bash
cd roast66
npm install
npm run dev:static
```

Features that write data, submit orders, or use admin APIs still require the
backend and database. Set `VITE_USE_STATIC_MENU=false` to force a local build to
read the menu from the API instead.

## Configuration

ASP.NET configuration uses double underscores in environment-variable names. For example, `Jwt:Key` becomes `Jwt__Key`.

### Required backend settings

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL keyword connection string or `postgresql://` URL; the application enforces Npgsql pooling with a maximum of 20 connections |
| `Authentication__LegacySharedLoginEnabled` | Temporary shared-login compatibility switch; defaults to `false` |
| `Admin__Username` | Legacy shared username; required only while the compatibility switch is `true` |
| `Admin__Password` | Legacy shared password; required only while the compatibility switch is `true` |
| `Jwt__Key` | Stable signing secret of at least 32 characters |
| `Jwt__Issuer` | JWT issuer; normally `Roast66Coffee` |
| `Jwt__Audience` | JWT audience; normally `Roast66Coffee` |
| `Jwt__TokenExpiryInHours` | Staff session duration; production default is `8` |
| `AllowedOrigins` | Comma-separated frontend origins allowed by CORS |

Development creates the local Owner during `initialize-local` using the explicit
development credentials. Production has no account defaults and fails during startup
when its signing configuration—or enabled legacy fallback—is incomplete or unsafe.

Generate a production JWT key with:

```bash
openssl rand -base64 48
```

Keep this value stable during normal deployments. Rotating it immediately invalidates every active staff session.

### Initialize the first Owner

Apply migrations, then run the published backend once with one-time bootstrap values:

```bash
Bootstrap__Username=owner \
Bootstrap__DisplayName="Shop Owner" \
Bootstrap__Password='replace-with-a-strong-password' \
dotnet CoffeeShopApi.dll initialize-owner
```

Remove the three `Bootstrap__*` values when the command succeeds. They are command
inputs, not runtime settings. Additional staff and Owners are created from `/admin` →
**Staff**. The complete rollout and recovery procedure is in
[`docs/operations/staff-authentication.md`](docs/operations/staff-authentication.md).

### Frontend settings

| Setting | Purpose |
| --- | --- |
| `VITE_API_URL` | API base URL including `/api`, with no trailing slash |
| `VITE_USE_STATIC_MENU` | Overrides menu data selection. Localhost defaults to the bundled JSON snapshot; set `false` to force API reads or `true` to use the snapshot on any host. |
| `VITE_ENABLE_ONLINE_PAYMENTS` | Enables the configured online checkout UI when set to `true` |
| `VITE_VAPID_PUBLIC_KEY` | Optional public key for staff web push |

### Idempotent order submission

`POST /api/order` and the legacy `POST /api/admin/orders` route require an
`X-Idempotency-Key` header containing a non-empty client-generated value of at
most 128 characters. A UUID is recommended.

- The first valid request for a key returns `201 Created`.
- A retry with the same key and equivalent normalized payload returns `200 OK`
  with the original order and `Idempotency-Replayed: true`.
- Reusing a key with a different customer or order payload returns `409 Conflict`.
- Omitting the header or exceeding its length limit returns `400 Bad Request`.
- Keys do not expire while their order is retained. An intentional repeat order
  must use a new key, even when its contents are identical.

The browser stores an in-progress submission key in tab-scoped session storage,
reuses it when an unchanged submission is retried after a network failure, and
clears it after receiving a successful response.

Vite settings are embedded at build time, so changing them requires rebuilding the static site.

### Optional integrations

- Resend: customer and support email delivery
- Web Push/VAPID: staff-device notifications
- Payments: provider-neutral checkout with Stripe as the included gateway, disabled by default
- SMS: provider-neutral sender contract with no provider installed, disabled by default

Online payment configuration uses `Payments__DefaultProvider` (currently `stripe`) and
`Payments__FrontendBaseUrl`. Provider credentials remain isolated under their own prefix,
such as `Stripe__SecretKey` and `Stripe__WebhookSecret`. The legacy
`POST /api/payments/webhook` route targets the default provider; provider-specific webhook
routes use `POST /api/payments/{provider}/webhook`.

Orders are created before online checkout, so payment availability never blocks order placement.
The rollout, webhook, refund, reconciliation, and outage procedure is maintained in
[`docs/operations/payment-rollout-runbook.md`](docs/operations/payment-rollout-runbook.md).

SMS delivery is isolated behind `ISmsSender`. To add a provider, implement that contract,
register the adapter in dependency injection, and expose a provider-specific authenticated
delivery-status webhook that normalizes updates through `NotificationService`.

See the checked-in `.env.example` and `appsettings.Example.json` files for the complete key list.

## Database Changes and Menu Data

Direct `dotnet` API startup does not apply migrations or seed data. Backend container startup applies pending migrations before launching the API, but never seeds menu data.

Create a migration locally:

```bash
dotnet ef migrations add MigrationName --project CoffeeShopApi
```

Apply migrations from a published application:

```bash
dotnet CoffeeShopApi.dll migrate
```

The migration command takes a PostgreSQL advisory lock, applies pending EF migrations, and exits. Render invokes it as a pre-deploy command on paid services. The backend Docker entrypoint also runs it before starting the API so free services, which do not support pre-deploy commands, cannot launch against an outdated schema. Running both is safe because EF migrations are idempotent and the advisory lock serializes concurrent attempts.

Menu seeding is intentionally separate from schema migration. The destructive
default-menu reset is available only in Development (and automated Testing),
never in a deployed Production or Staging API. In Development, use `/admin` →
**Bulk Menu Operations** → **Seed Default Menu**, or send an authenticated
`POST /api/admin/menu/reset-to-defaults` request with this JSON body:

```json
{ "confirmation": "RESET DEFAULT MENU" }
```

For a new production database, import an explicitly reviewed menu JSON backup
through the authenticated bulk-menu workflow instead of enabling default-menu
reset behavior.

## Testing and Quality Checks

Run the fast backend suite. PostgreSQL-only tests skip unless their required
environment variables are set:

```bash
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

Run the backend exactly as CI does, including migration, physical-schema,
data-access, RLS, retention, readiness, and migration-lock contracts. The
wrapper starts and removes its own PostgreSQL 17 container and the suite creates
and drops isolated databases inside it:

```bash
scripts/ci/with-postgres.sh \
  dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

The harness rejects non-loopback hosts and any database or user other than the
dedicated disposable test identities, then verifies the container's unique run
identity before creating a database. The wrapper also requires every
PostgreSQL contract to execute, preventing CI from silently omitting database
release coverage.

Collect meaningful backend coverage locally:

```bash
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj \
  --settings coverlet.runsettings \
  --collect:"XPlat Code Coverage" \
  --results-directory CoffeeShopApi.Tests/TestResults/backend
coverage_report=$(rg --files CoffeeShopApi.Tests/TestResults/backend | \
  rg '/coverage\.cobertura\.xml$' | head -n 1)
python3 scripts/ci/backend_coverage.py "$coverage_report"
```

Generated EF migrations, model snapshots, and designer files are excluded from
application coverage. Their behavior remains protected by PostgreSQL migration
and schema tests. The summary command reports application, controller, service,
and security-path coverage and fails below the checked-in CI floors.

Run the complete frontend gate:

```bash
cd roast66
npm ci
npm test
npm run lint
npm run build
npm audit --audit-level=high
```

Run every local release gate, including a disposable PostgreSQL 17 integration
server and the production Docker/browser smoke, from the candidate checkout:

```bash
npx playwright install chromium
scripts/ci/full-local-smoke.sh /path/to/baseline-checkout
```

Use a clean checkout of the PR base revision as the baseline so the smoke proves
that the candidate can upgrade the schema currently deployed. With no argument,
the script uses the candidate as its own baseline, which is useful for a clean
installation check but does not exercise a real version-to-version upgrade.

The full smoke runs the coverage reporter tests; all backend tests with required
PostgreSQL contracts and coverage gates; frontend tests, lint, build, and audit;
then the Render release contracts. The release portion builds the production
backend image, upgrades representative baseline data, proves migration failure
prevents startup, verifies migration ordering and idempotence, proves the
representative order graph and price/name snapshots survive the upgrade and
restart, calls readiness and menu endpoints, and renders the built frontend in
Chromium.

The production frontend build performs TypeScript checks before Vite bundles the application.
The complete critical-scenario matrix, CI gate ownership, coverage policy, and
focused commands are in
[`docs/operations/testing-and-release-readiness.md`](docs/operations/testing-and-release-readiness.md).

## Render Deployment

- [`render.dev.yaml`](render.dev.yaml) reuses the developer-owned free API and
  static frontend with the mock-only Supabase PostgreSQL development database.
- [`render.prod.yaml`](render.prod.yaml) creates client-owned production resources:
  a paid always-on Docker API, paid PostgreSQL 17, and a static frontend in the
  protected Production environment.

Render setup must select the appropriate custom Blueprint path. Development
accepts free-tier cold starts and deploys from `dev`. Production follows `prod`
with automatic deploys disabled; deployments are manual after branch promotion.
Populate every `sync: false` value in the owning Render workspace and never put
credentials in Git or documentation.

The complete account-creation, fresh production bootstrap, verification,
release, alerting, and recovery procedure is in
[`docs/operations/production-hosting-and-recovery.md`](docs/operations/production-hosting-and-recovery.md).

## Operations Runbook

Application-log contents, PII boundaries, retention, and exposure response are
defined in [`docs/operations/logging-and-data-retention.md`](docs/operations/logging-and-data-retention.md).

The bounded admin order-list contract, fixed 50-order page decision, completion
visibility window, and search/filter semantics are defined in
[`docs/operations/admin-order-history.md`](docs/operations/admin-order-history.md).

### Lost or shared staff device

1. An Owner disables the affected named account in `/admin` → **Staff**.
2. Confirm that account's existing token receives `401` from an admin endpoint.
3. Reset its password before re-enabling it, if the account will be reused.
4. Confirm another staff account remains signed in and record the response time.

Disabling or resetting a named account refreshes its security stamp and revokes all of
that account's sessions without disrupting other staff. Rotate `Jwt__Key` only when
the signing key itself may be exposed; that emergency action still signs out everyone.

### Migration, rollback, and restore

Production recovery, replacement-first restore, catastrophic rebuild, and manual
release procedures are maintained in
[`docs/operations/production-hosting-and-recovery.md`](docs/operations/production-hosting-and-recovery.md).
The [`Supabase runbook`](docs/operations/supabase-database-runbook.md) applies
only to the disposable shared development database.

### Monitoring and automation

- Liveness endpoint: `GET /api/health` checks only that the API process can respond.
- Readiness endpoint: `GET /api/health/ready` checks the required database connection and confirms that no EF migrations are pending; Render uses it for routing.
- Payment, SMS, email, and push targets do not gate readiness.
- GitHub Actions run tests and security checks on pushes and pull requests.
- Dependabot monitors npm and NuGet dependencies.
- CodeQL scans C# and JavaScript/TypeScript.

Use Render health checks and client-owned alert destinations for production incidents.

## License

This project is licensed under the MIT License.
