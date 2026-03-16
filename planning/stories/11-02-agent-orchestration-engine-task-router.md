# [Story] AGENT ORCHESTRATION ENGINE: Task Router

Parent epic: [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`

## User Story

As a platform operator, I want the task router capability, so that the Task Router determines which agent should perform a given task.

## Description

The Task Router determines which agent should perform a given task.

Inputs to the router include:

## Acceptance Criteria

- [ ] The Task Router behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: task type, required capability, agent availability.
- [ ] Dependencies and integration points with the rest of AGENT ORCHESTRATION ENGINE are documented.

## Dev Notes

- Parent epic: [Epic] AGENT ORCHESTRATION ENGINE
- Source section: `codex/sections/11-agent-orchestration-engine.md`
- Known technical details:
  - task type
  - required capability
  - agent availability
  - system load
  - priority level

### Source Excerpt

The Task Router determines which agent should perform a given task.

Inputs to the router include:

``` id="orch-3"
task type
required capability
agent availability
system load
priority level
```

Example routing decisions:

``` id="orch-4"
Generate React UI → ReactFrontendAgent
Write C# API → CSharpDevelopmentAgent
Design circuit schematic → CircuitDesignAgent
Create tutorial video → VideoProductionAgent
```

Routing rules are maintained in the **Agent Capability Registry**.

---

