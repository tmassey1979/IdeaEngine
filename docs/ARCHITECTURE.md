# Architecture

## Flow

```
Idea -> Architecture -> Repo -> Issues -> Code -> Review -> Deploy -> Feedback
```

## Current Workspace

```
docs/
runner/
agents/
services/
ui/
sdk/
templates/
```

## Current Runtime

- `dragon-agent-runner` loads agent plugins from the workspace.
- agents expose `name`, `description`, `version`, and `execute(context)` through the shared SDK.
- CLI mode runs one agent directly.
- service mode reads newline-delimited JSON jobs, validates them against the shared schema, and emits structured job results.
- failed jobs follow the codex retry schedule and spill into a local dead-letter queue file when retries are exhausted.
- agents receive workspace, git, credentials, job publishing, and logging utilities through the SDK context.
- `dragon-orchestrator` can inspect the repo backlog, select the next open story, and publish a follow-up implementation job for the system to work on itself.
- `dragon-orchestrator execute-once` can now run that selected job immediately, persist an execution record, and enqueue review/test follow-up jobs for the next loop iteration.

## Next Expected Layers

- queue-backed orchestration
- API and UI surfaces
- issue generation and execution pipelines
- persistent job storage and credentials management
