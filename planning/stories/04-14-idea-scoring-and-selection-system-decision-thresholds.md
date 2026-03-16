# [Story] IDEA SCORING AND SELECTION SYSTEM: Decision Thresholds

Parent epic: [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story

As a product owner, I want the decision thresholds capability, so that the Decision Engine determines the outcome.

## Description

The Decision Engine determines the outcome.

## Acceptance Criteria

- [ ] The Decision Thresholds behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: Score ≥ 70 → BUILD, Score 50-69 → DEFER, Score 30-49 → ARCHIVE.
- [ ] Dependencies and integration points with the rest of IDEA SCORING AND SELECTION SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] IDEA SCORING AND SELECTION SYSTEM
- Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
- Known technical details:
  - Score ≥ 70 → BUILD
  - Score 50-69 → DEFER
  - Score 30-49 → ARCHIVE
  - Score < 30 → REJECT

### Source Excerpt

The Decision Engine determines the outcome.

```
Score ≥ 70 → BUILD
Score 50-69 → DEFER
Score 30-49 → ARCHIVE
Score < 30 → REJECT
```

---

