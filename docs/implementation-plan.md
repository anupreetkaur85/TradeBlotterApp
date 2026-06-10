# Unified Implementation Plan

This is the executable build sequence for the merged backend and frontend
design. Detailed decisions live in the numbered documents linked from
[docs/README.md](./README.md).

## Step 0: Repository foundation

Create:

```text
backend/
frontend/
docs/
.gitignore
README.md
```

Backend:

- `TradeBlotter.sln`
- Domain, Application, Infrastructure, and Api projects
- Domain and API integration test projects
- `Directory.Build.props` targeting `net9.0`

Frontend:

- Vue 3 TypeScript Vite app
- Pinia, Vitest, Vue Test Utils, ESLint, and Prettier

Verify:

```powershell
dotnet build backend/TradeBlotter.sln
npm --prefix frontend run build
```

Checkpoint: both empty applications build and local configuration contains no
committed secrets.

## Step 1: Domain and position tests

Implement:

- `TradeSide`
- `Trade`
- `Position`
- Internal `PositionState`
- `PositionCalculator`
- Trade validation constraints

Write NUnit tests first for:

- Empty input
- Single long and short
- Weighted additions
- Partial long close and short cover
- Exact close
- Long-to-short and short-to-long crossing
- Multiple symbols
- Deterministic ordering contract
- Decimal precision and checked overflow

Representative contract:

```csharp
public static IReadOnlyList<Position> Calculate(
    IReadOnlyList<PositionTrade> orderedTrades);
```

The calculator assumes chronological input and uses explicit loops. It has no
ASP.NET Core, Dapper, or SQL dependency.

Verify:

```powershell
dotnet test backend/TradeBlotter.Api.Tests --filter "TestCategory!=RequiresSqlServer"
```

Checkpoint: domain tests are green before persistence work.

## Step 2: SQL Server migrations and Dapper

Create versioned scripts:

```text
001_create_trades.sql
002_create_trade_indexes.sql
```

`002_create_trade_indexes.sql` creates:

- Blotter index `(ExecutedAtUtc DESC, TradeId DESC)`.
- Positions covering index `IX_Trades_Symbol_Chrono` on
  `(Symbol, ExecutedAtUtc, TradeId)` `INCLUDE (Side, Quantity, Price)`.
- Filtered unique index on `ClientRequestId WHERE ClientRequestId IS NOT NULL`
  for idempotent create.

Add:

- DbUp migration runner
- `ISqlConnectionFactory`
- Dapper trade reader/writer
- Position-input query ordered oldest first
- Seed store using stable `SeedKey`

Required SQL behavior:

```sql
SELECT TradeId, Symbol, Side, Quantity, Price, ExecutedAtUtc
FROM dbo.Trades
ORDER BY ExecutedAtUtc DESC, TradeId DESC;
```

Insert with one `OUTPUT INSERTED` round trip. Use `CommandDefinition`,
cancellation tokens, explicit timeouts, and no `SELECT *`.

Verify:

- Apply migrations to a clean database.
- Re-run migrations with no changes.
- Insert and read a trade.
- Confirm the index exists.

Checkpoint: repository integration tests pass against SQL Server.

## Step 3: Required REST API

Implement Minimal API route groups:

- `POST /trades`
- `GET /trades`
- `GET /positions`

Add:

- Explicit request validation
- Symbol normalization
- Server UTC timestamp
- ValidationProblemDetails
- Global exception handling
- Swagger/OpenAPI
- CORS for configured Vite origin
- Serilog request logging
- SQL readiness and process liveness checks

Representative mapping:

```csharp
var trades = app.MapGroup("/trades").WithTags("Trades");
trades.MapPost("/", TradeHandlers.CreateAsync);
trades.MapGet("/", TradeHandlers.GetAllAsync);

app.MapGet("/positions", PositionHandlers.GetAllAsync)
    .WithTags("Positions");
```

Integration tests must cover valid/invalid create, newest-first reads, position
rules, empty arrays, and sanitized failures.

Checkpoint: required endpoints work through HTTP and real SQL Server.

## Step 4: Optional seed administration

Implement development/test-only:

- `GET /admin/seed-data`
- `PUT /admin/seed-data`

`PUT` accepts:

```json
{
  "enabled": true
}
```

Enabling inserts missing deterministic seed rows. Disabling deletes only rows
where `IsSeedData = 1`. The transaction must be idempotent and preserve all
user-created trades.

Map the routes only when `AdminEndpoints:Enabled` is true.

Checkpoint: integration tests prove repeated enable/disable operations and
user-trade preservation.

Apply the incremental configuration and observability guidance in
[the initialization/logging addendum](./addendum-configuration-and-logging.md):

- Preserve the scaffold's current configuration section names.
- Log startup decisions and migration/seed completion with structured fields.
- Keep migration and requested startup-seed failures fatal.
- Enforce `Seeding:Enabled` for admin seed mutations.
- Add typed-options validation without changing endpoint or schema contracts.

