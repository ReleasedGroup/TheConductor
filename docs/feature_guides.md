# Feature Guides

Status: Planned

Feature guides will provide task-focused documentation for major Conductor capabilities.

Planned guides include repository onboarding, workflow profile management, Symphony instance operations, secret handling, alert review, and report generation.

## Secret Handling

The secret descriptor list supports GitHub PAT and OpenAI API key credentials as separate types. Descriptor rows show the credential name, scope, target environment variable, and a masked value only. OpenAI API key descriptors map to `OPENAI_API_KEY`; GitHub PAT descriptors map to `GITHUB_TOKEN`.
