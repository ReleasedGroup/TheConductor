# Conductor Requirements Specification

Status: Draft v0.1
Date: 2026-04-29
Source documents: `docs/overview.md`, `docs/UIOverview.png`, and Symphony reference repository `https://github.com/releasedgroup/symphony` at commit `de7e675ed1aa6da8c21f70283261054b970c95e4`.

## 1. Purpose

Conductor is the fleet control layer above Symphony. Symphony executes coding-agent work for one repository. Conductor manages many Symphony instances across projects and repositories, providing portfolio visibility, operational control, governance, reporting, and delivery risk management.

The purpose of this document is to define the functional and non-functional requirements for Conductor so the product can be designed, built, tested, and deployed against a clear baseline.

## 2. Product Vision

Conductor must act as a software delivery control surface for a software company. It must show the health of many projects and repositories, manage Symphony runtimes, reveal active and blocked work, track failed runs and AI usage, and generate business-facing reports.

The product split is intentional:

- Symphony remains the single-repository coding-agent orchestrator.
- Conductor owns multi-repository orchestration, provisioning, monitoring, policy, reporting, and operational governance.

Conductor must not replace Symphony's agent loop. It must supervise Symphony instances and normalize their state into a company-wide operating model.

## 3. Definitions

| Term | Meaning |
| --- | --- |
| Project | A business or delivery grouping that can contain one or more repositories. |
| Repository | A GitHub repository imported into Conductor. |
| Symphony instance | A running Symphony runtime that manages one repository and one workflow profile. |
| Execution mode | The way a Symphony instance is run: local process, local container, or Azure container. |
| Workflow profile | A Conductor-managed template or configuration source used to generate or manage a repo-specific `WORKFLOW.md`. |
| Run | A Symphony execution attempt for a GitHub issue. |
| Run attempt | A single try within a run, including retries and continuations. |
| Active issue | A GitHub issue in a workflow-defined active state and visible to Symphony. |
| Blocked issue | An issue that cannot proceed because of blockers, labels, policy, failures, approval requirements, or missing resources. |
| Portfolio dashboard | The top-level Conductor view across all projects and repositories. |
| Repository command centre | The detailed operational view for one repository and its Symphony instance. |

## 4. Requirement Language

The following terms are used consistently:

- Must: required for the specified release scope.
- Should: expected unless a documented tradeoff is accepted.
- Could: optional enhancement.
- Must not: prohibited behavior.

Priorities:

- P0: required for the first usable release.
- P1: required for the first production-ready release.
- P2: important follow-up capability.

## 5. Scope

### 5.1 In Scope

Conductor must provide:

- Multi-project and multi-repository registry.
- Manual registration of existing Symphony instances.
- Provisioning and lifecycle control for Symphony instances.
- Local process and local Docker execution modes for early releases.
- Azure execution mode design compatibility, with implementation in a later phase.
- GitHub organization and repository discovery.
- Consolidated health, runtime, issue, run, token, and rate-limit telemetry.
- Fleet dashboard and repository drill-down UI.
- Workflow profile creation, comparison, validation, and deployment.
- Run timeline and issue drill-down.
- Alerts and notification center.
- Delivery, reliability, and executive reports.
- Policy and governance controls.
- Secret handling and identity management.
- Auditability for administrative actions.

### 5.2 Out of Scope for the First Usable Release

The first usable release does not need to:

- Replace Symphony's single-repo orchestration loop.
- Run one container per individual agent attempt.
- Implement a full remote-agent execution model inside Symphony.
- Support non-GitHub issue trackers.
- Provide a public multi-tenant SaaS billing system.
- Automatically resolve every failed run.
- Provide Azure execution if local process and Docker are complete and Azure-ready abstractions exist.

## 6. Users and Stakeholders

### 6.1 Primary Users

- Software delivery operator: monitors repositories, restarts instances, investigates failures, and handles alerts.
- Engineering lead: reviews delivery health, blocked work, failed runs, PR backlog, and workflow compliance.
- Developer: inspects repository-level run details, issue timelines, logs, and PR links.
- Executive or client-facing manager: consumes delivery reports and risk summaries.
- System administrator: configures execution infrastructure, secrets, identity, and policies.

### 6.2 User Outcomes

Conductor must enable users to:

- See all automated software work in one place.
- Identify which repositories need attention.
- Understand what Symphony agents are doing now.
- Start, stop, restart, and inspect Symphony instances.
- Import a GitHub repository and place it under orchestration.
- Generate and validate a repository workflow.
- Track token usage and estimated AI cost.
- Detect stalled, failing, blocked, or offline automation.
- Produce readable delivery and reliability reports.

## 7. Assumptions and Dependencies

### 7.1 Symphony Dependency Baseline

Conductor depends on the current Symphony model:

- Symphony is a `.NET 10` service.
- Symphony is designed around one long-running orchestrator for one repository.
- Symphony stores its own instance state in SQLite.
- Symphony expects a repository-owned or externally mounted `WORKFLOW.md`.
- Symphony uses a mounted workspace root with shared clone and per-issue worktrees.
- Symphony runs Codex app-server inside the same host/container as the orchestrator.
- Symphony supports GitHub PAT authentication through `GITHUB_TOKEN`.
- Each Symphony instance may require its own GitHub PAT and its own OpenAI API key or Codex credential source.

### 7.2 Current Symphony Runtime Endpoints

Conductor must integrate with the current Symphony runtime API:

| Method | Endpoint | Conductor use |
| --- | --- | --- |
| GET | `/` | Emergency dashboard link and operator handoff. |
| GET | `/api/v1/health` | Health and liveness checks. |
| GET | `/api/v1/runtime` | Runtime, workflow, orchestration, persistence, and version snapshot. |
| GET | `/api/v1/workflow` | Read editable workflow source where supported. |
| PUT | `/api/v1/workflow` | Save validated workflow source where supported and policy allows. |
| GET | `/api/v1/state` | Running sessions, retry queue, tracked issues, activity, coordination, tokens, and rate limits. |
| GET | `/api/v1/{issue_identifier}` | Issue-specific runtime/debug details. |
| POST | `/api/v1/refresh` | Request immediate best-effort poll/reconcile. |

### 7.3 Symphony Container Dependency Baseline

For Docker execution, Conductor must align with Symphony's current container guide:

- One container per Symphony instance.
- `WORKFLOW.md` mounted externally, normally at `/config/WORKFLOW.md`.
- SQLite and workspaces mounted under `/var/lib/symphony`.
- Codex login/config state mounted under `/home/symphony/.codex`.
- `GITHUB_TOKEN` injected externally per instance.
- `OPENAI_API_KEY` or the configured Codex/OpenAI credential injected externally per instance when required.
- `Persistence__ConnectionString` configured for the mounted SQLite database.
- `Orchestration__InstanceId` assigned per instance.
- `ASPNETCORE_URLS` bound to `http://0.0.0.0:8080` inside the container.

