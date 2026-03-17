# Dragon Backend

This folder is the C# backend foundation for Dragon Idea Engine.

Current scope:

- shared contracts for backlog-driven self-build jobs
- API-first provider contracts for model-backed agent execution
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
- recovery-aware scheduling so follow-up recovery stories are seeded ahead of ordinary backlog stories
- source-issue linkage for recovery work so recovery stories carry explicit parent context
- workflow-state tracking for active recovery children so parent issues are held until linked recovery work is resolved
- GitHub heartbeat comments that keep one live in-progress status thread per story
- automatic GitHub label transitions for in-progress, quarantined, and completed states
- stage-aware heartbeat content that shows the current stage, when it last changed, whether it appears stalled, and the latest stage outcome
- a small CLI that can print planned self-build jobs from backlog context and run one local self-build cycle
- an OpenAI Responses API provider scaffold behind a provider abstraction for unattended agent execution

Useful commands:

```bash
dotnet test backend/Dragon.Backend.slnx
dotnet run --project backend/src/Dragon.Backend.Cli -- provider-describe
dotnet run --project backend/src/Dragon.Backend.Cli -- plan-from-backlog --title "[Story] Dragon Idea Engine Master Codex: Core System Principles" --number 22 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- cycle-once --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- run-until-idle --max-cycles 20 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- run-polling --max-passes 10 --idle-passes 2 --max-cycles 20 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- queue --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-issues --owner tmassey1979 --repo IdeaEngine --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-cycle-once --owner tmassey1979 --repo IdeaEngine --sync-github --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-run-until-idle --owner tmassey1979 --repo IdeaEngine --sync-github --max-cycles 20 --root .
GH_BIN=/home/temassey/.local/bin/gh dotnet run --project backend/src/Dragon.Backend.Cli -- github-run-polling --owner tmassey1979 --repo IdeaEngine --sync-github --max-passes 10 --idle-passes 2 --max-cycles 20 --root .
dotnet run --project backend/src/Dragon.Backend.Cli -- sync-workflow --owner tmassey1979 --repo IdeaEngine --issue 23 --root .
```

Provider direction:

- `IAgentModelProvider` is the backend abstraction boundary
- `OpenAiResponsesProvider` is the initial production-oriented provider target
- additional providers should be added later without changing role-based agent behavior

`--sync-github` is guarded: it only comments on and closes an issue after the workflow reaches `validated`, and the sync comment now includes recent execution IDs and changed-path trace data.
`run-until-idle` is bounded by `--max-cycles` and keeps cycling until the local queue is empty and there is no more schedulable backlog work in the current issue set.
`run-polling` is the next bare-minimum unattended step: it repeats `run-until-idle` across multiple passes and only stops after the loop has stayed idle for the configured number of polls.

Repeated failures are also guarded: when the same stage keeps failing across cycles, the issue is marked `quarantined` in `.dragon/state/issues.json`, the loop stops reseeding it automatically, and the GitHub issue gets a maintained remediation comment plus a `quarantined` label instead of being closed.

Long stalls are guarded too: before seeding new work, the loop now sweeps existing workflows and quarantines any story whose current stage has shown no progress for more than an hour.

The quarantine sync now keeps a dedicated remediation thread on the GitHub issue with the blocked stage, recent failure context, changed paths, and a short recovery checklist.
It also creates or reuses a `[Recovery]` GitHub story so stuck work comes with a trackable next step.
Those recovery stories now seed ahead of ordinary stories and carry explicit `recover_issue` job metadata through the loop.
They also carry `sourceIssueNumber` linkage so the loop can reason about the parent quarantined story explicitly.
That linkage is now persisted in workflow state too, which lets the loop skip reseeding parent issues with active recovery children and avoid auto-closing a validated parent while linked recovery work is still open.
When the last linked recovery child validates, the parent returns to active flow and GitHub heartbeat sync now calls out that recovery hold release explicitly.
The loop now also requeues that released parent once, so paused work resumes automatically instead of only becoming eligible again.
GitHub heartbeat sync now distinguishes that requeue event too, so the issue history shows not just that hold was released, but that the system actually resumed the parent work.
GitHub sync now also includes a compact recovery-chain summary so parent and child relationships are visible at a glance in heartbeat and remediation updates.
If a parent already has active recovery children, quarantine sync now reuses the most recent linked recovery issue instead of spawning overlapping recovery stories for the same parent.
Scheduling now follows that same rule: when multiple unresolved recovery stories exist for one parent, the loop prefers the most recent one and ignores the older overlap.
Queue consumption now follows it too: before consuming work, the loop removes superseded older recovery jobs so only the latest unresolved recovery path executes for that parent.
GitHub sync now follows it as well: when multiple active recovery children exist, older overlapping recovery issues are marked `superseded` and get a maintained comment pointing to the newer active recovery path.
GitHub backlog discovery now follows it too: superseded recovery issues are excluded from the live issue set the loop uses for scheduling.
If a recovery issue becomes active again or validates, GitHub sync now clears any stale `superseded` label so the visible state can recover cleanly.
Recovery scheduling is now more selective too: recovery stories are prioritized while they are still the active path for a quarantined parent, but ordinary backlog work regains priority once that recovery hold has been released.
When a parent has returned to active flow and no recovery child remains active, GitHub sync now retires leftover open recovery issues for that parent so the backlog stops carrying stale cleanup branches.
The parent heartbeat now lists any recovery issues retired during that sync, so the resume-and-cleanup path is visible from the parent issue as well.

While work is still active, the backend now upserts a single heartbeat comment on the GitHub issue instead of emitting a new comment every cycle.
That heartbeat now includes the current stage, when that stage was last observed, whether it appears stalled, and the latest recorded stage outcome.

GitHub labels now follow the workflow too: active stories keep `in-progress`, stalled stories add `stalled`, quarantined stories swap to `quarantined`, and validated stories remove the temporary workflow labels before closing.
