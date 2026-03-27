# Roast66 Coffee — Production Readiness Assessment & Client Handoff Plan

**Assessment Date:** March 17, 2025  
**Status:** Ready for deployment with minor fixes recommended

---

## Executive Summary

The Roast66 Coffee application is **substantially production-ready**. Core features (menu, orders, admin, notifications) are implemented with validation, rate limiting, health checks, and structured logging. A few robustness fixes and deployment steps remain before client handoff.

---

## 1. Code Robustness Review

### ✅ Strengths

| Area | Status | Notes |
|------|--------|-------|
| **API validation** | ✅ | MenuItem, Order, OrderItem, LoginModel, NotificationSettingsModel validated |
| **Integration tests** | ✅ | 20 tests pass (order creation, duplicate detection, menu CRUD, admin login, validation, rate limits) |
| **Duplicate order detection** | ✅ | Same customer + same content within 2 min → 409; configurable via `Order:DuplicateDetectionWindowMinutes` |
| **Rate limiting** | ✅ | Login (5/min), Order (30/min) per IP in production |
| **CORS** | ✅ | Fail-fast if `AllowedOrigins` missing in production |
| **Secrets** | ✅ | `appsettings.json` gitignored; use env vars in production |
| **Health check** | ✅ | `/api/health` for liveness probes |
| **Logging** | ✅ | Serilog with configurable levels |
| **Database** | ✅ | Migrations, RLS, EF Core with PostgreSQL |

### ✅ Issues Fixed (Production Ready)

| Issue | Severity | Resolution |
|-------|----------|------------|
| **Duplicate order endpoint, one unrate-limited** | High | Added `[EnableRateLimiting("Order")]` to `AdminController.PostOrder`. |
| **Health check path mismatch** | Low | Updated `render.yaml` to use `/api/health` (standard, no auth). |
| **render.yaml AllowedOrigins default** | Low | Updated default to `https://roast66-web.onrender.com`. |
| **JWT TokenExpiryInHours = 0** | Medium | `GenerateToken` now falls back to 1 hour when value is 0 or unparseable; `render.yaml` sets `Jwt__TokenExpiryInHours=16` for event-day sessions. |

### 📝 Minor / Optional

- **Frontend test coverage**: Expanded to 23 tests (Button, FormInput, Card, Loading, AdminLogin, Menu, App, PrivateRoute).
- **File naming**: Fixed — `ApplicationDBContext.cs` → `ApplicationDbContext.cs`, `NotifcationSettingsService.cs` → `NotificationSettingsService.cs`.

---

## 2. TODO.md Alignment

### Before Going Live (from TODO.md)

| Task | Status | Action |
|------|--------|--------|
| Deploy to Render | finished | Follow plan in Section 4 below |
| Rotate credentials | finished | Do if `appsettings.json` ever had real secrets |
| Local dev setup | done | README documents `appsettings.Example.json` and `CoffeeShopApi/.env` for Docker; added `CoffeeShopApi/.env.example` |

### Optional (Phase 4)

| Task | Status |
|------|--------|
| Expand frontend test coverage | Done | Added tests for Button, FormInput, Card, Loading, AdminLogin, Menu (23 tests total) |
| Database backups |FINISHED| Render provides; verify retention in Dashboard |

---

## 3. Architecture Snapshot

```
Frontend (React)          Backend (.NET 8 API)        Database
roast66/                  CoffeeShopApi/               PostgreSQL
  - OrderPage → POST /api/admin/orders  ← AdminController (rate-limited)
  - OrderPage → GET  /api/menu         ← MenuController
  - ViewOrders → GET /api/admin/orders ← AdminController
  - Admin login → POST /api/admin/login
  - Order lookup → GET /api/order/lookup
  - Order Status prepay → POST /api/payments/checkout-session (existingOrderId) → Stripe Checkout → webhook marks order paid
```

Order submission uses `POST /api/admin/orders` (AdminController) with rate limiting applied.

---

## 4. Client Handoff Plan

### Phase 1: Pre-Deployment (1–2 hours)

1. **Robustness fixes applied**
   - Rate limiting on `AdminController` POST orders.
   - `render.yaml` health check aligned to `/api/health`.

