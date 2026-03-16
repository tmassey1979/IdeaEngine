# [Story] AGENT ORCHESTRATION ENGINE: Resource-Aware Scheduling

Parent epic: [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`

## User Story

As a platform operator, I want the resource-aware scheduling capability, so that because the system may run on Raspberry Pi hardware, orchestration must consider resource constraints.

## Description

Because the system may run on Raspberry Pi hardware, orchestration must consider resource constraints.

Scheduling factors include:

## Acceptance Criteria

- [ ] The Resource-Aware Scheduling behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: CPU availability, memory usage, node capacity.
- [ ] Dependencies and integration points with the rest of AGENT ORCHESTRATION ENGINE are documented.

## Dev Notes

- Parent epic: [Epic] AGENT ORCHESTRATION ENGINE
- Source section: `codex/sections/11-agent-orchestration-engine.md`
- Known technical details:
  - CPU availability
  - memory usage
  - node capacity
  - task priority

### Source Excerpt

Because the system may run on Raspberry Pi hardware, orchestration must consider resource constraints.

Scheduling factors include:

``` id="orch-14"
CPU availability
memory usage
node capacity
task priority
```

Heavy tasks may be routed to more capable nodes.

---

