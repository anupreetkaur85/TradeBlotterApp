# Trade Blotter Assignment and Traceability

This file summarizes the supplied `trade-blotter-exercise.docx` and maps each
requirement to the merged design and implementation plan. The source document
remains authoritative for assessment requirements.

## Assessment summary

Build a Vue 3 single-page trade blotter backed by a C# REST API. Users must be
able to:

- Submit a trade with symbol, side, quantity, and price.
- View all trades newest first without reloading the page.
- Sort the blotter by at least one column.
- See net quantity and average cost per symbol, derived from trade history.

Required API:

- `POST /trades`
- `GET /trades`
- `GET /positions`

Required frontend:

- Vue 3 Composition API
- Pinia
- Vite
- Trade form validation
- Blotter columns for timestamp, symbol, side, quantity, price, and notional
- Visually distinct buys and sells
- Reactive positions panel

Submission expectations:

- Public GitHub repository
- Local setup instructions
- Documented assumptions and future improvements
- Clear commit history
- Full AI-tool transcript reviewed for secrets

## Owner-requested extensions

The implementation intentionally differs from the minimum assessment stack:

- .NET 9 instead of .NET 8
- SQL Server instead of SQLite
- Dapper instead of Entity Framework
- Versioned SQL migrations
- Optional, idempotent admin seed controls
- Serilog
- NUnit unit and integration tests
- Low-allocation backend hot paths with minimal LINQ

These extensions must not displace the polished core workflow.

## Traceability matrix

| # | Requirement | Design | Implementation |
|---|---|---|---|
| 1 | Submit a trade | [design](./design.md#submit-trade-flow) | [plan Step 3](./implementation-plan.md#step-3-required-rest-api) |
| 2 | Trades newest first | [design](./design.md#read-models) | [plan Step 2](./implementation-plan.md#step-2-sql-server-migrations-and-dapper) |
| 3 | Derived positions | [design](./design.md#position-calculation) | [plan Step 1](./implementation-plan.md#step-1-domain-and-position-tests) |
| 4 | Trade fields | [API contracts](./extras/04-api-and-database-contracts.md) | [plan Step 1](./implementation-plan.md#step-1-domain-and-position-tests) |
| 5 | Flat symbols omitted | [position rules](./extras/05-position-calculation.md) | NUnit calculator tests |
| 6 | Correct mixed buy/sell average | [position rules](./extras/05-position-calculation.md) | NUnit add/reduce/flip tests |
| 7 | Persistence | [database design](./extras/07-database-migrations-and-seeding.md) | DbUp and Dapper |
| 8 | Trade form | [frontend design](./design.md#frontend-components) | [plan Step 6](./implementation-plan.md#step-6-trade-form-and-reactive-submit) |
| 9 | Client/server validation | [API errors](./design.md#error-contract) | ValidationProblemDetails and inline field errors |
| 10 | Reactive submit | [submit flow](./design.md#submit-trade-flow) | Pinia submit action |
| 11 | Required blotter columns | [frontend components](./design.md#frontend-components) | `TradeBlotter.vue` |
| 12 | Sortable table | [frontend state](./design.md#pinia-state-boundary) | Store getter and sort headers |
| 13 | Scannable sides | [UI rules](./design.md#ui-and-accessibility-rules) | `SideBadge.vue` and `SideToggle.vue` |
| 14 | Reactive positions | [submit flow](./design.md#submit-trade-flow) | Refresh after successful create |
| 15 | Pinia ownership | [state boundary](./design.md#pinia-state-boundary) | `tradeStore.ts` |
| 16 | Vite tooling | [frontend architecture](./design.md#frontend-architecture) | Vue TypeScript scaffold |
| 17 | Position tests | [testing](./design.md#testing-strategy) | NUnit domain project |
| 18 | Error handling/status codes | [error contract](./design.md#error-contract) | ProblemDetails middleware/endpoints |
| 19 | Swagger | [backend architecture](./design.md#backend-architecture) | OpenAPI configuration |
| 20 | Optional seeding | [seed design](./extras/07-database-migrations-and-seeding.md) | Development admin endpoints |
| 21 | README and transcript | [delivery](./design.md#submission-quality) | [plan Step 7](./implementation-plan.md#step-7-submission-quality) |

## Deliberate scope

Positions are server-authoritative. The frontend does not duplicate the
average-cost algorithm. SignalR provides multi-client updates; authentication,
real market data, realized P&L, and large-ledger projections remain future work.
