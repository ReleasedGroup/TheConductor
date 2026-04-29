# Conductor Testing Guide

This guide describes the initial test project layout and the commands used to validate the solution.

## Test Projects

The solution includes the Sprint 0 test skeletons from `docs/technical.md`:

| Project | Purpose |
| --- | --- |
| `tests/Conductor.Core.Tests` | Unit tests for domain rules, application services, workflow generation, release selection, secret behavior, and alert logic. |
| `tests/Conductor.Persistence.Tests` | SQLite persistence tests for migrations, repositories, query projections, retention, and audit writes. |
| `tests/Conductor.Api.Tests` | Minimal API tests using `WebApplicationFactory` for endpoint behavior, authorization, and validation responses. |
| `tests/Conductor.Blazor.Tests` | bUnit component tests for dashboard UI, status badges, forms, and secret-safe rendering. |
| `tests/Conductor.Integration.Tests` | Fixture-backed integration tests for clients, runners, and cross-project workflows that do not require external credentials by default. |
| `tests/Conductor.Host.Tests` | Host smoke tests for application startup, routing, and health behavior. |

All test projects target `net10.0`, use xUnit, and are included in `Conductor.slnx`.

## Local Validation

Run the same baseline checks used by CI:

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build
dotnet format Conductor.slnx --verify-no-changes
./scripts/validate-docs.ps1
```

Warnings are treated as failures. Do not commit changes that require suppressing compiler, analyzer, package, formatter, or test warnings unless the suppression is intentional and documented.

## External Tests

Tests that require real external services must be opt-in so the default suite remains deterministic.

Use these flags when those suites are added:

```text
CONDUCTOR_RUN_REAL_GITHUB_TESTS=1
CONDUCTOR_RUN_DOCKER_TESTS=1
GITHUB_TOKEN=...
```

CI should leave these flags unset unless a workflow is explicitly validating external integrations.
