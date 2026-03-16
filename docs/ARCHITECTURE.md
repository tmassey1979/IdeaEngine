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

- The long-term backend/runtime target is C#.
- The current JavaScript runner/orchestrator remains in place as a working prototype for behavior and backlog flow.
- A new C# backend foundation now lives under `backend/` and currently covers shared self-build contracts, backlog indexing, bounded developer-operation planning, local queue persistence, workflow-state tracking including active recovery children and automatic parent resume, per-issue execution records, GitHub backlog discovery, stronger review/test execution, repeated-failure quarantine, long-stall quarantine sweeps, quarantined-work remediation signaling and recovery-story creation on GitHub with overlap prevention, recovery-aware scheduling with source-issue linkage and latest-path preference, queue cleanup for superseded recovery jobs, GitHub superseded-path visibility for older overlapping recovery issues, stage-aware in-progress heartbeat comments with timing, stalled-state, recovery-release, requeue, and recovery-chain data, automatic workflow-label transitions, and validated workflow sync with a guard that only updates GitHub after the workflow reaches `validated`.
- `dragon-agent-runner` loads agent plugins from the workspace.
- agents expose `name`, `description`, `version`, and `execute(context)` through the shared SDK.
- CLI mode runs one agent directly.
- service mode reads newline-delimited JSON jobs, validates them against the shared schema, and emits structured job results.
- failed jobs follow the codex retry schedule and spill into a local dead-letter queue file when retries are exhausted.
- agents receive workspace, git, credentials, job publishing, and logging utilities through the SDK context.
- `dragon-orchestrator` can inspect the repo backlog, select the next open story, and publish a follow-up implementation job for the system to work on itself.
- `dragon-orchestrator execute-once` can now run that selected job immediately, persist an execution record, and enqueue review/test follow-up jobs for the next loop iteration.
- `dragon-orchestrator cycle-once` now prefers queued work first, which lets the system continue through developer -> review -> test stages across repeated invocations while maintaining per-issue workflow state under `.dragon/state/issues.json`.

## Next Expected Layers

- queue-backed orchestration
- API and UI surfaces
- issue generation and execution pipelines
- persistent job storage and credentials management
