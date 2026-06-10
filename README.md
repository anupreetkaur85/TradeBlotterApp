# Trade Blotter

A trade blotter: enter trades, view them in a live blotter, and see positions
(net quantity and average cost per symbol) derived from the trade history.

- **Backend:** .NET 9 ASP.NET Core Minimal API, Dapper + stored procedures over
  SQL Server, DbUp migrations, Serilog, Swagger. Low-latency design (no LINQ on
  request/position hot paths).
- **Frontend:** Vue 3 (Composition API) + Pinia + Vite + TypeScript — trade entry
  form, sortable live blotter, reactive positions panel, typed API client.

Planning and design live in [`docs/`](docs/README.md); a review with gaps and
scalability analysis is in [`docs/evaluation.md`](docs/evaluation.md).

## Status

| Area | State |
| --- | --- |
| Backend API, persistence, sprocs, seeding, tests | Implemented |
| Unit + integration tests | NUnit unit tests plus SQL Server integration coverage |
| Frontend SPA | Implemented (Vue 3 + Pinia + Vite); type-check, unit tests, build green |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (for the frontend)
- **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`) — *optional*. Ships with Visual
  Studio or the [SQL Server Express LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb)
  installer. Any SQL Server works; override the connection string to point elsewhere.

> **No SQL Server?** Set `Database:UseInMemory` to `true` to run the whole service
> against an in-memory store — see [Running without SQL Server](#running-without-sql-server).

Verify LocalDB is running (if you use it):

```powershell
sqllocaldb info MSSQLLocalDB
```

## Quick start — run both together

From the repo root, launch the API and the web app in one step (each opens in its
own PowerShell window):

```powershell
.\run.ps1                         # SQL Server
.\run.ps1 -UseInMemory            # no SQL Server required
.\run.ps1 -UseInMemory -OpenBrowser
```

This starts the API on `http://localhost:5080` and the web app on
`http://localhost:5173` (the ports `frontend/.env` and the API's CORS policy
already expect). Then open:

- Web app: **http://localhost:5173**
- API docs (Swagger): **http://localhost:5080/api-docs**

> The API root (`http://localhost:5080/`) redirects to `/api-docs`. Other routes:
> `/health/live`, `/health/ready`, `/trades`, `/positions`.

Close the two windows (or `Ctrl+C` in each) to stop. To run them separately, use
the two sections below.

## Running the backend

```powershell
cd backend
dotnet run --project TradeBlotter.Api
```

On startup the service **creates the database and runs all migrations** (table,
indexes/constraints, stored procedures) via DbUp — no manual DB setup needed.
In `Development` it also exposes Swagger and the seed admin endpoints.

- API base (example): `http://localhost:5080` (set `ASPNETCORE_URLS` to choose)
- API docs (Swagger UI): `/api-docs` — the root `/` redirects here (Development only)
- Liveness: `/health/live`
- Readiness: `/health/ready`

### Connection string

Configured in `backend/TradeBlotter.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "TradeBlotter": "Server=(localdb)\\MSSQLLocalDB;Database=TradeBlotter;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
}
```

Override without editing the file via environment variable:

```powershell
$env:ConnectionStrings__TradeBlotter = "Server=...;Database=TradeBlotter;..."
```

### Running without SQL Server

The service can run entirely **in-memory** — no database, no migrations — for
reviewers who don't have SQL Server. Flip the toggle in
`appsettings.json` (`"Database": { "UseInMemory": true }`) or via environment
variable:

```powershell
$env:Database__UseInMemory = "true"
dotnet run --project backend/TradeBlotter.Api
```

Everything works the same (trades, positions, symbol search, seeding, real-time)
— data just lives in memory and resets on restart. SQL Server remains the default
and production path.

## Running the frontend

```powershell
cd frontend
npm install
npm run dev
```

The dev server runs at `http://localhost:5173` (allowed by the backend's CORS
policy). It calls the API at `VITE_API_BASE_URL` (`frontend/.env`, default
`http://localhost:5080`) — start the backend first.

| Command | Purpose |
| --- | --- |
| `npm run dev` | Vite dev server with HMR |
| `npm run build` | Type-check (`vue-tsc`) + production build to `dist/` |
| `npm run type-check` | Type-check only |
| `npm run test` | Vitest unit tests (store + formatting) |
| `npm run test:coverage` | Vitest with coverage |

