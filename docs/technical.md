# Conductor Technical Specification and Sprint Plan

Status: Draft v0.1
Date: 2026-04-29
Source: `docs/requirements.md`

## 1. Purpose

This document defines the technical design, implementation plan, validation strategy, CI/CD approach, and sprint roadmap for Conductor.

Conductor is a .NET 10 application with a Blazor front end that supervises many Symphony instances. It provides fleet visibility, instance lifecycle control, GitHub repository import, release-based Symphony provisioning, per-instance credentials, alerts, reporting, and operational dashboards.

## 2. Technical Decisions

| Area | Decision |
| --- | --- |
| Runtime | .NET 10 |
| Host | ASP.NET Core |
| Front end | Blazor Web App with interactive server rendering |
| API style | ASP.NET Core Minimal APIs grouped by feature |
| Database | SQLite using EF Core |
| Background work | `BackgroundService` workers with internal queues and persisted operation records |
| Live updates | SignalR, primarily through Blazor interactive server circuits and explicit hubs where useful |
| Styling | Tailwind CSS-compatible utility-first styling or scoped CSS following the dark operational UI in `docs/UIOverview.png` |
| Tests | xUnit, bUnit, WebApplicationFactory, Testcontainers or fakes where useful |
| CI/CD | GitHub Actions for restore, build, test, lint/format checks, packaging, and release artifacts |
| Deployment | Local developer run first, Docker image second, Azure-ready design later |

### 2.1 SQLite Decision

SQLite is the selected database for MVP and initial deployments. This keeps the product easy to install on a workstation or internal server while the domain model stabilizes.

The persistence layer must still isolate EF Core provider-specific details so a future SQL Server or PostgreSQL provider can be added without rewriting domain or application services.

SQLite requirements:

- Use WAL mode.
- Use EF Core migrations.
- Use short transactions for polling writes.
- Avoid long-running read transactions in dashboard queries.
- Add indexes for dashboard, polling, and reporting queries from the first migration.
- Treat SQLite as a single-node deployment database; do not design MVP background workers as horizontally scaled writers.

## 3. Architecture Overview

```text
+--------------------------------------------------------------+
|                      Conductor Host                          |
|                                                              |
|  Blazor Web App                 Minimal API endpoints         |
|  Dashboard                      Repos / Instances / Runs      |
|  Repositories                   Reports / Alerts / Policies   |
|  Instance Detail                Settings / Secrets            |
+----------------------------+---------------------------------+
                             |
                             v
+--------------------------------------------------------------+
|                    Application Layer                         |
|  Repo import workflows       Instance lifecycle use cases     |
|  Release resolution          Snapshot collection services     |
|  Workflow generation         Alert evaluation                 |
|  Report generation           Audit/event recording            |
+----------------------------+---------------------------------+
                             |
                             v
+--------------------------------------------------------------+
|                       Domain Layer                           |
|  Projects / Repositories / SymphonyInstances                 |
|  WorkflowProfiles / Snapshots / Runs / Issues                |
|  Alerts / Policies / Reports / Secrets / AuditEvents         |
|  Domain rules and status classification                      |
+----------------------------+---------------------------------+
                             |
                             v
+--------------------------------------------------------------+
|                   Infrastructure Layer                       |
|  EF Core SQLite             GitHub client                    |
|  Symphony API client        GitHub Releases client            |
|  Local process runner       Docker runner                     |
|  Secret protection          Report renderers                  |
|  File/runtime cache         Notification adapters             |
+--------------------------------------------------------------+
```

### 3.1 Architectural Style

Use a modular monolith. Conductor does not need microservices for the MVP. A modular monolith gives clean boundaries while preserving simple deployment, transactions, and local debugging.

Rules:

- Domain types must not reference ASP.NET Core, EF Core, Docker, GitHub SDKs, Azure SDKs, or UI components.
- Application services orchestrate use cases and depend on interfaces.
- Infrastructure projects implement interfaces for persistence, GitHub, Symphony HTTP, runners, releases, secrets, and reporting.
- The Blazor host composes services and owns UI/API endpoints.
- Background workers call application services, not infrastructure adapters directly unless the adapter is their direct responsibility.

## 4. Solution Layout

Recommended repository structure:

```text
src/
  Conductor.Host/
    Program.cs
    appsettings.json
    Components/
    Endpoints/
    Workers/
    wwwroot/
  Conductor.Core/
    Domain/
    Application/
    Abstractions/
    Policies/
  Conductor.Infrastructure.Persistence.Sqlite/
    ConductorDbContext.cs
    Migrations/
    Repositories/
  Conductor.Infrastructure.GitHub/
    GitHubRepositoryClient.cs
    GitHubReleaseClient.cs
  Conductor.Infrastructure.Symphony/
    SymphonyApiClient.cs
    SymphonyContracts.cs
  Conductor.Infrastructure.Runners.Local/
    LocalProcessSymphonyRunner.cs
  Conductor.Infrastructure.Runners.Docker/
    DockerSymphonyRunner.cs
  Conductor.Infrastructure.Secrets/
    DataProtectionSecretProtector.cs
  Conductor.Infrastructure.Reporting/
    MarkdownReportRenderer.cs
    HtmlReportRenderer.cs
  Conductor.Infrastructure.Notifications/
    EmailNotificationSender.cs
    TeamsNotificationSender.cs
tests/
  Conductor.Core.Tests/
  Conductor.Persistence.Tests/
  Conductor.Api.Tests/
  Conductor.Blazor.Tests/
  Conductor.Integration.Tests/
docs/
  requirements.md
  technical.md
  api.md
  deployment.md
  testing.md
```

