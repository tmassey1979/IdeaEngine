# Dragon Idea Engine

Dragon Idea Engine is an autonomous software factory scaffold built around a plugin-driven agent runner.

Current implementation focus:

- root workspace structure
- plugin-based agent runtime
- starter agent plugins
- shared agent SDK
- job lifecycle, retry, dead-letter, and SDK utility foundations
- minimum self-build orchestration that can select the next open story and publish the next job

See [docs/MASTER_CODEX.md](/mnt/c/code/Playground/IdeaEngine/docs/MASTER_CODEX.md) for the extracted product codex and [docs/ARCHITECTURE.md](/mnt/c/code/Playground/IdeaEngine/docs/ARCHITECTURE.md) for the current implementation shape.

Current self-build entrypoint:

```bash
node services/dragon-orchestrator/src/cli.js run-once --owner tmassey1979 --repo IdeaEngine
node services/dragon-orchestrator/src/cli.js execute-once --owner tmassey1979 --repo IdeaEngine
```