2. **Verify local setup**
   - `dotnet test` — all tests pass.
   - `docker-compose up --build` — full stack runs.
   - Seed menu via Admin → Bulk Menu Operations.

3. **Prepare credentials**
   - Generate strong `Admin__Password`.
   - Generate 32+ char `Jwt__Key`.
   - Confirm `Jwt__TokenExpiryInHours=16`.

### Phase 2: Deploy to Render (30–60 min)

1. Push code to GitHub.
2. In Render Dashboard: New → Blueprint → connect repo.
3. Apply `render.yaml` (creates `roast66-db`, `roast66-api`, `roast66-web`).
4. Set environment variables (marked `sync: false`):
   - **roast66-api:** `Admin__Username`, `Admin__Password`, `Jwt__Key`, `AllowedOrigins`
   - **roast66-web:** `REACT_APP_API_URL` (e.g. `https://roast66-api.onrender.com/api`)
5. First deploy will use placeholder URLs. After deploy:
   - Set `AllowedOrigins` to actual frontend URL (e.g. `https://roast66-web.onrender.com`).
   - Set `REACT_APP_API_URL` to actual API URL.
   - Redeploy both services.
6. Seed menu: Admin login → Bulk Menu Operations → Seed Default Menu.

### Phase 3: Post-Deployment Verification

- [x ] Health: `GET https://roast66-api.onrender.com/api/health` returns 200.
- [ x] Menu: `GET https://roast66-api.onrender.com/api/menu` returns items (after seed).
- [x ] Place test order from frontend.
- [ x] Admin login, view orders, advance status.
- [ x] Order status lookup with order ID + phone.

### Phase 4: Client Handoff

1. **Credentials**
   - Provide admin username/password securely.
   - Document how to change password (update `Admin__Password` in Render, redeploy).

2. **Documentation**
   - README (deployment, env vars).
   - This document (PRODUCTION_READINESS.md).

3. **Ongoing**
   - Database backups: confirm Render retention (e.g. 7 days free tier).
   - Monitor `/api/health` and logs in Render Dashboard.

---

## 5. Free-Tier Resilience Options

When on Render free tier (or similar), services spin down after inactivity and there is no built-in redundancy. These options can help at no cost:

| Option | What it does | Effort |
|--------|--------------|--------|
| **UptimeRobot** | Free uptime monitoring (pings every 5 min). Alerts you when the API is down. | 5 min setup |
| **Neon PostgreSQL** | Free Postgres with external connections. Use Neon as DB instead of Render’s; deploy API to both Render and Fly.io pointing at Neon. Gives API redundancy. | 1–2 hrs |
| **Fly.io mirror** | Deploy the API to Fly.io (free tier) as a second instance. Both point to the same DB (Neon or Render Postgres if it allows external connections). Use a DNS failover service (e.g. free tier of a DNS provider) to switch traffic if Render is down. | 2–3 hrs |
| **Oracle Cloud Always Free** | 2 ARM VMs, 200GB storage. Run API + DB there as a backup. More setup, but always-on. | 4+ hrs |

**Practical starting point:** Set up **UptimeRobot** to monitor `/api/health`. When you’re ready for redundancy, move the DB to **Neon** (free) and deploy the API to both Render and Fly.io.

---

## 6. Checklist Summary

| Category | Items |
|----------|-------|
| **Fixed** | Rate limit, health path, AllowedOrigins, JWT expiry fallback, duplicate order detection |
| **Deploy** | Render Blueprint, env vars, seed menu |
| **Handoff** | Credentials, README, this doc |

---

## 7. Troubleshooting

### Login returns 500 or "Invalid credentials" (server error)

**Cause:** `Jwt__Key` is missing or shorter than 32 characters. The API validates credentials correctly but fails when signing the JWT token.

**Fix:** In Render Dashboard → roast66-api (or your API service) → Environment:
1. Add or update `Jwt__Key` with a secret string **at least 32 characters** (e.g. `openssl rand -base64 32`).
2. Redeploy the API service.

If `Jwt__Key` is missing, the app will now fail at startup with a clear error instead of failing at login time.

---

## 8. Quick Reference — Render Env Vars

