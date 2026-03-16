# [Story] Dragon Idea Engine Master Codex Addendum: Example Agent Using SDK

Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story

As a platform architect, I want the example agent using sdk capability, so that the dragon idea engine master codex addendum epic can be completed predictably.

## Description

Implement the Example Agent Using SDK portion of Dragon Idea Engine — Master Codex Addendum.

## Acceptance Criteria

- [ ] The Example Agent Using SDK behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: export const developerAgent: DragonAgent = {, name: "developer",, description: "implements repository issues",.
- [ ] Dependencies and integration points with the rest of Dragon Idea Engine Master Codex Addendum are documented.

## Dev Notes

- Parent epic: [Epic] Dragon Idea Engine Master Codex Addendum
- Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
- Known technical details:
  - export const developerAgent: DragonAgent = {
  - name: "developer",
  - description: "implements repository issues",
  - version: "1.0",
  - async execute(context) {

### Source Excerpt

```ts
export const developerAgent: DragonAgent = {

  name: "developer",

  description: "implements repository issues",

  version: "1.0",

  async execute(context) {

    const repo = await context.workspace.cloneRepo(context.repo)

    await context.git.createBranch("feature-" + context.job.issue)

    // AI coding step

    await context.git.commit("feat: implement issue")

    await context.git.push()

    return {
      success: true
    }

  }

}
```

---

