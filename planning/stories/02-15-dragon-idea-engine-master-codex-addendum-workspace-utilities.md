# [Story] Dragon Idea Engine Master Codex Addendum: Workspace Utilities

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the workspace utilities capability, so that sDK automatically provides isolated workspaces.

## Description

SDK automatically provides isolated workspaces.

Example:

## Acceptance Criteria

- [ ] The Workspace Utilities behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: /workspaces, dragon-crm, issue-42.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - /workspaces
  - dragon-crm
  - issue-42
  - const repo = await workspace.cloneRepo(repoUrl)

### Source Excerpt

SDK automatically provides isolated workspaces.

Example:

```
/workspaces
  dragon-crm
    issue-42
```

Usage:

```ts
const repo = await workspace.cloneRepo(repoUrl)
```

---

