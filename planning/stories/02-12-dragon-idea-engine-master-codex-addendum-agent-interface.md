# [Story] Dragon Idea Engine Master Codex Addendum: Agent Interface

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the agent interface capability, so that every agent must implement this interface.

## Description

Every agent must implement this interface.

## Acceptance Criteria

- [ ] The Agent Interface behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: export interface DragonAgent {, name: string, description: string.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - export interface DragonAgent {
  - name: string
  - description: string
  - version: string
  - registerArgs(cli)

### Source Excerpt

Every agent must implement this interface.

```ts
export interface DragonAgent {

  name: string

  description: string

  version: string

  registerArgs(cli)

  execute(context: AgentContext): Promise<AgentResult>

}
```

---

