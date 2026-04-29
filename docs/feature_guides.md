# Feature Guides

Status: Planned

Feature guides will provide task-focused documentation for major Conductor capabilities.

Planned guides include repository onboarding, workflow profile management, Symphony instance operations, secret handling, alert review, and report generation.

## Dashboard Active Repositories

The dashboard active repositories table shows the persisted operational projection for imported repositories. Each row combines repository metadata with related project, Symphony instance, tracked issue, run, pull request, and event data.

The table includes:

- project and repository identity
- repository health derived from persisted Symphony instance health
- active issue count as workload
- running agent count
- failed run count
- open pull request count
- most recent repository activity timestamp

When no repository projection data exists, the dashboard renders an empty state. If the persistence projection cannot be read, the dashboard keeps the shell available and shows an unavailable-data state for the table.
