# [Story] MANDATORY OPEN SOURCE POLICY: License Selection

Parent epic: [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`

## User Story

As a product owner, I want the license selection capability, so that default license for all generated projects: - **MIT License** – permissive, widely used - Optional alternatives (configurable per project):.

## Description

Default license for all generated projects:

- **MIT License** – permissive, widely used
- Optional alternatives (configurable per project):

## Acceptance Criteria

- [ ] The License Selection behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: Apache 2.0, GPL v3, BSD 3-Clause.
- [ ] Dependencies and integration points with the rest of MANDATORY OPEN SOURCE POLICY are documented.

## Dev Notes

- Parent epic: [Epic] MANDATORY OPEN SOURCE POLICY
- Source section: `codex/sections/09-mandatory-open-source-policy.md`
- Known technical details:
  - Apache 2.0
  - GPL v3
  - BSD 3-Clause
  - project type
  - target users

### Source Excerpt

Default license for all generated projects:

- **MIT License** – permissive, widely used
- Optional alternatives (configurable per project):

``` id="os-2"
Apache 2.0
GPL v3
BSD 3-Clause
```

The **License Agent** selects the appropriate license based on:

``` id="os-3"
project type
target users
compliance requirements
reuse considerations
```

---