Initial solution files:

```text
Conductor.slnx
Directory.Build.props
Directory.Packages.props
global.json
.editorconfig
```

## 5. Project Responsibilities

### 5.1 `Conductor.Host`

Responsibilities:

- ASP.NET Core app startup.
- Blazor Web App pages and components.
- Minimal API route registration.
- Authentication and authorization configuration.
- SignalR hub registration.
- Hosted background service registration.
- Static assets and CSS.
- Health endpoints.

The host must stay thin. Business workflows belong in `Conductor.Core.Application`.

### 5.2 `Conductor.Core`

Responsibilities:

- Domain entities.
- Value objects.
- Domain events.
- Status classification logic.
- Use case interfaces.
- Application services.
- DTOs used by application APIs.
- Validation rules that do not require infrastructure.

Examples:

- `ImportRepositoryService`
- `RegisterInstanceService`
- `StartInstanceService`
- `CollectInstanceSnapshotService`
- `GenerateWorkflowService`
- `EvaluateAlertsService`
- `GenerateReportService`

### 5.3 Persistence Infrastructure

Responsibilities:

- EF Core SQLite context and migrations.
- Entity configurations.
- Query projections for dashboards and reports.
- Transaction management.
- SQLite options and initialization.

Persistence must use explicit UTC timestamps and avoid relying on local machine time.

### 5.4 GitHub Infrastructure

Responsibilities:

- Discover organizations and repositories.
- Fetch repository metadata.
- Validate PAT permissions where possible.
- Resolve GitHub Releases for Symphony.
- Download release assets.
- Optionally inspect PR/check status later.

### 5.5 Symphony Infrastructure

Responsibilities:

- Typed HTTP client for Symphony endpoints.
- Health, runtime, state, workflow, issue detail, and refresh calls.
- Resilient timeout handling.
- JSON payload preservation.
- Contract mapping to Conductor snapshot models.

### 5.6 Runner Infrastructure

Responsibilities:

- Provision local process and Docker instances.
- Start, stop, restart, destroy, and inspect instances.
- Inject per-instance credentials.
- Capture logs.
- Record operation events.

Azure runner interfaces should be designed but not implemented in the first sprint set unless explicitly prioritized.

## 6. Domain Model

### 6.1 Core Entities

| Entity | Purpose |
| --- | --- |
| `Project` | Business grouping for repositories. |
| `Repository` | GitHub repository imported into Conductor. |
| `SymphonyInstance` | Managed or manually registered Symphony runtime. |
| `WorkflowProfile` | Reusable `WORKFLOW.md` source template and settings. |
| `SymphonyReleaseArtifact` | Cached release asset or image provenance. |
| `InstanceSnapshot` | Health/runtime/state snapshot captured from Symphony. |
| `TrackedIssue` | Normalized issue summary. |
| `Run` | Normalized Symphony issue execution. |
| `RunAttempt` | Attempt/retry details. |
| `Event` | Operational event stream. |
| `Alert` | Attention item derived from events/snapshots/policies. |
| `Policy` | Governance settings. |
| `Report` | Generated Markdown/HTML/PDF report metadata and content. |
| `SecretDescriptor` | Metadata for encrypted credentials. |
| `AuditEvent` | User/system action audit trail. |
| `BackgroundOperation` | Long-running lifecycle/provisioning/report job status. |

### 6.2 Key Value Objects

- `ProjectId`
- `RepositoryId`
- `SymphonyInstanceId`
- `WorkflowProfileId`
- `SecretId`
- `GitHubRepositoryFullName`
- `ReleaseSelector`
- `ReleaseTag`
- `ExecutionMode`
- `InstanceHealthStatus`
- `InstanceLifecycleStatus`
- `CredentialInheritanceMode`
- `UtcDateTimeRange`

Use strongly typed IDs in domain code. EF Core can persist them through value converters.

### 6.3 Status Model

Instance status is split into:

- Lifecycle status: `NotProvisioned`, `Provisioned`, `Starting`, `Running`, `Stopping`, `Stopped`, `Failed`, `Destroyed`.
- Health status: `Unknown`, `Healthy`, `Warning`, `Critical`, `Offline`.
- Delivery status: `Healthy`, `AttentionNeeded`, `Blocked`, `AtRisk`.

This separation prevents a running but failing repository from being shown as simply "up".

## 7. Database Design

### 7.1 SQLite Configuration

Use connection string:

```text
Data Source=./data/conductor.db;Cache=Shared
```

Startup configuration:

```sql
PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=5000;
```

### 7.2 Initial Tables

Initial migration should create:

- `Projects`
- `Repositories`
- `SymphonyInstances`
- `WorkflowProfiles`
- `SymphonyReleaseArtifacts`
- `InstanceSnapshots`
- `TrackedIssues`
- `Runs`
- `RunAttempts`
- `Events`
- `Alerts`
- `Reports`
- `SecretDescriptors`
- `EncryptedSecretValues`
- `AuditEvents`
- `BackgroundOperations`

### 7.3 Indexes

Required indexes:

