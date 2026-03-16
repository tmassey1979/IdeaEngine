# [Story] AGENT CAPABILITY REGISTRY AND DISCOVERY: Agent Reliability Scoring

Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`

## User Story

As a platform operator, I want the agent reliability scoring capability, so that agents are assigned reliability scores based on past performance.

## Description

Agents are assigned reliability scores based on past performance.

Metrics include:

## Acceptance Criteria

- [ ] The Agent Reliability Scoring behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: task success rate, average completion time, failure frequency.
- [ ] Dependencies and integration points with the rest of AGENT CAPABILITY REGISTRY AND DISCOVERY are documented.

## Dev Notes

- Parent epic: [Epic] AGENT CAPABILITY REGISTRY AND DISCOVERY
- Source section: `codex/sections/12-agent-capability-registry-and-discovery.md`
- Known technical details:
  - task success rate
  - average completion time
  - failure frequency
  - resource efficiency
  - Agent: CSharpDevelopmentAgent

### Source Excerpt

Agents are assigned reliability scores based on past performance.

Metrics include:

``` id="acr-12"
task success rate
average completion time
failure frequency
resource efficiency
```

Example score record:

``` id="acr-13"
Agent: CSharpDevelopmentAgent
Success Rate: 95%
Average Task Time: 3.8 minutes
Reliability Score: 0.93
```

Agents with higher reliability are prioritized.

---

