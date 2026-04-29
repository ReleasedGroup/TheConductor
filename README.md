# Conductor

Conductor is the fleet control layer above Symphony. It is being built as a .NET 10 modular monolith with a thin host, a core domain/application layer, and infrastructure adapters for external systems.

## Solution Layout

- `Conductor.slnx` is the .NET solution entry point.
- `src/Conductor.Host` contains the Blazor Web App host, health endpoints, worker registration, and the dashboard/repository screens.
- `src/Conductor.Core` contains domain types, value objects, and infrastructure-facing contracts.
- `src/Conductor.Infrastructure.Persistence.Sqlite` contains EF Core SQLite mappings, read-model queries, and development seed data.
- `src/Conductor.Infrastructure.*` projects contain adapter boundaries for GitHub, Symphony HTTP, runners, secrets, reporting, and notifications.
- `tests/Conductor.*.Tests` projects contain the initial unit, persistence, API, Blazor, host, and integration test suites.

## Build And Test

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build
```

## Run Locally

```powershell
dotnet run --project src/Conductor.Host/Conductor.Host.csproj
```

In `Development`, startup creates `./data/conductor.db` when needed, applies the SQLite connection PRAGMAs, and inserts deterministic seed data for the dashboard and repository screens. The seed set includes projects, GitHub repositories, Symphony instances, and latest snapshot payloads, and it is safe to run repeatedly.
