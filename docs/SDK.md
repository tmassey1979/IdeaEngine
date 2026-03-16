# SDK

`dragon-agent-sdk` is the shared Node-based agent package for the current implementation.

It provides:

- agent interface normalization with `name`, `description`, `version`, and `execute(context)`
- shared job parsing and result helpers
- structured logging with `jobId`, `agent`, and timestamps
- isolated workspace management under `workspaces/`
- git helpers for clone, branch, commit, push, and pull request payload stubs
- credential resolution with project-first lookup
- local job publishing to queue files under `.dragon/queues/`

Current scope is intentionally local-first so the SDK can back the runner before RabbitMQ and API services are built.
