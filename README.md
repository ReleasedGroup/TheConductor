# The Conductor

Conductor is the fleet control layer above Symphony. It supervises many Symphony instances, collects operational state, and presents portfolio-level delivery visibility.

## Development Baseline

This repository targets .NET 10 and uses solution-wide MSBuild defaults from `Directory.Build.props`.

Package versions are managed centrally in `Directory.Packages.props`. Project files should reference packages without inline `Version` attributes unless a local exception is deliberately required.

The planned host configuration starts in `src/Conductor.Host/appsettings.json`. It defines local SQLite, instance, and Symphony release cache paths without storing credentials.
