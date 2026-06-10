# Architecture Overview

This is a concise companion to the detailed
[architecture and delivery plan](./01-architecture-and-delivery-plan.md).

## System context

```text
Vue 3 SPA (Vite + Pinia)
          |
          | JSON/HTTP
          v
.NET 9 ASP.NET Core Minimal API
          |
          | Dapper / Microsoft.Data.SqlClient
          v
SQL Server (Trades only)
```

The application has one source of truth: the trade ledger. The blotter reads
that ledger newest first. Positions replay it oldest first through the domain
calculator. There is no `Positions` table.

## Backend layout

```text
backend/
  TradeBlotter.sln
  src/
    TradeBlotter.Domain/
    TradeBlotter.Application/
    TradeBlotter.Infrastructure/
    TradeBlotter.Api/
  tests/
    TradeBlotter.Domain.Tests/
    TradeBlotter.Api.IntegrationTests/
  database/
    migrations/
    seeds/
```

Dependency direction:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application/Domain
```

`Domain` has no infrastructure dependencies. `Infrastructure` owns Dapper,
SQL connections, migrations, and seed operations. `Api` owns route groups,
ProblemDetails, Swagger, Serilog, health checks, and dependency composition.

## Key decisions

| Concern | Decision |
|---|---|
| Runtime | .NET 9 |
| API style | Minimal API route groups |
| Data access | Dapper with parameterized static SQL |
| Database | Local SQL Server or LocalDB |
| Migrations | DbUp with versioned SQL scripts |
| Quantity | Whole shares using C# `long` and SQL `bigint` |
| Money | C# `decimal` and SQL `decimal(19,4)` |
| Timestamp | Server-assigned UTC `DateTimeOffset` / `datetimeoffset(7)` |
| Positions | In-process O(n) fold with explicit loops |
| Logging | Serilog structured console logs |
| Tests | NUnit plus real SQL Server integration tests |

## Low-latency approach

- Async SQL I/O and SqlClient connection pooling.
- One insert round trip using `OUTPUT INSERTED`.
- Indexed newest-first blotter query.
- Minimal position query columns.
- Explicit loops and dictionary accumulators instead of LINQ in hot paths.
- Stable SQL ordering instead of in-memory trade sorting.
- No cache, broker, or stored position projection until scale measurements
  justify their consistency and operational cost.

## Runtime configuration

```json
{
  "ConnectionStrings": {
    "TradeBlotter": "Server=(localdb)\\MSSQLLocalDB;Database=TradeBlotter;Integrated Security=true;TrustServerCertificate=true"
  },
  "Database": {
    "MigrateOnStartup": true,
    "SeedOnStartup": false
  },
  "AdminEndpoints": {
    "Enabled": true
  }
}
```

Migration-on-startup and admin endpoints default to local development/test.
Production should run migrations as a deployment step and not map unauthenticated
admin endpoints.
