# [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Data Structure

Parent epic: [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story

As a product owner, I want the idea data structure capability, so that example idea record: Stored in the `ideas` table.

## Description

Example idea record:

Stored in the `ideas` table.

## Acceptance Criteria

- [ ] The Idea Data Structure behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: {, "ideaId": "uuid",, "title": "Local contractor CRM",.
- [ ] Dependencies and integration points with the rest of IDEA SCORING AND SELECTION SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] IDEA SCORING AND SELECTION SYSTEM
- Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
- Known technical details:
  - {
  - "ideaId": "uuid",
  - "title": "Local contractor CRM",
  - "description": "Simple CRM for contractors",
  - "tags": ["crm","small-business"],

### Source Excerpt

Example idea record:

```json
{
  "ideaId": "uuid",
  "title": "Local contractor CRM",
  "description": "Simple CRM for contractors",
  "tags": ["crm","small-business"],
  "submittedBy": "user",
  "source": "ui",
  "createdAt": "timestamp"
}
```

Stored in the `ideas` table.

---

