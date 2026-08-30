# Roast 66 Coffee

Roast 66 Coffee is the ordering and shop-operations application for Roast 66's mobile coffee business. Customers can browse the menu, build drinks, place pickup orders, and follow preparation through a private tracking link. Staff use dedicated admin and cash views to manage the menu, process the order queue, and configure notifications.

The application is a React single-page frontend backed by an ASP.NET Core API and PostgreSQL. It is designed to run locally with Docker Compose and deploy to Render from the included Blueprint.

## What the Application Does

### Customer experience

- Browse a categorized menu in English or Spanish.
- Build drinks with quantities, flavors, add-ons, and notes.
- Place pickup orders with optional email notifications.
- Detect accidental duplicate submissions within a configurable window.
- Receive a cryptographically random private tracking token after ordering.
- Follow an order from received through preparing, ready for pickup, and completed.
- Return to an active tracked order from the navigation bar on the same device.
- Optionally download an order summary.

### Staff experience

- Use `/admin` for the order queue, menu management, bulk menu operations, and notification settings.
- Use `/cash` for a streamlined shop-device workflow.
- Advance order status and trigger customer-ready notifications.
- Import, export, or explicitly seed menu data.
- Register staff devices for web-push notifications.

### Production safeguards

- Production refuses to start without explicit admin credentials and a stable JWT signing key.
- Admin sessions expire after eight hours, reject malformed or expired tokens, and synchronize logout across browser tabs.
- Public order APIs use 256-bit tracking tokens instead of sequential IDs and customer identity.
- Public tracking responses omit phone numbers, email addresses, provider IDs, and internal database fields.
- Public order creation, tracking, login, and password-support endpoints are rate limited.
- Production migrations run as a locked, one-time deployment step rather than during application startup.

Online payments and external SMS are feature-gated and disabled by default until their production integrations are approved and hardened. Payments use a provider-neutral application service; Stripe is the included gateway. SMS uses a provider-neutral sender contract with a disabled default implementation, so no SMS vendor is installed or contacted by default.

## Architecture

| Area | Technology |
| --- | --- |
| Frontend | React 18, TypeScript, React Router 7, Axios, Tailwind CSS, Vite |
| Backend | .NET 8, ASP.NET Core Web API, Entity Framework Core |
| Database | PostgreSQL |
| Authentication | JWT bearer tokens for staff routes |
| Testing | xUnit, ASP.NET integration tests, Vitest, Testing Library |
| Deployment | Docker and Render Blueprint |
| Automation | GitHub Actions, Dependabot, CodeQL |

Repository layout:

```text
CoffeeShopApi/        ASP.NET Core API, EF models, migrations, and services
CoffeeShopApi.Tests/  Backend unit and API integration tests
roast66/              React frontend and frontend tests
scripts/ops/          Health-check and keepalive helpers
render.yaml           Render database, API, and static-site Blueprint
docker-compose.yml    Local frontend, backend, and PostgreSQL stack
```

## Local Development

### Prerequisites

- Docker with Docker Compose for the recommended setup
- Node.js 20+ and npm for frontend-only development
- .NET 8 SDK for backend development and EF migrations
- PostgreSQL when running the backend without Docker

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
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Admin__Username` | Staff login username |
| `Admin__Password` | Staff login password |
| `Jwt__Key` | Stable signing secret of at least 32 characters |
| `Jwt__Issuer` | JWT issuer; normally `Roast66Coffee` |
| `Jwt__Audience` | JWT audience; normally `Roast66Coffee` |
| `Jwt__TokenExpiryInHours` | Staff session duration; production default is `8` |
| `AllowedOrigins` | Comma-separated frontend origins allowed by CORS |

Development and testing have explicit local credential defaults. Production has none and will fail during startup if required authentication settings are missing or unsafe.

Generate a production JWT key with:

```bash
openssl rand -base64 48
```

Keep this value stable during normal deployments. Rotating it immediately invalidates every active staff session.

### Frontend settings

| Setting | Purpose |
| --- | --- |
| `VITE_API_URL` | API base URL including `/api`, with no trailing slash |
| `VITE_USE_STATIC_MENU` | Overrides menu data selection. Localhost defaults to the bundled JSON snapshot; set `false` to force API reads or `true` to use the snapshot on any host. |
| `VITE_ENABLE_ONLINE_PAYMENTS` | Enables the configured online checkout UI when set to `true` |
| `VITE_VAPID_PUBLIC_KEY` | Optional public key for staff web push |

Vite settings are embedded at build time, so changing them requires rebuilding the static site.

### Optional integrations

- Resend: customer and support email delivery
- Web Push/VAPID: staff-device notifications
- Payments: provider-neutral checkout with Stripe as the included gateway, disabled by default
- SMS: provider-neutral sender contract with no provider installed, disabled by default
- Supabase heartbeat: optional free-tier connection warmup

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

Normal API startup never applies migrations or seeds data.

Create a migration locally:

```bash
dotnet ef migrations add MigrationName --project CoffeeShopApi
```

Apply migrations from a published application:

```bash
dotnet CoffeeShopApi.dll migrate
```

The migration command takes a PostgreSQL advisory lock, applies pending EF migrations, and exits. Render invokes it as a pre-deploy command so migrations finish before new application instances receive traffic.

Menu seeding is intentionally separate from schema migration. Use either:

- `/admin` → **Bulk Menu Operations** → **Seed Default Menu**
- Authenticated `GET /api/Admin/seed-menu?confirm=true`

## Testing and Quality Checks

Run backend tests:

```bash
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

