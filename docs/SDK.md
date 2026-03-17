# SDK

The current platform direction is a C# backend, not the earlier Node prototype.

This document tracks the shared backend helper surface that supports self-build work:

- job and workflow contracts in `backend/src/Dragon.Backend.Contracts`
- orchestration helpers in `backend/src/Dragon.Backend.Orchestrator`
- deterministic developer-operation planning for bounded repository edits
- queue, workflow-state, and execution-record persistence under `.dragon/`
- GitHub synchronization helpers for heartbeat, quarantine, recovery, and completion flows

Near-term scope stays local-first so the backend can keep iterating on self-build behavior before more distributed infrastructure is added.