- `Repositories(ProjectId)`
- `Repositories(Provider, Owner, Name)` unique
- `SymphonyInstances(RepositoryId)`
- `SymphonyInstances(Status, HealthStatus)`
- `SymphonyInstances(ExecutionMode)`
- `InstanceSnapshots(SymphonyInstanceId, CapturedAtUtc)`
- `TrackedIssues(RepositoryId, GitHubIssueNumber)`
- `TrackedIssues(RepositoryId, SymphonyStatus, IsBlocked)`
- `Runs(SymphonyInstanceId, Status, StartedAtUtc)`
- `Runs(RepositoryId, GitHubIssueNumber)`
- `Events(SymphonyInstanceId, OccurredAtUtc)`
- `Events(RepositoryId, OccurredAtUtc)`
- `Alerts(Status, Severity, CreatedAtUtc)`
- `Reports(ReportType, GeneratedAtUtc)`
- `AuditEvents(ActorUserId, OccurredAtUtc)`
- `SecretDescriptors(ScopeType, ScopeId, SecretType)`
- `BackgroundOperations(Status, CreatedAtUtc)`

### 7.4 JSON Storage

SQLite JSON columns should be stored as text. Use JSON for raw third-party payloads and flexible telemetry, not for frequently filtered fields.

Raw JSON fields:

- `InstanceSnapshots.HealthJson`
- `InstanceSnapshots.RuntimeJson`
- `InstanceSnapshots.StateJson`
- `Events.PayloadJson`
- `TrackedIssues.LabelsJson`
- `TrackedIssues.AssigneesJson`
- `TrackedIssues.PullRequestsJson`
- `Reports.MetadataJson`

Frequently queried values must be extracted into first-class columns.

### 7.5 Snapshot Normalized Columns

`InstanceSnapshots` stores raw Symphony health, runtime, and state payloads together with normalized fields used by dashboard and reporting queries.

Health normalization:

- `HealthStatus`
- `HttpStatusCode`
- `LatencyMilliseconds`
- `ErrorMessage`

Runtime normalization:

- `ApplicationName`
- `ApplicationVersion`
- `RuntimeInstanceId`
- `WorkflowOwner`
- `WorkflowRepository`
- `WorkflowSourcePath`
- `PersistenceProvider`
- `RuntimeDefaultsJson`

State normalization:

- `ActiveIssueCount`
- `RunningSessionCount`
- `RetryQueueCount`
- `FailedRunCount`
- `TokenInputTotal`
- `TokenOutputTotal`

## 8. Application Services

### 8.1 Repository Import

Flow:

1. User selects GitHub repository.
2. Conductor fetches repository metadata.
3. User chooses project, execution mode, release selector, workflow profile, GitHub credential, and OpenAI/Codex credential.
4. Conductor validates credentials and required configuration.
5. Conductor creates or updates `Repository`.
6. Conductor creates `SymphonyInstance` in `NotProvisioned` state if orchestration is requested.
7. Audit event is recorded.

### 8.2 Manual Instance Registration

Flow:

1. User enters instance URL.
2. Conductor calls `/api/v1/health`.
3. Conductor calls `/api/v1/runtime`.
4. Conductor creates `SymphonyInstance`.
5. Conductor queues initial state collection.
6. Dashboard begins displaying the instance.

### 8.3 Symphony Release Resolution

Flow:

1. Read instance release selector.
2. If selector is `latest`, call GitHub Releases latest endpoint.
3. If selector is a tag, call tag-specific release endpoint.
4. Select compatible asset or image metadata.
5. Check local cache.
6. Download and verify artifact if missing.
7. Store `SymphonyReleaseArtifact`.
8. Record selected artifact on provisioning operation.

Rules:

- Restarts must reuse the instance's resolved release tag.
- New instance creation with `latest` may resolve to a newer tag.
- Upgrades require an explicit upgrade operation.

### 8.4 Workflow Generation

Inputs:

- Repository metadata.
- Workflow profile.
- Execution mode.
- Port.
- Workspace paths.
- Secret environment variable names.

Outputs:

- `WORKFLOW.md` in instance `config` folder.
- Workflow validation result.
- Audit event.

Rules:

- Never inline GitHub PAT or OpenAI API key.
- Use `tracker.api_key: $GITHUB_TOKEN`.
- Use container Linux paths for Docker mode.
- Use host paths for local process mode.

### 8.5 Instance Lifecycle

Lifecycle operations are asynchronous and persisted as `BackgroundOperation` rows.

Supported operations:

- `ProvisionInstance`
- `StartInstance`
- `StopInstance`
- `RestartInstance`
- `DestroyInstance`
- `RefreshInstance`
- `UpgradeInstance`

Each operation must:

- Validate user authorization.
- Validate instance status.
- Record audit event.
- Create operation row.
- Execute through selected runner.
- Record success/failure.
- Emit events.
- Trigger UI update.

### 8.6 Snapshot Collection

Workers:

- `HealthCollectorWorker`
- `RuntimeCollectorWorker`
- `StateCollectorWorker`

Default polling:

- Health: 10 seconds.
- State: 30 seconds.
- Runtime: 2 minutes.

The MVP may implement one combined `InstanceCollectorWorker` with separate schedules per instance, then split workers later if needed.

Rules:

- One failing instance must not block others.
- Use per-instance timeout and backoff.
- Persist raw JSON and normalized aggregates.
- Emit health transition events.
- Notify UI after meaningful changes.

### 8.7 Alert Evaluation

Alert evaluation runs after snapshot ingestion and on a periodic schedule.

Initial alert rules:

- Instance offline.
- Health warning/critical.
- Repeated run failures.
- Stalled run.
- High token usage.
- Low GitHub rate limit.
- Active issues with no running sessions.

Offline instance evaluation records a health snapshot for each pollable registered
Symphony instance. When an instance transitions to `Offline`, Conductor emits an
`InstanceOffline` event and creates one unresolved critical in-app alert for that
instance so repeated failed polls do not duplicate the same active alert.

MVP alert delivery is in-app only. External delivery adapters are later sprints.

