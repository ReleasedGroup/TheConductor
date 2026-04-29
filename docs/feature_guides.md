# Feature Guides

Status: Planned

Feature guides will provide task-focused documentation for major Conductor capabilities.

Planned guides include repository onboarding, workflow profile management, Symphony instance operations, secret handling, alert review, and report generation.

## Assign Instance Credentials

Operators can assign credential references from the Instances page or through `PUT /api/instances/{instanceId}/credentials`.

Each Symphony instance tracks GitHub and OpenAI/Codex credentials independently. For each credential, choose `InheritDefault`, `SpecificSecret`, or `None`. Specific selections must reference an existing descriptor with a compatible type and scope; Conductor stores only the descriptor reference on the instance and never returns the secret value after save.
