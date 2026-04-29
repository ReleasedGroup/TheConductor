# Dashboard

The dashboard renders fleet-level projection data through `IDashboardProjectionStore`.
The first implementation stores a deterministic JSON projection at
`src/Conductor.Host/Data/dashboard-projection.json` so the Blazor dashboard can
render from persisted data before the EF Core dashboard query projection lands.

Current metric tile keys:

- `healthy-repositories`
- `active-agents`
- `blocked-issues`
- `open-pull-requests`
- `ai-spend-today`

The same dashboard projection can include `instanceRuntimes` entries for the
Symphony runtime panel. Each entry carries the instance key and display name,
repository and base URL, `healthStatus`, `lifecycleStatus`, Symphony version,
workflow owner/repository/source path metadata, the last health/snapshot/seen
timestamps, and last snapshot counters for active issues, running sessions,
retry queue, failed runs, and token totals.

Run the dashboard slice checks with:

```powershell
dotnet test Conductor.slnx
```

## Instance Registry

The `/instances` page provides the first manual Symphony registration UI. It uses
the same registration service as `POST /api/instances/register`, then refreshes
the persisted instance list from `IInstanceSummaryQueryService`.