### 8.8 Reporting

Reports are generated from persisted Conductor data.

MVP outputs:

- Markdown.
- HTML.

PDF can be added using Playwright rendering once HTML reports are stable.

Initial report types:

- Daily Delivery Brief.
- Weekly Software Factory Report.
- Project Report.

## 9. Infrastructure Interfaces

### 9.1 Symphony Runner

```csharp
public interface ISymphonyRunner
{
    ExecutionMode Mode { get; }

    Task<ProvisionedInstance> ProvisionAsync(
        SymphonyInstanceSpec spec,
        CancellationToken cancellationToken);

    Task StartAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken);

    Task StopAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken);

    Task RestartAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken);

    Task<InstanceHealthProbe> GetHealthAsync(
        SymphonyInstanceId instanceId,
        CancellationToken cancellationToken);

    Task<InstanceLogs> GetLogsAsync(
        SymphonyInstanceId instanceId,
        LogQuery query,
        CancellationToken cancellationToken);

    Task DestroyAsync(
        SymphonyInstanceId instanceId,
        DestroyInstanceOptions options,
        CancellationToken cancellationToken);
}
```

### 9.2 Symphony API Client

```csharp
public interface ISymphonyApiClient
{
    Task<SymphonyHealthResponse> GetHealthAsync(Uri baseUri, CancellationToken ct);
    Task<SymphonyRuntimeResponse> GetRuntimeAsync(Uri baseUri, CancellationToken ct);
    Task<SymphonyWorkflowDocument> GetWorkflowAsync(Uri baseUri, CancellationToken ct);
    Task<SymphonyWorkflowDocument> SaveWorkflowAsync(Uri baseUri, SymphonyWorkflowDocument document, CancellationToken ct);
    Task<SymphonyStateResponse> GetStateAsync(Uri baseUri, CancellationToken ct);
    Task<SymphonyIssueResponse?> GetIssueAsync(Uri baseUri, string issueIdentifier, CancellationToken ct);
    Task<SymphonyRefreshResponse> RequestRefreshAsync(Uri baseUri, CancellationToken ct);
}
```

### 9.3 Secret Store

```csharp
public interface ISecretStore
{
    Task<SecretDescriptor> CreateAsync(CreateSecretRequest request, CancellationToken ct);
    Task RotateAsync(SecretId secretId, RotateSecretRequest request, CancellationToken ct);
    Task<ResolvedSecret> ResolveAsync(SecretReference reference, CancellationToken ct);
    Task<ResolvedSecret?> ResolveAsync(SecretResolutionRequest request, CancellationToken ct);
    Task<IReadOnlyList<SecretDescriptor>> ListAsync(SecretQuery query, CancellationToken ct);
    Task DeleteAsync(SecretId secretId, CancellationToken ct);
}
```

Secret values must be protected with ASP.NET Core Data Protection before persistence. `SecretDescriptor` rows contain only metadata; encrypted payloads are stored separately and must only be returned to runner/provisioning code paths that need to inject them. UI and ordinary API responses must receive descriptors only.

### 9.4 Release Resolver

```csharp
public interface ISymphonyReleaseResolver
{
    Task<ResolvedSymphonyRelease> ResolveAsync(
        ReleaseSelector selector,
        RuntimeTarget target,
        CancellationToken cancellationToken);
}
```

`RuntimeTarget` includes execution mode, OS, architecture, and preferred image/artifact type.

## 10. Blazor Front End Design

### 10.1 Blazor Mode

Use a .NET 10 Blazor Web App with interactive server rendering.

Reasons:

- Internal operations app.
- Real-time dashboard interactions.
- Easy integration with server-side application services.
- Minimal client-side credential exposure.
- Faster MVP delivery than a separate SPA and API client.

### 10.2 Page Structure

Routes:

```text
/                         Dashboard
/projects                 Project list
/projects/{id}            Project detail
/repositories             Repository list
/repositories/{id}        Repository command centre
/instances                Instance list
/instances/{id}           Instance detail
/runs                     Run list
/runs/{id}                Run detail
/pull-requests            PR list
/issues                   Issue list
/reports                  Report list
/reports/{id}             Report detail
/policies                 Policy list
/infrastructure           Runtime/cache/runner view
/alerts                   Alert center
/settings                 Settings
/settings/secrets         Secret descriptors
```

### 10.3 Component Structure

Shared components:

- `AppShell`
- `SideNav`
- `TopBar`
- `MetricTile`
- `StatusBadge`
- `HealthHeatmap`
- `WorkloadDonut`
- `NeedsAttentionPanel`
- `RepositoryTable`
- `LiveActivityStream`
- `RunTimeline`
- `InstanceHealthPanel`
- `WorkflowEditor`
- `SecretSelector`
- `ConfirmDialog`
- `AsyncOperationToast`

### 10.4 Dashboard Data Loading

Use query services that return projection DTOs rather than EF entities.

The initial persistence query service contracts and SQLite implementation are documented in
`docs/persistence-query-services.md`.

Dashboard projection:

- Fleet metrics.
- Health heatmap buckets.
- Workload counts.
- Needs attention items.
- Active repository rows.
- Live activity events.

The workload overview projection groups active issue workload by status and returns a stable ordered status list so the Blazor dashboard can render every bucket, including zero-count buckets, without coupling UI code to persistence entities.

Refresh behavior:

- Initial SSR load uses query service.
- Interactive updates use SignalR notifications and targeted reloads.
- If SignalR disconnects, fall back to timed refresh.

### 10.5 UI Design Requirements