## API

| Method & route | Description |
| --- | --- |
| `POST /trades` | Submit a trade (`symbol`, `side`, `quantity`, `price`). Returns `201` with the stored trade incl. notional. Optional `Idempotency-Key` header makes retries safe (replays return `200`). |
| `GET /trades` | Returns all trades newest first as a JSON array. |
| `GET /positions` | Derived positions (net qty, average cost) per symbol; flat symbols omitted. |
| `GET /symbols/search?q=app` | Symbol autocomplete search, capped at 25 results. |
| `GET /admin/seed-data` | *(dev only)* Seed status `{ enabled, seedRowCount }`. |
| `PUT /admin/seed-data` | *(dev only)* Enable/disable sample seed data. `409` if `Seeding:Enabled` is false. |
| `GET /health/live` | Process liveness. |
| `GET /health/ready` | SQL Server readiness. |
| `/hubs/blotter` (SignalR) | Real-time hub; pushes `TradeCreated` and seed-change reconciliation events. |

Validation failures return RFC 7807 `ValidationProblemDetails` (`400`).

### Optional seeding

Seed data is **off by default** and never required (trades can be entered
manually). In `Development`, enable a deterministic sample set:

```bash
curl -X PUT http://localhost:5080/admin/seed-data -H "Content-Type: application/json" -d '{"enabled":true}'
```

Disabling removes only seed rows (`IsSeedData = 1`); user-entered trades are never
touched.

## Real-time updates (SignalR)

A blotter is a shared, live view: when one trader submits a trade, **everyone
watching should see it immediately** — across tabs, browsers, and machines — and
positions should re-derive without a manual refresh. A plain REST SPA can't do
that (each client only sees its own actions until it reloads).

The service exposes a SignalR hub at **`/hubs/blotter`**. After a trade is
committed, the API broadcasts:

- **`TradeCreated`** — the new trade (clients prepend it, deduped by id), and
- **`BlotterChanged`** — a signal to fully reconcile (e.g. after a seed change).

The web client connects on load (showing a **● Live / Connecting… / Offline**
status), prepends pushed trades, refreshes positions, and **reconciles on
reconnect** (events missed while disconnected are recovered by a refetch).
Broadcasting runs on a background queue, so a real-time hiccup never fails or
slows `POST /trades`.

### How to test it

**In the browser (end-to-end):**

1. Start both (`.\run.ps1`) and open **two** windows/tabs at `http://localhost:5173`.
2. Confirm both headers show a green **● Live**.
3. Submit a trade in one window — it appears in the **other** instantly (no
   reload), and both positions panels update. (Different browsers/machines work
   too, not just tabs.)
4. To see recovery: stop the backend → both show **Offline/Connecting…**; restart
   it → they return to **Live** and refetch the latest state.

**Without the UI:**

```powershell
# The SignalR negotiate handshake should return 200 with a WebSockets transport.
curl -X POST "http://localhost:5080/hubs/blotter/negotiate?negotiateVersion=1"
```

**Automated:** the cross-client push and reconnect behavior are covered by the
frontend store tests (`frontend/src/stores/tradeStore.spec.ts`, which mock the
realtime client) and verified end-to-end during development with two browser
contexts.

## Configuration

| Section | Key | Meaning |
| --- | --- | --- |
| `Migrations` | `RunOnStartup` | Run DbUp migrations on boot (default `true`). |
| `Seeding` | `Enabled` | Master switch for any seed mutation. |
| `Seeding` | `RunOnStartup` | Seed on boot; honored only when `Enabled` (validated at startup). |
| `Admin` | `Enabled` | Map the `/admin/*` routes (dev only; no auth). |

Base config is conservative (seeding/admin off); `appsettings.Development.json`
enables the admin surface without auto-mutating data.

## Tests

The suite is **hybrid**:

- **Unit + component tests** (the bulk) — domain logic plus the full HTTP API
  exercised against the **in-memory** provider. **No SQL Server required.**
- **SQL integration slice** (`[Category("RequiresSqlServer")]`) — a thin set that
  validates the stored procedures, Dapper mappings, and DbUp migrations against a
  real SQL Server (LocalDB `TradeBlotter_Test`, reset between tests with Respawn).

