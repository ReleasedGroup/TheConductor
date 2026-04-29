# Feature Guides

Status: Planned

Feature guides will provide task-focused documentation for major Conductor capabilities.

Planned guides include repository onboarding, workflow profile management, Symphony instance operations, secret handling, alert review, and report generation.

## Assign Instance Credentials

Operators can assign credential references from the Instances page or through `PUT /api/instances/{instanceId}/credentials`.

Each Symphony instance tracks GitHub and OpenAI/Codex credentials independently. For each credential, choose `InheritDefault`, `SpecificSecret`, or `None`. Specific selections must reference an existing descriptor with a compatible type and scope; Conductor stores only the descriptor reference on the instance and never returns the secret value after save.

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

## Secret Handling

The secret descriptor list supports GitHub PAT and OpenAI API key credentials as separate types. Descriptor rows show the credential name, scope, target environment variable, and a masked value only. OpenAI API key descriptors map to `OPENAI_API_KEY`; GitHub PAT descriptors map to `GITHUB_TOKEN`.

Descriptors may include validation status, validation timestamp, a short validation message, and validation metadata JSON such as accepted token prefixes and the runtime environment variable used for injection. Plaintext token values are only accepted during create or rotate workflows and must not be returned in descriptor responses.

## Workflow Profile Management

Workflow profiles are managed from `/settings/workflows`. Operators can create a profile, edit an existing profile, mark one profile as the default, and preview the raw `WORKFLOW.md` source before saving.

Each save increments the profile revision when profile content or default state changes. When a profile becomes the default, Conductor clears the default flag from any previous default profile in the same transaction.

The Repositories page uses the saved profile list when creating a Symphony instance shell. Selecting a profile persists the `WorkflowProfileId` on the instance record for later workflow generation and validation.
