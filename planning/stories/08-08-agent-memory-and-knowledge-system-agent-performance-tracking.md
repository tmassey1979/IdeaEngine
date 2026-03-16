# [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Agent Performance Tracking

Parent epic: [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`

## User Story

As a platform architect, I want the agent performance tracking capability, so that each agent's effectiveness is monitored.

## Description

Each agent's effectiveness is monitored.

Metrics tracked:

## Acceptance Criteria

- [ ] The Agent Performance Tracking behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: task success rate, average execution time, error frequency.
- [ ] Dependencies and integration points with the rest of AGENT MEMORY AND KNOWLEDGE SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
- Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
- Known technical details:
  - task success rate
  - average execution time
  - error frequency
  - cost per task
  - Agent: ReactFrontendAgent

### Source Excerpt

Each agent's effectiveness is monitored.

Metrics tracked:

``` id="mem-14"
task success rate
average execution time
error frequency
cost per task
```

Example:

``` id="mem-15"
Agent: ReactFrontendAgent
Success Rate: 0.92
Average Task Time: 4.5 minutes
Error Rate: 3%
```

Agents with lower performance may trigger retraining or replacement.

---

