# Roast 66 Coffee

Roast 66 Coffee is a full-stack web application for managing a coffee shop menu, orders, and admin operations. It uses a React frontend, an ASP.NET Core Web API backend, and PostgreSQL for data storage.

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Quick Start](#quick-start)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Run the App](#run-the-app)
- [Database Migrations](#database-migrations)
- [Seed the Database](#seed-the-database)
- [Deployment (Render)](#deployment-render)
- [Production Operations](#production-operations)
- [License](#license)

## Features

- View menu items and offerings.
- Place customer orders.
- Manage menu items from an admin dashboard.
- Bulk import/export menu data as JSON.
- Seed a default menu from admin bulk operations.
- Use responsive UI across desktop and mobile.

## Tech Stack

### Frontend

- React (Vite)
- TypeScript
- React Router
- Axios
- Tailwind CSS
- React Icons
- Vitest + Testing Library

### Backend

- .NET 8 (ASP.NET Core Web API)
- Entity Framework Core
- PostgreSQL

### Hosting and Infrastructure

- Render (frontend/backend hosting)
- Docker (containerization)

## Quick Start

Use Docker Compose for the fastest local setup:

1. Copy environment files:
   - `env.example` -> `.env` (repo root, optional overrides)
   - `roast66/.env.example` -> `roast66/.env`
   - `CoffeeShopApi/.env.example` -> `CoffeeShopApi/.env`
2. Ensure backend connection string uses `Host=postgres-db` in `CoffeeShopApi/.env`.
3. Start the stack:

```bash
docker-compose up --build
```

Services:

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:5001`
- PostgreSQL: started by Compose

## Prerequisites

Install the following:

- Node.js 18+
- .NET 8 SDK
- Docker Desktop
- PostgreSQL (only required for non-Docker local runs)

## Installation

### Clone the Repository

```bash
git clone https://github.com/your-username/Roast66.git
cd Roast66
```

### Install Frontend Dependencies

```bash
cd roast66
npm install
```

## Configuration

### Frontend Environment (`roast66/.env`)

Copy `roast66/.env.example` to `roast66/.env`.

```env
VITE_API_URL=http://localhost:5001/api
VITE_ENABLE_STRIPE_CHECKOUT=false
```

### Backend Configuration (local appsettings)

For local non-Docker runs, copy:

- `CoffeeShopApi/appsettings.Example.json` -> `CoffeeShopApi/appsettings.json`

Required keys:

- `ConnectionStrings:DefaultConnection` (PostgreSQL connection string)
- `Admin:Username`, `Admin:Password` (admin credentials)
- `Jwt:Key` (minimum 32 chars), `Jwt:Issuer`, `Jwt:Audience`
- `AllowedOrigins` (comma-separated frontend URLs; required in production)

### Backend Environment (`CoffeeShopApi/.env`) for Docker

For Docker Compose, backend settings are loaded from `CoffeeShopApi/.env` (not `appsettings.json`).

- Copy `CoffeeShopApi/.env.example` -> `CoffeeShopApi/.env`
- Use `Host=postgres-db` (service name) in the connection string, not `localhost`

## Run the App

### Option A: Docker Compose

From the repository root:

```bash
docker-compose up --build
```

### Option B: Run Without Docker

Start backend:

```bash
cd CoffeeShopApi
dotnet ef database update
dotnet run
```

Backend default: `http://localhost:80`  
Set `PORT` to use another port.

Start frontend:

```bash
cd roast66
npm start
```

Frontend default: `http://localhost:3000`

## Database Migrations

```bash
cd CoffeeShopApi
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Seed the Database

Use one of the following options:

- Admin UI: sign in at `/admin`, then run **Seed Default Menu** from Bulk Menu Operations.
- API (admin auth required):

```http
GET /api/Admin/seed-menu?confirm=true
```

## Deployment (Render)

This repository includes a `render.yaml` Blueprint for one-click deployment.

1. Push the repository to GitHub and connect it in Render.
2. Create a new Blueprint Instance in Render.
3. Render provisions:
   - PostgreSQL database: `roast66-db`
   - Backend API service: `roast66-api` (Docker)
   - Frontend static site: `roast66-web`
4. Configure required environment variables in Render.

### Backend Environment Variables (`roast66-api`)

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection`
- `Admin__Username`
- `Admin__Password` (use strong value)
- `Jwt__Key` (stable secret, at least 32 chars)
- `Jwt__Issuer=Roast66Coffee`
- `Jwt__Audience=Roast66Coffee`
- `Jwt__TokenExpiryInHours=16`
- `AllowedOrigins` (comma-separated frontend URLs)
- `Order__DuplicateDetectionWindowMinutes=2`

Optional:

- Stripe: `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__FrontendBaseUrl`
- Alerts/email: `Resend__ApiKey`, `Resend__From`, `Support__AlertEmail`

`Jwt__Key` note:

- Blueprint may auto-generate this value.
- If unset, the API creates a random key at startup and logs a warning.
- For production stability, set a fixed secret manually (example generation):

```bash
openssl rand -base64 48
```

### Frontend Environment Variables (`roast66-web`)

- `VITE_API_URL` (example: `https://roast66coffee.onrender.com/api`)
- `VITE_ENABLE_STRIPE_CHECKOUT=false` (set `true` when ready)
- `VITE_VAPID_PUBLIC_KEY` (optional; web push for staff devices)

Static site output is `roast66/dist/` (configure `staticPublishPath` accordingly).

### Post-Deploy Checks

1. Redeploy backend if `AllowedOrigins` needs actual frontend URL updates.
2. Redeploy frontend if `VITE_API_URL` or other `VITE_*` vars changed (they are baked in at build time).
3. Seed menu data via admin UI or `GET /api/Admin/seed-menu?confirm=true`.

## Production Operations

### Post-Deployment Operations

- Database backups: verify retention policy in Render dashboard.
- Health check endpoint: `GET /api/health`
- **GitHub automation (free):** CI runs backend and frontend tests on push/PR to `main`; Dependabot opens weekly dependency PRs; CodeQL scans C# and JavaScript for security issues. Workflows live under `.github/workflows/`.
- **Scheduled health ping (optional):** `scheduled-health-ping.yml` runs every five minutes. Set Actions secrets **`API_HEALTH_CHECK_URL`** (e.g. `https://<api-host>/api/health`) and **`HEALTH_CHECK_URL`** (frontend static URL). Optional **`SUPABASE_WAKE_URL`**. Schedules run from the default branch; GitHub may pause them after long repo inactivity—any push or re-enabling Actions refreshes that.
- **External uptime (recommended):** Use a free monitor (e.g. Better Stack, UptimeRobot) if you want alerts when the API is down; the scheduled ping does not notify on failure by default. See `PRODUCTION_READINESS.md` §5 for Neon/Fly optional redundancy.
- Keepalive mode (free-tier mitigation):
  - Admin dashboard sends `POST /api/ops/keepalive/heartbeat` while open.
  - API warmup runs in configured `KeepAlive:*` window.
  - Optional helper: `scripts/ops/keepalive-pulse.sh` (requires `ADMIN_JWT_TOKEN`).
- Stripe checkout (optional):
  - Frontend starts checkout via `POST /api/payments/checkout-session`.
  - Webhook endpoint: `POST /api/payments/webhook`.
  - Orders finalize on `checkout.session.completed`.

### Rotate Admin Password and JWT Key

1. Open `roast66-api` -> **Environment** in Render Dashboard.
2. Update `Admin__Username` and/or `Admin__Password`.
3. Rotate `Jwt__Key` when needed (recommended after security incidents).
4. Save and redeploy `roast66-api`.
5. Staff sign in again with updated credentials.

Notes:

- Rotating `Admin__Password` changes future login credentials.
- Rotating `Jwt__Key` invalidates active sessions (`/admin`, `/cash`).

### Production Runbook

- Staging parity:
  - Keep staging env var keys aligned with production.
  - Run migrations and checkout tests in staging before release.
- Backup verification cadence:
  - Daily: confirm latest backup exists.
  - Weekly: restore to staging and validate key reads (`/api/menu`, `/api/order/lookup`).
- Incident rollback:
  - Roll frontend/backend to last known-good deployment.
  - For data issues, validate restore in staging first.
- Billing guardrails:
  - Configure Render/Supabase budget alerts.
  - Review usage monthly before plan changes.

### Payments and Compliance Checklist

- Stripe business profile and KYC complete.
- Terms, Privacy Policy, and Refund/Cancellation Policy published.
- Reconciliation process defined (daily payouts, monthly close).

## License

This project is licensed under the MIT License.
