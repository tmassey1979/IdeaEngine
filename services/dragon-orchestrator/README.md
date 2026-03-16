# Dragon Orchestrator

Minimum self-build orchestration service.

Current responsibilities:

- read the IdeaEngine backlog from GitHub or a local issue list
- choose the next open story in ascending issue order
- map that story to an available agent capability
- publish the resulting job into `.dragon/queues/dragon.jobs.ndjson`
- execute a single self-build step and queue review/test follow-ups
- consume queued jobs and track per-issue workflow state across developer, review, and test stages

Example:

```bash
node services/dragon-orchestrator/src/cli.js run-once --owner tmassey1979 --repo IdeaEngine
node services/dragon-orchestrator/src/cli.js execute-once --owner tmassey1979 --repo IdeaEngine
node services/dragon-orchestrator/src/cli.js cycle-once --owner tmassey1979 --repo IdeaEngine
```