- Dark mode first.
- Dense operational layout.
- No marketing landing page.
- Use familiar icons for actions.
- Status must not rely on color alone.
- Tables must support filtering and sorting.
- Dangerous operations require confirmation.
- Long-running operations must show progress.

## 11. API Design

Minimal APIs will serve both Blazor components and external callers. APIs should be stable enough for automation, but Blazor components may call application services directly where it keeps implementation simpler.

Endpoint groups:

```text
/api/projects
/api/repos
/api/instances
/api/runs
/api/issues
/api/reports
/api/policies
/api/alerts
/api/secrets
/api/operations
/api/settings
```

### 11.1 Error Response

Use a consistent problem shape:

```json
{
  "error": {
    "code": "instance_not_found",
    "message": "The Symphony instance was not found.",
    "correlationId": "..."
  }
}
```

For validation:

```json
{
  "error": {
    "code": "validation_failed",
    "message": "One or more fields are invalid.",
    "fields": {
      "githubCredential": ["A GitHub credential is required."]
    },
    "correlationId": "..."
  }
}
```

## 12. Security Design

### 12.1 Authentication

MVP options:

- Local single-admin mode for development.
- ASP.NET Core Identity with SQLite for internal deployment.
- Microsoft Entra ID later for organization deployment.

Technical path:

1. Implement authorization policies from the start.
2. Use a development auth provider for local development.
3. Add ASP.NET Core Identity before production use.
4. Keep user identity abstraction independent of the chosen auth provider.

### 12.2 Authorization Policies

Roles:

- Viewer.
- Developer.
- Operator.
- Administrator.

Policies:

- `CanViewDashboard`
- `CanViewSecretsMetadata`
- `CanManageSecrets`
- `CanStartStopInstances`
- `CanEditWorkflowProfiles`
- `CanManagePolicies`
- `CanGenerateReports`
- `CanManageInfrastructure`

### 12.3 Secret Handling

Secret types:

- `GitHubPat`
- `GitHubAppPrivateKey`
- `OpenAiApiKey`
- `CodexHomeReference`
- `ContainerRegistryCredential`
- `NotificationWebhook`

Secret scopes:

- Global.
- Project.
- Repository.
- Symphony instance.

Resolution precedence:

1. Instance-scoped secret.
2. Repository-scoped secret.
3. Project-scoped secret.
4. Global default.

Credential modes:

- `SpecificSecret` resolves the selected secret descriptor directly.
- `InheritDefault` resolves scoped descriptors using the precedence order above.
- `None` resolves no credential even when scoped defaults exist.

Secret storage:

- Store encrypted values using ASP.NET Core Data Protection for MVP.
- Store secret descriptors separately from encrypted payloads in `SecretDescriptors` and `EncryptedSecretValues`.
- Never return secret values through ordinary APIs.
- Never persist resolved secret values in operation logs.

Runner injection:

- Local process: process environment.
- Docker: container environment and/or mounted Codex home path.
- Azure later: Key Vault references or secret environment variables.

### 12.4 Redaction

All log/event/report generation paths must use a shared redaction service.

Redact:

- GitHub PAT formats.
- OpenAI API key formats.
- `GITHUB_TOKEN` values.
- `OPENAI_API_KEY` values.
- Authorization headers.
- Connection strings containing credentials.

## 13. Local Process Runner Design

### 13.1 Runtime Layout

```text
data/
  conductor.db
instances/
  {instanceId}/
    config/
      WORKFLOW.md
    logs/
      stdout.log
      stderr.log
    runtime/
      symphony-release.json
      extracted/
    symphony-data/
      data/
      workspaces/
      codex-home/
cache/
  symphony-releases/
    {tag}/
      manifest.json
      artifacts/
```

### 13.2 Start Command

For local release bundles:

```text
Symphony --port {port} {workflowPath}
```

Actual executable name is resolved from the downloaded release artifact for the host OS and architecture.

### 13.3 Process Management

- Start with explicit working directory.
- Set environment variables per instance.
- Redirect stdout/stderr to instance logs.
- Store process ID.
- Detect process exit.
- On restart, stop existing process first.
- Avoid killing unrelated processes if PID no longer belongs to Symphony.

## 14. Docker Runner Design

### 14.1 Container Naming

Container name format:

```text
conductor-symphony-{owner}-{repo}-{instanceShortId}
```

Names must be sanitized and length-limited.

### 14.2 Docker Configuration

