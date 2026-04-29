# API Documentation

Status: Planned

This document will describe Conductor's public and internal HTTP APIs as endpoints become stable.

The current API direction is defined in [requirements.md](requirements.md) and [technical.md](technical.md). Endpoint-level request and response examples will be added when the Minimal API surface is implemented.

## Instance Registration

`POST /api/instances/register` registers an already running Symphony instance by URL.

Request:

```json
{
  "baseUrl": "http://localhost:5173",
  "displayName": "Billing Symphony"
}
```

Behavior:

- Normalizes base URLs and accepts URLs pointing at `/api/v1/health`, `/api/v1/runtime`, `/api/v1/state`, or `/api/v1/refresh`.
- Calls Symphony `/api/v1/health` and rejects offline or non-successful health responses.
- Calls Symphony `/api/v1/runtime` before writing any database records.
- Attempts an initial `/api/v1/state` capture and stores it with the first snapshot when available.
- Stores the instance, repository metadata from runtime where available, an event, and an audit record in one transaction.

Responses:

- `201 Created` with the registered instance summary.
- `400 Bad Request` with validation errors when the URL, health probe, or runtime probe fails.
- `409 Conflict` when the normalized base URL is already registered.
