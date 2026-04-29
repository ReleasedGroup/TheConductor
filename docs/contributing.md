# Contributing Guide

Contributions should be tied to a tracked GitHub issue and reviewed through a pull request.

Before opening a pull request:

- Update documentation for any changed user workflow, configuration, API shape, deployment requirement, security behavior, testing command, or operational runbook.
- Run the relevant local validation commands.
- Keep generated files, secrets, local credentials, and machine-specific settings out of commits.

Documentation-only changes should pass:

```powershell
./scripts/validate-docs.ps1
```
