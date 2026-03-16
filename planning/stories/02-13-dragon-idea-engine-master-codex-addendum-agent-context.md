# [Story] Dragon Idea Engine Master Codex Addendum: Agent Context

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the agent context capability, so that the runtime context passed to agents.

## Description

The runtime context passed to agents.

## Acceptance Criteria

- [ ] The Agent Context behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: interface AgentContext {, job, payload.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - interface AgentContext {
  - job
  - payload
  - workspace
  - logger

### Source Excerpt

The runtime context passed to agents.

```ts
interface AgentContext {

  job
  payload
  workspace
  logger
  credentials
  repo
  config

}
```

---

