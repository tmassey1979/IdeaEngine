# [Story] Dragon Idea Engine Master Codex Addendum: Job Publishing

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the job publishing capability, so that agents can publish new jobs.

## Description

Agents can publish new jobs.

Example:

## Acceptance Criteria

- [ ] The Job Publishing behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: await jobs.publish({, agent: "review",, action: "review_pr",.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - await jobs.publish({
  - agent: "review",
  - action: "review_pr",
  - repo: "dragon-crm",
  - payload: { pr: 22 }

### Source Excerpt

Agents can publish new jobs.

Example:

```ts
await jobs.publish({
  agent: "review",
  action: "review_pr",
  repo: "dragon-crm",
  payload: { pr: 22 }
})
```

---