### 7.4 External Dependencies

Conductor depends on:

- GitHub API access for repository discovery, issue/PR metadata, labels, milestones, branch protection, and Actions status.
- GitHub Releases access for resolving and downloading Symphony release metadata and artifacts from `https://github.com/releasedgroup/symphony/releases`.
- Docker or Podman for local container execution mode.
- Local operating system process APIs for local process mode.
- A central relational database for Conductor state.
- Azure services for cloud execution mode in later phases.
- Email, Teams, Slack, or GitHub comments for alert delivery.

### 7.5 Symphony Release Acquisition Baseline

Conductor must acquire Symphony from GitHub Releases when creating Symphony instances. It must not require a local Symphony source checkout or manually installed Symphony binary as the default provisioning path.

Default behavior:

- Resolve the latest Symphony release at instance-creation time from `https://github.com/releasedgroup/symphony/releases/latest` or the equivalent GitHub Releases API.
- Do not hard-code the current latest tag in product logic.
- Allow an operator or policy to pin a specific Symphony release tag for reproducibility.
- Select the correct release asset for the target execution mode, operating system, and architecture.
- Cache downloaded release assets locally with release tag, asset name, source URL, download timestamp, and checksum/digest where available.
- Record the resolved release tag and artifact provenance on the Symphony instance record.

As of 2026-04-29, GitHub resolves the latest release page to `v0.0.7-alpha`, but Conductor must always resolve latest dynamically unless a version is pinned.

For local process mode, Conductor should download and extract the appropriate self-contained release bundle. For Docker mode, Conductor should prefer a release-specific container image or release artifact when one is available; if only source/package assets are published, Conductor may build or prepare a local image/runtime from the resolved release and must record that provenance.

## 8. Product-Level Requirements

### PRD-001: Fleet Control Surface

Priority: P0

Conductor must provide a unified control surface for many repositories and many Symphony instances. It must consolidate health, workload, run, issue, PR, token, and risk signals into portfolio and repository-level views.

Acceptance criteria:

- A user can see the number of managed repositories.
- A user can see which Symphony instances are healthy, warning, critical, or offline.
- A user can open a repository command centre from the dashboard.
- A user can identify failed runs, blocked issues, open PRs, and estimated token spend.

### PRD-002: Symphony Supervision, Not Replacement

Priority: P0

Conductor must supervise Symphony instances without duplicating Symphony's internal scheduling and agent execution responsibilities.

Acceptance criteria:

- Conductor treats Symphony as the source of truth for per-repository run execution state.
- Conductor does not directly run Codex sessions for repository issues in the MVP.
- Conductor can request refresh and collect state but does not bypass Symphony's orchestration loop.

### PRD-003: One Symphony Instance Per Repository

Priority: P0

Conductor must model one primary Symphony instance per repository/workflow profile for the initial product.

Acceptance criteria:

- A repository can have one default Symphony instance.
- The data model allows future additional instances per repository for separate workflow profiles.
- UI labels and provisioning flows do not imply that one Symphony instance manages all repositories.

### PRD-004: Release Phasing

Priority: P0

Conductor must be deliverable in phases:

1. Fleet visibility.
2. Local process and Docker orchestration.
3. Reporting.
4. Azure runner.
5. Policy and governance.

Acceptance criteria:

- Each phase has testable completion criteria.
- The architecture does not block Azure support even if Azure implementation follows local execution.
- Reporting and policy features can be introduced without rewriting the core registry, collector, or runner model.

## 9. Functional Requirements

### 9.1 Projects and Repositories

#### FR-001: Project Registry

Priority: P0

Conductor must allow users to create, view, update, archive, and search projects.

Required project fields:

- Name.
- Client name or internal owner.
- Status.
- Description.
- Default branch policy.
- Created and updated timestamps.

Acceptance criteria:

- A repository can be assigned to a project.
- The dashboard can filter by project.
- Reports can be scoped to one project.

#### FR-002: Repository Registry

Priority: P0

Conductor must maintain a registry of managed repositories.

Required repository fields:

- Provider.
- Owner.
- Name.
- Full name.
- Clone URL.
- Web URL.
- Default branch.
- Visibility.
- Archived status.
- Project association.
- Last synced timestamp.
- Orchestration eligibility.

Acceptance criteria:

- A user can view all managed repositories.
- A user can view repository health and orchestration status.
- A repository can exist before a Symphony instance is provisioned.

#### FR-003: GitHub Repository Discovery

Priority: P1

Conductor should discover accessible GitHub organizations and repositories.

Discovery must include:

- Organizations.
- Repositories.
- Default branch.
- Visibility.
- Archived status.
- Open issues.
- Labels.
- Milestones.
- Pull requests.
- Branch protection summary.
- GitHub Actions status summary.

Acceptance criteria:

- A user can search GitHub repositories available to the configured identity.
- A user can import selected repositories.
- Imported repository metadata is stored and refreshable.

#### FR-004: Repository Import Wizard

Priority: P1

Conductor must provide a guided flow to import a GitHub repository and prepare it for orchestration.

The wizard must collect or infer:

- Project.
- Repository owner/name.
- Default branch.
- Execution mode.
- Symphony release policy: latest release or pinned release tag.
- Workflow profile.
- Token or GitHub App installation scope.
- GitHub credential selection: inherit default, select existing secret, or provide instance-specific PAT.
- OpenAI/Codex credential selection: inherit default, select existing secret, or provide instance-specific OpenAI API key.
- Codex home/source for credentials.
- Instance name.
- Port or endpoint.

Acceptance criteria:

- A repository can be imported without manually editing database records.
- Required fields are validated before import completes.
- The wizard explains missing prerequisites through actionable validation messages.

### 9.2 Symphony Instance Registry and Lifecycle

#### FR-005: Instance Registry

Priority: P0

Conductor must track each Symphony instance as a first-class resource.

Required fields:

- Instance ID.
- Repository ID.
- Display name.
- Execution mode.
- Base URL.
- Port.
- Container name where applicable.
- Azure resource ID where applicable.
- Status.
- Health status.
- Symphony version.
- Symphony release tag.
- Symphony artifact source URL.
- Symphony artifact checksum or digest where available.
- GitHub credential secret reference.
- OpenAI/Codex credential secret reference.
- Credential inheritance mode.
- Workflow path.
- Data path.
- Created timestamp.
- Last started timestamp.
- Last seen timestamp.

Acceptance criteria:

- A user can list all instances.
- A user can filter instances by status, execution mode, and project.
- Each instance can be linked back to its repository and project.

#### FR-006: Manual Instance Registration

Priority: P0

Conductor must allow a user to register an existing Symphony instance by URL.

Acceptance criteria:

- Conductor validates `/api/v1/health` before accepting the instance.
- Conductor collects `/api/v1/runtime` to identify version, workflow, and repository metadata where available.
- Conductor starts polling state after registration.
- Validation failures are visible and do not create partially active records.

#### FR-007: Local Process Runner