Run the PostgreSQL migration-lock integration test against a disposable database:

```bash
REQUIRE_POSTGRES_INTEGRATION_TESTS=true \
POSTGRES_INTEGRATION_CONNECTION_STRING="$STAGING_DATABASE_URL" \
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj \
  --filter FullyQualifiedName~PostgresMigrationLockTests
```

Run the complete frontend gate:

```bash
cd roast66
npm test -- --run
npm run lint
npm run build
npm audit
```

The current baseline is 41 backend tests and 67 frontend tests. The production frontend build performs TypeScript checks before Vite bundles the application.

## Render Deployment

[`render.yaml`](render.yaml) provisions:

- `roast66-db`: PostgreSQL
- `roast66-api`: Docker-based ASP.NET API
- `roast66-web`: Vite static frontend

Deployment flow:

1. Connect the GitHub repository to Render and create a Blueprint instance.
2. Populate every `sync: false` secret in the Render dashboard.
3. Confirm `Admin__Username`, `Admin__Password`, and `Jwt__Key` before the first production deployment.
4. Confirm `AllowedOrigins`, `Payments__FrontendBaseUrl`, and `VITE_API_URL` use the actual Render URLs.
5. Let the API pre-deploy command apply migrations.
6. Seed the menu explicitly if this is a new database.

Post-deploy verification:

1. `GET /api/health/ready` succeeds, confirming the API can connect to PostgreSQL.
2. `GET /api/health` succeeds, confirming the API process is responsive.
3. The menu loads from the public frontend.
4. Staff can sign in at both `/admin` and `/cash`.
5. A test order can be placed and retrieved using its private tracking link.
6. An unauthenticated request to an admin endpoint returns `401`.

## Operations Runbook

### Lost or shared staff device

1. Change `Admin__Password` and `Jwt__Key` in Render immediately.
2. Redeploy the API.
3. Confirm an old token receives `401` from an admin endpoint.
4. Confirm the previous password can no longer sign in.
5. Sign trusted staff devices in again and record the rotation time.

JWT rotation invalidates all staff devices because the application does not currently maintain per-device revocation records.

### Migration, rollback, and restore

The provider-specific commands and decision criteria are maintained in [`docs/operations/supabase-database-runbook.md`](docs/operations/supabase-database-runbook.md).

1. Confirm a current backup exists before deploying a migration.
2. Prefer additive expand/migrate/contract changes that remain compatible during deployment.
3. Verify health, menu reads, admin login, and a known tracking token after migration.
4. Roll application images back only to versions compatible with the current schema.
5. Prefer a forward fix for schema problems; run a migration `Down` only when its data safety is proven.
6. Restore backups into staging first and validate migration history, row counts, tracking, and payment identifiers.
7. Perform and record a staging restore drill at least quarterly.

### Monitoring and automation

- Liveness endpoint: `GET /api/health` checks only that the API process can respond.
- Readiness endpoint: `GET /api/health/ready` checks the required database connection and is used by Render for routing.
- Payment, SMS, email, push, Supabase, and keep-alive targets do not gate readiness.
- GitHub Actions run tests and security checks on pushes and pull requests.
- Dependabot monitors npm and NuGet dependencies.
- CodeQL scans C# and JavaScript/TypeScript.
- The optional scheduled health workflow uses `API_HEALTH_CHECK_URL` and `HEALTH_CHECK_URL` repository secrets.
- `scripts/ops/keepalive-pulse.sh` can keep the API warm using `API_BASE_URL` and `ADMIN_JWT_TOKEN`.

Use an external uptime monitor when alert delivery is required; the scheduled GitHub health ping does not provide a full incident-alerting service.

## License

This project is licensed under the MIT License.
