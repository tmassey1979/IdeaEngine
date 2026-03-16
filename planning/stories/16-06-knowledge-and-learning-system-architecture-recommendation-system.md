# [Story] KNOWLEDGE AND LEARNING SYSTEM: Architecture Recommendation System

Parent epic: [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`

## User Story

As a platform architect, I want the architecture recommendation system capability, so that when a new project is generated, the system consults previous architectures.

## Description

When a new project is generated, the system consults previous architectures.

Example decision flow:

## Acceptance Criteria

- [ ] The Architecture Recommendation System behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: New Idea Detected, ↓, Search Similar Projects.
- [ ] Dependencies and integration points with the rest of KNOWLEDGE AND LEARNING SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] KNOWLEDGE AND LEARNING SYSTEM
- Source section: `codex/sections/16-knowledge-and-learning-system.md`
- Known technical details:
  - New Idea Detected
  - ↓
  - Search Similar Projects
  - Retrieve Successful Architectures
  - Rank Architectures

### Source Excerpt

When a new project is generated, the system consults previous architectures.

Example decision flow:

```text
New Idea Detected
      ↓
Search Similar Projects
      ↓
Retrieve Successful Architectures
      ↓
Rank Architectures
      ↓
Recommend Best Architecture
```

This dramatically reduces design errors.

---