```powershell
cd backend

# Without SQL Server (unit + in-memory component tests only)
dotnet test --filter "TestCategory!=RequiresSqlServer"

# Everything (adds the SQL slice; needs LocalDB)
dotnet test TradeBlotter.sln

# Only the SQL slice
dotnet test --filter "TestCategory=RequiresSqlServer"

# With coverage
dotnet test TradeBlotter.sln --collect:"XPlat Code Coverage"
```

The SQL slice points elsewhere with `TRADEBLOTTER_TEST_CONNECTION`.

## Architecture notes

- **Positions are derived, never stored.** `GET /positions` folds the trade
  ledger using moving weighted-average cost with sign-flip handling. The
  algorithm is in
  [`docs/extras/05-position-calculation.md`](docs/extras/05-position-calculation.md).
- **Data access** is via stored procedures called with Dapper (no LINQ on hot
  paths); `decimal` money, server-assigned UTC timestamps, whole-share `long`
  quantities.
- **Structure:** a single `TradeBlotter.Api` project organized by concern folders
  (`Domain`, `Services`, `Validation`, `Contracts`, `Abstractions`, `Endpoints`,
  `Hubs`, `Infrastructure`, …) with a parallel `TradeBlotter.Api.Tests` project.
  A pluggable persistence provider (`Infrastructure/Persistence` for SQL,
  `…/InMemory` for the no-database mode) sits behind the repository abstractions.

## Assumptions & design decisions

- **.NET 9, SQL Server (LocalDB), Dapper + sprocs** instead of the brief's .NET 8 /
  SQLite / EF — chosen for a production-like, low-latency stack (repository owner's
  direction).
- **Average cost** = moving weighted average; reducing a position leaves the
  remaining average unchanged; crossing zero opens the residual at the crossing
  price. Realized P&L is not reported (not required).
- **Oversells are allowed to go short** — a blotter records what happened rather
  than enforcing inventory.
- **Idempotency** is opt-in via `Idempotency-Key`. Without it, submission is
  at-least-once; a transient retry after a committed insert could duplicate (the
  key removes that risk).
- **Admin/seed endpoints are dev-only and unauthenticated** — gated by config,
  not security. Keep `Admin:Enabled` false in production until an auth policy exists.

## What I'd improve / future work

### Frontend

The SPA is implemented (trade entry form with client + server validation,
fuzzy symbol autocomplete, dark mode, live sortable blotter with notional and
visually distinct Buy/Sell, reactive positions panel, Pinia store, typed API
client). **Real-time multi-user updates are implemented** via SignalR — new
trades push to every connected client (across browsers/machines) with a "● Live"
status indicator; see [docs/websocket-evaluation.md](docs/websocket-evaluation.md).
Remaining enhancements:

- **Table virtualization** — render only visible rows so the blotter stays smooth
  beyond a few thousand trades.
- **Server pagination + server-side sort** — add a separate cursor-based
  contract once ledger size requires it; keep the required `GET /trades`
  behavior stable for the assessment.
- **SignalR backplane** (Redis / Azure SignalR) for multi-instance scale-out;
  hub authorization and groups once auth exists.
- **Position deltas instead of full refetch** — return the affected position from
  `POST /trades` to avoid refetching all positions after every submit.
- **Optimistic, reconciled submit** — render the new trade optimistically and
  reconcile against the server response for snappier perceived latency (without
  inventing client-side IDs).

### Backend

- **Persisted read model / CQRS** for positions at very large ledger sizes. The
  current request-time derived fold is intentionally simple and correct for this
  assessment scale.
- **Broader resilience**: command-level retry tuning, circuit breaker, and a read
  replica for the read-heavy endpoints.
- **Reversible migrations** and a production CORS/auth posture for the admin surface.
- **Metrics and distributed tracing** around database and HTTP operations.

## AI assistance

This project was built with **Claude Code**. Per the assessment, the full
transcript is included at **[`docs/ai-transcript/`](docs/ai-transcript/)**
([`transcript.md`](docs/ai-transcript/transcript.md)). Secrets and emails were
scrubbed (the DB uses Windows auth, no password).
