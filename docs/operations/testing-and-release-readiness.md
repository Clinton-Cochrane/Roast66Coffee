# Testing and Release Readiness

This runbook defines which tests protect production behavior, which gates must
pass, and how to reproduce CI locally. It intentionally names test files and
scenarios rather than test counts; counts change as coverage grows and do not
describe whether the important behavior is protected.

## Testing strategy

Use the cheapest test that proves the contract:

- Unit tests cover parsing, validation, state transitions, snapshots,
  idempotency, notification retries, redaction, and service decisions.
- ASP.NET integration tests send real HTTP requests through routing,
  authentication, authorization, rate limiting, serialization, and middleware.
  They use a uniquely named in-memory database for isolation.
- PostgreSQL integration tests apply the real migration chain to a newly created
  database and exercise behavior that EF InMemory cannot represent: schema,
  indexes, constraints, concurrency, migration locks, rollback, and RLS.
- The release smoke builds the production images and frontend bundle, upgrades
  representative baseline data, starts the API through its real entrypoint, and
  checks the rendered menu in Chromium.

Keep implementation code self-documenting. Add a comment when a maintainer
needs the reason behind an invariant, security boundary, concurrency decision,
provider limitation, or intentionally destructive operation. Avoid comments
that merely repeat a type, method, or statement name.

## Critical production scenario matrix

| Critical path | Executable scenarios | Primary coverage |
| --- | --- | --- |
| Order validation | Missing/invalid customer and items are rejected; unavailable items fail; retries replay only equivalent payloads; conflicting keys return conflict. | `Integration/ValidationApiTests.cs`, `OrderIdempotencyTests.cs`, `OrderIdempotencyPostgresTests.cs`, `OrderSnapshotTests.cs` |
| Authentication and authorization | Valid login returns a usable JWT; invalid credentials fail; admin routes reject anonymous callers; production refuses missing/default secrets. | `Integration/ApiIntegrationTests.cs`, `Integration/MenuLifecycleApiTests.cs`, `SecurityConfigurationTests.cs`, `JwtTokenSettingsTests.cs` |
| Rate limiting | Login and public order requests succeed below their window and return `429` after a deliberately small Testing-only window is exhausted. | `Integration/RateLimitTests.cs` |
| Menu history | Archived/deleted menu changes preserve historical order names and prices; the migration and rollback preserve representative orders. | `MenuHistoryPostgresTests.cs`, `MenuMigrationTests.cs`, `OrderSnapshotTests.cs` |
| Migrations and physical schema | EF model matches the latest snapshot; every migration applies; every mapped table/column exists; applying twice is safe; concurrent migration locking works. | `DatabaseModelContractTests.cs`, `DatabaseReleasePostgresTests.cs`, `PostgresMigrationLockTests.cs`, `scripts/ci/render-smoke.sh` |
| Retention | Old notification audit rows are purged across channels while recent rows and unrelated business records remain. | `SensitiveLoggingTests.cs`, `DatabaseReleasePostgresTests.cs`, `Integration/ApiIntegrationTests.cs` |
| Readiness | Database connection failure or pending migrations is unhealthy; optional providers do not gate readiness; the production API is not considered ready before migration. | `DatabaseReadinessHealthCheckTests.cs`, `Integration/ApiIntegrationTests.cs`, `scripts/ci/render-smoke.sh` |
| Notification failure | Provider failures are retried within bounds, partial push failure does not block other devices, and logs/audit rows omit secrets and customer payloads. | `StaffPushNotificationTests.cs`, `SensitiveLoggingTests.cs`, `Integration/StaffPushOrderIntegrationTests.cs` |
| Row-level security | After the real migration chain, explicitly granted `anon` and `authenticated` roles still see no order rows because the deny policy is active. | `DatabaseReleasePostgresTests.RowLevelSecurity_HidesOrdersFromSupabaseClientRoles` |

