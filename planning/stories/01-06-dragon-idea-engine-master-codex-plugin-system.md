# [Story] Dragon Idea Engine Master Codex: Plugin System

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the plugin system capability, so that agents are dynamically loaded.

## Description

Agents are dynamically loaded.

Directory:

## Acceptance Criteria

- [ ] The Plugin System behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: /agents, agents/, architect/.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - /agents
  - agents/
  - architect/
  - developer/
  - review/

### Source Excerpt

Agents are dynamically loaded.

Directory:

```
/agents
```

Example:

```
agents/
  architect/
  developer/
  review/
```

Plugin interface:

```
name
description
registerArgs()
execute()
```

Example plugin skeleton:

```
export interface DragonAgent {

  name: string

  description: string

  registerArgs(cli)

  execute(context)
}
```

---

