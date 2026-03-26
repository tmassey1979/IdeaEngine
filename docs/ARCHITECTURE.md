# Architecture

## Flow

```
Idea -> Architecture -> Repo -> Issues -> Code -> Review -> Deploy -> Feedback
```

## Current Workspace

```
docs/
backend/
codex/
planning/
templates/
```

## Current Runtime

- The long-term backend/runtime target is C#.
- The current runtime lives under `backend/`.
- Agent execution is now CLI-first through the local Codex runtime rather than a hosted model API.
- The C# backend currently covers shared self-build contracts, backlog indexing, bounded developer-operation planning, local queue persistence, workflow-state tracking including active recovery children and automatic parent resume, per-issue execution records, GitHub backlog discovery with superseded recovery filtering, stronger review/test execution, repeated-failure quarantine, long-stall quarantine sweeps, quarantined-work remediation signaling and recovery-story creation on GitHub with overlap prevention, recovery-aware scheduling with source-issue linkage, latest-path preference, and released-parent de-prioritization, queue cleanup for superseded recovery jobs, bounded run-until-idle orchestration for multi-cycle execution, GitHub superseded-path visibility for older overlapping recovery issues with stale-label cleanup when paths reactivate, retirement of stale open recovery branches after parent resume with parent-heartbeat audit visibility, stage-aware in-progress heartbeat comments with timing, stalled-state, recovery-release, requeue, and recovery-chain data, automatic workflow-label transitions, and validated workflow sync with a guard that only updates GitHub after the workflow reaches `validated`.
- The backend resolves per-agent model settings but executes model-backed work through the local Codex CLI.
- The deprecated JavaScript runner/orchestrator scaffold has been removed from the repo so backend implementation and documentation align on the C# path.

## Next Expected Layers

- richer C# queue-backed orchestration
- Codex CLI-backed agent execution
- API and UI surfaces on top of the backend runtime
- further issue generation and execution pipelines
- persistent job storage and credentials management
- database-backed agent configuration with encryption at rest
- unattended worker hosting, notification policy, and controlled auto-redeploy after validated major changes

## Core System Principles

- Agents are plugins loaded dynamically by the runner.
- The runner supports CLI and service execution modes.
- Jobs flow through an event-driven queue contract.
- Agent/provider configuration is persisted in the database, not flat files.
- Secrets and sensitive agent settings must be encrypted at rest; environment variables are bootstrap-only for local development and first-run setup.
- CLI arguments may override database-backed agent/provider configuration for the current process only; they should not persist unless an explicit save flow is invoked.
