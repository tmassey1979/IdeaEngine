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
- The C# backend currently covers shared self-build contracts, backlog indexing, bounded developer-operation planning, local queue persistence, workflow-state tracking including active recovery children and automatic parent resume, per-issue execution records, GitHub backlog discovery with superseded recovery filtering, stronger review/test execution, repeated-failure quarantine, long-stall quarantine sweeps, quarantined-work remediation signaling and recovery-story creation on GitHub with overlap prevention, recovery-aware scheduling with source-issue linkage, latest-path preference, and released-parent de-prioritization, queue cleanup for superseded recovery jobs, bounded run-until-idle orchestration for multi-cycle execution, GitHub superseded-path visibility for older overlapping recovery issues with stale-label cleanup when paths reactivate, retirement of stale open recovery branches after parent resume with parent-heartbeat audit visibility, stage-aware in-progress heartbeat comments with timing, stalled-state, recovery-release, requeue, and recovery-chain data, automatic workflow-label transitions, and validated workflow sync with a guard that only updates GitHub after the workflow reaches `validated`.
- The deprecated JavaScript runner/orchestrator scaffold has been removed from the repo so backend implementation and documentation align on the C# path.

## Next Expected Layers

- richer C# queue-backed orchestration
- API and UI surfaces on top of the backend runtime
- further issue generation and execution pipelines
- persistent job storage and credentials management