PostgreSQL scenarios are marked with the `PostgreSQLIntegration` trait. They
return without execution during the fast suite when no integration connection
is configured. CI and the full local smoke set
`REQUIRE_POSTGRES_INTEGRATION_TESTS=true`, making a missing connection a hard
failure rather than a skip.

## Required CI gates

The `CI` workflow runs these independent jobs on pushes and pull requests to
`main`:

| Job | Required checks and published evidence |
| --- | --- |
| Backend (.NET) | Coverage reporter unit tests; restore; complete xUnit suite with PostgreSQL required; generated-source exclusion; minimum 70% line and 50% branch application coverage; controller/service/security summary in the GitHub job summary; Cobertura XML artifact. |
| Frontend (React) | Reproducible `npm ci`; Vitest; ESLint; TypeScript plus production Vite build; npm audit failing on high or critical known vulnerabilities. |
| Render release contracts | Candidate and base checkouts; production backend image build; real baseline-to-candidate migration; fail-closed migration startup; readiness; API menu; production frontend build; Chromium menu render; restart/migration idempotence; browser traces uploaded on failure. |

CodeQL and Dependabot remain separate security automation. A green dependency
audit does not replace tests, and raw test counts do not replace the scenario
matrix above.

## Coverage policy

[`coverlet.runsettings`](../../coverlet.runsettings) excludes `Migrations/**`,
the EF model snapshot, and `*.Designer.cs`. These files are generated inputs to
the release process; counting their unexecuted scaffolding made the old raw
percentage misleading. Their outcome is instead tested by applying migrations
to PostgreSQL and inspecting the resulting schema and behavior.

[`scripts/ci/backend_coverage.py`](../../scripts/ci/backend_coverage.py) rejects
a report if generated files reappear, publishes weighted application and scope
coverage, and enforces the current floor. The line and branch floors were set
just below the measured application baseline so existing behavior is protected
immediately. Raise a floor when the stable main-branch baseline leaves enough
margin for ordinary refactoring; never lower one merely to make a pull request
green. A deliberate reduction requires an explanation in the pull request and
an equivalent critical-path test at a more appropriate layer.

Coverage identifies unexecuted code, not correctness. New or changed critical
behavior must have an explicit scenario even when the percentage already passes.

## Focused local commands

Fast backend suite (PostgreSQL contracts do not execute):

```bash
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

Complete backend suite against a dedicated local PostgreSQL server:

```bash
REQUIRE_POSTGRES_INTEGRATION_TESTS=true \
POSTGRES_INTEGRATION_CONNECTION_STRING="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres" \
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj
```

Never use a shared staging or production connection. The test harness creates
and force-drops uniquely named databases and creates inert `anon` and
`authenticated` roles when missing.

Frontend gate:

```bash
cd roast66
npm ci
npm test
npm run lint
npm run build
npm audit --audit-level=high
```

Individual backend class or frontend file:

```bash
dotnet test CoffeeShopApi.Tests/CoffeeShopApi.Tests.csproj \
  --filter FullyQualifiedName~RateLimitTests
cd roast66
npm test -- src/pages/OrderPage.test.tsx
```

## Full local smoke

Prerequisites are Docker with Compose support, .NET 8, Node.js 20+, npm,
Python 3, ripgrep, and a Chromium browser installed for Playwright:

```bash
cd roast66
npm ci
npx playwright install chromium
cd ..
```

From the candidate checkout, pass a clean checkout of the pull request's base
revision:

```bash
scripts/ci/full-local-smoke.sh /path/to/baseline-checkout
```

The script starts and removes its own PostgreSQL container, creates disposable
test databases, and leaves coverage XML in the printed `/tmp` directory. The
release-smoke portion uses uniquely named Docker networks, containers, and
images and removes them on exit. It runs `npm ci`, so uncommitted edits inside
`roast66/node_modules` are not preserved.

Omitting the baseline argument uses the candidate for both sides. That checks a
clean installation and the production stack, but it cannot prove that the
candidate upgrades the revision currently deployed.
