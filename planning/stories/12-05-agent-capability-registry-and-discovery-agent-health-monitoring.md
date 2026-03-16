# [Story] AGENT CAPABILITY REGISTRY AND DISCOVERY: Agent Health Monitoring

Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`

## User Story

As a platform operator, I want the agent health monitoring capability, so that the registry tracks the operational health of each agent.

## Description

The registry tracks the operational health of each agent.

Metrics collected include:

## Acceptance Criteria

- [ ] The Agent Health Monitoring behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: heartbeat signals, task completion success, error rates.
- [ ] Dependencies and integration points with the rest of AGENT CAPABILITY REGISTRY AND DISCOVERY are documented.

## Dev Notes

- Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
- Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`
- Known technical details:
  - heartbeat signals
  - task completion success
  - error rates
  - resource consumption
  - agent marked offline

### Source Excerpt

The registry tracks the operational health of each agent.

Metrics collected include:

``` id="acr-8"
heartbeat signals
task completion success
error rates
resource consumption
```

Agents periodically send heartbeats.

If heartbeats stop:

``` id="acr-9"
agent marked offline
tasks reassigned
node flagged for investigation
```

---