Container environment:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
SYMPHONY_WORKFLOW_PATH=/config/WORKFLOW.md
Persistence__ConnectionString=Data Source=/var/lib/symphony/data/symphony.db;Cache=Shared;Mode=ReadWriteCreate
Orchestration__InstanceId={instanceId}
GITHUB_TOKEN={resolved per-instance secret}
OPENAI_API_KEY={resolved per-instance secret when configured}
```

Container mounts:

```text
{instance}/config/WORKFLOW.md:/config/WORKFLOW.md:ro
{instance}/symphony-data:/var/lib/symphony
{instance}/symphony-data/codex-home:/home/symphony/.codex
```

Port:

```text
{hostPort}:8080
```

### 14.3 Image Strategy

Preferred order:

1. Release-specific official Symphony image if available.
2. Release artifact that includes a Docker image reference or Dockerfile.
3. Local image built from the resolved release source/artifact.

Every container must record:

- Release selector.
- Resolved release tag.
- Image name.
- Image digest or local image ID.
- Artifact source URL.

## 15. GitHub Integration

### 15.1 Authentication

MVP uses one or more stored GitHub PATs. Each repository or instance can use its own PAT.

The GitHub client receives a `SecretReference`, resolves it only for the request scope, and disposes of the plaintext value immediately after use.

Selected PATs are validated against the target repository before import where practical. The GitHub adapter calls the repository metadata endpoint with the selected token, treats `200` as repository reachability, inspects the returned repository permissions when GitHub includes them, and maps invalid-token, not-found/no-access, forbidden/SSO-policy, and rate-limit responses to actionable validation results without storing the plaintext token.

### 15.2 Repository Discovery

Use GitHub REST or GraphQL depending on API coverage. Keep a repository-facing abstraction so the rest of the app does not care which API is used.

Discovery fields:

- Owner.
- Name.
- Full name.
- Clone URL.
- HTML URL.
- Default branch.
- Visibility.
- Archived flag.
- Open issue count.
- PR count.
- Labels.
- Milestones.
- Branch protection summary.
- Actions status summary.

### 15.3 Releases

Use GitHub Releases API for Symphony release resolution.

Behaviors:

- Resolve `latest`.
- Resolve specific tag.
- Cache metadata.
- Download compatible assets.
- Persist provenance.
- Surface failure if release assets do not match target runtime.

## 16. Symphony API Integration

### 16.1 HTTP Client

Use `IHttpClientFactory` with named client:

```text
SymphonyApi
```

Defaults:

- Health timeout: 3 seconds.
- State/runtime timeout: 10 seconds.
- Issue detail timeout: 10 seconds.
- Workflow save timeout: 15 seconds.

Use Polly-style resilience only if available and lightweight; do not hide repeated failures.

### 16.2 Snapshot Mapping

Health:

- Raw status.
- HTTP status.
- Latency.
- Error message.

Runtime:

- Application name/version.
- Instance ID.
- Workflow owner/repo.
- Workflow source path.
- Persistence provider.
- Runtime defaults.

State:

- Running count.
- Retrying count.
- Tracked count.
- Running sessions.
- Retry queue.
- Tracked issue distribution.
- Recent activity.
- Lease status.
- Token totals.
- Runtime seconds.
- Rate limits.

## 17. CI/CD

### 17.1 GitHub Actions Workflows

Create these workflows:

```text
.github/workflows/ci.yml
.github/workflows/release.yml
.github/workflows/docs.yml
```

### 17.2 CI Workflow

Triggers:

- Pull request to `main`.
- Push to `main`.
- Manual dispatch.

Jobs:

1. Restore.
2. Build with warnings as errors.
3. Run unit tests.
4. Run integration tests that do not require external credentials.
5. Run Blazor component tests.
6. Run formatting check.
7. Upload test results and coverage.

Commands:

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build --collect:"XPlat Code Coverage"
dotnet format Conductor.slnx --verify-no-changes
```

### 17.3 Documentation Workflow

Docs workflow should:

- Validate Markdown links where practical.
- Check for trailing whitespace.
- Check that required docs exist.
- Optionally render docs to HTML artifact.

Required docs for CI:

- `docs/requirements.md`
- `docs/technical.md`
- `docs/deployment.md` before first release.
- `docs/testing.md` before first release.
- `docs/api.md` before first API-stable release.

### 17.4 Release Workflow

Release workflow should:

- Run CI first.
- Publish self-contained builds for target runtimes.
- Build Docker image.
- Tag image with release tag and commit SHA.
- Generate SBOM if tooling is available.
- Create GitHub Release.
- Attach release archives.

Initial target runtimes:

- `win-x64`
- `linux-x64`

Later:

- `linux-arm64`
- `osx-arm64`

### 17.5 Deployment Strategy

MVP deployment options:

- `dotnet run` for development.
- Self-contained release bundle for internal server.
- Docker image for single-host deployment.

Deployment must include:

- SQLite database path.
- Instance root path.
- Release cache path.
- Data Protection key storage path.
- Logging configuration.
- Admin user bootstrap.

## 18. Testing Strategy

### 18.1 Test Pyramid

| Layer | Tooling | Scope |
| --- | --- | --- |
| Unit | xUnit, FluentAssertions | Domain rules, status classification, release asset selection, workflow generation, secret resolution precedence. |
| Persistence | xUnit, EF Core SQLite | Migrations, repositories, query projections, retention cleanup. |
| API | WebApplicationFactory | Minimal API behavior, auth policies, validation errors. |
| Blazor | bUnit | Components, forms, tables, status badges, workflow editor behavior. |
| Integration | xUnit, WireMock/fakes | Symphony API client, GitHub client fakes, runner fakes. |
| Docker optional | Docker available check | Docker runner smoke tests when Docker is installed. |
| E2E later | Playwright | Dashboard and repository command centre flows. |

### 18.2 Required MVP Tests

Core:

- Health status classification.
- Release selector resolution behavior with fake GitHub Releases.
- Asset selection by OS/architecture.
- Secret resolution precedence.
- Workflow generation for Docker paths.
- Workflow generation for local process paths.
- Per-instance credential injection model.
- Alert creation for offline instance.
- Snapshot normalization from Symphony state JSON.

Persistence:

- Create/read/update project.
- Import repository uniqueness.
- Instance status updates.
- Snapshot insertion and latest snapshot query.
- Run issue projection query.
- Audit event insertion.

API:

- Register manual instance.
- Start instance queues background operation.
- Unauthorized lifecycle call returns 403.
- Validation failure returns field errors.

Blazor:

- Dashboard metric tile renders.
- Status badge includes text and color class.
- Secret selector never renders secret value.
- Instance restart confirmation appears.

### 18.3 Test Data

Use deterministic fixture JSON for Symphony responses:

