# Unified Full-Stack Design

This design combines the low-latency .NET 9 backend plan with the frontend
collaborator's interaction and UI specification.

## System architecture

```text
Vue 3 SPA
  Composition API + TypeScript + Pinia + Vite
                        |
                        | JSON / HTTP
                        v
.NET 9 ASP.NET Core Minimal API
  Application services + domain calculator
  Dapper + Serilog + ProblemDetails + OpenAPI
                        |
                        v
SQL Server
  Trades ledger only
```

The API is one independently deployable, stateless microservice. Trades are the
only stored source of truth. Positions are derived by replaying trades and are
never persisted.

## Backend architecture

```text
backend/
  TradeBlotter.sln
  TradeBlotter.Api/
    Domain/
    Services/
    Validation/
    Contracts/
    Abstractions/
    Endpoints/
    Hubs/
    Infrastructure/
    Database/migrations/
  TradeBlotter.Api.Tests/
    Application/
    Domain/
    Integration/
    Support/
```

- `TradeBlotter.Api`: one deployable service organized by concern folders.
- `Domain` remains framework-free; `Infrastructure` implements persistence
  abstractions with SQL Server and in-memory providers.
- `TradeBlotter.Api.Tests`: NUnit unit, in-memory component, and SQL integration
  tests grouped by test type.

Dependency direction:

```text
Endpoints -> Services/Domain
Infrastructure -> Abstractions/Domain
```

### Domain contracts

```csharp
public enum TradeSide
{
    Buy,
    Sell
}

public sealed record Trade(
    long TradeId,
    string Symbol,
    TradeSide Side,
    long Quantity,
    decimal Price,
    DateTimeOffset ExecutedAtUtc);

public sealed record Position(
    string Symbol,
    long NetQuantity,
    decimal AverageCost);
```

- Quantity is a positive whole-share `long`.
- Money uses `decimal`.
- Symbol is trimmed, validated, and uppercased.
- The server assigns UTC timestamps.
- Trades are append-only in assessment scope.

## Position calculation

Process trades in ascending `(ExecutedAtUtc, TradeId)` order.

1. Opening from flat sets average cost to the trade price.
2. Adding in the same direction recalculates weighted average cost.
3. Reducing without crossing zero leaves average cost unchanged.
4. Exact close removes the position.
5. Crossing zero opens the residual position at the crossing trade's price.

Example:

| Trade | Net | Average |
|---|---:|---:|
| Buy 100 @ 10 | 100 | 10 |
| Buy 100 @ 20 | 200 | 15 |
| Sell 50 @ 30 | 150 | 15 |
| Sell 200 @ 18 | -50 | 18 |

The repository returns ordered input. The calculator uses one explicit loop and
a dictionary keyed by symbol; it does not group or sort with LINQ.

See [position calculation](./extras/05-position-calculation.md) for implementation and
test cases.

## Persistence

SQL Server stores:

```text
TradeId bigint identity
Symbol varchar(16)
Side char(1)
Quantity bigint
Price decimal(19,4)
ExecutedAtUtc datetimeoffset(7)
IsSeedData bit
SeedKey varchar(50) null
ClientRequestId uniqueidentifier null   -- idempotency key (see below)
```

DbUp applies ordered SQL migrations. Dapper executes parameterized SQL and
selects only required columns. Two indexes back the read paths:

- Blotter, newest first: `(ExecutedAtUtc DESC, TradeId DESC)`.
- Positions, chronological per symbol: `IX_Trades_Symbol_Chrono` on
  `(Symbol, ExecutedAtUtc, TradeId)` `INCLUDE (Side, Quantity, Price)`, so the
  position fold runs as a single covering scan.
- Idempotency dedupe: a filtered unique index on `ClientRequestId`
  `WHERE ClientRequestId IS NOT NULL`.

Seed controls are development/test-only:

- `GET /admin/seed-data`
- `PUT /admin/seed-data`

Enabling inserts missing stable seed keys. Disabling removes only seed rows and
never user trades. The toggle runs in a serializable transaction scoped to
`IsSeedData = 1` rows so it cannot interleave with concurrent user inserts.

Configuration and operational logging follow the incremental
[database initialization addendum](./addendum-configuration-and-logging.md).
The existing `Migrations`, `Seeding`, and `Admin` section names are retained so
the in-progress scaffold does not need structural rework.

## API contract

### POST /trades

```json
{
  "symbol": "AAPL",
  "side": "Buy",
  "quantity": 100,
  "price": 187.5
}
```

Returns `201 Created` with server ID, normalized symbol, timestamp, and
notional. An optional `Idempotency-Key` header makes the create safe to retry
(see "Reliability, idempotency, and concurrency"); a repeat returns the existing
trade with `200 OK`.

### GET /trades

Returns all trades ordered by `timestamp DESC, tradeId DESC`.

### GET /positions

Returns non-flat positions ordered by symbol.

### Error contract