Priority: P1

Conductor must support starting Symphony as a local process for development and small internal deployments.

Required behavior:

- Resolve the configured Symphony release, defaulting to latest.
- Download and cache the appropriate release bundle from GitHub Releases when not already available.
- Extract the release bundle into an instance or runtime cache folder.
- Generate or select an instance folder.
- Generate or select `WORKFLOW.md`.
- Allocate a loopback port.
- Set required environment variables, including the instance-specific `GITHUB_TOKEN` and OpenAI/Codex credential when configured.
- Start Symphony with a workflow path and port.
- Track process ID.
- Stop and restart the process.
- Capture stdout/stderr logs.
- Detect process exit.

Acceptance criteria:

- A user can start a local-process Symphony instance from Conductor.
- The instance uses the resolved GitHub release artifact rather than a developer source checkout by default.
- The instance appears healthy after startup.
- A user can stop and restart the instance.
- Process failures change instance health and create events.

#### FR-008: Docker Runner

Priority: P1

Conductor must support running one Symphony container per managed repository.

Required behavior:

- Resolve the configured Symphony release, defaulting to latest.
- Use a release-specific container image when available, or prepare/build a local runtime image from the resolved release artifact/source.
- Record the release tag, image tag/digest, and artifact provenance.
- Create a stable instance directory.
- Write or mount `WORKFLOW.md`.
- Create or bind data/workspace directories.
- Mount a Codex home directory.
- Inject the instance-specific `GITHUB_TOKEN` or short-lived token.
- Inject the instance-specific `OPENAI_API_KEY` or configured OpenAI/Codex credential when required.
- Set `Persistence__ConnectionString`.
- Set `Orchestration__InstanceId`.
- Set `ASPNETCORE_URLS`.
- Allocate host port.
- Start, stop, restart, and destroy containers.
- Read container logs.
- Detect unhealthy or repeatedly restarting containers.

Acceptance criteria:

- A user can start a Docker-backed Symphony instance.
- Docker provisioning uses the resolved GitHub release or a release-specific image rather than an unversioned local build by default.
- Docker provisioning can assign a different GitHub PAT and OpenAI API key to each Symphony container.
- The container uses mounted state rather than ephemeral-only state.
- Deleting an instance requires explicit confirmation before data destruction.
- Container names are deterministic and collision-safe.

#### FR-009: Azure Runner Compatibility

Priority: P1 for design, P2 for implementation

Conductor must be designed to support Azure-hosted Symphony instances.

Azure execution should support:

- Azure Container Apps as the preferred initial target.
- Azure Container Instances or AKS as possible alternatives.
- Azure SQL or PostgreSQL for Conductor state.
- Azure Files or equivalent persistent storage for Symphony data and workspaces.
- Azure Key Vault for secrets.
- Managed Identity for Conductor.
- Application Insights and Log Analytics integration.

Acceptance criteria:

- Runner abstractions do not expose Azure-specific types in the core domain model.
- Instance records can store Azure resource identifiers.
- The provisioning model can express persistent storage, identity, network, and logging requirements.

#### FR-010: Runner Abstraction

Priority: P0

Conductor must define a runner abstraction for Symphony lifecycle operations.

The abstraction must support:

- Provision.
- Start.
- Stop.
- Restart.
- Health check.
- Logs query.
- Destroy.

Acceptance criteria:

- Local process, Docker, and Azure runners can implement the same core lifecycle contract.
- The UI and API can invoke lifecycle operations without knowing implementation-specific details.
- Runner operations produce structured events.

#### FR-052: Symphony Release Resolver

Priority: P0

Conductor must resolve Symphony releases from GitHub Releases when creating a Symphony instance.

Required behavior:

- Support `latest` as the default release selector.
- Resolve `latest` dynamically at provisioning time through the GitHub Releases API or `releases/latest` redirect.
- Support pinned release tags for reproducible environments.
- Retrieve release metadata including tag name, release URL, asset names, asset URLs, publication timestamp, prerelease flag, and checksum/digest where available.
- Select an asset compatible with the target execution mode, operating system, and architecture.
- Fail instance creation with a clear validation error when no compatible asset or image can be found.

Acceptance criteria:

- Creating an instance with the default selector uses the latest GitHub release at that moment.
- Creating an instance with a pinned tag uses that tag even if a newer release exists.
- The resolved release tag is visible in instance details and retained in audit/provisioning events.
- Release resolution failures do not create a partially provisioned active instance.

#### FR-053: Symphony Release Artifact Cache

Priority: P1

Conductor must cache downloaded Symphony release artifacts to avoid repeated downloads and to support predictable restarts.

Required behavior:

- Store cached artifacts by release tag, asset name, operating system, architecture, and execution mode.
- Store a release manifest with source URL, resolved tag, download timestamp, file size, checksum/digest where available, and extraction path.
- Reuse cached artifacts for new instances when the release selector resolves to the same tag and compatible asset.
- Provide a way to refresh the cache when the `latest` selector resolves to a newer release.
- Provide a way to clear unused cached artifacts safely.

Acceptance criteria:

- Starting or restarting an existing instance does not unexpectedly upgrade Symphony.
- Creating a new instance with `latest` may use a newer resolved release than older instances.
- Operators can see which instances are behind the current latest release.
- Cache cleanup does not remove artifacts still referenced by active instances.

### 9.3 Instance Provisioning and Files

#### FR-011: Instance Folder Layout

Priority: P1

Conductor must create a stable per-instance folder layout for local and Docker execution.

Required logical layout:

```text
instances/{instanceId}/
  config/
    WORKFLOW.md
  state/
    conductor-instance.json
  logs/
  runtime/
    symphony-release.json
  symphony-data/
    data/
    workspaces/
    codex-home/
```

Acceptance criteria:

- Instance files are grouped by instance ID.
- Paths are stored in the instance record.
- The selected Symphony release tag and artifact source are recorded in an instance runtime manifest.
- Instance secret references are stored as references only; plaintext credential values are not written to the instance folder.
- Secrets are not written to unencrypted configuration files unless explicitly documented for local development.

#### FR-012: Port Allocation

Priority: P1

Conductor must allocate and track local host ports for Symphony instances.

Acceptance criteria:

- Port collisions are detected before startup.
- A user can override the port where policy allows.
- Port assignments survive restart.
- Released ports can be reused only after the instance is stopped or deleted.

#### FR-013: Instance Naming

Priority: P1

Conductor must generate deterministic, safe instance names.

Acceptance criteria:

- Names include provider/owner/repository where possible.
- Names are safe for process labels, Docker container names, logs, and URLs.
- Name collisions are resolved deterministically.

### 9.4 Workflow Profiles

#### FR-014: Workflow Profile Registry

Priority: P1

Conductor must store reusable workflow profiles.

Required fields:

