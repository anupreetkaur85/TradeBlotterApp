# Evaluation: Merits, Gaps, and Scalability

This document reviews the planning set against the assessment's grading criteria,
records the gaps found, and splits the remediation into two tracks:

- **Backend gaps are treated as fixes to implement.** The repository owner has
  chosen to harden the service beyond the assessment's 3–4 hour suggestion. These
  are folded into [design.md](./design.md) and [implementation-plan.md](./implementation-plan.md).
- **Frontend gaps are treated as "what I'd improve".** They are deliberately
  deferred and belong in the root README's future-work section.

One constraint shapes every backend fix: the brief requires positions to be
**derived from trades, never stored separately**. No fix below introduces a
persisted position table. Caching is in-memory and derived; it is not a second
source of truth.

## Verdict

The plan is strong-to-excellent against the brief's six grading dimensions. The
main risk is not quality but scope relative to the stated time box; the
`Delivery priority` section in [implementation-plan.md](./implementation-plan.md)
mitigates that by sequencing correctness first. The hardening below is
intentionally additive and stays behind the required core in priority.

## Merit against the brief's rubric

| Rubric dimension | Coverage | Evidence |
|---|---|---|
| Domain modeling | Excellent | Clean `Trade`/`Position`; mixed buy/sell and zero-cross fully specified |
| API design | Strong | RFC 7807 ProblemDetails, 201/400, sanitized 500 |
| Vue patterns | Excellent | Clear Pinia boundary; form draft stays local to the composable |
| UI judgment | Excellent | Side shown with text and color, tabular numerals, accessibility |
| Tests | Excellent | Position matrix + integration + Vitest; exceeds "a test or two" |
| Repo quality | Strong | README, transcript, and secret review all planned |

## Backend gaps — to fix (compliant with "positions are derived")

| # | Gap | Fix | Where |
|---|---|---|---|
| B1 | `POST /trades` is not idempotent; a network retry after a committed insert duplicates a trade and corrupts positions | Accept an `Idempotency-Key` (client request id); store it with a filtered unique index; a repeat returns the existing trade (`200`) instead of inserting | design "Reliability, idempotency, and concurrency"; plan Step 4b |
| B2 | Positions read scans per symbol chronologically but no covering index exists for that access path | Add `IX_Trades_Symbol_Chrono` on `(Symbol, ExecutedAtUtc, TradeId)` `INCLUDE (Side, Quantity, Price)` | design "Persistence"; plan Step 2 |
| B3 | No transient-fault handling for SQL Server | Configure `Microsoft.Data.SqlClient` retry logic (or a small Polly policy) around Dapper calls | design "Reliability…"; plan Step 4b |
| B4 | `GET /trades` returns the entire ledger | Preserve the assessment-required array response. Introduce pagination later through a separately versioned contract | design "API contract"; plan Step 4b |
| B5 | `GET /positions` recomputes the full fold on every call | Keep request-time calculation for correctness. A `MAX(TradeId)` cache is unsafe when seed rows can be deleted without changing the maximum user trade id | design "Read models"; plan Step 4b |
| B6 | Seed enable/disable can interleave with inserts | Run the toggle in a serializable transaction that only ever touches `IsSeedData = 1` rows; document the isolation choice | design "Persistence"; plan Step 4 |
| B7 | "Code coverage" is a goal but ungated | Enforce a coverlet line/branch threshold in the test run so coverage regressions fail the build | plan Step 7 |

Notes on the chosen approach for B5: a persisted position snapshot updated on
insert would give O(1) reads, but it violates the brief's "not stored
separately" rule and adds a second source of truth. The implementation therefore
recomputes positions from the ordered ledger on each request. A persisted read
model is listed as future CQRS work, not assessment scope.

## Frontend shortcomings — what I'd improve (future)

These are intentionally out of scope for the submission and should appear in the
root README under future improvements:

- **Table virtualization.** The blotter loads all trades into the store and
  renders them into the DOM. Past a few thousand rows this degrades; a virtual
  scroller (for example `vue-virtual-scroller`) keeps it smooth.
- **Server pagination and server-side sort.** Once the ledger is large, the
  blotter should page from the API (gap B4) and sort server-side; client-side
  sort over the full set stops being correct when only a page is loaded.
- **Real-time, multi-user updates.** Implemented with a server-to-client SignalR
  hub. Clients reconcile with REST after reconnect because events are not durable.
- **Position deltas instead of full refetch.** Today a successful submit refetches
  all positions. Returning the affected position (or a delta) from `POST /trades`
  would cut chatter under rapid entry while keeping the server authoritative.
- **Optimistic, reconciled submit.** Optionally render the new trade optimistically
  and reconcile against the server response, improving perceived latency without
  inventing client-side IDs.

## Scalability analysis

### Backend

- Stateless service scales horizontally behind a load balancer.
- The structural bottleneck is the full-ledger fold on `GET /positions`. The
  covering index reduces database I/O while staying within the "derived" rule.
  The durable scale path (persisted read model / CQRS, set-based or
  incremental projection) is future work.
- Writes are single-row inserts suitable for human entry. A high-ingest feed
  would want batched/bulk insert or queue-based ingestion (future).
- `GET /trades` needs pagination (B4) before it is production-real.
- DbUp is forward-only; production would add reversible migration strategy and a
  read replica for the read-heavy endpoints.

### Frontend

- The store holds the full trade set in memory; see virtualization and server
  pagination above. These are the dominant client-side scale limits.
- Full-set client sort scales only while the full set is loaded; it must move
  server-side alongside pagination.
- SignalR provides multi-user liveness. A production deployment still needs a
  scale-out backplane and durable reconciliation semantics.

## Documentation hygiene (fixed)

The earlier per-tool drafts (Claude and Codex) were moved to `extras/`, which
broke the cross-references in `README.md`, `assignment.md`, and `design.md`. The
links now resolve: the merged set (`assignment`, `design`, `implementation-plan`,
this `evaluation`) is authoritative, and references to the draft documents point
into `extras/`.

## Status summary

| Track | Item | Disposition |
|---|---|---|
| Docs | Broken cross-references | Fixed |
| Backend | B1–B7 | Folded into design + implementation plan to implement |
| Frontend | Virtualization, pagination/sort, position deltas | Deferred to root README future work |
| Full stack | Real-time trade updates | Implemented with SignalR and REST reconciliation on reconnect |
