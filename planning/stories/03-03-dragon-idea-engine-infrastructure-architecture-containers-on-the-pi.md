# [Story] Dragon Idea Engine Infrastructure Architecture: Containers on the Pi

Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`

## User Story

As a platform operator, I want the containers on the pi capability, so that the Pi runs a Docker stack containing: Optional:.

## Description

The Pi runs a Docker stack containing:

Optional:

## Acceptance Criteria

- [ ] The Containers on the Pi behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: dragon-ui, dragon-api, dragon-orchestrator.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Infrastructure Architecture are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
- Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`
- Known technical details:
  - dragon-ui
  - dragon-api
  - dragon-orchestrator
  - dragon-agent-runner
  - rabbitmq

### Source Excerpt

The Pi runs a Docker stack containing:

```
dragon-ui
dragon-api
dragon-orchestrator
dragon-agent-runner
rabbitmq
postgres
```

Optional:

```
ollama (local AI models)
redis (cache)
```

---

