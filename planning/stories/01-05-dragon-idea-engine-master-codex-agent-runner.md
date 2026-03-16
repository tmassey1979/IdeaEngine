# [Story] Dragon Idea Engine Master Codex: Agent Runner

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the agent runner capability, so that project: Responsibilities:.

## Description

Project:

Responsibilities:

## Acceptance Criteria

- [ ] The Agent Runner behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: dragon-agent-runner, load agent plugins, run CLI commands.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - dragon-agent-runner
  - load agent plugins
  - run CLI commands
  - connect to RabbitMQ
  - execute queued jobs

### Source Excerpt

Project:

```
dragon-agent-runner
```

Responsibilities:

```
load agent plugins
run CLI commands
connect to RabbitMQ
execute queued jobs
return results
```

CLI example:

```
dragon-agent-runner developer --repo crm --issue 42
```

Service mode:

```
dragon-agent-runner --service
```

---