- Name.
- Description.
- Repository scope or global scope.
- Active states.
- Terminal states.
- Labels.
- Milestones.
- Max concurrent agents.
- Max turns.
- Retry backoff.
- Codex timeouts and sandbox settings.
- Prompt template.
- Hook templates.
- Default flag.
- Version/revision metadata.

Acceptance criteria:

- A user can create and edit a workflow profile.
- A repository can be assigned a default workflow profile.
- Profiles can be compared to repository-specific workflows.

#### FR-015: WORKFLOW.md Generation

Priority: P1

Conductor must generate a Symphony-compatible `WORKFLOW.md` for each provisioned instance.

Generated workflow must include:

- GitHub tracker settings.
- `api_key: $GITHUB_TOKEN` or equivalent token indirection.
- Owner and repository.
- Active states and terminal states.
- Polling interval.
- Agent concurrency and turn limits.
- Codex command and timeouts.
- Server port.
- Workspace paths appropriate to execution mode.
- Hook settings.
- Prompt body.

Acceptance criteria:

- Generated workflows are accepted by Symphony.
- Docker workflows use Linux container paths.
- Local process workflows use valid host paths.
- Generated workflows never inline secrets by default.
- Generated workflows are credential-agnostic and rely on per-instance environment injection for GitHub and OpenAI/Codex secrets.

#### FR-016: Workflow Editing

Priority: P1

Conductor should provide a workflow editor that supports structured profile editing and raw Markdown editing.

Acceptance criteria:

- A user can edit workflow front matter.
- A user can edit prompt body.
- A user can preview the rendered `WORKFLOW.md`.
- A user can compare a repository workflow to the company standard profile.
- Saving an invalid workflow is blocked.

#### FR-017: Workflow Validation

Priority: P1

Conductor must validate workflows before deployment.

Validation should include:

- Required GitHub owner/repo fields.
- Required active and terminal states.
- Valid agent limits.
- Valid timeout values.
- Valid workspace paths for the selected execution mode.
- Missing GitHub and OpenAI/Codex secret references required by the selected credential mode.
- Symphony validation result where `/api/v1/workflow` validation is available.

Acceptance criteria:

- Invalid workflows produce field-level errors.
- Workflow validation runs before starting a new instance.
- Workflow validation runs before saving changes to a live instance.

#### FR-018: Workflow Drift Detection

Priority: P2

Conductor should detect when a live repository workflow has drifted from its assigned workflow profile.

Acceptance criteria:

- Drift status is visible in the repository command centre.
- A user can see a diff between profile and live workflow.
- A user can reapply the profile after confirmation.

### 9.5 Telemetry Collection

#### FR-019: Health Polling

Priority: P0

Conductor must poll each Symphony instance health endpoint.

Required behavior:

- Poll `/api/v1/health`.
- Default interval: 10 seconds.
- Track latency, success/failure, and last seen time.
- Classify health as healthy, warning, critical, or offline.

Acceptance criteria:

- Offline instances are detected within a configurable threshold.
- Health status changes create events.
- Health polling failures do not stop polling other instances.

#### FR-020: Runtime Polling

Priority: P0

Conductor must poll Symphony runtime metadata.

Required behavior:

- Poll `/api/v1/runtime`.
- Default interval: 2 minutes.
- Store application name/version.
- Store orchestration instance ID and lease settings.
- Store workflow source path and selected workflow settings.
- Store persistence configuration summary without secrets.

Acceptance criteria:

- Symphony version appears in instance details.
- Runtime polling captures workflow owner/repo when available.
- Runtime errors are visible and retained.

#### FR-021: State Polling

Priority: P0

Conductor must poll Symphony state.

Required behavior:

- Poll `/api/v1/state`.
- Default interval: 30 seconds.
- Normalize running sessions.
- Normalize retry queue.
- Normalize tracked issue distribution.
- Normalize recent activity.
- Normalize lease state.
- Normalize token totals.
- Normalize runtime seconds.
- Normalize latest rate-limit payload.

Acceptance criteria:

- Dashboard metrics are derived from stored snapshots.
- Polling one failing instance does not affect other instances.
- Raw response JSON is retained for traceability.

#### FR-022: Issue Detail Collection

Priority: P1

Conductor must fetch issue-specific details on demand.

Required behavior:

- Call `/api/v1/{issue_identifier}`.
- Show run status, workspace path, attempts, retry information, recent events, last error, labels, blockers, and pull requests where available.

Acceptance criteria:

- Issue detail opens from repository views and run timelines.
- Not found responses are handled gracefully.

#### FR-023: Refresh Request

Priority: P1

Conductor must allow an operator to request an immediate Symphony refresh.

Required behavior:

- Call `POST /api/v1/refresh`.
- Display accepted, queued, and coalesced results.
- Create an audit event.

Acceptance criteria:

- The refresh action is available from the repository command centre.
- The UI does not imply refresh guarantees immediate completion.

#### FR-024: Event Normalization

Priority: P1

Conductor must normalize activity and lifecycle signals into a common event stream.

Events must include:

- Instance lifecycle events.
- Polling failures.
- Health status changes.
- Run started.
- Run failed.
- Retry scheduled.
- PR opened or detected.
- Workflow changed.
- Policy applied.
- Alert raised or resolved.

Acceptance criteria:

- Events can be filtered by project, repository, instance, issue, severity, and type.
- Events are retained for reporting.
- User actions include actor identity.

### 9.6 Dashboard and User Interface

#### FR-025: Portfolio Dashboard

Priority: P0

Conductor must provide a portfolio dashboard as the default screen.

The dashboard must show:

- Managed repository count.
- Healthy repository count.
- Active agent count.
- Blocked issue count.
- Open PR count.
- Estimated AI spend today.
- Orchestration health heatmap.
- Workload overview.
- Needs attention list.
- Active repositories table.
- Live activity stream.
- Quick actions.

Acceptance criteria:

- The dashboard can be filtered by project and date scope.
- Critical and warning states are visually distinct.
- A user can navigate from dashboard items to details.

#### FR-026: Main Navigation

Priority: P0

Conductor must provide primary navigation for:

- Dashboard.
- Projects.
- Repositories.
- Runs.
- Pull Requests.
- Issues.
- Reports.
- Policies.
- Infrastructure.
- Alerts.
- Settings.

Acceptance criteria:

- Navigation is visible on desktop.
- Navigation supports responsive behavior on smaller screens.
- Favorites can surface commonly inspected repositories.

#### FR-027: Search

Priority: P1

Conductor must provide global search across repositories, projects, issues, runs, and PRs.

Acceptance criteria:

- Search accepts repository names, project names, issue identifiers, PR numbers, and run IDs.
- Results link to the appropriate detail page.
- Search works with keyboard focus from the dashboard.

#### FR-028: Repository Command Centre

Priority: P0

Conductor must provide a detailed operational view for one repository.

Required sections:

- Repository identity.
- Instance health.
- Runtime metadata.
- Workload counts.
- Active issues.
- Running sessions.
- Retry queue.
- Recent events.
- Run timeline.
- Issue drill-down.
- Workflow profile/status.
- Logs access.
- Lifecycle actions.

