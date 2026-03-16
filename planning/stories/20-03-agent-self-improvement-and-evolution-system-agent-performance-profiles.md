# [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Agent Performance Profiles

Parent epic: [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`

## User Story

As a platform operator, I want the agent performance profiles capability, so that each agent has a performance profile stored in the knowledge system.

## Description

Each agent has a performance profile stored in the knowledge system.

Example profile structure:

## Acceptance Criteria

- [ ] The Agent Performance Profiles behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: agentId, agentType, supportedTasks.
- [ ] Dependencies and integration points with the rest of AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
- Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
- Known technical details:
  - agentId
  - agentType
  - supportedTasks
  - successRate
  - averageExecutionTime

### Source Excerpt

Each agent has a performance profile stored in the knowledge system.

Example profile structure:

```text
agentId
agentType
supportedTasks
successRate
averageExecutionTime
qualityScore
failurePatterns
lastImprovementIteration
```

These profiles allow the system to compare agents and choose the best ones for specific tasks.

---

