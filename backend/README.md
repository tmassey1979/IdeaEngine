# Dragon Backend

This folder is the C# backend foundation for Dragon Idea Engine.

Current scope:

- shared contracts for backlog-driven self-build jobs
- backlog metadata loading from `planning/backlog.json`
- bounded developer-operation planning for C# orchestration
- local queue storage and workflow state for the bootstrap loop
- GitHub-backed story discovery and validated workflow sync through `gh`
- a small CLI that can print planned self-build jobs from backlog context and run one local self-build cycle

Useful commands:

```bash
dotnet test backend/Dragon.Backend.slnx
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- cycle-once --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- queue --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-issues --owner tmassey1979 --repo IdeaEngine --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- sync-workflow --owner tmassey1979 --repo IdeaEngine --issue 23 --root .
```
