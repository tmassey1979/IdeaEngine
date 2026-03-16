# [Story] Dragon Idea Engine Master Codex Addendum: Job Result Schema

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the job result schema capability, so that agents must return results in this format.

## Description

Agents must return results in this format.

## Acceptance Criteria

- [ ] The Job Result Schema behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: {, "jobId": "uuid",, "status": "success",.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - {
  - "jobId": "uuid",
  - "status": "success",
  - "agent": "developer",
  - "duration": 12345,

### Source Excerpt

Agents must return results in this format.

```json
{
  "jobId": "uuid",
  "status": "success",
  "agent": "developer",
  "duration": 12345,
  "result": {},
  "logs": [],
  "errors": []
}
```

---

