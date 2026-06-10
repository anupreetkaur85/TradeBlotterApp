# Assessment Analysis

## Required behavior

The application must let a user:

- Enter a trade with symbol, side, quantity, and price.
- See submitted trades immediately in a newest-first blotter.
- Sort the blotter by at least one column.
- See positions derived reactively from all trades.
- Distinguish buys and sells visually.

The backend must expose:

- `POST /trades`
- `GET /trades`
- `GET /positions`
- Swagger/OpenAPI documentation

The original exercise permits SQLite or SQL Server LocalDB and asks for
.NET 8. This implementation will use SQL Server and .NET 9 by owner request.
That deviation must be stated in the root README.

## Domain assumptions

These assumptions should be encoded in validation, tests, and API
documentation:

- Symbols are case-insensitive on input and stored in normalized uppercase.
- A symbol contains 1 to 16 letters, digits, `.`, or `-`.
- Quantity is a positive whole number represented by `long`.
- Price is a positive decimal represented by `decimal(19,4)`.
- Notional is `quantity * price`, represented by `decimal`.
- Timestamp is assigned by the server in UTC; clients do not set it.
- Trades are immutable and append-only for assessment scope.
- Trades with the same timestamp are ordered deterministically by descending
  database identifier.
- A zero net position is omitted from `GET /positions`.
- Average cost uses a moving weighted-average method, not FIFO.

## Position calculation rules

Process trades in deterministic chronological order:

1. Opening a position sets average cost to the trade price.
2. Adding in the same direction recalculates weighted average cost:
   `(old absolute quantity * old average + trade quantity * trade price) /
   new absolute quantity`.
3. Reducing a position without crossing zero leaves average cost unchanged.
4. Closing a position sets net quantity and average cost to zero.
5. Crossing through zero starts the residual position in the opposite
   direction at the crossing trade's price.

Example:

| Trade | Resulting net quantity | Resulting average cost |
|---|---:|---:|
| Buy 100 @ 10 | 100 | 10 |
| Buy 100 @ 14 | 200 | 12 |
| Sell 50 @ 20 | 150 | 12 |
| Sell 200 @ 11 | -50 | 11 |

Realized P&L is not required and should not be added to the core implementation.

## Important engineering risks

### Position correctness

Mixed buys and sells are order-dependent. A SQL `GROUP BY` with a simple
average is incorrect. The implementation must query trades oldest first and
fold them through a tested domain calculator.

### Numeric precision

JavaScript numbers cannot safely represent every 64-bit integer or arbitrary
SQL decimal. Assessment-scale quantities can be sent as JSON numbers, but
frontend validation should enforce a safe upper bound. Monetary formatting
must not use binary floating-point arithmetic for backend calculations.

### Immediate UI consistency

After a successful `POST /trades`, the Pinia store should insert the returned
trade and refresh positions. The server response is authoritative for ID,
timestamp, normalization, and decimal values.

### Seed lifecycle

"Seeding on/off" must not mean deleting all trades. Seed rows need an explicit
marker so disabling seed data removes only known seed records. The operation
must be transactional and idempotent.

### Assessment time limit

The exercise recommends 3-4 hours. Build the vertical slice first:

- Correct position logic
- Three required endpoints
- Functional form, blotter, and position panel
- Focused tests
- Clear setup instructions

Admin seed controls, broader integration coverage, and UI polish follow once
the core is working.

## Acceptance criteria

- Valid trades persist and return `201 Created`.
- Invalid trades return `400` with field-specific problem details.
- Trades load newest first with deterministic tie-breaking.
- Position output matches the moving-average rules for long and short flows.
- The page loads trades and positions without a manual refresh.
- Submitting a trade updates the blotter and positions without a page reload.
- Seed data can be enabled repeatedly without duplication and disabled without
  removing user-created trades.
- Swagger lists all public and development-only admin endpoints.
- Backend unit and integration tests pass.
- Frontend component/store tests pass.
- Root README explains prerequisites, database setup, migrations, seeding, and
  how to run all applications and tests.
