# Database, Migrations, and Seeding

This document expands the database section of
[API and database contracts](./04-api-and-database-contracts.md).

## Schema

`backend/database/migrations/001_create_trades.sql`:

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
    CONSTRAINT CK_Trades_Symbol_NotBlank CHECK (LEN(Symbol) > 0),
    CONSTRAINT CK_Trades_Side CHECK (Side IN ('B', 'S')),
    CONSTRAINT CK_Trades_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_Trades_Price_Positive CHECK (Price > 0)
);
```

`backend/database/migrations/002_create_trade_indexes.sql`:

```sql
CREATE INDEX IX_Trades_ExecutedAtUtc_TradeId
ON dbo.Trades (ExecutedAtUtc DESC, TradeId DESC)
INCLUDE (Symbol, Side, Quantity, Price);

CREATE UNIQUE INDEX UX_Trades_SeedKey
ON dbo.Trades (SeedKey)
WHERE SeedKey IS NOT NULL;
```

The first index supports the newest-first blotter and can be scanned backward
for chronological position replay. Add a separate symbol/time index only after
query-plan evidence shows it is useful.

## Migration runner

Use DbUp because the application uses Dapper and needs plain, reviewable SQL.
DbUp journals applied scripts and executes new scripts in filename order.

```text
001_create_trades.sql
002_create_trade_indexes.sql
003_future_change.sql
```

Schema scripts are forward-only. Do not include optional sample trades in the
DbUp migration set.

Local development/test may run migrations at startup when
`Database:MigrateOnStartup` is true. CI/production should also support a
separate migration command so multiple API replicas do not race at startup.

## Seed source

Keep deterministic sample rows in
`backend/database/seeds/001_sample_trades.sql` or an equivalent parameterized
seed command. Each seed row has:

- `IsSeedData = 1`
- A stable unique `SeedKey`
- A fixed UTC timestamp
- Data that demonstrates weighted additions, partial closes, a full close,
  and a long-to-short or short-to-long crossing

User-created trades always have `IsSeedData = 0` and `SeedKey = NULL`.

## Seed toggle semantics

Configuration:

```json
{
  "Database": {
    "SeedOnStartup": false
  },
  "AdminEndpoints": {
    "Enabled": true
  }
}
```

API:

- `GET /admin/seed-data`
- `PUT /admin/seed-data` with `{ "enabled": true|false }`

Enabling seed data:

1. Begin a transaction.
2. Insert each deterministic row only when its `SeedKey` does not exist.
3. Commit and return inserted/current seed counts.

Disabling seed data:

1. Begin a transaction.
2. Delete only rows where `IsSeedData = 1`.
3. Commit and return deleted/current seed counts.

Both operations are idempotent. There is no force-reset endpoint and no admin
operation deletes user trades.

## Local connection strings

LocalDB:

```text
Server=(localdb)\MSSQLLocalDB;Database=TradeBlotter;Integrated Security=true;TrustServerCertificate=true
```

Default local SQL Server:

```text
Server=localhost;Database=TradeBlotter;Integrated Security=true;TrustServerCertificate=true
```

Keep credentials in user secrets or environment variables, never committed
configuration.

## Integration test database

Preferred: SQL Server Testcontainers with the production migration scripts.

Fallback: create a uniquely named LocalDB database per test run. Reset rows
between tests with Respawn or explicit table cleanup, and drop the test
database at fixture disposal. Tests must never point at the developer's normal
`TradeBlotter` database.
