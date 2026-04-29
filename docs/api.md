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
