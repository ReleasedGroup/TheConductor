# User Guide

Status: Planned

This guide will explain how operators use Conductor once the application shell and workflows are implemented.

Initial user workflows will cover dashboard review, repository import, instance registration, lifecycle actions, alert triage, and report generation.

## Dashboard Review

The dashboard includes a needs-attention panel for active critical and warning items. Each row shows the affected repository or Symphony instance, the current severity, the reason it needs attention, and a link to the source area for follow-up.

## Manual Instance Registration

Use the Instances page to register an existing Symphony runtime. Provide the instance base URL and an optional display name. Conductor validates the Symphony health endpoint, reads runtime metadata, captures the first state snapshot when available, and then lists the runtime in the instance registry.

If the health or runtime endpoint cannot be reached, Conductor shows the validation result and does not create an active instance record. A URL that is already registered returns a duplicate registration message.
