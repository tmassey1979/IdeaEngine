# Dragon Idea Engine

Dragon Idea Engine is an autonomous software factory scaffold built around a plugin-driven agent runner.

The backend direction is now C#. The existing Node-based services remain useful as a behavior prototype, but the durable backend path is being rebuilt under [`backend/`](/mnt/c/code/Playground/IdeaEngine/backend).

Current implementation focus:

- root workspace structure
- plugin-based agent runtime
- starter agent plugins
- shared agent SDK
- job lifecycle, retry, dead-letter, and SDK utility foundations
- minimum self-build orchestration that can select the next open story and publish the next job

See [docs/MASTER_CODEX.md](/mnt/c/code/Playground/IdeaEngine/docs/MASTER_CODEX.md) for the extracted product codex and [docs/ARCHITECTURE.md](/mnt/c/code/Playground/IdeaEngine/docs/ARCHITECTURE.md) for the current implementation shape.

Current C# backend entrypoint:

```bash
dotnet test backend/Dragon.Backend.slnx
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- cycle-once --root .
```

Current self-build entrypoint:

```bash
node services/dragon-orchestrator/src/cli.js cycle-once --owner tmassey1979 --repo IdeaEngine
node services/dragon-orchestrator/src/cli.js queue
node services/dragon-orchestrator/src/cli.js consume-next
```
