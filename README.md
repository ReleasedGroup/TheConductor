# Conductor

Conductor is the fleet control layer above Symphony. It is being built as a .NET 10 modular monolith with a thin host, a core domain/application layer, and infrastructure adapters for external systems.

## Solution Layout

- `Conductor.slnx` is the .NET solution entry point.
- `src/Conductor.Host` contains the Blazor Web App host, health endpoints, worker registration, the dashboard, repository screens, and reusable dashboard components.
- `src/Conductor.Core` contains domain types, value objects, and infrastructure-facing contracts, including the workflow profile, release artifact, and secret descriptor entities.
- `src/Conductor.Infrastructure.Persistence.Sqlite` contains EF Core SQLite mappings, DbContext registration, read-model queries, migrations, and development seed data.
- `src/Conductor.Infrastructure.*` projects contain adapter boundaries for GitHub, Symphony HTTP, runners, secrets, reporting, and notifications.
- `tests/Conductor.*.Tests` projects contain the initial unit, persistence, API, Blazor, host, and integration test suites.

## Build And Test

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build
dotnet format Conductor.slnx --no-restore --verify-no-changes
```

## Run Locally

```powershell
dotnet run --project src/Conductor.Host/Conductor.Host.csproj
```

The root route (`/`) serves the dashboard baseline for local startup checks, including reusable status badges, shared UI state components, repository orchestration health, workload, needs-attention, active repository, quick-action, and live activity sections loaded through the dashboard projection query interface. The same startup baseline also exposes `/health/live` and `/health/ready`.

In `Development`, startup applies the current EF Core migration set and inserts deterministic seed data for the SQLite dashboard and repository query services. The seed set includes projects, GitHub repositories, Symphony instances, and latest snapshot payloads, and it is safe to run repeatedly.

Set `Conductor:BootstrapDevelopmentDatabase` to `false` to skip the development database bootstrap in test hosts or other controlled startup scenarios.

## Persistence Configuration

The host registers `ConductorDbContext` from `src/Conductor.Infrastructure.Persistence.Sqlite` using the `ConnectionStrings:Conductor` value. The default is:

```text
Data Source=./data/conductor.db;Cache=Shared
```

The SQLite registration creates the database directory when a file-backed connection string is used and applies the required startup PRAGMAs for foreign keys, WAL journaling, and a 5 second busy timeout when EF Core opens a connection.

## Symphony Release Resolution

`Conductor.Infrastructure.GitHub` registers `ISymphonyReleaseResolver` for resolving Symphony releases from GitHub Releases. The resolver calls the latest release endpoint for the default `latest` selector, calls the tag-specific release endpoint for pinned selectors, and selects a release asset that matches the requested execution mode, operating system, and architecture.

The resolver returns the resolved tag, release metadata, selected asset URL, size, content type, and checksum/digest when GitHub provides one. Later provisioning and runner services can persist that result through the existing `SymphonyReleaseArtifact` and `SymphonyInstance` provenance fields.

## Instance Collector

The host registers an `InstanceCollector` background worker that polls non-destroyed Symphony instances from persistence. Defaults are configured in `src/Conductor.Host/appsettings.json`: health every 10 seconds, state every 30 seconds, runtime every 2 minutes, with a 1 second loop delay. Each collection writes an `InstanceSnapshots` row, updates the instance health timestamps, emits health transition events, and creates or resolves the built-in offline alert.

## Persistence Migrations

Apply the current EF Core migration set with:

```powershell
dotnet ef database update --project src/Conductor.Infrastructure.Persistence.Sqlite --startup-project src/Conductor.Infrastructure.Persistence.Sqlite
```
