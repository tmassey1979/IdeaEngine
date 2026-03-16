# [Story] Dragon Idea Engine Master Codex: Docker Deployment

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the docker deployment capability, so that recommended containers: Scaling workers:.

## Description

Recommended containers:

Scaling workers:

## Acceptance Criteria

- [ ] The Docker Deployment behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: dragon-ui, dragon-api, dragon-orchestrator.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - dragon-ui
  - dragon-api
  - dragon-orchestrator
  - dragon-agent-runner
  - rabbitmq

### Source Excerpt

Recommended containers:

```
dragon-ui
dragon-api
dragon-orchestrator
dragon-agent-runner
rabbitmq
postgres
```

Scaling workers:

```
docker compose up --scale agent-runner=10
```

---

