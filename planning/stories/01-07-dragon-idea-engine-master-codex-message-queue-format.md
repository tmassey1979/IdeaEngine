# [Story] Dragon Idea Engine Master Codex: Message Queue Format

Parent epic: [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story

As a platform architect, I want the message queue format capability, so that standard job message: Runner logic:.

## Description

Standard job message:

Runner logic:

## Acceptance Criteria

- [ ] The Message Queue Format behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: {, "agent": "developer",, "repo": "dragon-crm",.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex
- Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
- Known technical details:
  - {
  - "agent": "developer",
  - "repo": "dragon-crm",
  - "issue": 42,
  - "priority": "normal",

### Source Excerpt

Standard job message:

```
{
  "agent": "developer",
  "repo": "dragon-crm",
  "issue": 42,
  "priority": "normal",
  "payload": {}
}
```

Runner logic:

```
route message → matching agent
```

---

