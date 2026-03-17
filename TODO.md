# Roast66 Coffee - Production TODO

Remaining steps to complete production readiness and post-launch improvements.

---

## Before Going Live

- [ ] **Deploy to Render**
  - Push code to GitHub and connect repository to Render
  - Create Blueprint Instance from `render.yaml`
  - Set environment variables in Render Dashboard (see README)
  - Redeploy after setting `AllowedOrigins` and `REACT_APP_API_URL` with actual URLs

- [ ] **Rotate credentials** (if `appsettings.json` ever contained real production secrets)
  - Admin password
  - JWT key
  - Database password

- [ ] **Local dev setup**
  - Ensure `CoffeeShopApi/appsettings.json` exists (copy from `appsettings.Example.json`) for new clones

---

## Optional Improvements (Phase 4)

### Integration / API tests
- [x] Add integration tests for critical API flows (order creation, menu CRUD, admin login)
- [x] Add API tests for validation (e.g. invalid MenuItem, invalid Order)
- [ ] Expand frontend test coverage beyond `App.test.js` and `PrivateRoute.test.jsx`

### Rate limiting
- [x] Add rate limiting on login endpoint (built-in .NET 8 middleware, 5 req/min per IP)
- [x] Add rate limiting on order submission endpoint (30 req/min per IP)
- [x] Configure limits appropriate for production traffic (Testing env uses 1000/min)

### Operational
- [x] Configure structured logging for production (Serilog with console sink, configurable via appsettings)
- [x] Add health check endpoint at `/api/health`
- [ ] Set up database backups (Render provides this for Postgres; verify retention)

---

## Completed

- [x] Integration tests for API flows (ApiIntegrationTests, ValidationApiTests, RateLimitTests)
- [x] Rate limiting on login and order endpoints (per-IP, configurable)
- [x] Serilog structured logging
- [x] Health check endpoint at `/api/health`
- [x] Remove secrets from version control (appsettings.json gitignored)
- [x] CORS production guard (fail fast if AllowedOrigins missing)
- [x] Docker Compose env vars for Postgres
- [x] Render Blueprint (render.yaml)
- [x] PORT support in backend for Render
- [x] README deployment documentation
- [x] API input validation (MenuItem, Order, OrderItem, LoginModel, NotificationSettingsModel)
