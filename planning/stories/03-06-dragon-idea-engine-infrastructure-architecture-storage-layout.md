# [Story] Dragon Idea Engine Infrastructure Architecture: Storage Layout

Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`

## User Story

As a platform operator, I want the storage layout capability, so that recommended filesystem structure: Details:.

## Description

Recommended filesystem structure:

Details:

## Acceptance Criteria

- [ ] The Storage Layout behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: /dragon-engine, /config, /repos.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Infrastructure Architecture are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Infrastructure Architecture
- Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`
- Known technical details:
  - /dragon-engine
  - /config
  - /repos
  - /workspaces
  - /logs

### Source Excerpt

Recommended filesystem structure:

```
/dragon-engine
    /config
    /repos
    /workspaces
    /logs
    /agents
    /models
```

Details:

```
repos       → cached repositories
workspaces  → temporary agent workspaces
logs        → job and system logs
agents      → plugin agents
models      → local AI models
```

---