```text
tests/Conductor.Integration.Tests/Fixtures/Symphony/health-ok.json
tests/Conductor.Integration.Tests/Fixtures/Symphony/runtime-basic.json
tests/Conductor.Integration.Tests/Fixtures/Symphony/state-running.json
tests/Conductor.Integration.Tests/Fixtures/Symphony/state-retrying.json
tests/Conductor.Integration.Tests/Fixtures/Symphony/issue-detail.json
```

### 18.4 External Integration Tests

Real GitHub and Docker tests must be opt-in.

Environment flags:

```text
CONDUCTOR_RUN_REAL_GITHUB_TESTS=1
CONDUCTOR_RUN_DOCKER_TESTS=1
GITHUB_TOKEN=...
```

CI must skip these tests unless explicitly enabled.

## 19. Documentation Requirements

### 19.1 Required Documents

The documentation set should include:

- `docs/requirements.md`
- `docs/technical.md`
- `docs/api.md`
- `docs/deployment.md`
- `docs/testing.md`
- `docs/user_guide.md`
- `docs/feature_guides.md`
- `docs/changelog.md`
- `docs/contributing.md`

### 19.2 Documentation Updates by Feature

Every feature PR must update documentation when it changes:

- User workflow.
- Configuration.
- API shape.
- Deployment requirement.
- Security behavior.
- Testing command.
- Operational runbook.

### 19.3 Generated API Documentation

Once APIs stabilize:

- Emit OpenAPI document from Minimal APIs.
- Commit or publish OpenAPI artifact.
- Reference it from `docs/api.md`.

## 20. Observability

### 20.1 Logging

Use structured logging through `ILogger`.

Required scopes:

- `CorrelationId`
- `ProjectId`
- `RepositoryId`
- `SymphonyInstanceId`
- `OperationId`
- `RunId`

### 20.2 Metrics

Track:

- Collector cycle duration.
- Collector failures.
- Symphony API latency.
- Background operation duration.
- Alert counts.
- Report generation duration.
- Database migration status.

For MVP, expose metrics on an internal diagnostics page. OpenTelemetry can be added later.

### 20.3 Health Checks

Host endpoints:

```http
GET /health/live
GET /health/ready
```

Readiness checks:

- SQLite open/read/write check.
- Data Protection key availability.
- Instance root path writable.
- Release cache path writable.

## 21. Configuration

Example `appsettings.json`:

```json
{
  "Conductor": {
    "InstanceRoot": "./data/instances",
    "ReleaseCacheRoot": "./data/cache/symphony-releases",
    "DefaultHealthPollSeconds": 10,
    "DefaultStatePollSeconds": 30,
    "DefaultRuntimePollSeconds": 120,
    "AllowLocalProcessRunner": true,
    "AllowDockerRunner": true
  },
  "ConnectionStrings": {
    "Conductor": "Data Source=./data/conductor.db;Cache=Shared"
  }
}
```

Environment overrides:

```text
ConnectionStrings__Conductor
Conductor__InstanceRoot
Conductor__ReleaseCacheRoot
Conductor__AllowDockerRunner
Conductor__AllowLocalProcessRunner
ASPNETCORE_URLS
```

Do not store GitHub PATs or OpenAI API keys in `appsettings.json`.

## 22. Sprint Plan

Assumption: two-week sprints, one small product team. Sprint scope can be compressed or expanded depending on staffing.

### Sprint 0: Foundation and Tooling

Goal: Establish the solution skeleton and engineering baseline.

Deliverables:

- Create .NET 10 solution and projects.
- Add Blazor Web App host.
- Add EF Core SQLite persistence project.
- Add test projects.
- Add `.editorconfig`, `Directory.Build.props`, package management, and basic appsettings.
- Add CI workflow for restore/build/test/format.
- Add docs workflow with whitespace check.

Acceptance:

- `dotnet build` passes.
- Empty test suite runs.
- CI runs on PR.
- App starts locally and serves a placeholder dashboard.

### Sprint 1: Domain Model and Persistence

Goal: Create durable portfolio state.

Deliverables:

- Implement core entities and typed IDs.
- Implement `ConductorDbContext`.
- Create initial migration.
- Add seed/dev data.
- Add repository query services.
- Add persistence tests.

Acceptance:

- SQLite database is created automatically in development.
- Projects, repositories, and instances can be created through application services.
- Dashboard can load seeded repository/instance data.

### Sprint 2: Blazor Shell and Dashboard

Goal: Build the first real control surface.

Deliverables:

- App shell, side nav, top bar.
- Dashboard route.
- Metric tiles.
- Active repositories table.
- Needs attention panel.
- Live activity placeholder.
- Status badges and shared UI components.

Acceptance:

- Dashboard visually follows `docs/UIOverview.png`.
- Dashboard renders from persisted projection data.
- Components have bUnit coverage for key states.

### Sprint 3: Symphony API Client and Manual Registration

Goal: Monitor existing Symphony instances.

Deliverables:

- Implement `ISymphonyApiClient`.
- Add manual instance registration UI/API.
- Implement health/runtime/state polling worker.
- Store snapshots.
- Display instance health and runtime data.
- Add fixture-based integration tests.

Acceptance:

- User can register a running Symphony URL.
- Dashboard updates from collected Symphony state.
- Offline instance creates event and alert.

### Sprint 4: Repository and Secret Management

Goal: Prepare orchestration inputs safely.

Deliverables:

- Secret descriptor model and encrypted secret store.
- Secret management UI with masked values.
- GitHub PAT and OpenAI API key secret types.
- Per-instance credential resolution.
- Repository import data model and initial UI.
- GitHub client abstraction with fake implementation for tests.

