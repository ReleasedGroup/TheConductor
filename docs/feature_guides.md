# Feature Guides

Status: Planned

Feature guides will provide task-focused documentation for major Conductor capabilities.

Planned guides include repository onboarding, workflow profile management, Symphony instance operations, secret handling, alert review, and report generation.

## Secret Handling

The secret descriptor list supports GitHub PAT and OpenAI API key credentials as separate types. Descriptor rows show the credential name, scope, target environment variable, and a masked value only. OpenAI API key descriptors map to `OPENAI_API_KEY`; GitHub PAT descriptors map to `GITHUB_TOKEN`.

Descriptors may include validation status, validation timestamp, a short validation message, and validation metadata JSON such as accepted token prefixes and the runtime environment variable used for injection. Plaintext token values are only accepted during create or rotate workflows and must not be returned in descriptor responses.
