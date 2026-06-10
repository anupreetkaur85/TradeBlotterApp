# Architecture and Delivery Plan

## Target architecture

Use a modular monolith deployed as one small API microservice plus one SPA:

```text
Browser
  |
  v
Vue 3 SPA (Vite + Pinia)
  |
  | JSON/HTTP
  v
ASP.NET Core .NET 9 API
  |-- endpoint/contract layer
  |-- application services
  |-- domain position calculator
  |-- Dapper repositories
  |
  v
SQL Server
```

The service is independently deployable and stateless. Splitting trades and
positions into separate services would add network and consistency costs
without helping this assessment because positions are derived from the same
trade ledger.

## Proposed repository structure

```text
tradeblotter/
  backend/
    TradeBlotter.sln
    Directory.Build.props
    src/
      TradeBlotter.Api/
      TradeBlotter.Application/
      TradeBlotter.Domain/
      TradeBlotter.Infrastructure/
    tests/
      TradeBlotter.Domain.Tests/
      TradeBlotter.Api.IntegrationTests/
    database/
      migrations/
      seeds/
  frontend/
    src/
      api/
      components/
      stores/
      types/
      views/
    tests/
  docs/
  README.md
  .gitignore
```

Four backend production projects preserve clear dependency direction without
creating separate deployables:

- `Domain`: trade/position types and pure position calculation.
- `Application`: use cases, validation, repository interfaces, DTO mapping.
- `Infrastructure`: Dapper repositories, SQL connection factory, migrations,
  and seed operations.
- `Api`: HTTP contracts, endpoint wiring, middleware, OpenAPI, and composition.

Dependencies point inward:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application/Domain
```

## Backend request flows

### Submit trade

1. Parse request JSON.
2. Validate and normalize the symbol and values.
3. Assign server UTC timestamp.
4. Insert through a parameterized Dapper command.
5. Return the inserted representation with `201 Created`.

### Read trades

1. Execute one indexed SQL query ordered by `ExecutedAtUtc DESC, TradeId DESC`.
2. Stream/map rows through Dapper.
3. Return an array. Do not perform an in-memory sort.

### Read positions

1. Query the minimal fields required by the calculator, ordered oldest first.
2. Fold rows into per-symbol accumulators with explicit loops.
3. Omit flat positions.
4. Sort the small result set by symbol for stable output.

### Toggle seed data

1. Reject the operation outside allowed environments/configuration.
2. Open a SQL transaction.
3. Enabling inserts missing, deterministic seed rows.
4. Disabling deletes only rows marked as seed data.
5. Commit and return current status/counts.

## Frontend state flow

Pinia owns server state:

- `trades`
- `positions`
- initial loading state
- submitting state
- API error state
- active blotter sort

Components receive state and emit user intent. They do not call `fetch`
directly. On submit, the store awaits the API response, prepends the returned
trade, then refreshes positions. Disable duplicate submission while the request
is in flight.

Initial trades and positions requests are independent. Use
`Promise.allSettled` so one failed panel does not make the whole dashboard
unusable. After a successful trade insert, a failed position refresh must not
be reported as a failed trade. Keep the authoritative returned trade visible
and expose a retryable positions error.

Form draft state remains local to the form/composable. Pinia owns only
server-backed data, request state, and table sort state.

## Cross-cutting choices

### Low latency

- Use async database I/O and SQL connection pooling.
- Use parameterized static SQL and Dapper `CommandDefinition`.
- Select only needed columns.
- Sort/filter in SQL where appropriate.
- Use explicit loops in backend hot paths; avoid LINQ allocations.
- Add a covering order index for the blotter query.
- Keep position calculation O(number of trades), with one dictionary lookup
  per trade.
- Use response compression only after measurement; payloads are initially
  small.

### Error handling

Use RFC 7807 `ProblemDetails` consistently:

- `400` validation failure
- `404` unknown resource where applicable
- `409` data conflict if introduced later
- `500` unexpected error with a correlation/trace ID and no internal details

### Observability

- Serilog structured console logs.
- Enrich with timestamp, level, trace ID, request path, elapsed milliseconds,
  and status code.
- Log one request-completion event rather than noisy per-layer success logs.
- Never log connection strings or full request bodies.
- Add `/health/live` and `/health/ready`; readiness checks SQL connectivity.

### Security boundary

Authentication is explicitly out of assessment scope. Admin seed endpoints
must therefore be development/test-only by environment or
`AdminEndpoints:Enabled`. Production deployment should not map them until
authentication/authorization exists.

## Delivery phases

### Phase 1: Foundation

- Add solution/projects and central compiler settings.
- Add NuGet/npm dependencies.
- Configure app settings and local SQL connection string via user secrets or
  environment variable.
- Add migration runner and initial schema.

Exit condition: API starts, SQL readiness is healthy, frontend starts.

### Phase 2: Domain-first backend

- Define domain types and validation constraints.
- Implement the moving-average position calculator.
- Write NUnit tests before endpoint wiring.
- Implement Dapper trade repository and migrations.

Exit condition: calculator tests pass and repository can insert/read trades.

### Phase 3: REST API

- Implement required endpoints and contracts.
- Configure ProblemDetails, OpenAPI, Serilog, health checks, and CORS.
- Implement seed status/toggle endpoint.
- Add API integration tests.

Exit condition: all backend acceptance tests pass against SQL Server.

### Phase 4: Frontend

- Add typed API client and Pinia store.
- Build form, blotter, and positions panel.
- Add loading, empty, validation, and error states.
- Add accessible visual treatment and responsive layout.
- Add frontend unit/component tests.

Exit condition: the core workflow works without reload and UI tests pass.

### Phase 5: Submission quality

- Run formatting, builds, tests, and coverage.
- Verify a clean database can be migrated and seeded.
- Add root README and architecture notes.
- Include the required AI-tool transcript, after checking it for secrets.
- Record known limitations and next improvements.

## Production hardening backlog

These are intentionally outside the assessment core:

- Authentication and authorization for admin operations.
- Cursor pagination and server-side filtering for large blotters.
- Server-Sent Events or WebSockets for multi-user live updates.
- Cached/materialized positions driven by an event stream.
- Idempotency keys on trade submission.
- Audit trail, trade correction/cancellation workflow, and concurrency policy.
- Metrics, tracing export, rate limiting, and deployment manifests.
