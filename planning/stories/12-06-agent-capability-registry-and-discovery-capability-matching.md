# [Story] AGENT CAPABILITY REGISTRY AND DISCOVERY: Capability Matching

Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`

## User Story

As a platform operator, I want the capability matching capability, so that when a task enters the system, the Task Router queries the registry.

## Description

When a task enters the system, the Task Router queries the registry.

Example process:

## Acceptance Criteria

- [ ] The Capability Matching behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: Task arrives, ↓, Identify required capability.
- [ ] Dependencies and integration points with the rest of AGENT CAPABILITY REGISTRY AND DISCOVERY are documented.

## Dev Notes

- Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
- Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`
- Known technical details:
  - Task arrives
  - ↓
  - Identify required capability
  - Query registry for matching agents
  - Select best available agent

### Source Excerpt

When a task enters the system, the Task Router queries the registry.

Example process:

``` id="acr-10"
Task arrives
      ↓
Identify required capability
      ↓
Query registry for matching agents
      ↓
Select best available agent
```

Matching criteria may include:

``` id="acr-11"
capability match
node resource availability
agent reliability score
task priority
```

---

