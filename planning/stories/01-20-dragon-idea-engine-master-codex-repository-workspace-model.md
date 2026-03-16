# [Story] Dragon Idea Engine Master Codex: Repository Workspace Model

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the repository workspace model capability, so that developer agents must use **isolated workspaces**.

## Description

Developer agents must use **isolated workspaces**.

Example:

## Acceptance Criteria

- [ ] The Repository Workspace Model behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: /workspaces, dragon-crm, issue-42.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - /workspaces
  - dragon-crm
  - issue-42
  - issue-43
  - clone repo

### Source Excerpt

Developer agents must use **isolated workspaces**.

Example:

```
/workspaces
   dragon-crm
      issue-42
      issue-43
```

Workflow:

```
clone repo
create branch
implement issue
push PR
destroy workspace
```

Benefits:

```
no repo corruption
parallel agents
clean environments
```

---

