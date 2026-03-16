# [Story] Dragon Idea Engine Master Codex: System Architecture

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the system architecture capability, so that multiple runners can exist.

## Description

Multiple runners can exist.

Example:

## Acceptance Criteria

- [ ] The System Architecture behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: Dragon Idea Engine UI, Dragon API Controller, RabbitMQ.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - Dragon Idea Engine UI
  - Dragon API Controller
  - RabbitMQ
  - Dragon Agent Runner                Dragon Agent Runner
  - (worker)                            (worker)

### Source Excerpt

```
                Dragon Idea Engine UI
                        │
                        ▼
                Dragon API Controller
                        │
                        ▼
                    RabbitMQ
                        │
       ┌────────────────┴─────────────────┐
       │                                  │
Dragon Agent Runner                Dragon Agent Runner
   (worker)                            (worker)
       │                                  │
       ▼                                  ▼
  Agent Plugins                       Agent Plugins
```

Multiple runners can exist.

Example:

```
10+ workers
100+ concurrent jobs
```

---

