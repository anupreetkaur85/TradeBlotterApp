# Trade Blotter Planning Documents

This folder converts the assessment in `trade-blotter-exercise.docx` into an
implementation-ready plan for a Vue 3 frontend and a low-latency .NET 9
microservice.

## Authoritative set

These documents are the plan of record. Read them in order:

1. [Assignment and traceability](./assignment.md)
2. [Unified full-stack design](./design.md)
3. [Unified implementation plan](./implementation-plan.md)
4. [Evaluation: merits, gaps, and scalability](./evaluation.md)
5. [Addendum: database initialization configuration and logging](./addendum-configuration-and-logging.md)

The evaluation reviews the plan against the grading criteria. Its backend gaps
are folded into the design and implementation plan as fixes to implement; its
frontend gaps are deferred to the root README as future work.

Quantities are whole shares, timestamps are server-assigned, seed disable removes
seed rows only, and admin endpoints never clear user trades.

The addendum is incremental guidance for the backend scaffold already underway.
It preserves the existing `Migrations`, `Seeding`, and `Admin` configuration
sections and does not change public API or database contracts.

## Supporting drafts (`extras/`)

The earlier per-tool drafts (Claude and Codex) were superseded by the merged set
above and moved to `extras/`. They are kept for reference and traceability only:

- [Assessment analysis](./extras/00-assessment-analysis.md)
- [Architecture and delivery plan](./extras/01-architecture-and-delivery-plan.md)
- [Architecture overview](./extras/01-architecture-overview.md)
- [Backend implementation plan](./extras/02-backend-implementation-plan.md)
- [Frontend implementation plan](./extras/03-frontend-implementation-plan.md)
- [API and database contracts](./extras/04-api-and-database-contracts.md)
- [API contract quick reference](./extras/04-api-contract.md)
- [Position calculation](./extras/05-position-calculation.md)
- [Testing and quality plan](./extras/05-testing-and-quality-plan.md)
- [Database, migrations, and seeding](./extras/07-database-migrations-and-seeding.md)

Where a draft conflicts with the authoritative set, the authoritative set wins.

## Recommended implementation order

1. Scaffold the solution, API, Vue application, and test projects.
2. Create the SQL migration runner, schema, indexes, and optional seed data.
3. Implement and unit-test the position calculation domain service.
4. Implement trade persistence and REST endpoints.
5. Apply backend hardening (idempotency, safe transient-fault retry, health
   checks, and contract-preserving reads) - see implementation plan Step 4b.
6. Implement the Pinia store and API client.
7. Build the trade form, blotter, and positions panel.
8. Add integration and frontend tests.
9. Add Serilog, health checks, OpenAPI, setup documentation, and final polish.

## Scope decisions

- Use .NET 9 as requested by the repository owner, despite the assessment
  naming .NET 8.
- Use SQL Server through Dapper; do not introduce Entity Framework.
- Keep the service stateless. Positions are computed from the trade ledger on
  each request and are never persisted as a second source of truth.
- Avoid LINQ in backend request and domain hot paths. Explicit loops and direct
  Dapper mappings make allocation and behavior visible.
- Keep seeding optional, idempotent, and restricted to development/test
  environments by default.
- Deliver the required core first. Backend hardening is added beyond the time box
  but stays behind the core in delivery priority. SignalR updates are implemented;
  frontend virtualization and distributed infrastructure remain future work.
