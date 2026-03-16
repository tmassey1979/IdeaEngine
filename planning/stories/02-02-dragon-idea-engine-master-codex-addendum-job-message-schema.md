# [Story] Dragon Idea Engine Master Codex Addendum: Job Message Schema

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the job message schema capability, so that all jobs published to RabbitMQ must follow this structure.

## Description

All jobs published to RabbitMQ must follow this structure.

## Acceptance Criteria

- [ ] The Job Message Schema behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: {, "jobId": "uuid",, "agent": "developer",.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - {
  - "jobId": "uuid",
  - "agent": "developer",
  - "action": "implement_issue",
  - "repo": "dragon-crm",

### Source Excerpt

All jobs published to RabbitMQ must follow this structure.

```json
{
  "jobId": "uuid",
  "agent": "developer",
  "action": "implement_issue",
  "repo": "dragon-crm",
  "project": "DragonCRM",
  "issue": 42,
  "priority": "normal",
  "createdAt": "timestamp",
  "payload": {},
  "metadata": {
    "requestedBy": "system",
    "source": "orchestrator"
  }
}
```

---

