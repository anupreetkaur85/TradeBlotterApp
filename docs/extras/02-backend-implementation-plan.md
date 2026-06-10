# Backend Implementation Plan

## Technology baseline

- .NET 9 ASP.NET Core Web API
- C# with nullable reference types enabled
- Dapper and `Microsoft.Data.SqlClient`
- SQL Server/LocalDB
- DbUp for ordered SQL migrations, or a small equivalent runner
- Serilog.AspNetCore with compact JSON console output
- Swashbuckle/OpenAPI
- NUnit, NSubstitute, FluentAssertions, and
  `Microsoft.AspNetCore.Mvc.Testing`
- Testcontainers for SQL Server integration tests when Docker is available

Do not add Entity Framework merely for migrations. SQL migration scripts
remain the reviewable source of truth.

## Project setup

Add `Directory.Build.props` with:

- `TargetFramework` set to `net9.0`
- nullable enabled
- implicit usings enabled
- warnings as errors for production projects
- deterministic builds
- analyzers at a practical warning level

Use Minimal API route groups consistently. Group `/trades`, `/positions`, and
development-only `/admin` endpoints in separate endpoint modules so `Program`
remains composition-only. Minimal APIs reduce ceremony and fit the requested
small, low-latency service; handlers must still delegate to application
services rather than contain domain or SQL logic.

## Domain model

### Trade

```text
TradeId        long
Symbol         string
Side           Buy | Sell
Quantity       long
Price          decimal
ExecutedAtUtc  DateTimeOffset
IsSeedData     bool (persistence metadata, not exposed publicly)
SeedKey        string? (persistence metadata, seed rows only)
```

Prefer an enum or constrained value type for `TradeSide`. Persist side as
`char(1)` (`B`/`S`) with a database check constraint. Public JSON can use
`"Buy"` and `"Sell"`.

### Position

```text
Symbol           string
NetQuantity      long
AverageCost      decimal
```

Position is an output derived from trades. Do not create a `Positions` table.

### Position accumulator

Implement a mutable internal accumulator per symbol to keep the hot loop
allocation-light:

```text
NetQuantity
AverageCost
```

For each signed trade quantity:

- If flat, open at trade price.
- If trade direction matches position direction, calculate weighted average.
- If it reduces but does not cross zero, retain average.
- If it reaches zero, clear average.
- If it crosses zero, set residual quantity and reset average to trade price.

Use `checked` around quantity addition and notional multiplication where
overflow is possible. Round only when serializing/displaying if required; do
not repeatedly round the internal average.

Representative accumulator:

```csharp
internal struct PositionState
{
    public long NetQuantity;
    public decimal AverageCost;

    public void Apply(long delta, decimal price)
    {
        if (NetQuantity == 0)
        {
            NetQuantity = delta;
            AverageCost = price;
            return;
        }

        var sameDirection = (NetQuantity > 0 && delta > 0)
            || (NetQuantity < 0 && delta < 0);

        if (sameDirection)
        {
            var oldAbsolute = Math.Abs(NetQuantity);
            var deltaAbsolute = Math.Abs(delta);
            var newAbsolute = checked(oldAbsolute + deltaAbsolute);

            AverageCost =
                ((oldAbsolute * AverageCost) + (deltaAbsolute * price))
                / newAbsolute;
            NetQuantity = checked(NetQuantity + delta);
            return;
        }

        var oldSize = Math.Abs(NetQuantity);
        var tradeSize = Math.Abs(delta);
        NetQuantity = checked(NetQuantity + delta);

        if (tradeSize == oldSize)
        {
            AverageCost = 0m;
        }
        else if (tradeSize > oldSize)
        {
            AverageCost = price;
        }
    }
}
```

## Application layer

Implement three focused services:

- `TradeCommandService.SubmitAsync`
- `TradeQueryService.GetAllAsync`
- `PositionQueryService.GetAllAsync`

Repository abstractions:

```text
ITradeWriter.InsertAsync
ITradeReader.GetAllNewestFirstAsync
ITradeReader.GetPositionInputsOldestFirstAsync
ISeedDataStore.GetStatusAsync / SetEnabledAsync
```

Validation can be a small explicit validator. FluentValidation is optional,
but unnecessary for four fields. Return a structured validation result and let
the API translate it to `ValidationProblemDetails`.

Avoid LINQ in request paths:

- Use explicit loops for normalization/mapping when a collection transform is
  needed.
- Let SQL perform ordering.
- Build position output with a pre-sized `List<Position>`.
- `Array.Sort` or `List<T>.Sort` is acceptable for the small symbol list.

## Infrastructure layer

### Connection factory

Create an `ISqlConnectionFactory` that returns a closed or newly opened
`SqlConnection` based on a validated connection string. Open immediately
before commands and dispose promptly. Rely on SqlClient pooling.

Recommended local setting:

```json
{
  "ConnectionStrings": {
    "TradeBlotter": "Server=(localdb)\\MSSQLLocalDB;Database=TradeBlotter;Integrated Security=true;TrustServerCertificate=true"
  }
}
```

