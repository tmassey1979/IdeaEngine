# Dragon Backend

This folder is the C# backend foundation for Dragon Idea Engine.

Current scope:

- shared contracts for backlog-driven self-build jobs
- backlog metadata loading from `planning/backlog.json`
- bounded developer-operation planning for C# orchestration
- a small CLI that can print planned self-build jobs from backlog context

Useful commands:

```bash
dotnet test backend/Dragon.Backend.slnx
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
```
