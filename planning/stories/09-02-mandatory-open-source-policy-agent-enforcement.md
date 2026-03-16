# [Story] MANDATORY OPEN SOURCE POLICY: Agent Enforcement

Parent epic: [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`

## User Story

As a product owner, I want the agent enforcement capability, so that all development agents must include a **license header** in every generated file.

## Description

All development agents must include a **license header** in every generated file.

Example MIT header:

## Acceptance Criteria

- [ ] The Agent Enforcement behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: /*, * Copyright (c) 2026 Dragon Idea Engine, * Licensed under the MIT License..
- [ ] Dependencies and integration points with the rest of MANDATORY OPEN SOURCE POLICY are documented.

## Dev Notes

- Parent epic: [Epic] MANDATORY OPEN SOURCE POLICY
- Source section: `codex/sections/09-mandatory-open-source-policy.md`
- Known technical details:
  - /*
  - * Copyright (c) 2026 Dragon Idea Engine
  - * Licensed under the MIT License.
  - */
  - Copyright (c) 2026 Dragon Idea Engine

### Source Excerpt

All development agents must include a **license header** in every generated file.

Example MIT header:

``` text
/*
 * Copyright (c) 2026 Dragon Idea Engine
 * Licensed under the MIT License.
 */
```

The system will reject or flag any generated code that lacks a valid open source license.

---

