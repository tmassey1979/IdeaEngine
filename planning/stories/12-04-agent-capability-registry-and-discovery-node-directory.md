# [Story] AGENT CAPABILITY REGISTRY AND DISCOVERY: Node Directory

Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`

## User Story

As a platform operator, I want the node directory capability, so that the system tracks all nodes participating in the platform.

## Description

The system tracks all nodes participating in the platform.

Nodes may include:

## Acceptance Criteria

- [ ] The Node Directory behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: raspberry pi nodes, developer machines, server nodes.
- [ ] Dependencies and integration points with the rest of AGENT CAPABILITY REGISTRY AND DISCOVERY are documented.

## Dev Notes

- Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
- Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`
- Known technical details:
  - raspberry pi nodes
  - developer machines
  - server nodes
  - cloud instances
  - nodeId

### Source Excerpt

The system tracks all nodes participating in the platform.

Nodes may include:

``` id="acr-6"
raspberry pi nodes
developer machines
server nodes
cloud instances
```

Node metadata includes:

``` id="acr-7"
nodeId
nodeType
cpuCapacity
memoryCapacity
networkLatency
location
activeAgents
```

This information helps the orchestration engine schedule tasks efficiently.

---