**Backend (roast66-api)**
```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<your_postgres_connection_string>
Admin__Username=<admin_username>
Admin__Password=<strong_password>
Jwt__Key=<min_32_chars_base64_or_random>
Jwt__Issuer=Roast66Coffee
Jwt__Audience=Roast66Coffee
Jwt__TokenExpiryInHours=16
AllowedOrigins=https://roast66coffee-frontend.onrender.com
Order__DuplicateDetectionWindowMinutes=2
```

**Frontend (roast66-web)**
```
REACT_APP_API_URL=https://roast66coffee.onrender.com/api
REACT_APP_ENABLE_STRIPE_CHECKOUT=false
```

**Keepalive / Warmup (roast66-api)**
```
KeepAlive__Enabled=true
KeepAlive__ProbeIntervalMinutes=4
KeepAlive__ActiveWindowMinutes=10
KeepAlive__SupabaseHeartbeatUrl=<optional_supabase_rest_url>
KeepAlive__SupabaseServiceRoleKey=<optional_service_role_key>
```

**Payments (roast66-api)**
```
Stripe__SecretKey=<stripe_secret_key>
Stripe__WebhookSecret=<stripe_webhook_secret>
Stripe__FrontendBaseUrl=https://roast66-web.onrender.com
```

**SMS notifications (roast66-api)**
```
Twilio__AccountSID=<twilio_account_sid>
Twilio__AuthToken=<twilio_auth_token>
Twilio__FromPhoneNumber=<twilio_long_code_or_toll_free>
Twilio__AdminPhoneNumber=<fallback_admin_phone>
Twilio__MessagingServiceSid=<twilio_messaging_service_sid>
Twilio__StatusCallbackUrl=https://<api-host>/api/admin/notifications/twilio-status-callback
```

**Stripe account (production):** All card charges go to the Stripe account that issued the **secret key** you configure (`sk_live_...` for production, `sk_test_...` for sandbox). There is no separate “app link” beyond using that account’s API keys in Render. Add a **webhook endpoint** in the Stripe Dashboard (Developers → Webhooks) pointing to `https://<roast66-api-host>/api/payments/webhook`, subscribing at minimum to `checkout.session.completed` and `payment_intent.payment_failed`, and paste the endpoint signing secret into `Stripe__WebhookSecret`. For local testing, run `stripe listen --forward-to http://localhost:<port>/api/payments/webhook` and use the CLI’s webhook secret in `Stripe__WebhookSecret`.

**Forgot password alerts (roast66-api):**
```
Resend__ApiKey=<resend_api_key>
Resend__From=<verified_sender@example.com>
Support__AlertEmail=<family_tech_email>
```

**Current status:** backend and frontend baseline env vars are configured in Render. Keep secrets redacted in docs and rotate before first customer service.

**Credential rotation:**
- Update `Admin__Username` / `Admin__Password` in Render -> `roast66-api` -> Environment -> Redeploy.
- Rotate `Jwt__Key` any time a device is lost; this immediately invalidates active sessions.

**Session security note:** If a trailer device is lost, rotate `Jwt__Key` in Render immediately. This invalidates all active sessions (admin and cash views) and forces fresh login.

---

## 9. Staging, Backups, and Rollback Runbook

### Staging parity checklist

- Provision a staging stack with the same env var keys as production.
- Run database migrations on staging before production deploy.
- Verify smoke tests:
  - `GET /api/health`
  - `GET /api/menu`
  - place test order and status lookup
  - Stripe test checkout + webhook completion

### Backup verification

- **Daily:** confirm newest backup exists in provider dashboard.
- **Weekly:** restore latest backup into staging and run smoke tests.
- Record restore duration and any failures in operations notes.

### Incident rollback

1. Identify blast radius (frontend, API, DB, or payment webhooks).
2. Roll back API/frontend to last known-good deploy.
3. If DB corruption is suspected:
   - Restore backup into staging and verify data integrity.
   - Restore production only after staging verification passes.
4. Confirm service health via `/api/health` and core order flows.

### Owners / contacts

- **Primary owner:** Application maintainer / business operator.
- **Payment owner:** business account owner with Stripe dashboard access.
- **Escalation path:** hosting provider support -> payment provider support.

---

## 12. Notifications (Twilio) Rollout

### Twilio onboarding checklist

