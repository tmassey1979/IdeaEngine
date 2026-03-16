# [Story] Dragon Idea Engine Master Codex Addendum: Credential Manager

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the credential manager capability, so that agents access credentials through SDK.

## Description

Agents access credentials through SDK.

Example:

## Acceptance Criteria

- [ ] The Credential Manager behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: context.credentials.get("github"), project credentials, global credentials.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - context.credentials.get("github")
  - project credentials
  - global credentials

### Source Excerpt

Agents access credentials through SDK.

Example:

```ts
context.credentials.get("github")
```

Credential resolution order:

```
project credentials
global credentials
```

---