Acceptance criteria:

- A user can restart the Symphony instance from this view if authorized.
- A user can inspect current running sessions and token usage.
- A user can open linked GitHub issues and PRs.

#### FR-029: Run Timeline

Priority: P1

Conductor must show a timeline for each issue/run.

Timeline entries should include:

- Issue discovered.
- Workspace prepared.
- Branch created or detected.
- Codex session started.
- Files changed where available.
- Tests started/failed/passed where available.
- Continuation started.
- Retry scheduled.
- PR opened.
- Waiting for human review.
- Run completed.

Acceptance criteria:

- Timeline entries are chronological.
- Failed events include error summary.
- Links to issue, PR, logs, and attempts are available where data exists.

#### FR-030: Needs Attention Rail

Priority: P0

Conductor must surface high-priority operational issues in a needs attention list.

Attention items must include:

- Offline Symphony instance.
- Repeated failed runs.
- Stalled run.
- Blocked issue.
- High GitHub rate-limit pressure.
- High token spend.
- PR waiting too long for review.
- Workflow drift.
- Container restart loop.

Acceptance criteria:

- Each item has severity, timestamp, source repository, summary, and action link.
- Items can be acknowledged or resolved where appropriate.

#### FR-031: UI Design Quality

Priority: P0

Conductor must feel like a polished software-company control surface rather than a developer afterthought.

Required visual direction:

- Dark mode first.
- Strong contrast.
- Clear green/amber/red status language.
- Dense but readable operational layout.
- GitHub-style links for external resources.
- Separate orchestration health from project delivery health.
- Use the orchestra metaphor sparingly and keep engineering labels clear.

Acceptance criteria:

- Dashboard matches the intent of `docs/UIOverview.png`.
- Text remains readable and does not overlap at supported viewport widths.
- Operational data density is high without hiding critical actions.

### 9.7 Issues, Pull Requests, and Runs

#### FR-032: Tracked Issue Summary

Priority: P0

Conductor must summarize tracked issues per repository.

Required fields:

- GitHub issue number or identifier.
- Title.
- State.
- Labels.
- Milestone.
- Assignees.
- URL.
- Symphony status.
- Last run status.
- Last activity timestamp.
- Blocked flag.
- Blocker reason.

Acceptance criteria:

- Issues can be filtered by state, label, status, and blocked flag.
- Issue rows link to GitHub and Conductor detail.

#### FR-033: Run Summary

Priority: P0

Conductor must store and display normalized run records.

Required fields:

- Symphony instance ID.
- Repository ID.
- GitHub issue number or identifier.
- Symphony run ID where available.
- Status.
- Started timestamp.
- Finished timestamp.
- Attempt count.
- Token input/output/total.
- Error summary.
- Branch name.
- Pull request URL.

Acceptance criteria:

- Runs can be filtered by repository, status, time, and issue.
- Failed runs show error summary.
- Running runs show age and last activity.

#### FR-034: Pull Request Summary

Priority: P1

Conductor should track PRs related to Symphony-managed work.

Required fields:

- Repository.
- PR number.
- Title.
- State.
- Author.
- URL.
- Linked issue.
- Review status.
- Checks status.
- Age.
- Last update.

Acceptance criteria:

- Open PR count appears on dashboard.
- PRs waiting too long for review are surfaced as attention items.

### 9.8 Reports

#### FR-035: Report Generation

Priority: P1

Conductor must generate reports in Markdown and HTML, and should generate PDF.

Report records must include:

- Report type.
- Scope.
- Period start/end.
- Generated timestamp.
- Markdown.
- HTML.
- PDF path or artifact reference where available.

Acceptance criteria:

- A user can generate a report from the UI.
- A user can view prior reports.
- Report data is based on persisted Conductor data, not only live polling.

#### FR-036: Daily Delivery Brief

Priority: P1

Conductor must generate a daily delivery brief.

The brief must include:

- What moved yesterday or during the selected period.
- What is being worked on now.
- What is blocked.
- What failed.
- What needs human review.
- Notable PRs.
- Token/cost summary.

Acceptance criteria:

- Report can be scoped to all projects or one project.
- Report uses plain-English summaries suitable for delivery leads.

#### FR-037: Weekly Software Factory Report

Priority: P1

Conductor must generate a weekly software factory report.

The report must include:

- Active repositories.
- Issues completed.
- PRs opened.
- PRs merged where available.
- Agent hours.
- Human review backlog.
- AI token usage and estimated cost.
- Failure categories.
- Delivery risks.

Acceptance criteria:

- Report can compare current period to previous period where data exists.
- Failure categories are grouped consistently.

#### FR-038: Client Project Report

Priority: P2

Conductor should generate client-facing project reports.

The report should include:

- Plain-English status.
- Features completed.
- Bugs fixed.
- Items awaiting approval.
- Risks.
- Next week's plan.

Acceptance criteria:

- Sensitive internal operational details can be excluded.
- Report is scoped to one project/client.

#### FR-039: Engineering Reliability Report

Priority: P2

Conductor should generate engineering reliability reports.

The report should include:

- Failing repositories.
- Repeat failure patterns.
- Long-running issues.
- Stalled sessions.
- Flaky test indicators where available.
- Rate-limit pressure.
- Repositories missing workflow standards.

Acceptance criteria:

- Reliability reports link back to source runs and repositories.
- Repeat failures are grouped by repository and category.

### 9.9 Policy and Governance

#### FR-040: Policy Registry

Priority: P2

Conductor must support company-wide and project-specific orchestration policies.

Policy settings should include:

- Maximum running repositories.
- Maximum global agents.
- Maximum agents per repository.
- Daily token/cost budget.
- Business-hours-only execution.
- Required human approval categories.
- Auto-pause triggers.
- Alert triggers.

Acceptance criteria:

- Policies can be created, updated, enabled, disabled, and applied.
- Policy scope can be global, project, or repository.
- Policy changes are audited.

#### FR-041: Concurrency Governance

Priority: P2

Conductor must enforce configured concurrency limits where it controls provisioning or lifecycle.

Acceptance criteria:

- Conductor can prevent starting too many instances.
- Conductor can pause or stop lower-priority instances when policy requires.
- Conductor clearly distinguishes limits it enforces from limits enforced inside Symphony.

#### FR-042: Budget Governance

Priority: P2

Conductor must track token usage and estimated AI cost against configured budgets.

Acceptance criteria:

- Token usage is aggregated by repository, project, and portfolio.
- Budget thresholds create warnings or critical alerts.
- Policies can pause or block new starts when budgets are exceeded.

#### FR-043: Human Approval Requirements

Priority: P2

Conductor should identify work categories requiring human approval.

Examples:

- Database migrations.
- Public API changes.
- Security-sensitive files.
- Production infrastructure changes.

Acceptance criteria:

- Approval requirements are visible in issue/run detail.
- Approval state can block or flag automation according to policy.

