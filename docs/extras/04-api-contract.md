# API Contract Quick Reference

The full contract and SQL design are in
[API and database contracts](./04-api-and-database-contracts.md).

## Conventions

- JSON uses camelCase.
- Timestamps are ISO 8601 UTC.
- Side is `"Buy"` or `"Sell"`.
- Errors use RFC 7807 ProblemDetails.
- The server assigns trade IDs and timestamps.

## POST /trades

Request:

```json
{
  "symbol": "aapl",
  "side": "Buy",
  "quantity": 100,
  "price": 187.25
}
```

Response: `201 Created`

```json
{
  "tradeId": 42,
  "symbol": "AAPL",
  "side": "Buy",
  "quantity": 100,
  "price": 187.2500,
  "timestamp": "2026-06-09T23:30:00.123Z",
  "notional": 18725.0000
}
```

Invalid input returns `400` ValidationProblemDetails.

## GET /trades

Returns `200` with all trades ordered by
`timestamp DESC, tradeId DESC`. An empty ledger returns `[]`.

## GET /positions

Returns `200` with positions ordered by symbol:

```json
[
  {
    "symbol": "AAPL",
    "netQuantity": 150,
    "averageCost": 15.0000
  }
]
```

Flat symbols are omitted. Average cost follows the moving-average rules in
[position calculation](./05-position-calculation.md).

## Development seed API

These routes are mapped only when `AdminEndpoints:Enabled` is true.

### GET /admin/seed-data

```json
{
  "enabled": true,
  "seedTradeCount": 8
}
```

### PUT /admin/seed-data

Request:

```json
{
  "enabled": false
}
```

Enabling inserts missing deterministic seed rows. Disabling deletes only rows
marked as seed data. Both operations are transactional and idempotent. No admin
route deletes user-entered trades.

## Health and documentation

- `GET /health/live`
- `GET /health/ready`
- Swagger UI in Development