Acceptance:

- User can store multiple GitHub PAT descriptors.
- User can store multiple OpenAI API key descriptors.
- Instance can reference different GitHub and OpenAI credentials.
- Secret values never render after save.

### Sprint 5: GitHub Repository Discovery

Goal: Import repositories from GitHub.

Deliverables:

- GitHub repository discovery client.
- Repository import wizard.
- PAT validation where practical.
- Repository/project association.
- Import audit events.
- Repository list/detail pages.

Acceptance:

- User can search and import accessible GitHub repositories.
- Imported repository metadata is persisted.
- Import works with fake GitHub tests in CI.
- Real GitHub test is opt-in.

### Sprint 6: Symphony Release Resolver and Workflow Generator

Goal: Generate deployable Symphony instance configuration.

Deliverables:

- GitHub Releases resolver.
- Release artifact cache.
- Compatible asset selection.
- `WorkflowProfile` model and editor.
- `WORKFLOW.md` generator.
- Workflow validation service.
- Instance folder layout creation.

Acceptance:

- Creating an instance with `latest` resolves current release metadata.
- Pinned tag resolution is supported.
- Generated Docker and local workflows are valid.
- Release provenance is stored.

### Sprint 7: Local Process Runner

Goal: Start Symphony locally from a release artifact.

Deliverables:

- Local process runner.
- Runtime extraction.
- Port allocation.
- Per-instance environment injection.
- Log capture.
- Start/stop/restart operations.
- Operation status UI.

Acceptance:

- User can start a local Symphony instance from Conductor.
- Instance receives selected GitHub and OpenAI credentials.
- Logs are visible.
- Restart does not change resolved release tag.

### Sprint 8: Docker Runner

Goal: Run one Symphony container per repository.

Deliverables:

- Docker runner.
- Container naming and volume mapping.
- Container environment injection.
- Image strategy implementation.
- Container log retrieval.
- Destroy confirmation.
- Docker integration tests behind opt-in flag.

Acceptance:

- User can start Docker-backed Symphony instance.
- Mounted workflow/data/Codex directories are created.
- Health polling sees the container become healthy.
- Per-instance credentials are isolated.

### Sprint 9: Runs, Issues, Timelines, and Alerts

Goal: Make repository command centres operationally useful.

Deliverables:

- Normalize tracked issues from state snapshots.
- Normalize runs and retry queue.
- Repository command centre.
- Issue drill-down.
- Run timeline.
- Initial alert rules.
- Alert center page.

Acceptance:

- User can inspect running sessions, retrying issues, and recent activity.
- Alerts appear for offline/stalled/failed conditions.
- Issue detail fetch works on demand.

### Sprint 10: Reports

Goal: Produce delivery and reliability reports.

Deliverables:

- Report generation framework.
- Daily Delivery Brief.
- Weekly Software Factory Report.
- Project Report.
- Markdown and HTML renderers.
- Report list/detail pages.

Acceptance:

- User can generate reports from persisted data.
- Reports include blockers, failures, active work, PRs where available, and token usage.
- Report generation is persisted as background operation.

### Sprint 11: Policy and Governance MVP

Goal: Add basic guardrails.

Deliverables:

- Policy model and editor.
- Concurrency policy checks.
- Budget threshold model.
- Human approval category flags.
- Workflow drift detection.
- Governance events.

Acceptance:

- Policy can block starting too many instances.
- Token budget thresholds create alerts.
- Workflow drift is visible.

### Sprint 12: Production Hardening and Release

Goal: Prepare first production-ready release.

Deliverables:

- Authentication and role-based authorization.
- Deployment guide.
- Testing guide.
- API documentation.
- Docker image build.
- Release workflow.
- Backup/restore documentation for SQLite.
- Diagnostics page and health checks.

Acceptance:

- Release workflow publishes artifacts.
- Deployment guide can be followed on a clean machine.
- Required docs are present.
- Security review checklist is complete.

## 23. Definition of Done

For code changes:

- Builds with warnings as errors.
- Relevant tests pass.
- No formatting changes pending.
- No secrets in code, docs, logs, or snapshots.
- Audit events added for privileged actions.
- Documentation updated.
- UI states include loading, empty, error, and success states.

For documentation changes:

- Markdown has no trailing whitespace.
- Links are valid where practical.
- Requirement or design changes are reflected in both requirements and technical docs when necessary.

## 24. Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| SQLite write contention under heavy polling | Keep transactions short, use WAL, tune polling, batch writes, and avoid horizontal writer scaling in MVP. |
| Symphony release assets vary by tag | Implement robust asset selection and clear provisioning errors. |
| Docker availability differs by host | Detect Docker capability at startup and show runner availability in infrastructure page. |
| Secrets leak through logs | Central redaction service and tests with representative token patterns. |
| Blazor Server circuit disconnects | Use reconnect UI and fallback refresh. |
| Polling load grows with instances | Add jitter, backoff, and future partitioned collectors. |
| GitHub rate limits | Cache repository metadata, back off failed calls, and surface rate-limit alerts. |
| Workflow edits break running instances | Validate before save and keep prior workflow revision. |

## 25. Future Technical Extensions

- Azure Container Apps runner.
- GitHub App authentication and short-lived installation tokens.
- External notification channels: Teams, Slack, email.
- OpenTelemetry traces and metrics.
- PostgreSQL or SQL Server provider.
- PDF report rendering.
- Symphony event stream ingestion.
- Instance upgrade workflow.
- Multi-node Conductor with distributed locking.
