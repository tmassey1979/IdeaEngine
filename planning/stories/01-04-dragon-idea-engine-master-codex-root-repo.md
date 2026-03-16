# [Story] Dragon Idea Engine Master Codex: Root Repo

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the root repo capability, so that structure:.

## Description

Structure:

## Acceptance Criteria

- [ ] The Root Repo behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: DragonIdeaEngine, ├─ docs, ├─ runner.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - DragonIdeaEngine
  - ├─ docs
  - ├─ runner
  - ├─ agents
  - ├─ services

### Source Excerpt

```
DragonIdeaEngine
```

Structure:

```
DragonIdeaEngine
│
├─ docs
│   MASTER_CODEX.md
│   AGENT_PLUGIN_SPEC.md
│   ARCHITECTURE.md
│
├─ runner
│   dragon-agent-runner
│
├─ agents
│   architect-agent
│   developer-agent
│   review-agent
│   test-agent
│   refactor-agent
│
├─ services
│   dragon-api
│   dragon-orchestrator
│
├─ ui
│   dragon-ui
│
├─ sdk
│   dragon-agent-sdk
│
└─ templates
    repo-templates
```

---

