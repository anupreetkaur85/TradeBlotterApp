# Testing and Quality Plan

## Test pyramid

Prioritize fast domain tests, then API integration tests, then a small number
of frontend component/workflow tests.

```text
Many:  pure NUnit domain tests
Some:  API + real SQL Server integration tests
Some:  Pinia and Vue component tests
Few:   browser end-to-end smoke tests
```

## TDD workflow

For each behavior:

1. Write a failing test describing the observable rule.
2. Implement the smallest correct behavior.
3. Refactor while keeping tests green.
4. Add boundary/error tests before moving to the next layer.

Commit in coherent slices where practical: domain behavior, persistence,
endpoints, state management, and UI.

## Backend unit tests

Use NUnit for all .NET test projects. The highest-value suite is the pure
position calculator.

### Position calculator cases

| Case | Expected behavior |
|---|---|
| Single buy | Positive quantity, trade price average |
| Single sell | Negative quantity, trade price average |
| Two buys at different prices | Weighted average |
| Two sells at different prices | Weighted average for short |
| Partial long close | Quantity decreases, average unchanged |
| Partial short cover | Absolute quantity decreases, average unchanged |
| Exact close | Symbol omitted/flat |
| Long-to-short cross | Residual short at crossing trade price |
| Short-to-long cross | Residual long at crossing trade price |
| Multiple symbols | Independent accumulators |
| Out-of-order input policy | Caller/order contract is explicit |
| Quantity overflow | Controlled failure |
| Decimal precision | Expected tolerance/exact decimal result |

Use decimal assertions, not floating-point tolerances.

### Validation cases

- Symbol normalization
- Empty/invalid/too-long symbol
- Unknown side
- Zero/negative quantity
- Zero/negative price
- Price scale above supported precision
- Quantity beyond configured frontend/API limit

### Application service cases

Mock only repository boundaries:

- Valid command calls writer with normalized data.
- Invalid command never calls writer.
- Cancellation propagates.
- Repository failure is not converted into a false success.

Do not unit-test Dapper implementation details with mocks; test them against
SQL Server.

## Backend integration tests

Use `WebApplicationFactory<Program>` with configuration overrides and a real
ephemeral SQL Server:

Preferred:

- Testcontainers SQL Server when Docker is available.

Fallback:

- A dedicated LocalDB database with a unique test database name per run.

The fixture must:

1. Start/create the database.
2. Apply the same production migration scripts.
3. Create an API test host with the test connection string.
4. Reset data between tests without allowing parallel cross-test leakage.
5. Dispose/drop the test database.

### API scenarios

- `POST /trades` persists and returns `201`, normalized symbol, server
  timestamp, and notional.
- Invalid create requests return `400` ProblemDetails.
- `GET /trades` returns newest first with deterministic ties.
- `GET /positions` derives weighted averages correctly from stored trades.
- Flat symbols are omitted.
- Empty database returns empty arrays.
- Seed enable inserts the expected rows.
- Repeated seed enable does not duplicate.
- Seed disable removes only seed rows.
- Admin routes are unavailable when disabled by configuration.
- Unexpected failures return sanitized `500` ProblemDetails with trace ID.
- Readiness changes appropriately when SQL is unavailable, where practical.

These tests constitute backend end-to-end coverage from HTTP through SQL and
back.

## Frontend tests

Use Vitest, Vue Test Utils, and a mocked API boundary.

### Store

- Parallel initial loading and final state.
- Submit success inserts the authoritative returned trade.
- Position refresh occurs after submit.
- Submit validation/API error is retained for display.
- Position refresh failure does not remove a successfully created trade.
- Sort behavior is stable and canonical server order is not mutated.
- Initial loading preserves trades when only positions fail, and vice versa.
- A successful create remains successful when the subsequent position refresh
  fails.

### Components

- Field validation and accessible errors.
- Pending/disabled submission state.
- Successful reset/focus behavior.
- Trade rendering and notional formatting.
- Sort controls and `aria-sort`.
- Buy/sell text and styles.
- Position long/short/empty rendering.
- Loading and API error states.
- Field errors are associated through `aria-describedby`.
- Sort headers expose the active direction through `aria-sort`.

## Browser end-to-end test

If included, run one Playwright smoke flow against the real API and test SQL
database:

1. Start with seed data disabled.
2. Load the page and confirm empty states.
3. Enter `AAPL`, Buy, 100, 10.
4. Confirm the trade row and long position.
5. Enter `AAPL`, Sell, 150, 12.
6. Confirm both rows and a short 50 position at average cost 12.

This verifies the complete frontend-to-database path without creating a large,
fragile browser suite.

## Coverage

Collect backend coverage with `coverlet.collector`:

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

Use ReportGenerator to publish an HTML and Cobertura summary if desired.

Recommended targets:

- Domain project: at least 90% line and branch coverage.
- Application project: at least 80% line coverage.
- Overall backend: at least 75% line coverage.

Coverage is a guardrail, not the goal. Exclude generated code and trivial
composition/bootstrap code where justified. Do not write assertion-free tests
to raise percentages.

Collect frontend coverage with:

```powershell
npm run test:coverage
```

Focus thresholds on stores, API error handling, and calculation/presentation
helpers rather than generated setup.

## Static quality gates

Backend:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes
```

Frontend:

```powershell
npm ci
npm run lint
npm run type-check
npm run test:coverage
npm run build
```

Database:

- Apply all migrations to a clean database.
- Re-run the migrator and verify no changes/errors.
- Enable seed data twice and verify stable counts.
- Disable seed data twice and verify user trades remain.

## Manual acceptance pass

- Use Swagger to submit valid and invalid trades.
- Confirm timestamps are UTC over the wire and local in the UI.
- Verify long, short, partial close, exact close, and cross-through-zero flows.
- Verify sorting and responsive layout.
- Verify keyboard navigation, labels, focus, and color-independent meaning.
- Inspect logs for trace IDs, status codes, elapsed times, and absence of
  secrets.
- Follow the README from a clean checkout.

## CI plan

One GitHub Actions workflow should:

1. Restore/build/test backend with coverage.
2. Install/lint/type-check/test/build frontend.
3. Start SQL Server as a service container for integration tests.
4. Upload test and coverage artifacts.

Do not require the development admin seed endpoint for tests; tests can use the
same infrastructure service directly or call the endpoint only when testing
its public behavior.

## Definition of done

- All required endpoint and UI behaviors pass automated or documented manual
  acceptance.
- Position edge cases are covered by NUnit tests.
- Integration tests execute against real SQL Server schema/migrations.
- No secrets, local database files, `.vs`, build output, or dependency folders
  are committed.
- Swagger, health checks, and Serilog work locally.
- A clean setup can migrate, optionally seed, run, and test from README
  instructions.
- The required AI transcript is included and reviewed for sensitive values.
