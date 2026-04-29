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
- blocked issue count from tracked issues
- open pull request count, currently returned as zero until pull request metadata is persisted
- health buckets by `InstanceHealthStatus`
- repository rows for the active repositories table
- instance summary rows for dashboard drill-down

Estimated spend is currently returned as zero until run and usage persistence models are added. Open pull request count is also returned as zero until pull request metadata is added to the mapped domain persistence model.

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

SQLite cannot aggregate `DateTimeOffset` consistently through EF Core, so latest health-check and snapshot timestamps are calculated after loading the filtered no-tracking row set.
