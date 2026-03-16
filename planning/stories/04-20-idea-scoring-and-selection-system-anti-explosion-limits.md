# [Story] IDEA SCORING AND SELECTION SYSTEM: Anti-Explosion Limits

Parent epic: [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story

As a product owner, I want the anti-explosion limits capability, so that to prevent runaway project creation the system enforces limits.

## Description

To prevent runaway project creation the system enforces limits.

Example policies:

## Acceptance Criteria

- [ ] The Anti-Explosion Limits behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: maxActiveProjects = 20, maxNewProjectsPerWeek = 3, maxConcurrentIdeaEvaluations = 10.
- [ ] Dependencies and integration points with the rest of IDEA SCORING AND SELECTION SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] IDEA SCORING AND SELECTION SYSTEM
- Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
- Known technical details:
  - maxActiveProjects = 20
  - maxNewProjectsPerWeek = 3
  - maxConcurrentIdeaEvaluations = 10

### Source Excerpt

To prevent runaway project creation the system enforces limits.

Example policies:

```
maxActiveProjects = 20
maxNewProjectsPerWeek = 3
maxConcurrentIdeaEvaluations = 10
```

---

