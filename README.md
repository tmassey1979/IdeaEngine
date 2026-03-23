# Dragon Idea Engine

Dragon Idea Engine is an autonomous software factory scaffold centered on a C# backend.

The supported backend path now lives under [`backend/`](/mnt/c/code/Playground/IdeaEngine/backend). The earlier Node-based runtime scaffold has been retired from the repo so the implementation surface matches the current direction.

Current implementation focus:

- C# self-build contracts and workflow state
- local queue persistence and bounded orchestration
- API-first agent execution design with OpenAI as the initial provider target
- GitHub-backed backlog discovery and workflow sync
- recovery, quarantine, and validation flow
- regression coverage around changed-path canonicalization

See [codex/MASTER_CODEX.md](/mnt/c/code/Playground/IdeaEngine/codex/MASTER_CODEX.md) for the extracted product codex, [docs/ARCHITECTURE.md](/mnt/c/code/Playground/IdeaEngine/docs/ARCHITECTURE.md) for the current implementation shape, [docs/OPENAI_PROVIDER.md](/mnt/c/code/Playground/IdeaEngine/docs/OPENAI_PROVIDER.md) for the provider strategy, and [docs/AUTONOMY_ROADMAP.md](/mnt/c/code/Playground/IdeaEngine/docs/AUTONOMY_ROADMAP.md) for the unattended-operation roadmap.

Current C# backend entrypoint:

```bash
dotnet test backend/Dragon.Backend.slnx
npm run status:ui
dotnet run --project backend/src/Dragon.Backend.Cli -- provider-describe
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- cycle-once --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- run-polling --root . --max-passes 10 --idle-passes 2 --max-cycles 100
dotnet run --project backend/src/Dragon.Backend.Cli -- run-watch --root . --poll-seconds 30 --max-passes 10 --idle-passes 2 --max-cycles 100
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-issues --owner tmassey1979 --repo IdeaEngine --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-cycle-once --owner tmassey1979 --repo IdeaEngine --sync-github --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-run-polling --owner tmassey1979 --repo IdeaEngine --sync-github --max-passes 10 --idle-passes 2 --max-cycles 100 --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-run-watch --owner tmassey1979 --repo IdeaEngine --sync-github --poll-seconds 30 --max-passes 10 --idle-passes 2 --max-cycles 100 --root .
```

Dashboard status export:

```bash
npm run status:ui
```

That refreshes [ui/dragon-ui/sample-status.json](/mnt/c/code/Playground/IdeaEngine/ui/dragon-ui/sample-status.json) from the live backend `status` snapshot so the mock dashboard reflects current queue and workflow state without editing the sample file by hand.

To run the local self-build loop and refresh the dashboard snapshot in one step:

```bash
npm run run:ui
```

To keep polling for new work while still stopping after the loop has gone idle
for multiple passes:

```bash
npm run run:polling
```

To keep the local runner alive between passes with a controlled poll interval:

```bash
npm run run:watch
```

Docker stack:

```bash
cp .env.docker.example .env
npm run docker:up
```

That now boots:

- the backend worker container
- Postgres
- RabbitMQ
- a bundled observability stack

The backend status endpoint is exposed at `http://127.0.0.1:5078/status`, which
matches the live-first dashboard path.

For Raspberry Pi host bootstrap, env configuration, reboot-safe service install, updates, health checks, diagnostics, and backups, see [docs/PI_SETUP.md](/mnt/c/code/Playground/IdeaEngine/docs/PI_SETUP.md), [scripts/setup-pi.sh](/mnt/c/code/Playground/IdeaEngine/scripts/setup-pi.sh), [scripts/configure-pi-env.sh](/mnt/c/code/Playground/IdeaEngine/scripts/configure-pi-env.sh), [scripts/update-pi.sh](/mnt/c/code/Playground/IdeaEngine/scripts/update-pi.sh), [scripts/healthcheck-pi.sh](/mnt/c/code/Playground/IdeaEngine/scripts/healthcheck-pi.sh), [scripts/collect-pi-diagnostics.sh](/mnt/c/code/Playground/IdeaEngine/scripts/collect-pi-diagnostics.sh), [scripts/backup-pi.sh](/mnt/c/code/Playground/IdeaEngine/scripts/backup-pi.sh), and [scripts/restore-pi.sh](/mnt/c/code/Playground/IdeaEngine/scripts/restore-pi.sh).
