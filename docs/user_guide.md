# User Guide

Status: Planned

This guide will explain how operators use Conductor once the application shell and workflows are implemented.

Initial user workflows will cover dashboard review, repository import, instance registration, lifecycle actions, alert triage, and report generation.

## Dashboard Review

The dashboard includes a needs-attention panel for active critical and warning items. Each row shows the affected repository or Symphony instance, the current severity, the reason it needs attention, and a link to the source area for follow-up.

## Secret Management

Open `/settings/secrets` to create and maintain credential descriptors. The page supports GitHub PAT, OpenAI API key, Codex home, and other secret descriptors scoped globally, by project, by repository, or by Symphony instance.

Saved secret values are masked in the descriptor list. Operators can rotate or delete a descriptor from the list, but stored values are not shown again after creation or rotation.

## Manual Instance Registration

Use the Instances page to register an existing Symphony runtime. Provide the instance base URL and an optional display name. Conductor validates the Symphony health endpoint, reads runtime metadata, captures the first state snapshot when available, and then lists the runtime in the instance registry.

If the health or runtime endpoint cannot be reached, Conductor shows the validation result and does not create an active instance record. A URL that is already registered returns a duplicate registration message.

## Repository Import

Use the Repositories page to import a GitHub repository by `owner/name`. The initial flow stores repository metadata, optional project assignment, visibility, default branch, and archived state.

When a project is selected during import, the repository is linked immediately and the import result confirms the project association.

The page can also create a first Symphony instance shell in `NotProvisioned` state. The shell captures execution mode, instance URL, port, release selector, and credential inheritance choices so later provisioning work can start from a validated record.

If workflow profiles exist, choose one while creating the instance shell. Conductor stores that profile reference on the instance so later workflow generation can render the correct `WORKFLOW.md` source.

Imported repositories appear in the managed repository registry with project, visibility, default branch, archive state, orchestration eligibility, instance counts, last sync, and latest health metadata. Open a repository row to review its detail page, including clone and web URLs, sync metadata, orchestration status, and active Symphony instances attached to that repository.

## Workflow Profiles

Use the Workflows page to create and edit reusable `WORKFLOW.md` profiles. Each profile stores a name, optional description, raw Markdown source, default flag, revision number, and created/updated timestamps.

Mark one profile as the default when it should be the standard choice for new Symphony instance shells. Setting a profile as default clears the previous default profile.

The editor shows the current source beside the form before save. Saving a profile validates required fields and preserves an audit event for create and update operations.
