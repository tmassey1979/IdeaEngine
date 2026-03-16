# [Story] Dragon Idea Engine Infrastructure Architecture: Repository Layout (Full Project)

Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`

## User Story

As a platform operator, I want the repository layout (full project) capability, so that final recommended GitHub structure for the main repository:.

## Description

Final recommended GitHub structure for the main repository:

## Acceptance Criteria

- [ ] The Repository Layout (Full Project) behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: DragonIdeaEngine, ├─ docs, ├─ runner.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Infrastructure Architecture are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
- Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`
- Known technical details:
  - DragonIdeaEngine
  - ├─ docs
  - ├─ runner
  - ├─ sdk
  - ├─ agents

### Source Excerpt

Final recommended GitHub structure for the main repository:

```
DragonIdeaEngine
│
├─ docs
│   MASTER_CODEX.md
│   AGENT_JOB_SCHEMA.md
│   AGENT_PLUGIN_SPEC.md
│   AGENT_LIFECYCLE.md
│   ARCHITECTURE.md
│
├─ runner
│   dragon-agent-runner
│
├─ sdk
│   dragon-agent-sdk
│
├─ agents
│   architect-agent
│   developer-agent
│   review-agent
│   test-agent
│   refactor-agent
│   documentation-agent
│   feedback-agent
│
├─ services
│   dragon-api
│   dragon-orchestrator
│
├─ ui
│   dragon-ui
│
├─ infrastructure
│   docker
│   swarm
│   raspberrypi-image
│
├─ templates
│   project-templates
│
└─ examples
    sample-project
```

---