Validation returns RFC 7807 ValidationProblemDetails:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-...",
  "errors": {
    "quantity": ["Quantity must be greater than zero."]
  }
}
```

Unexpected failures return sanitized ProblemDetails with a trace ID. Serilog
records structured request completion events without request bodies or secrets.

## Reliability, idempotency, and concurrency

These behaviors harden the service beyond the assessment minimum. They never
persist positions; the ledger remains the only stored source of truth.

- **Idempotent create.** `POST /trades` accepts an optional `Idempotency-Key`
  header (a client request id). The id is stored in `ClientRequestId` under a
  filtered unique index. A retried request with the same key returns the
  already-created trade with `200 OK` instead of inserting a duplicate, so a
  network retry after a committed insert cannot corrupt positions. Requests
  without a key behave as plain inserts.
- **Transient-fault resilience.** Dapper calls run under
  `Microsoft.Data.SqlClient` configurable retry logic (or an equivalent small
  Polly policy) so transient SQL Server errors (timeouts, failovers) retry with
  backoff rather than surfacing as 500s.
- **Seed concurrency.** Enable/disable runs in a serializable transaction and
  coordinates through the unique seed key; user trades are never modified.

## Read models

`GET /trades` performs ordering in SQL and returns the assessment-required JSON
array, newest first. Pagination is deferred because adding an envelope would
change the required response contract; it should be introduced through a
separately versioned endpoint if ledger size requires it. `GET /positions`
queries only symbol, side, quantity, and price in chronological order (covered
by `IX_Trades_Symbol_Chrono`) and folds them in memory.

The position fold is O(n) over the ledger. It is deliberately recomputed for
each request so seed deletion and concurrent writes cannot leave a cache stale.
A persisted read model / CQRS projection remains a documented scale path rather
than premature implementation.

## Frontend architecture

```text
frontend/src/
  api/
    httpClient.ts
    tradeApi.ts
  components/
    TradeEntryForm.vue
    SideToggle.vue
    TradeBlotter.vue
    SideBadge.vue
    PositionsPanel.vue
    InlineAlert.vue
  composables/
    useTradeForm.ts
  stores/
    tradeStore.ts
  types/
    api.ts
    trade.ts
  views/
    TradeBlotterView.vue
```

Components do not call `fetch`. The typed API client owns base URL, JSON
parsing, cancellation, and ProblemDetails conversion.

## Pinia state boundary

Pinia owns server-backed dashboard data and request/sort state:

```ts
type TradeState = {
  trades: Trade[]
  positions: Position[]
  tradesLoading: boolean
  positionsLoading: boolean
  submitting: boolean
  tradesError: string | null
  positionsError: string | null
  submitError: ProblemDetails | null
  sortKey: SortKey
  sortDirection: 'asc' | 'desc'
}
```

The form draft and client field errors remain local to `useTradeForm`.

Initial loading uses `Promise.allSettled`, allowing one panel to remain usable
if the other request fails. Sorting returns a copy and never mutates canonical
newest-first server order.

## Submit trade flow

```text
TradeEntryForm
  -> validate local draft
  -> store.submitTrade
  -> POST /trades
  -> prepend authoritative response
  -> refresh GET /positions
  -> reset/refocus form
```

The server response supplies the ID, timestamp, normalization, and decimal
values. Do not create optimistic IDs.

If position refresh fails after a successful POST:

- Keep the new trade visible.
- Treat form submission as successful.
- Reset/refocus the form.
- Show a retryable error only in the positions panel.

## Frontend components

### TradeEntryForm

- Symbol text field, Buy/Sell toggle, quantity, and price.
- Inline validation with `aria-describedby`.
- Submit disabled while invalid or pending.
- Normalize symbol and map server field errors.
- Clear quantity/price after success and refocus symbol.

### TradeBlotter

- Timestamp, symbol, side, quantity, price, and notional.
- Default newest-first ordering.
- Sortable timestamp, symbol, or notional headers.
- Header buttons with `aria-sort`.
- Numeric columns right-aligned with tabular numerals.
- Horizontal scrolling on narrow screens.

### PositionsPanel

- Symbol, signed net quantity, and average cost.
- Long/short meaning shown with text/sign as well as color.
- Independent loading, retryable error, and empty states.

### SideBadge and SideToggle

Use shared design tokens:

- Buy: restrained green treatment plus visible `Buy` text.
- Sell: restrained red treatment plus visible `Sell` text.

Do not rely on color alone.

## UI and accessibility rules

- Desktop: compact form and positions area with the blotter receiving most
  width.
- Mobile: form, positions, then horizontally scrollable blotter.
- Visible labels and keyboard focus.
- At least 44px interactive targets.
- Accessible live region for submit result.
- Specific loading, empty, and error states for each panel.
- `Intl.NumberFormat` for price/notional and browser-local timestamp display.

## Testing strategy

Backend:

- NUnit position tests for open, weighted add, partial close, exact close,
  long/short crossing, multiple symbols, precision, and overflow.
- NUnit application validation tests.
- `WebApplicationFactory` integration tests against real SQL Server migrations.
- Seed idempotency and user-trade preservation tests.

Frontend:

- Vitest store tests for independent loading and submit behavior.
- Test successful POST plus failed position refresh.
- Form validation and ProblemDetails mapping tests.
- Blotter formatting, sorting, and ARIA tests.
- Positions long, short, empty, loading, and error tests.

Optional Playwright smoke coverage exercises submit-to-database behavior.

## Submission quality

The root README must include prerequisites, SQL setup, migrations, optional
seeding, run/test commands, assumptions, and future improvements. Include the
required AI transcript after reviewing it for credentials and other sensitive
values.
