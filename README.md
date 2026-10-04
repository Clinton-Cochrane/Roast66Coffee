# Roast 66 Coffee

A full-stack pickup-ordering application for a mobile coffee business, built with
React, TypeScript, ASP.NET Core, and PostgreSQL. Customers browse the menu in
English or Spanish, customize drinks, and track orders through a private link.
Staff manage the menu, order queue, accounts, and notifications through `/admin`
and `/cash`.

[Quick start](#quick-start) · [Development](#development) ·
[Architecture](#architecture) · [Checks](#checks) ·
[Deployment and runbooks](#deployment-and-runbooks)

## Quick start

### Full application with Docker Compose

Requires Docker with the Compose plugin and a running Docker daemon. Run these
commands from the repository root:

```bash
git clone https://github.com/Clinton-Cochrane/Roast66Coffee.git
cd Roast66Coffee
cp env.example .env
cp CoffeeShopApi/.env.example CoffeeShopApi/.env
cp roast66/.env.example roast66/.env
scripts/ci/local-preflight.sh compose
docker compose up --build -d
```

The initializer applies migrations, seeds the menu if it is empty, and creates
a local Owner account. Existing menu and order data are preserved.

| Service | Local URL |
| --- | --- |
| Frontend | <http://localhost:3000> |
| Staff login | <http://localhost:3000/admin> |
| API base | <http://localhost:5001/api> |
| Liveness | <http://localhost:5001/api/health> |
| Database readiness | <http://localhost:5001/api/health/ready> |

With the example configuration, sign in as `admin` with password
`Development1!`. These credentials are for local development only. If you change
the database credentials in the root `.env`, update the connection string in
`CoffeeShopApi/.env` to match; its Docker hostname must stay `postgres-db`.

Useful Compose commands:

```bash
docker compose logs -f backend
docker compose down
```

`docker compose down` keeps the database volume. To permanently delete local
orders, accounts, and menu edits and initialize a fresh database:

```bash
docker compose down --volumes
docker compose up --build -d
```

### Frontend only

For menu and layout work, the bundled menu snapshot lets you run without an API
or database. Requires Node.js 24 (the pinned version is in
[`.node-version`](.node-version)) and npm.

```bash
cd roast66
cp .env.example .env
npm ci
npm run dev:static
```

Open the URL printed by Vite, normally <http://localhost:5173>. Ordering, staff
login, and other API-backed actions require the full stack.

## Development

Compose builds and serves the frontend through Nginx; it does not provide hot
reload. For frontend hot reload against the Compose API, start the stack above,
then stop the Compose frontend from the repository root to free port 3000:

```bash
docker compose stop frontend
cd roast66
npm ci
VITE_USE_STATIC_MENU=false npm run dev -- --port 3000 --strictPort
```

This port matches the API's example CORS configuration. Keep
`VITE_API_URL=http://localhost:5001/api` in `roast66/.env`. Using API menu data
ensures menu IDs match the database when submitting orders.

### Run the API without Docker

Requires the .NET 10 SDK and a local PostgreSQL database. Create an empty database
and a user that owns it, then copy the application settings:

```bash
cp CoffeeShopApi/appsettings.Example.json CoffeeShopApi/appsettings.json
```

Edit `ConnectionStrings:DefaultConnection` for your local database and set
`AllowedOrigins` to `http://localhost:5173` for Vite's default port. The `.env`
files are consumed by Compose and Vite; `dotnet run` does not load the backend
`.env` file.

From the API directory, initialize the schema, menu, and local Owner, then start
the API:

```bash
cd CoffeeShopApi
export ASPNETCORE_ENVIRONMENT=Development
export PORT=5001
dotnet run --no-launch-profile -- initialize-local
dotnet run --no-launch-profile
```

In another terminal, run the frontend:

```bash
cd roast66
cp .env.example .env
npm ci
VITE_USE_STATIC_MENU=false npm run dev
```

Ordinary API startup does not migrate or seed the database. `initialize-local`
is Development-only; use it for first-time local setup. `Program.cs` defaults the
API to port **8080** when `PORT` is unset.

### Configuration

The checked-in examples document available settings:

- [`env.example`](env.example): local Compose database credentials.
- [`CoffeeShopApi/.env.example`](CoffeeShopApi/.env.example): backend Compose settings.
- [`CoffeeShopApi/appsettings.Example.json`](CoffeeShopApi/appsettings.Example.json): native backend settings.
- [`roast66/.env.example`](roast66/.env.example): frontend settings.

ASP.NET environment variables use double underscores for nested keys, such as
`ConnectionStrings__DefaultConnection` and `Jwt__Key`.

| Setting | What developers need to know |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string; use `postgres-db` inside Compose and `localhost` for a host database. |
| `AllowedOrigins` | Comma-separated frontend origins allowed by API CORS; match your Vite or Compose URL. |
| `Jwt__Key` | Staff-token signing key; production requires a stable secret of at least 32 characters. |
| `VITE_API_URL` | API base URL including `/api`, with no trailing slash. |
| `VITE_USE_STATIC_MENU` | `true` selects `public/data/menu.json`; `false` selects the API. Local development defaults to the snapshot; Compose forces API reads. |
| `VITE_ENABLE_ONLINE_PAYMENTS` | Enables checkout UI when `true`; disabled in the example settings. |

Vite configuration is embedded at build time. Rebuild the frontend after changing
settings for a Docker or hosted build. Keep local `.env` and `appsettings.json`
files out of Git.

Email uses Resend, staff web push uses VAPID, and online payments use a
provider-neutral service with a Stripe adapter. SMS has a disabled default
implementation with no provider installed. External integrations are optional
for local development; see the runbooks before enabling payments or SMS.

### Database migrations

Install the EF Core CLI matching the backend's major version if needed:

```bash
dotnet tool install --global dotnet-ef --version '10.*'
```

Run EF commands from `CoffeeShopApi/` so the design-time factory finds your local
`appsettings.json`:

```bash
dotnet ef migrations add DescribeTheChange
dotnet ef database update
```

Commit generated migrations with the model change. Migrations update the schema;
menu seeding is a separate operation. Production menu data is imported through
the authenticated bulk-menu workflow. Bootstrap and migration procedures are in
the [hosting runbook](docs/operations/production-hosting-and-recovery.md).

## Architecture

| Layer | Stack |
| --- | --- |
| Frontend | React 18, TypeScript, React Router 7, Axios, Tailwind CSS, Vite |
| API | .NET 10, ASP.NET Core, Entity Framework Core |
| Database | PostgreSQL 17 in Compose and production |
| Staff authentication | ASP.NET Core Identity and JWT bearer tokens |
| Tests | xUnit, ASP.NET integration tests, Vitest, Testing Library, Playwright |
| Delivery | Docker, Render Blueprints, GitHub Actions |

```text
CoffeeShopApi/
  Controllers/        HTTP endpoints
  Services/           Ordering, menu, staff, payments, and notifications
  Data/               EF context, database initialization, migration runner
  Models/             Entities and API contracts
  Migrations/         Versioned database schema
CoffeeShopApi.Tests/   Backend unit, HTTP integration, and PostgreSQL tests
roast66/
  src/pages/          Customer and staff screens
  src/components/     Shared UI and workflow components
  src/lib/            Shared order and transport behavior
  src/i18n/           English and Spanish strings
  public/             Static assets and bundled menu snapshot
  e2e/                Browser smoke tests
docs/operations/      Release, deployment, and operational runbooks
scripts/ci/           Preflight, database test harness, coverage, release smoke
```

Start with [`Program.cs`](CoffeeShopApi/Program.cs) and
[`Startup.cs`](CoffeeShopApi/Startup.cs) for API startup and service registration,
and [`App.tsx`](roast66/src/App.tsx) for frontend routes.

Core behavior to preserve when changing the application:

- **Orders:** `OrderService` validates live menu availability and snapshots names
  and prices so later menu edits do not rewrite order history.
- **Retries:** `POST /api/order` requires a client-generated `X-Idempotency-Key`
  of at most 128 characters. Equivalent retries return the existing order;
  conflicting payloads return `409`. Intentional repeat orders need a new key.
- **Tracking:** public lookup uses a random private token. Public responses omit
  customer contact details and internal database fields.
- **Staff access:** named accounts control staff routes. Disabling an account or
  resetting its password revokes that account's sessions.
- **Startup:** container startup applies migrations under a PostgreSQL advisory
  lock before serving traffic. Readiness requires database connectivity and no
  pending migrations.

## Checks

Run commands from the repository root unless shown otherwise. The fast backend
suite skips PostgreSQL contracts when their integration environment is absent:

```bash
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

For the complete backend suite, the wrapper starts and removes its own disposable
PostgreSQL 17 container:

```bash
scripts/ci/with-postgres.sh \
  dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

Frontend checks:

```bash
cd roast66
npm ci
npm test
npm run lint
npm run build
npm audit --audit-level=high
```

GitHub Actions checks backend coverage, frontend tests/lint/build, dependency
security, and production release contracts. CodeQL and Dependabot provide
additional security automation. For coverage commands, focused tests, browser
setup, and the full baseline-to-candidate release smoke, see
[Testing and Release Readiness](docs/operations/testing-and-release-readiness.md).

## Contributing

Use a focused topic branch and open a pull request against `main`. Describe the
problem, resulting behavior, and validation performed. Add tests for behavior
changes, include migrations for schema changes, and commit `package-lock.json`
when changing frontend dependencies. Generated builds and local credentials
stay out of commits.

## Deployment and runbooks

Render deploys use [`render.dev.yaml`](render.dev.yaml) for shared development
from `dev` and [`render.prod.yaml`](render.prod.yaml) for production from `prod`.
Production deployments are manual after branch promotion. Deployment requires
environment-specific secrets and first-Owner initialization; follow the hosting
runbook for the complete procedure.

| Guide | Covers |
| --- | --- |
| [Testing and release readiness](docs/operations/testing-and-release-readiness.md) | Critical scenarios, CI gates, coverage, and release smoke |
| [Production hosting and recovery](docs/operations/production-hosting-and-recovery.md) | Render setup, bootstrap, releases, backups, and recovery |
| [Staff authentication](docs/operations/staff-authentication.md) | Account setup, session revocation, and recovery |
| [Payment rollout](docs/operations/payment-rollout-runbook.md) | Checkout configuration, webhooks, refunds, and outages |
| [Logging and data retention](docs/operations/logging-and-data-retention.md) | Sensitive-data boundaries and retention rules |
| [Admin order history](docs/operations/admin-order-history.md) | Pagination, visibility, search, and filters |
| [Development database](docs/operations/supabase-database-runbook.md) | Shared development database operations |
