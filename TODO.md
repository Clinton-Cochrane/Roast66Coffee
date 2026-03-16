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
- [ ] Add integration tests for critical API flows (order creation, menu CRUD, admin login)
- [ ] Add API tests for validation (e.g. invalid MenuItem, invalid Order)
- [ ] Expand frontend test coverage beyond `App.test.js` and `PrivateRoute.test.jsx`

### Rate limiting
- [ ] Add rate limiting on login endpoint (e.g. AspNetCoreRateLimit or similar)
- [ ] Add rate limiting on order submission endpoint
- [ ] Configure limits appropriate for production traffic

### Operational
- [ ] Configure structured logging for production (e.g. Serilog)
- [ ] Add health check endpoint at `/api/health` (or document existing `/api/admin/ping`)
- [ ] Set up database backups (Render provides this for Postgres; verify retention)

---

## Completed

- [x] Remove secrets from version control (appsettings.json gitignored)
- [x] CORS production guard (fail fast if AllowedOrigins missing)
- [x] Docker Compose env vars for Postgres
- [x] Render Blueprint (render.yaml)
- [x] PORT support in backend for Render
- [x] README deployment documentation
- [x] API input validation (MenuItem, Order, OrderItem, LoginModel, NotificationSettingsModel)
