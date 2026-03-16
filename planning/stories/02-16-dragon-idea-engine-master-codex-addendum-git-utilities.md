# [Story] Dragon Idea Engine Master Codex Addendum: Git Utilities

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the git utilities capability, so that the SDK provides helpers: Example:.

## Description

The SDK provides helpers:

Example:

## Acceptance Criteria

- [ ] The Git Utilities behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: cloneRepo(), createBranch(), commit().
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - cloneRepo()
  - createBranch()
  - commit()
  - push()
  - createPullRequest()

### Source Excerpt

The SDK provides helpers:

```
cloneRepo()
createBranch()
commit()
push()
createPullRequest()
```

Example:

```ts
await git.createBranch("feature/login")
```

---

