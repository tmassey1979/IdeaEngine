# Dragon Backend

This folder is the C# backend foundation for Dragon Idea Engine.

Current scope:

- shared contracts for backlog-driven self-build jobs
- backlog metadata loading from `planning/backlog.json`
- bounded developer-operation planning for C# orchestration
- local queue storage and workflow state for the bootstrap loop
- GitHub-backed story discovery and validated workflow sync through `gh`
- review/test stages that now validate changed files and execute real project test commands
- per-issue execution records under `.dragon/runs/` that feed GitHub sync comments
- repeated-failure quarantine logic that marks stuck stories and skips reseeding them
- long-stall quarantine logic that escalates workflows with no stage movement for too long
- GitHub quarantine updates that maintain a remediation trail and label stuck stories without closing them
- recovery-story creation for quarantined work so blocked issues spawn narrower follow-up backlog items
- GitHub heartbeat comments that keep one live in-progress status thread per story
- automatic GitHub label transitions for in-progress, quarantined, and completed states
- stage-aware heartbeat content that shows the current stage, when it last changed, whether it appears stalled, and the latest stage outcome
- a small CLI that can print planned self-build jobs from backlog context and run one local self-build cycle

Useful commands:

```bash
dotnet test backend/Dragon.Backend.slnx
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- cycle-once --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- queue --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-issues --owner tmassey1979 --repo IdeaEngine --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-cycle-once --owner tmassey1979 --repo IdeaEngine --sync-github --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- sync-workflow --owner tmassey1979 --repo IdeaEngine --issue 23 --root .
```

`--sync-github` is guarded: it only comments on and closes an issue after the workflow reaches `validated`, and the sync comment now includes recent execution IDs and changed-path trace data.

Repeated failures are also guarded: when the same stage keeps failing across cycles, the issue is marked `quarantined` in `.dragon/state/issues.json`, the loop stops reseeding it automatically, and the GitHub issue gets a maintained remediation comment plus a `quarantined` label instead of being closed.

Long stalls are guarded too: before seeding new work, the loop now sweeps existing workflows and quarantines any story whose current stage has shown no progress for more than an hour.

The quarantine sync now keeps a dedicated remediation thread on the GitHub issue with the blocked stage, recent failure context, changed paths, and a short recovery checklist.
It also creates or reuses a `[Recovery]` GitHub story so stuck work comes with a trackable next step.

While work is still active, the backend now upserts a single heartbeat comment on the GitHub issue instead of emitting a new comment every cycle.
That heartbeat now includes the current stage, when that stage was last observed, whether it appears stalled, and the latest recorded stage outcome.

GitHub labels now follow the workflow too: active stories keep `in-progress`, stalled stories add `stalled`, quarantined stories swap to `quarantined`, and validated stories remove the temporary workflow labels before closing.