### 9.10 Alerts and Notifications

#### FR-044: Alert Rules

Priority: P1

Conductor must create alerts for operationally significant events.

Required alert types:

- Symphony instance offline.
- Container repeatedly restarts.
- Run stalled.
- Issue failed more than configured threshold.
- GitHub rate limit low.
- Token spend high.
- Worktree cleanup failed.
- Active issues but no running agents.
- PR waiting too long for review.
- Workflow config drift.

Acceptance criteria:

- Alert rules are configurable.
- Alerts include severity, source, timestamp, summary, and recommended action.
- Alerts appear in dashboard needs attention and alert center.

#### FR-045: Notification Channels

Priority: P2

Conductor should send alerts to external channels.

Supported channels should include:

- Email.
- Microsoft Teams.
- Slack.
- GitHub issue comment.
- Conductor notification center.

Acceptance criteria:

- Channels can be enabled/disabled per policy.
- Notifications are rate-limited to avoid noisy bot chatter.
- Exception and human-needed events are prioritized over routine state transitions.

### 9.11 Secrets, Identity, and Access Control

#### FR-046: Secret Storage

Priority: P0

Conductor must store secrets securely and must not leak tokens in logs, UI, events, reports, or generated workflows.

MVP options:

- Windows DPAPI.
- ASP.NET Core Data Protection.
- Azure Key Vault when deployed in Azure.

Acceptance criteria:

- Secrets are encrypted at rest.
- Secret values are masked in logs and UI.
- Generated `WORKFLOW.md` uses environment variable indirection by default.
- Secrets can be scoped globally, by project, by repository, or by Symphony instance.
- Instance-scoped secrets override inherited defaults without exposing values to other instances.
- GitHub PATs and OpenAI API keys are stored as separate secret types and referenced independently.

#### FR-047: GitHub Authentication

Priority: P0 for PAT, P2 for GitHub App

Conductor must support GitHub PAT authentication initially and should evolve to GitHub App authentication.

MVP behavior:

- Store one or more encrypted GitHub PATs.
- Allow each Symphony instance to use a different GitHub PAT when required.
- Support inherited default PATs for simple deployments.
- Inject the selected PAT as `GITHUB_TOKEN` into the target Symphony instance only.
- Validate token scope before repository import where possible.

Future behavior:

- Use GitHub App installation tokens.
- Generate short-lived repository-scoped tokens.
- Act as token broker for Symphony.

Acceptance criteria:

- PAT values are never displayed after entry.
- A repository or instance can be switched to a different stored PAT without changing other instances.
- Token validation failures produce actionable errors.
- The system design allows GitHub App migration without changing the Conductor domain model.

#### FR-054: OpenAI and Codex Credentials

Priority: P0

Conductor must support OpenAI/Codex credentials per Symphony instance.

Required behavior:

- Store one or more encrypted OpenAI API keys or Codex credential references.
- Allow each Symphony instance to use a different OpenAI API key when required.
- Support inherited default OpenAI/Codex credentials for simple deployments.
- Inject the selected OpenAI API key as `OPENAI_API_KEY` or the configured runtime environment variable into the target Symphony instance only.
- Support a per-instance Codex home directory when credentials are file-based rather than API-key-based.
- Validate that the selected credential is present before starting an instance.
- Keep OpenAI/Codex credentials separate from GitHub credentials in storage, UI, audit, and runner configuration.

Acceptance criteria:

- A user can assign different OpenAI API keys to two Symphony instances.
- Starting an instance fails safely when its selected OpenAI/Codex credential is missing.
- Rotating one instance's OpenAI API key does not affect other instances.
- OpenAI API key values are never displayed after entry.

#### FR-048: User Authentication and Authorization

Priority: P1

Conductor must require authenticated users for administrative and operational actions.

Roles should include:

- Viewer.
- Developer.
- Operator.
- Administrator.

Acceptance criteria:

- Viewers cannot start/stop instances or edit secrets.
- Operators can perform lifecycle actions.
- Administrators can manage secrets, policies, and infrastructure settings.
- All privileged actions are audited.

#### FR-049: Audit Log

Priority: P1

Conductor must audit important user and system actions.

Audit events must include:

- Actor.
- Action.
- Target resource.
- Timestamp.
- Outcome.
- Correlation ID where available.

Acceptance criteria:

- Audit events can be searched by actor, target, and time.
- Secret values are not captured.

### 9.12 Logs and Diagnostics

#### FR-050: Log Access

Priority: P1

Conductor must provide access to Symphony instance logs.

Required behavior:

- Local process logs are captured from stdout/stderr.
- Docker logs are available through the Docker runtime.
- Azure logs link to or query Log Analytics where implemented.
- Logs can be filtered by time and severity where supported.

Acceptance criteria:

- Logs are accessible from instance detail.
- Secret redaction is applied before display or persistence where Conductor processes logs.

#### FR-051: Diagnostics Bundle

Priority: P2

Conductor should generate a diagnostics bundle for an instance.

Bundle contents should include:

- Instance metadata.
- Recent health checks.
- Runtime snapshot.
- State snapshots.
- Recent events.
- Recent logs.
- Workflow summary.

Acceptance criteria:

- Bundle excludes secrets.
- Bundle can be attached to support tickets or reports.

## 10. Data Requirements

### DR-001: Central Database

Priority: P0

Conductor must store portfolio-level state in a central relational database. SQLite is acceptable only for local development; production should use SQL Server or PostgreSQL.

Acceptance criteria:

- State survives Conductor process restart.
- Database migrations are version-controlled.
- Data access patterns support dashboard and report queries.

### DR-002: Core Entities

Priority: P0

Conductor must model these entities:

- Projects.
- Repositories.
- SymphonyInstances.
- WorkflowProfiles.
- InstanceSnapshots.
- TrackedIssues.
- Runs.
- RunAttempts.
- Events.
- Alerts.
- Policies.
- Reports.
- SymphonyReleaseArtifacts.
- Secrets metadata, including scope, type, inheritance, and instance references.
- AuditEvents.

Acceptance criteria:

- Entity IDs are stable.
- Foreign-key relationships preserve project/repository/instance/run traceability.
- Raw Symphony payloads are retained where useful for troubleshooting.

### DR-003: Snapshot Storage

Priority: P0

Conductor must store periodic snapshots from Symphony health, runtime, and state polling.

Acceptance criteria:

- Snapshots include capture timestamp and source instance.
- Snapshot retention is configurable.
- Aggregates can be rebuilt from stored snapshots and events where practical.

### DR-004: Token and Cost Data

Priority: P1

Conductor must store token usage and cost estimates.

Acceptance criteria:

- Token usage can be grouped by instance, repository, project, day, and report period.
- Cost estimates record the pricing assumptions used at calculation time.
- Missing pricing data does not block token reporting.

### DR-005: Data Retention

Priority: P1

Conductor must support retention policies for high-volume data.

Retention categories:

