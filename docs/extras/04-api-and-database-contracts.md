# API and Database Contracts

## API conventions

- Base path: `/`
- Content type: `application/json`
- JSON property naming: camelCase
- Timestamps: ISO 8601 UTC, for example `2026-06-09T23:30:00.123Z`
- Side values: `Buy`, `Sell`
- Errors: RFC 7807 ProblemDetails

## POST /trades

Request:

```json
{
  "symbol": "AAPL",
  "side": "Buy",
  "quantity": 100,
  "price": 195.25
}
```

Response: `201 Created`

```json
{
  "tradeId": 42,
  "symbol": "AAPL",
  "side": "Buy",
  "quantity": 100,
  "price": 195.2500,
  "timestamp": "2026-06-09T23:30:00.123Z",
  "notional": 19525.0000
}
```

Validation response: `400 Bad Request`

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-...",
  "errors": {
    "quantity": ["Quantity must be greater than zero."]
  }
}
```

## GET /trades

Response: `200 OK`

```json
[
  {
    "tradeId": 42,
    "symbol": "AAPL",
    "side": "Buy",
    "quantity": 100,
    "price": 195.2500,
    "timestamp": "2026-06-09T23:30:00.123Z",
    "notional": 19525.0000
  }
]
```

Ordering is always `timestamp DESC, tradeId DESC`.

The assessment asks for all trades. Pagination can later be added through
optional cursor parameters without changing the default behavior.

## GET /positions

Response: `200 OK`

```json
[
  {
    "symbol": "AAPL",
    "netQuantity": 75,
    "averageCost": 193.1667
  },
  {
    "symbol": "MSFT",
    "netQuantity": -20,
    "averageCost": 430.0000
  }
]
```

- Results are sorted by symbol.
- Flat symbols are omitted.
- Average cost is the current open-position cost under moving-average rules.

## GET /admin/seed-data

Development/test only.

Response: `200 OK`

```json
{
  "enabled": true,
  "seedTradeCount": 8
}
```

## PUT /admin/seed-data

Development/test only.

Request:

```json
{
  "enabled": true
}
```

Response: `200 OK`

```json
{
  "enabled": true,
  "seedTradeCount": 8,
  "inserted": 8,
  "deleted": 0
}
```

Semantics:

- `enabled: true` inserts all missing seed rows.
- `enabled: false` deletes all and only rows with `IsSeedData = 1`.
- Repeating either operation is idempotent.
- The operation is transactional.
- User-created trades are never modified.

## Health endpoints

```text
GET /health/live
GET /health/ready
```

Liveness checks process health. Readiness verifies SQL Server connectivity with
a short timeout.

## SQL schema

Proposed `dbo.Trades`:

```sql
CREATE TABLE dbo.Trades
(
    TradeId        bigint IDENTITY(1,1) NOT NULL,
    Symbol         varchar(16) NOT NULL,
    Side           char(1) NOT NULL,
    Quantity       bigint NOT NULL,
    Price          decimal(19,4) NOT NULL,
    ExecutedAtUtc  datetimeoffset(7) NOT NULL,
    IsSeedData     bit NOT NULL
        CONSTRAINT DF_Trades_IsSeedData DEFAULT (0),
    SeedKey        varchar(50) NULL,

    CONSTRAINT PK_Trades PRIMARY KEY CLUSTERED (TradeId),
    CONSTRAINT CK_Trades_Symbol_NotBlank
        CHECK (LEN(Symbol) > 0),
    CONSTRAINT CK_Trades_Side
        CHECK (Side IN ('B', 'S')),
    CONSTRAINT CK_Trades_Quantity_Positive
        CHECK (Quantity > 0),
    CONSTRAINT CK_Trades_Price_Positive
        CHECK (Price > 0)
);
```

The API validator enforces the tighter symbol character rule. The database
still enforces core invariants if another writer is introduced.

## SQL indexes

Blotter query:

```sql
CREATE INDEX IX_Trades_ExecutedAtUtc_TradeId
ON dbo.Trades (ExecutedAtUtc DESC, TradeId DESC)
INCLUDE (Symbol, Side, Quantity, Price);
```

Position calculation initially reads the ledger oldest first. SQL Server can
scan the same index in reverse. Do not add another large index until the query
plan or measurements justify it.

Seed idempotency:

```sql
CREATE UNIQUE INDEX UX_Trades_SeedKey
ON dbo.Trades (SeedKey)
WHERE SeedKey IS NOT NULL;
```

## Insert command

Use one round trip:

```sql
INSERT INTO dbo.Trades
(
    Symbol,
    Side,
    Quantity,
    Price,
    ExecutedAtUtc,
    IsSeedData,
    SeedKey
)
OUTPUT
    INSERTED.TradeId,
    INSERTED.Symbol,
    INSERTED.Side,
    INSERTED.Quantity,
    INSERTED.Price,
    INSERTED.ExecutedAtUtc
VALUES
(
    @Symbol,
    @Side,
    @Quantity,
    @Price,
    @ExecutedAtUtc,
    0,
    NULL
);
```

## Seed design

Keep sample rows in `backend/database/seeds/001_sample_trades.sql` or as a
parameterized infrastructure command. Use varied data that exercises:

- Multiple symbols
- Long and short positions
- Weighted-average additions
- Partial closes
- One completely flat symbol

All seed rows have `IsSeedData = 1` and a stable `SeedKey`. Normal trade rows
use `NULL`; seed records use values such as
`sample-aapl-buy-001`. This makes enabling idempotent without disturbing
existing seed timestamps.

## Transaction and consistency notes

- Trade insertion is a single atomic statement.
- Position reads reflect committed trades at the database's default isolation
  level.
- Seed toggling uses one transaction.
- Enabling SQL Server `READ_COMMITTED_SNAPSHOT` is an optional database setup
  improvement if concurrent reads/writes become significant.
