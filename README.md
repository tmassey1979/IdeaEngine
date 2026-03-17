# Dragon Idea Engine

Dragon Idea Engine is an autonomous software factory scaffold centered on a C# backend.

The supported backend path now lives under [`backend/`](/mnt/c/code/Playground/IdeaEngine/backend). The earlier Node-based runtime scaffold has been retired from the repo so the implementation surface matches the current direction.

Current implementation focus:

- C# self-build contracts and workflow state
- local queue persistence and bounded orchestration
- GitHub-backed backlog discovery and workflow sync
- recovery, quarantine, and validation flow
- regression coverage around changed-path canonicalization

See [codex/MASTER_CODEX.md](/mnt/c/code/Playground/IdeaEngine/codex/MASTER_CODEX.md) for the extracted product codex and [docs/ARCHITECTURE.md](/mnt/c/code/Playground/IdeaEngine/docs/ARCHITECTURE.md) for the current implementation shape.

Current C# backend entrypoint:

```bash
dotnet test backend/Dragon.Backend.slnx
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- cycle-once --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-issues --owner tmassey1979 --repo IdeaEngine --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-cycle-once --owner tmassey1979 --repo IdeaEngine --sync-github --root .
```