- Health checks.
- State snapshots.
- Events.
- Logs.
- Reports.
- Audit events.

Acceptance criteria:

- Retention can be configured globally.
- Audit retention defaults to longer than operational telemetry retention.
- Deletion jobs are observable and audited.

## 11. API Requirements

### AR-001: API Style

Priority: P0

Conductor must expose an HTTP API for its UI and future integrations.

Acceptance criteria:

- API responses are JSON.
- Errors use a consistent structure.
- APIs are documented.
- API calls are authorized.

### AR-002: Repository APIs

Priority: P0

Conductor API must support:

```http
GET    /api/repos
POST   /api/repos/import
GET    /api/repos/{id}
PUT    /api/repos/{id}
DELETE /api/repos/{id}
```

Acceptance criteria:

- List endpoints support filtering, search, and pagination.
- Delete/archive behavior is explicit and safe.

### AR-003: Instance APIs

Priority: P0

Conductor API must support:

```http
GET    /api/instances
POST   /api/instances
GET    /api/instances/{id}
POST   /api/instances/{id}/start
POST   /api/instances/{id}/stop
POST   /api/instances/{id}/restart
POST   /api/instances/{id}/refresh
GET    /api/instances/{id}/health
GET    /api/instances/{id}/state
GET    /api/instances/{id}/logs
```

Acceptance criteria:

- Lifecycle operations are asynchronous where necessary.
- Operation status can be inspected after submission.
- Unauthorized lifecycle operations are blocked.

### AR-004: Run APIs

Priority: P1

Conductor API must support:

```http
GET    /api/runs
GET    /api/runs/{id}
POST   /api/runs/{id}/cancel
POST   /api/runs/{id}/retry
```

Acceptance criteria:

- Cancel and retry are only enabled where Conductor or Symphony can support the action.
- Unsupported actions return a clear capability error.

### AR-005: Report APIs

Priority: P1

Conductor API must support:

```http
GET    /api/reports
POST   /api/reports/daily
POST   /api/reports/weekly
POST   /api/reports/project/{projectId}
GET    /api/reports/{id}
GET    /api/reports/{id}/pdf
```

Acceptance criteria:

- Report generation can run asynchronously.
- Report results can be retrieved later.

### AR-006: Policy APIs

Priority: P2

Conductor API must support:

```http
GET    /api/policies
POST   /api/policies
PUT    /api/policies/{id}
POST   /api/policies/{id}/apply
```

Acceptance criteria:

- Policy validation occurs before save.
- Applying a policy records an audit event.

### AR-007: Live Updates

Priority: P1

Conductor should provide live updates to the UI.

Acceptance criteria:

- Dashboard metrics update without full-page refresh.
- Instance status changes appear promptly.
- Live update failures degrade to polling.

## 12. Non-Functional Requirements

### NFR-001: Reliability

Priority: P0

Conductor must continue operating when individual Symphony instances are offline, slow, or returning invalid data.

Acceptance criteria:

- One failing instance does not block collection from other instances.
- Polling failures are retried with backoff.
- Collector jobs are observable.

### NFR-002: Performance

Priority: P1

Conductor must keep dashboard interactions responsive for a software-company scale deployment.

Initial target scale:

- 100 managed repositories.
- 100 Symphony instances.
- 50 active agents.
- 10,000 tracked issues.
- 1,000 runs per day.

Performance targets:

- Dashboard initial load under 3 seconds on normal internal network conditions.
- Common filters/search under 1 second for target scale.
- Health status reflected within 15 seconds of instance state change.

### NFR-003: Scalability

Priority: P1

Conductor must scale beyond a single developer workstation.

Acceptance criteria:

- Background collectors can be partitioned or horizontally scaled in future.
- Database schema supports indexing by project, repository, instance, issue, status, and time.
- Azure execution mode does not require redesign of core entities.

### NFR-004: Security

Priority: P0

Conductor must treat repository code, issues, logs, tokens, and reports as sensitive.

Acceptance criteria:

- Secrets are encrypted at rest and masked in outputs.
- Administrative actions require authorization.
- Logs are redacted before presentation where possible.
- Containers/processes receive least practical credentials.

### NFR-005: Observability

Priority: P0

Conductor must be observable in its own right.

Required telemetry:

- API request logs.
- Background worker health.
- Polling latency and failures.
- Runner lifecycle operations.
- Alert rule execution.
- Report generation jobs.
- Database migration/version status.

Acceptance criteria:

- Operators can tell whether Conductor itself is healthy.
- Errors include correlation IDs.

### NFR-006: Maintainability

Priority: P0

Conductor must be modular and testable.

Acceptance criteria:

- Core domain logic is separated from infrastructure integrations.
- Runner implementations are isolated by execution mode.
- GitHub, Docker, Azure, persistence, reporting, and UI concerns are separated.
- Automated tests cover critical behaviors.

### NFR-007: Usability

Priority: P0

Conductor must be usable by technical and semi-technical stakeholders.

Acceptance criteria:

- Critical statuses are understandable without reading logs.
- Reports use plain English.
- Dangerous actions require confirmation.
- Empty states guide users toward the next action.

### NFR-008: Deployment

Priority: P1

Conductor must support local development, internal server deployment, and Azure production deployment.

Acceptance criteria:

- Environment configuration is documented.
- Database migrations can be applied safely.
- Docker deployment path is documented.
- Azure deployment path is documented when Azure runner ships.

### NFR-009: Testing

Priority: P0

Conductor must include automated testing appropriate to risk.

Required tests:

- Unit tests for domain logic and policy decisions.
- Integration tests for persistence.
- Integration tests for Symphony API client behavior.
- Integration tests or fakes for Docker runner.
- UI smoke tests for dashboard and repository command centre.
- End-to-end test for manual instance registration.

Acceptance criteria:

- Test commands are documented.
- CI can run deterministic tests without live GitHub credentials.
- Real GitHub tests are opt-in.

### NFR-010: Accessibility

Priority: P1

Conductor should meet baseline accessibility expectations for an internal web application.

Acceptance criteria:

- Keyboard navigation works for primary workflows.
- Status is not conveyed by color alone.
- Text contrast is sufficient in dark mode.
- Form fields and actions have accessible labels.

## 13. Symphony Change Requests

Conductor can be built against the current Symphony API, but the following Symphony additions would improve Conductor.

### SCR-001: Version Endpoint

Priority: P2

Symphony should expose:

```http
GET /api/v1/version
```

Expected payload:

```json
{
  "version": "1.3.0",
  "commit": "abc123",
  "buildTime": "2026-04-29T00:00:00Z"
}
```

### SCR-002: Structured Event Stream

Priority: P2

Symphony should expose server-sent events or another stream:

```http
GET /api/v1/events/stream
```

This would reduce polling and improve timeline freshness.

### SCR-003: Admin Pause and Resume

Priority: P2

Symphony should expose:

```http
POST /api/v1/pause
POST /api/v1/resume
```