Do not commit machine-specific credentials.

### Dapper repositories

- Store SQL as `const string` or dedicated `.sql` resources.
- Use `CommandDefinition` with cancellation tokens.
- Parameterize every value.
- Use one round trip for insertion with `OUTPUT INSERTED`.
- Set an explicit command timeout from configuration.
- Do not use `SELECT *`.
- Map persistence rows to domain/application records deliberately.

Representative newest-first query:

```csharp
private const string GetTradesSql = """
    SELECT
        TradeId,
        Symbol,
        Side,
        Quantity,
        Price,
        ExecutedAtUtc
    FROM dbo.Trades
    ORDER BY ExecutedAtUtc DESC, TradeId DESC;
    """;

public async Task<IReadOnlyList<TradeRow>> GetAllAsync(
    CancellationToken cancellationToken)
{
    await using var connection =
        await connectionFactory.OpenAsync(cancellationToken);

    var command = new CommandDefinition(
        GetTradesSql,
        commandTimeout: options.CommandTimeoutSeconds,
        cancellationToken: cancellationToken);

    var rows = await connection.QueryAsync<TradeRow>(command);
    return rows.AsList();
}
```

### Migrations

Run migrations at startup only in local development/test, controlled by:

```text
Database:MigrateOnStartup
Database:SeedOnStartup
```

For other environments, expose a CLI/deployment migration command so schema
changes are not coupled to every application replica starting.

Migration scripts:

```text
001_create_trades.sql
002_create_trade_indexes.sql
003_create_schema_versions_or_seed_metadata.sql
```

Every migration is forward-only, ordered, and recorded by the migration tool.
Seed SQL lives separately from schema migrations so seeding remains optional.

## API layer

### Middleware order

1. Forwarded headers when deployed behind a proxy
2. Serilog request logging
3. Exception handler/ProblemDetails
4. HTTPS redirection as appropriate
5. CORS for the configured frontend origin
6. Endpoint mapping

### Required endpoints

- `POST /trades`
- `GET /trades`
- `GET /positions`

### Admin seed endpoints

- `GET /admin/seed-data`
- `PUT /admin/seed-data`

`PUT` accepts `{ "enabled": true|false }`. It synchronously applies or removes
seed rows in a transaction and returns the resulting status. Repeating the same
request produces the same state.

Map these endpoints only when `AdminEndpoints:Enabled` is true. In Swagger,
group them as `Admin` and document that they are local-development controls.

Representative route composition:

```csharp
var trades = app.MapGroup("/trades").WithTags("Trades");
trades.MapPost("/", TradeHandlers.CreateAsync)
    .Produces<TradeResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem();
trades.MapGet("/", TradeHandlers.GetAllAsync)
    .Produces<TradeResponse[]>();

app.MapGet("/positions", PositionHandlers.GetAllAsync)
    .WithTags("Positions")
    .Produces<PositionResponse[]>();

if (app.Configuration.GetValue<bool>("AdminEndpoints:Enabled"))
{
    app.MapSeedDataEndpoints();
}
```

### OpenAPI

- Document response types and status codes.
- Show validation examples.
- Use string enum values for side.
- Provide examples for trade creation and seed toggling.
- Enable Swagger UI in development.

## Serilog plan

Configure via `appsettings.json`:

- Information default level
- Warning overrides for noisy framework namespaces
- Console sink using compact JSON or a concise development template
- Enrichers for log context and application name

Add a correlation/trace ID to ProblemDetails and request completion logs.
Database errors should include operation name and elapsed time, but not SQL
parameter values that may become sensitive.

## Performance plan

For assessment scale, a single ordered read plus O(n) calculation is the
correct simple design. Validate with a small benchmark only if time permits.

Immediate optimizations:

- Cover newest-first trade reads with an index.
- Use the primary key as deterministic tie-breaker.
- Query only `Symbol`, `Side`, `Quantity`, and `Price` for positions.
- Pre-size dictionaries/lists when row count is available cheaply.
- Avoid repeated string normalization and repeated dictionary lookups.
- Pass cancellation tokens through all layers.

Scale path:

- Add cursor pagination to trades.
- Incrementally maintain a cache or projection from an append-only event
  stream.
- Keep the trade ledger as source of truth and rebuild projections when needed.

Do not add caching or a message broker before measurement; it would increase
assessment complexity and create invalidation concerns.

## Backend implementation checklist

- [ ] Scaffold solution and projects.
- [ ] Add package references and central build settings.
- [ ] Define trade, side, position, and calculator types.
- [ ] Write calculator tests first.
- [ ] Add SQL schema and index migrations.
- [ ] Add connection factory and Dapper repositories.
- [ ] Implement application services and validators.
- [ ] Add required Minimal API route groups/endpoints.
- [ ] Add development-only seed controls.
- [ ] Configure ProblemDetails and exception handling.
- [ ] Configure Serilog, health checks, CORS, and Swagger.
- [ ] Add integration test host and SQL Server fixture.
- [ ] Run build, tests, coverage, and formatting.
