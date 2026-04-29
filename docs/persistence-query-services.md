# Persistence Query Services

Issue #18 adds the first read-side projection services for dashboard, repository list, and instance summary screens.

The application contracts live in `Conductor.Core.Application.Queries`:

- `IDashboardQueryService`
- `IRepositoryListQueryService`
- `IInstanceSummaryQueryService`

The SQLite implementation is registered by `AddConductorPersistence` and is backed by `SqliteProjectionQueryService`.

## Query Shape

The query services return DTO projections, not EF entities. This keeps Blazor components and Minimal API endpoints decoupled from the persistence model.

Dashboard projections include:

- fleet metrics for managed repositories, healthy repositories, and active agents
- health buckets by `InstanceHealthStatus`
- repository rows for the active repositories table
- instance summary rows for dashboard drill-down

Blocked issue count, open pull request count, and estimated spend are currently returned as zero until the corresponding issue, pull request, run, and usage persistence models are added.

Repository list projections include:

- repository identity and project name
- default branch and web URL
- active instance count
- running instance count
- worst current instance health
- latest health-check timestamp

Instance summary projections include:

- instance identity and repository identity
- project name
- execution mode and base URL
- lifecycle and health status
- latest health-check, last-seen, and snapshot timestamps

## SQLite Notes

Projection queries use `AsNoTracking` and project directly to DTO-friendly row shapes. Destroyed instances are excluded from dashboard and repository summary counts by default.

UTC timestamps are persisted as integer ticks in the SQLite mapping so aggregate queries such as latest health check and latest snapshot can run in SQLite instead of falling back to client-side aggregation.