Conductor needs a way to stop scheduling without killing a process or container.

### SCR-004: Graceful Shutdown

Priority: P2

Symphony should expose:

```http
POST /api/v1/shutdown
```

This would improve local process and container lifecycle management.

### SCR-005: Capabilities Endpoint

Priority: P2

Symphony should expose:

```http
GET /api/v1/capabilities
```

Conductor can use this to detect workflow editing, event stream, pause/resume, shutdown, validation, and future API support.

### SCR-006: Workflow Validation Endpoint

Priority: P2

Symphony should expose:

```http
POST /api/v1/workflow/validate
```

This lets Conductor validate generated profiles before replacing a live workflow.

## 14. MVP Requirements

### 14.1 MVP Phase 1: Fleet Visibility

Goal: A user can point Conductor at many existing Symphony instances and see everything in one place.

Required capabilities:

- Project registry.
- Repository registry.
- Manual Symphony instance registration by URL.
- Health polling.
- Runtime polling.
- State polling.
- Instance snapshots.
- Portfolio dashboard.
- Repository command centre.
- Runs/issues summary.
- Needs attention list.
- Basic alerts inside Conductor.

Definition of done:

- A user can register at least 10 Symphony instances.
- Conductor shows health, active issues, running sessions, retry queue, recent activity, and token totals for each.
- One offline instance does not affect monitoring of the rest.

### 14.2 MVP Phase 2: Local Orchestration

Goal: A user can import a GitHub repository and have Conductor run Symphony for it locally.

Required capabilities:

- GitHub repository import wizard.
- Symphony latest-release resolver.
- Symphony release artifact cache.
- Workflow profile registry.
- `WORKFLOW.md` generator.
- Per-instance GitHub PAT selection.
- Per-instance OpenAI/Codex credential selection.
- Local process runner.
- Docker runner.
- Port allocation.
- Instance folders.
- Start/stop/restart.
- Container/process logs.
- Basic encrypted secret storage.

Definition of done:

- A user can select a GitHub repository, choose a workflow profile, choose GitHub and OpenAI/Codex credentials, start a Docker-backed Symphony instance, and see it become healthy in Conductor.
- Instance creation resolves Symphony from GitHub Releases and records the release tag used.
- A user can restart or stop the instance from Conductor.
- The generated workflow is compatible with Symphony's current container and runtime expectations.

### 14.3 MVP Phase 3: Reporting

Goal: Conductor provides business value beyond operations monitoring.

Required capabilities:

- Daily delivery brief.
- Weekly software factory report.
- Project/client report.
- Markdown and HTML output.
- PDF output where practical.
- Token/cost summaries.
- Failure categories.

Definition of done:

- A user can generate a daily and weekly report from persisted data.
- Reports identify blocked work, failures, human review needs, and token/cost usage.

## 15. Acceptance Test Scenarios

### ATS-001: Register Existing Instance

Given a running Symphony instance URL, when an operator registers it in Conductor, then Conductor validates health, stores the instance, polls runtime and state, and displays it on the dashboard.

### ATS-002: Detect Offline Instance

Given a registered instance becomes unreachable, when health polling exceeds the offline threshold, then Conductor marks it offline, creates an event, raises an alert, and lists it under needs attention.

### ATS-003: Display Running Agents

Given Symphony reports running sessions through `/api/v1/state`, when Conductor polls state, then the repository command centre shows running issue, session ID, turn count, last activity, and token usage.

### ATS-004: Import Repository and Start Docker Instance

Given valid GitHub and Codex prerequisites, when a user imports a repository and starts Docker execution, then Conductor resolves the selected Symphony release from GitHub Releases, prepares the release artifact or image, writes the workflow, mounts required folders, starts the container, observes health, records the release tag, and shows the instance as healthy.

### ATS-005: Restart Instance

Given an authorized operator views a repository command centre, when they click restart and confirm, then Conductor restarts the underlying runner, records an audit event, and resumes polling.

### ATS-006: Generate Daily Brief

Given Conductor has stored run, issue, event, and token data for a day, when a user generates the daily delivery brief, then the report summarizes progress, current work, blockers, failures, human-review items, and token/cost usage.

### ATS-007: Workflow Validation Blocks Bad Save

Given a workflow edit contains invalid required fields, when a user attempts to save it, then Conductor blocks the save, shows validation errors, and does not replace the live workflow.

### ATS-008: Secret Redaction

Given GitHub and OpenAI credentials are configured, when logs, events, workflow previews, reports, or API responses are viewed, then credential values are not visible.

### ATS-009: Latest Symphony Release Resolution

Given an operator creates a Symphony instance with the default release selector, when provisioning begins, then Conductor resolves the current latest Symphony release from GitHub Releases, downloads or prepares the matching artifact, records the resolved tag on the instance, and does not auto-upgrade that instance on later restart unless an upgrade action is requested.

### ATS-010: Per-Instance Credentials

Given two Symphony instances are configured with different GitHub PATs and different OpenAI API keys, when Conductor starts both instances, then each runner receives only its selected `GITHUB_TOKEN` and OpenAI/Codex credential, secret references are recorded on the correct instance, and rotating one instance's credentials does not change the other instance.

## 16. Open Questions

- Should the first production database be SQL Server or PostgreSQL?
- Should the first UI implementation use Blazor Server or React/TypeScript?
- Which identity provider should secure Conductor users in the first production deployment?
- Should GitHub App authentication be part of the production MVP or a follow-up?
- What pricing model should be used for AI token cost estimates?
- Should Docker mode require official release-specific Symphony images, or should it continue supporting local image builds from GitHub release artifacts when images are unavailable?
- What is the minimum supported host OS for local process mode?
- Should Conductor support Podman in the same release as Docker?
- What retention periods are required for audit events, logs, snapshots, and reports?
- Which notification channel should be implemented first: Teams, email, Slack, or GitHub comments?

## 17. Requirements Traceability Summary

| Area | Key requirements |
| --- | --- |
| Fleet visibility | PRD-001, FR-005, FR-006, FR-019, FR-020, FR-021, FR-025, FR-028 |
| Symphony supervision | PRD-002, PRD-003, FR-010, FR-019, FR-023 |
| Local orchestration | FR-007, FR-008, FR-011, FR-012, FR-013, FR-015, FR-052, FR-053 |
| Workflow management | FR-014, FR-015, FR-016, FR-017, FR-018 |
| GitHub integration | FR-002, FR-003, FR-004, FR-032, FR-034, FR-047 |
| Reporting | FR-035, FR-036, FR-037, FR-038, FR-039 |
| Governance | FR-040, FR-041, FR-042, FR-043 |
| Alerts | FR-030, FR-044, FR-045 |
| Security | FR-046, FR-047, FR-048, FR-049, FR-054, NFR-004 |
| Operations | FR-050, FR-051, NFR-001, NFR-005, NFR-008 |
| Testing | NFR-009, ATS-001 through ATS-010 |
