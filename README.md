# Conductor

Conductor is the fleet control layer above Symphony. It is being built as a .NET 10 modular monolith with a thin host, a core domain/application layer, and infrastructure adapters for external systems.

## Solution Layout

- `Conductor.slnx` is the .NET solution entry point.
- `src/Conductor.Host` contains the ASP.NET Core Blazor Web App host.
- `src/Conductor.Core` contains domain types, value objects, and infrastructure-facing contracts.
- `src/Conductor.Infrastructure.*` projects contain adapter boundaries for GitHub, Symphony HTTP, runners, secrets, reporting, and notifications.
- `tests/Conductor.Core.Tests` contains focused validation for the initial core and infrastructure skeleton.
- `tests/Conductor.Host.Tests` contains host startup and placeholder dashboard smoke tests.

The SQLite persistence project is tracked separately in Sprint 0 issues.

## Build And Test

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build
```
