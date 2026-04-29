# API Documentation

Status: Draft

This document describes Conductor's public and internal HTTP APIs as endpoints become stable.

The current API direction is defined in [requirements.md](requirements.md) and [technical.md](technical.md).

## Symphony Runtime Client

Conductor registers a named HTTP client called `SymphonyApi` for calls to existing Symphony runtimes. The typed client uses the Symphony instance base URL and appends these runtime endpoints:

| Client method | Method | Endpoint | Notes |
| --- | --- | --- | --- |
| `GetHealthAsync` | `GET` | `/api/v1/health` | Uses a 3 second timeout, preserves raw JSON, and maps reported health to `InstanceHealthStatus`. Network failures are returned as `Offline` health probes. |
| `GetRuntimeAsync` | `GET` | `/api/v1/runtime` | Uses a 10 second timeout and preserves the runtime JSON payload for snapshot ingestion. |
| `GetWorkflowAsync` | `GET` | `/api/v1/workflow` | Reads workflow source from a `source`, `workflowSource`, `content`, or `workflow` JSON field, or treats the response body as raw source. Preserves the response ETag. |
| `SaveWorkflowAsync` | `PUT` | `/api/v1/workflow` | Sends `{ "source": "..." }`, forwards the document ETag as `If-Match` when present, and uses a 15 second timeout. |
| `GetStateAsync` | `GET` | `/api/v1/state` | Uses a 10 second timeout and preserves the state JSON payload for snapshot ingestion. |
| `GetIssueAsync` | `GET` | `/api/v1/{issue_identifier}` | URL-encodes the issue identifier, returns `null` for `404`, and otherwise preserves issue detail JSON. |
| `RequestRefreshAsync` | `POST` | `/api/v1/refresh` | Uses a 10 second timeout and returns whether Symphony accepted the best-effort refresh request. |

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

## Instance Credential Assignment

`PUT /api/instances/{instanceId}/credentials` assigns GitHub and OpenAI/Codex credential references for one Symphony instance. Responses return descriptor metadata only; secret values are never returned.

Request:

```json
{
  "gitHubCredential": {
    "inheritanceMode": "SpecificSecret",
    "secretId": "11111111-1111-1111-1111-111111111111"
  },
  "openAiCredential": {
    "inheritanceMode": "InheritDefault"
  },
  "requestedByUserId": "operator"
}
```

Behavior:

- Supports `InheritDefault`, `SpecificSecret`, and `None` independently for GitHub and OpenAI/Codex credentials.
- Requires `GitHubToken` descriptors for GitHub assignments.
- Accepts `OpenAiApiKey` or `CodexHome` descriptors for OpenAI/Codex assignments.
- Rejects descriptors scoped to a different project, repository, or Symphony instance.
- Records an operational event and audit event with descriptor IDs and names only.

Responses:

- `200 OK` with the updated assignment summary.
- `400 Bad Request` with validation errors when modes, secret IDs, types, or scopes are invalid.
- `404 Not Found` when the Symphony instance does not exist.
