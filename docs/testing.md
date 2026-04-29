# Testing Guide

Status: Draft v0.1
Date: 2026-04-29

## Local validation

Run the standard checks from the repository root:

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build
```

The Blazor component test project uses bUnit and covers the dashboard shell components, status badges, empty states, and projection-backed dashboard rendering.

## Dashboard projection data

The current dashboard loads from `src/Conductor.Host/Data/dashboard-projection.json` through `IDashboardProjectionStore`. The file-backed store is intentionally isolated behind the query interface so later EF Core projection queries can replace it without changing the Blazor components.