- Create/secure Twilio production account (2FA enabled).
- Buy sender (US long code recommended for this volume).
- Complete A2P 10DLC brand + campaign registration.
- Configure Messaging Service and attach sender.
- Set status callback URL to `/api/admin/notifications/twilio-status-callback`.

### Notification behavior (production)

- New order: sends to admin/barista/trailer (from notification settings) and customer confirmation.
- Status update to `ReadyForPickup`: sends customer pickup SMS.
- Delivery attempts are logged per order with status (`pending`, `sent`, `failed`, `retrying`, `skipped`).

### Cost estimate (US, ~500 orders/month)

- Estimated monthly Twilio spend: **$25-$60** (messages + number + carrier/A2P fees).
- Estimated Twilio upfront fees: **$20-$120** one-time (A2P onboarding/vetting range).
- Recommended client budget line item:
  - One-time implementation: **$3,000-$6,500**
  - Ongoing SMS platform: **$25-$60/month**

---

## 10. Payment Operations Checklist

- Complete Stripe account verification (KYC, business profile, payout bank).
- Configure statement descriptor and support contact email.
- Publish legal policies:
  - Terms of Service
  - Privacy Policy
  - Refund/Cancellation Policy
- Define bookkeeping cadence:
  - daily settlement reconciliation
  - weekly exception review (failed/refunded/disputed payments)
  - monthly close and export archive

---

## 11. Rollout Task List (Inside vs Outside Codebase)

Use this as the execution checklist for the 16-hour JWT session, hidden `/cash` staff view, forgot-password email alerts, and Stripe prepay flow.

### Outside Codebase (Render / Stripe / operations)

- [x] ~~Render web env vars baseline present: `REACT_APP_API_URL=https://roast66coffee.onrender.com/api`, `REACT_APP_ENABLE_STRIPE_CHECKOUT=false`.~~
- [x] ~~Render API baseline present: `ASPNETCORE_ENVIRONMENT=Production`, `AllowedOrigins=https://roast66coffee-frontend.onrender.com`, `Order__DuplicateDetectionWindowMinutes=2`.~~
- [x] ~~<span style="color:purple">**Set backend secrets in Render:** `Admin__Username`, `Admin__Password`, `Jwt__Key`, and add `Jwt__Issuer=Roast66Coffee`, `Jwt__Audience=Roast66Coffee`, `Jwt__TokenExpiryInHours=16`.</span>~~
- [x] ~~<span style="color:purple">**Fix backend connection string value** in `ConnectionStrings__DefaultConnection`.</span>~~
- [ ] <span style="color:purple">**Set Stripe API vars:** `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__FrontendBaseUrl`.</span>
- [ ] <span style="color:purple">**Set forgot-password email vars:** `Resend__ApiKey`, `Resend__From`, `Support__AlertEmail`.</span>
- [ ] <span style="color:purple">**Stripe Dashboard webhook setup:** endpoint `https://<api-host>/api/payments/webhook`, events `checkout.session.completed` + `payment_intent.payment_failed`, then copy signing secret into `Stripe__WebhookSecret`.</span>
- [ ] <span style="color:purple">**Emergency playbook drill:** practice rotating `Jwt__Key` and `Admin__Password` after a simulated lost-device incident.</span>
- [ ] <span style="color:purple">**Before first customer:** rotate `Admin__Password` and `Jwt__Key`, then redeploy API and verify `/admin` + `/cash` login.</span>

### Inside Codebase (code, tests, docs, deploy validation)

- [x] ~~Auth/session config updated to `Jwt__TokenExpiryInHours=16` defaults in code and docs.~~
- [x] ~~Staff view routing added (`/cash`) and kept hidden from public navigation links.~~
- [x] ~~Shared auth behavior implemented: `/cash` and `/admin` use same JWT login and explicit logout.~~
- [x] ~~401 handling implemented: frontend clears token and redirects to `/admin` or `/cash`.~~
- [x] ~~Forgot-password endpoint implemented: `POST /api/admin/forgot-password` with rate limiting and clear 503 response when not configured.~~
- [x] ~~Stripe prepay path implemented (`existingOrderId`) with webhook updating paid state.~~
- [x] ~~Migration files for `AddOrderStripePaymentFields` are present in codebase.~~
- [x] ~~Verification tests were run (`dotnet test`, `npm test -- --watchAll=false`) and passed during implementation.~~
