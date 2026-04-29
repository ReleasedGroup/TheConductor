# Feature Guides

Status: Planned

Feature guides will provide task-focused documentation for major Conductor capabilities.

Planned guides include repository onboarding, workflow profile management, Symphony instance operations, secret handling, alert review, and report generation.

## Secret Handling

Secret descriptors now model GitHub personal access tokens separately from OpenAI API keys. Descriptor data may include validation status, validation timestamp, a short validation message, and validation metadata JSON such as accepted token prefixes and the runtime environment variable used for injection.

Descriptor display must use the safe masked display string from the secret type metadata. GitHub PAT descriptors render as `github_pat_********` and are injected into Symphony through `GITHUB_TOKEN`; plaintext token values are only accepted during create or rotate workflows and must not be returned in descriptor responses.
