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

Run the dashboard slice checks with:

```powershell
dotnet test Conductor.slnx
```
