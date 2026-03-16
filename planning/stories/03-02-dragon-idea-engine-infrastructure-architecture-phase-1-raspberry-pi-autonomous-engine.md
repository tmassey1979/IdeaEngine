# [Story] Dragon Idea Engine Infrastructure Architecture: Phase 1 Raspberry Pi Autonomous Engine

Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`

## User Story

As a platform operator, I want the phase 1 raspberry pi autonomous engine capability, so that phase 1 runs **all components on a single device**.

## Description

Phase 1 runs **all components on a single device**.

Architecture:

## Acceptance Criteria

- [ ] The Phase 1 Raspberry Pi Autonomous Engine behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: RabbitMQ Queue, Agent Runner Workers, Agent Plugins.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Infrastructure Architecture are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
- Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`
- Known technical details:
  - RabbitMQ Queue
  - Agent Runner Workers
  - Agent Plugins

### Source Excerpt

Phase 1 runs **all components on a single device**.

Architecture:

```
                ┌───────────────────────┐
                │ Dragon UI (React)     │
                └─────────────┬─────────┘
                              │
                              ▼
                      ┌───────────────┐
                      │ Dragon API    │
                      └───────┬───────┘
                              │
                              ▼
                      ┌───────────────┐
                      │ Orchestrator  │
                      └───────┬───────┘
                              │
                              ▼
                        RabbitMQ Queue
                              │
                              ▼
                     Agent Runner Workers
                              │
                              ▼
                        Agent Plugins
```

Everything runs locally.

---

