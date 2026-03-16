# [Story] DISTRIBUTED AGENT CLUSTER ARCHITECTURE: Local Raspberry Pi Mode

Parent epic: [Epic] DISTRIBUTED AGENT CLUSTER ARCHITECTURE
Source section: `codex/sections/17-distributed-agent-cluster-architecture.md`

## User Story

As a platform operator, I want the local raspberry pi mode capability, so that in single-node installations, all services run on one device.

## Description

In single-node installations, all services run on one device.

Example configuration:

## Acceptance Criteria

- [ ] The Local Raspberry Pi Mode behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: orchestrator, message queue, database.
- [ ] Dependencies and integration points with the rest of DISTRIBUTED AGENT CLUSTER ARCHITECTURE are documented.

## Dev Notes

- Parent epic: [Epic] DISTRIBUTED AGENT CLUSTER ARCHITECTURE
- Source section: `codex/sections/17-distributed-agent-cluster-architecture.md`
- Known technical details:
  - orchestrator
  - message queue
  - database
  - agent containers
  - asset storage

### Source Excerpt

In single-node installations, all services run on one device.

Example configuration:

```text id="mtd4kg"
orchestrator
message queue
database
agent containers
asset storage
```

This allows developers to run Dragon Idea Engine on a single Raspberry Pi.

---