## Step 4b: Backend hardening (beyond the assessment time box)

The repository owner has opted to harden the service past the 3–4 hour
suggestion. These items stay behind the required core in delivery priority and
never persist positions. See [evaluation.md](./evaluation.md) for rationale.

Implement:

- **Idempotent create (B1).** Read an optional `Idempotency-Key` header into
  `ClientRequestId`; on a duplicate key return the existing trade with `200 OK`.
  Backed by the filtered unique index from Step 2.
- **Transient-fault resilience (B3).** Configure `Microsoft.Data.SqlClient`
  retry logic (or a small Polly policy) around Dapper commands with bounded
  backoff.
- **Contract-preserving reads (B4).** `GET /trades` returns the required
  newest-first JSON array. Add pagination only through a separately versioned
  contract after the assessment.
- **Correct derived positions (B5).** Recompute positions from the ordered
  ledger for each request. Do not use `MAX(TradeId)` as a cache version because
  deleting seed rows can change positions without changing the maximum user
  trade id.

Tests:

- Duplicate `Idempotency-Key` inserts exactly one row and returns the same trade.
- Simulated transient SQL error retries and then succeeds.
- Trade reads return the required newest-first array shape.
- Seed deletion immediately changes derived positions.

Checkpoint: hardening integration tests pass and the required endpoints are
unchanged in contract for clients that send no idempotency key or paging params.

## Step 5: Frontend foundation and dashboard reads

Create:

- API and domain TypeScript types
- ProblemDetails-aware HTTP client
- `tradeApi`
- Pinia `tradeStore`
- `TradeBlotterView`
- `TradeBlotter`
- `PositionsPanel`
- `SideBadge`

Initial loading:

```ts
const [tradesResult, positionsResult] = await Promise.allSettled([
  tradeApi.getTrades(),
  tradeApi.getPositions(),
])
```

Each panel owns its loading/error state. One failed endpoint must not suppress
the successful panel.

Implement:

- Newest-first canonical trade state
- Sort getter that returns a copy
- Sortable header buttons and `aria-sort`
- Tabular, right-aligned numeric columns
- Buy/Sell text plus color
- Independent empty, loading, error, and retry states
- Responsive table overflow

Checkpoint: dashboard displays real API data and remains partially usable when
one read endpoint fails.

## Step 6: Trade form and reactive submit

Create:

- `useTradeForm.ts`
- `TradeEntryForm.vue`
- `SideToggle.vue`

Local form state:

- Symbol
- Side
- Quantity
- Price
- Field errors

Submit flow:

```ts
const created = await tradeApi.createTrade(input)
trades.value.unshift(created)

try {
  positions.value = await tradeApi.getPositions()
} catch (error) {
  positionsError.value = toMessage(error)
}
```

Requirements:

- No optimistic ID or timestamp.
- Disable duplicate submit while pending.
- Map server field errors to controls.
- Use `aria-describedby` and a submit-result live region.
- On success, reset quantity/price and refocus symbol.
- A failed position refresh does not turn a created trade into a failed submit.

Frontend tests:

- Empty/non-positive validation
- Symbol normalization
- Pending button behavior
- Successful create/prepend/position refresh
- Successful create plus failed position refresh
- Server ProblemDetails mapping

Checkpoint: full submit-to-blotter-to-position workflow works without reload.

## Step 7: Submission quality

Run:

```powershell
dotnet restore backend/TradeBlotter.sln
dotnet build backend/TradeBlotter.sln --configuration Release --no-restore
dotnet test backend/TradeBlotter.sln --configuration Release --no-build --collect:"XPlat Code Coverage" `
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Threshold=80
dotnet format backend/TradeBlotter.sln --verify-no-changes

npm --prefix frontend ci
npm --prefix frontend run lint
npm --prefix frontend run type-check
npm --prefix frontend run test:coverage
npm --prefix frontend run build
```

Complete:

- Root README
- SQL Server and LocalDB setup
- Migration and optional seed instructions
- Backend/frontend run commands
- Test and coverage commands
- Assumptions and future improvements
- AI transcript reviewed for secrets
- Responsive and keyboard acceptance pass

Final manual sequence:

1. Start from a newly migrated database with seed disabled.
2. Submit a long position.
3. Add at another price and verify weighted average.
4. Partially close and verify unchanged average.
5. Cross through zero and verify the new basis.
6. Close flat and verify omission.
7. Verify sorting, errors, loading, responsive layout, and Swagger.

## Delivery priority

If time is constrained, preserve:

1. Correct position calculation and tests.
2. Required API endpoints.
3. Working form, blotter, positions, sorting, and validation.
4. SQL migrations and setup documentation.
5. Clear README and transcript.

Backend hardening (Step 4b) ranks after the core above: implement it once the
required workflow is green, and cut it before correctness, error handling, or the
core user workflow if time runs out.

Cut optional browser automation and visual embellishment before cutting
correctness, error handling, or the core user workflow.
