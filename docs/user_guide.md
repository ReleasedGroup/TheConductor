# User Guide

Status: Planned

This guide will explain how operators use Conductor once the application shell and workflows are implemented.

Initial user workflows will cover dashboard review, repository import, instance registration, lifecycle actions, alert triage, and report generation.

## Dashboard Review

The dashboard includes a needs-attention panel for active critical and warning items. Each row shows the affected repository or Symphony instance, the current severity, the reason it needs attention, and a link to the source area for follow-up.

## Secret Review

The Secrets page lists saved credential descriptors for orchestration. GitHub PAT and OpenAI API key descriptors are shown independently, and saved values are rendered only as masked placeholders.
