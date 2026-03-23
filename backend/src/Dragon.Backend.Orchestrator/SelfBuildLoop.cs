using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class SelfBuildLoop
{
    private const int MaxTransientProviderRequeuesPerJob = 2;
    private static readonly TimeSpan LongDelayedRetryAttentionThreshold = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions StatusSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly QueueStore queueStore;
    private readonly WorkflowStateStore workflowStateStore;
    private readonly ExecutionRecordStore executionRecordStore;
    private readonly LocalJobExecutor jobExecutor;
    private readonly GithubIssueService githubIssueService;
    private readonly Func<DateTimeOffset> nowProvider;

    public SelfBuildLoop(
        string rootDirectory,
        string queueName = "dragon.jobs",
        GithubIssueService? githubIssueService = null,
        LocalJobExecutor? jobExecutor = null,
        Func<string, string?>? environmentReader = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        RootDirectory = rootDirectory;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        queueStore = new QueueStore(rootDirectory, queueName, this.nowProvider);
        workflowStateStore = new WorkflowStateStore(rootDirectory);
        executionRecordStore = new ExecutionRecordStore(rootDirectory);
        this.jobExecutor = jobExecutor ?? LocalJobExecutor.CreateDefault(environmentReader ?? Environment.GetEnvironmentVariable);
        this.githubIssueService = githubIssueService ?? new GithubIssueService();
    }

    public string RootDirectory { get; }

    private string GithubSyncStatusPath => Path.Combine(RootDirectory, ".dragon", "status", "github-sync-status.json");
    private string GithubReplayStatusPath => Path.Combine(RootDirectory, ".dragon", "status", "github-replay-status.json");
    private string PendingGithubSyncPath => Path.Combine(RootDirectory, ".dragon", "status", "pending-github-sync.json");

    public IReadOnlyList<SelfBuildJob> ReadQueue() => queueStore.ReadAll();

    public StatusSnapshot ReadStatus(
        string lastCommand = "status",
        string workerMode = "status",
        string workerState = "snapshot",
        string? workerCompletionReason = null,
        DateTimeOffset? nextPollAt = null,
        int? pollIntervalSeconds = null,
        int idleStreak = 0,
        int idleTarget = 0,
        int? idlePassesRemaining = null,
        int? passBudgetRemaining = null,
        LatestPassSummary? latestPass = null,
        int? currentPassNumber = null,
        int? maxPasses = null,
        string? workerActivity = null)
    {
        var now = nowProvider();
        var queuedJobs = queueStore.ReadAll();
        var readyLeadJob = queueStore.Peek();
        var leadJob = readyLeadJob ?? queueStore.PeekAny();
        var workflows = workflowStateStore.ReadAll();
        var issues = workflows.Values
            .OrderBy(item => item.IssueNumber)
            .Select(workflow =>
            {
                var latestExecution = executionRecordStore.Read(workflow.IssueNumber)
                    .OrderByDescending(record => record.RecordedAt)
                    .FirstOrDefault();

                return new IssueStatusSnapshot(
                    workflow.IssueNumber,
                    workflow.IssueTitle,
                    workflow.OverallStatus,
                    InferCurrentStage(workflow),
                    queuedJobs.Count(job => job.Issue == workflow.IssueNumber),
                    workflow.Note,
                    latestExecution?.Summary,
                    latestExecution?.Notes,
                    latestExecution?.RecordedAt
                );
            })
            .ToArray();

        var latestGithubSync = ReadLatestGithubSync();
        var latestGithubReplay = ReadLatestGithubReplay();
        var pendingGithubSync = AnnotatePendingGithubSync(ReadPendingGithubSync(), pollIntervalSeconds);
        var baseLeadQuarantine = BuildLeadQuarantine(workflows, queuedJobs, pendingGithubSync);
        var rollup = BuildStatusRollup(workflows, queuedJobs);
        var latestActivity = BuildLatestActivity(issues);
        var pendingGithubSyncSummary = BuildPendingGithubSyncSummary(pendingGithubSync, now);
        var pendingGithubSyncNextRetryAt = pendingGithubSync
            .Select(item => item.NextRetryAt)
            .Where(value => value is not null)
            .Min();
        var pendingGithubSyncRetryState = BuildPendingGithubSyncRetryState(pendingGithubSyncNextRetryAt, now);
        var pendingGithubSyncRetryOverdueMinutes = pendingGithubSyncNextRetryAt is null
            ? 0
            : Math.Max(0, (int)Math.Floor((now - pendingGithubSyncNextRetryAt.Value).TotalMinutes));
        var nextDelayedRetryAt = queuedJobs
            .Select(ReadRetryNotBeforeUtc)
            .Where(value => value is not null)
            .Min();
        var replayPriorityReason = BuildReplayPriorityReason(nextDelayedRetryAt, pendingGithubSyncRetryState, pendingGithubSyncRetryOverdueMinutes);
        var replayPrioritySummary = BuildReplayPrioritySummary(replayPriorityReason);
        var nextWakeReason = DeriveNextWakeReason(nextPollAt, nextDelayedRetryAt);
        var delayedRetryUrgency = DeriveDelayedRetryUrgency(nextDelayedRetryAt, now);
        var delayedRetrySummary = nextDelayedRetryAt is null
            ? null
            : $"Next delayed provider retry unlocks at {nextDelayedRetryAt.Value:O}.";
        var waitSignal = BuildWaitSignal(replayPrioritySummary, nextWakeReason);
        var leadQuarantine = AnnotateLeadQuarantine(baseLeadQuarantine, pendingGithubSync);
        var leadJobSnapshot = leadJob is null
            ? null
            : new LeadJobSnapshot(
                leadJob.Issue,
                leadJob.Payload.Title,
                leadJob.Agent,
                leadJob.Action,
                leadJob.Metadata.GetValueOrDefault("targetArtifact"),
                leadJob.Metadata.GetValueOrDefault("targetOutcome"),
                leadJob.Metadata.GetValueOrDefault("requestedPriority"),
                string.Equals(leadJob.Metadata.GetValueOrDefault("requestedBlocking"), "true", StringComparison.OrdinalIgnoreCase),
                leadJob.Metadata.GetValueOrDefault("workType"),
                ReadRetryNotBeforeUtc(leadJob),
                readyLeadJob is null);
        var interventionTarget = AnnotateInterventionTarget(BuildInterventionTarget(leadQuarantine, leadJobSnapshot, pendingGithubSync));
        var health = DeriveStatusHealth(issues, baseLeadQuarantine, latestGithubReplay, interventionTarget, nextDelayedRetryAt, pendingGithubSyncNextRetryAt, now);
        var attentionSummary = BuildAttentionSummary(queuedJobs.Count, issues, health, baseLeadQuarantine, latestGithubReplay, interventionTarget, nextDelayedRetryAt, pendingGithubSyncNextRetryAt, now);
        var recentLoopSignal = BuildRecentLoopSignal(queuedJobs.Count, health, latestActivity, baseLeadQuarantine, latestGithubReplay, interventionTarget, pendingGithubSyncRetryOverdueMinutes);
        var interventionEscalationNote = BuildInterventionEscalationNote(interventionTarget);
        var effectiveWorkerActivity = string.IsNullOrWhiteSpace(workerActivity)
            ? BuildDefaultWorkerActivity(workerState, interventionTarget, leadJobSnapshot, latestGithubReplay, pendingGithubSyncRetryOverdueMinutes)
            : workerActivity;

        return new StatusSnapshot(
            now,
            "status",
            lastCommand,
            workerMode,
            workerState,
            workerCompletionReason,
            nextPollAt,
            pollIntervalSeconds,
            idleStreak,
            idleTarget,
            idlePassesRemaining,
            passBudgetRemaining,
            currentPassNumber,
            maxPasses,
            health,
            attentionSummary,
            rollup,
            leadJobSnapshot,
            leadQuarantine,
            latestActivity,
            recentLoopSignal,
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            queuedJobs.Count,
            issues,
            latestPass,
            latestGithubSync,
            latestGithubReplay,
            pendingGithubSync.Count,
            pendingGithubSync,
            effectiveWorkerActivity,
            pendingGithubSyncSummary,
            interventionTarget,
            interventionEscalationNote,
            0,
            nextWakeReason,
            nextDelayedRetryAt,
            delayedRetryUrgency,
            delayedRetrySummary,
            waitSignal,
            pendingGithubSyncNextRetryAt,
            pendingGithubSyncRetryState,
            pendingGithubSyncRetryOverdueMinutes,
            replayPriorityReason,
            replayPrioritySummary
        );
    }

    public StatusSnapshot WriteStatus(
        string outputPath,
        string lastCommand = "status",
        string workerMode = "status",
        string workerState = "snapshot",
        string? workerCompletionReason = null,
        DateTimeOffset? nextPollAt = null,
        int? pollIntervalSeconds = null,
        int idleStreak = 0,
        int idleTarget = 0,
        int? idlePassesRemaining = null,
        int? passBudgetRemaining = null,
        LatestPassSummary? latestPass = null,
        int? currentPassNumber = null,
        int? maxPasses = null,
        string? workerActivity = null)
    {
        var snapshot = ReadStatus(lastCommand, workerMode, workerState, workerCompletionReason, nextPollAt, pollIntervalSeconds, idleStreak, idleTarget, idlePassesRemaining, passBudgetRemaining, latestPass, currentPassNumber, maxPasses, workerActivity);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WriteTextAtomically(outputPath, JsonSerializer.Serialize(snapshot, StatusSerializerOptions));
        return snapshot;
    }

    public SelfBuildJob? EnqueuePersistentInterventionEscalationFollowUp(
        StatusSnapshot snapshot,
        int minimumCriticalStreak = 3,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine")
    {
        var signature = snapshot.InterventionTarget is null
            ? null
            : BuildInterventionTargetSignature(snapshot.InterventionTarget);
        PruneStaleInterventionEscalationFollowUps(
            snapshot.InterventionTarget,
            snapshot.InterventionEscalationStreak,
            minimumCriticalStreak,
            signature);

        if (snapshot.InterventionTarget is null ||
            !string.Equals(snapshot.InterventionTarget.Escalation, "critical", StringComparison.OrdinalIgnoreCase) ||
            snapshot.InterventionEscalationStreak < minimumCriticalStreak)
        {
            return null;
        }

        var issueNumber = snapshot.InterventionTarget.IssueNumber ??
            snapshot.InterventionTarget.RecoveryIssueNumber ??
            snapshot.InterventionTarget.PendingGithubSyncIssueNumber;
        if (issueNumber is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return null;
        }

        if (queueStore.ReadAll().Any(job =>
                string.Equals(job.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(job.Metadata.GetValueOrDefault("interventionEscalation"), "true", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(job.Metadata.GetValueOrDefault("interventionSignature"), signature, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (HasAcknowledgedInterventionEscalation(issueNumber.Value, signature))
        {
            return null;
        }

        var workflowTitle = workflowStateStore.ReadAll().TryGetValue(issueNumber.Value, out var workflow)
            ? workflow.IssueTitle
            : snapshot.InterventionTarget.Summary;
        var targetArtifact = snapshot.InterventionTarget.TargetArtifact;
        var targetOutcome = string.IsNullOrWhiteSpace(snapshot.InterventionTarget.TargetOutcome)
            ? "Summarize the persistent critical intervention target and the next operator action."
            : snapshot.InterventionTarget.TargetOutcome;
        var agent = InferOperatorSummaryAgent(targetArtifact ?? string.Empty);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["requestedBy"] = "system",
            ["source"] = "dragon-orchestrator-dotnet",
            ["requestedPriority"] = "high",
            ["requestedReason"] = "Persistent critical intervention target needs explicit operator summary.",
            ["interventionEscalation"] = "true",
            ["interventionSignature"] = signature!,
            ["interventionKind"] = snapshot.InterventionTarget.Kind,
            ["interventionEscalationLevel"] = snapshot.InterventionTarget.Escalation ?? "critical",
            ["interventionEscalationStreak"] = snapshot.InterventionEscalationStreak.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["workType"] = "operator-escalation"
        };

        if (!string.IsNullOrWhiteSpace(targetArtifact))
        {
            metadata["targetArtifact"] = targetArtifact;
        }

        if (!string.IsNullOrWhiteSpace(targetOutcome))
        {
            metadata["targetOutcome"] = targetOutcome;
        }

        if (snapshot.InterventionTarget.ObservedAt is not null)
        {
            metadata["interventionObservedAt"] = snapshot.InterventionTarget.ObservedAt.Value.ToString("O");
        }

        var job = new SelfBuildJob(
            agent,
            "summarize_issue",
            repo,
            project,
            issueNumber.Value,
            new SelfBuildJobPayload(
                workflowTitle,
                ["story"],
                workflowTitle,
                null,
                null),
            metadata);

        queueStore.Enqueue(job);
        return job;
    }

    private void PruneStaleInterventionEscalationFollowUps(
        InterventionTargetSnapshot? interventionTarget,
        int interventionEscalationStreak,
        int minimumCriticalStreak,
        string? currentSignature)
    {
        var keepCurrentCriticalTarget = interventionTarget is not null &&
            string.Equals(interventionTarget.Escalation, "critical", StringComparison.OrdinalIgnoreCase) &&
            interventionEscalationStreak >= minimumCriticalStreak &&
            !string.IsNullOrWhiteSpace(currentSignature);

        queueStore.RemoveAll(job =>
        {
            if (!string.Equals(job.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(job.Metadata.GetValueOrDefault("interventionEscalation"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!keepCurrentCriticalTarget)
            {
                return true;
            }

            return !string.Equals(job.Metadata.GetValueOrDefault("interventionSignature"), currentSignature, StringComparison.OrdinalIgnoreCase);
        });
    }

    private bool HasAcknowledgedInterventionEscalation(int issueNumber, string signature) =>
        executionRecordStore.Read(issueNumber).Any(record =>
            string.Equals(record.JobAction, "summarize_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Status, "success", StringComparison.OrdinalIgnoreCase) &&
            record.Notes.Contains($"Intervention escalation acknowledged: {signature}.", StringComparison.Ordinal));

    private InterventionTargetSnapshot AnnotateInterventionTarget(InterventionTargetSnapshot interventionTarget)
    {
        var signature = BuildInterventionTargetSignature(interventionTarget);
        var issueNumber = interventionTarget.IssueNumber ??
            interventionTarget.RecoveryIssueNumber ??
            interventionTarget.PendingGithubSyncIssueNumber;
        if (issueNumber is null || string.IsNullOrWhiteSpace(signature))
        {
            return interventionTarget;
        }

        return interventionTarget with
        {
            Acknowledged = HasAcknowledgedInterventionEscalation(issueNumber.Value, signature)
        };
    }

    private static void WriteTextAtomically(string outputPath, string contents)
    {
        var tempPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, contents);

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.Move(tempPath, outputPath);
    }

    public IReadOnlyList<GithubIssue> LoadGithubIssues(string owner, string repo) =>
        githubIssueService.ListStoryIssues(owner, repo, RootDirectory);

    public QuarantineReleaseResult ReleaseQuarantinedIssues(
        IReadOnlyList<GithubIssue> issues,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        string? githubOwner = null,
        bool syncValidatedWorkflows = false)
    {
        var released = new List<ReleasedQuarantineIssue>();
        var queuedIssueNumbers = queueStore.ReadAll().Select(job => job.Issue).ToHashSet();

        foreach (var workflow in workflowStateStore.ReadAll().Values.OrderBy(item => item.IssueNumber))
        {
            if (!string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (workflow.ActiveRecoveryIssueNumbers?.Any() == true)
            {
                continue;
            }

            if (queuedIssueNumbers.Contains(workflow.IssueNumber))
            {
                continue;
            }

            var issue = issues.FirstOrDefault(candidate => candidate.Number == workflow.IssueNumber);
            if (issue is null)
            {
                continue;
            }

            var retryAgent = FailurePolicy.InferCurrentStage(workflow);
            if (string.Equals(retryAgent, "complete", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(retryAgent, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                retryAgent = RecommendAgent(issue);
            }

            var changedPaths = retryAgent.Equals("review", StringComparison.OrdinalIgnoreCase)
                ? ReadLatestSuccessfulChangedPaths(workflow.IssueNumber)
                : [];

            var updatedWorkflow = workflowStateStore.ReleaseQuarantineForRetry(
                workflow.IssueNumber,
                $"Released from quarantine for retry at {retryAgent} after environment recovery.");
            var job = SelfBuildJobFactory.CreateRetry(issue, retryAgent, repo, project, changedPaths);
            queueStore.Enqueue(job);
            queuedIssueNumbers.Add(workflow.IssueNumber);
            var githubSync = TrySyncWorkflow(githubOwner, repo, updatedWorkflow, syncValidatedWorkflows);

            released.Add(new ReleasedQuarantineIssue(
                workflow.IssueNumber,
                workflow.IssueTitle,
                retryAgent,
                job.Action,
                changedPaths,
                githubSync));
        }

        return new QuarantineReleaseResult(released);
    }

    public QuarantineReleaseResult ReleaseQuarantinedIssuesFromGithub(
        string owner,
        string repo,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false)
    {
        var issues = LoadGithubIssues(owner, repo);
        return ReleaseQuarantinedIssues(issues, repo, project, owner, syncValidatedWorkflows);
    }

    public SelfBuildJob SeedNext(IReadOnlyList<GithubIssue> issues, string repo = "IdeaEngine", string project = "DragonIdeaEngine")
    {
        var nextIssue = SelectNextIssue(issues);

        var agent = RecommendAgent(nextIssue);
        var job = SelfBuildJobFactory.Create(nextIssue, agent, repo, project);
        queueStore.Enqueue(job);
        return job;
    }

    public CycleResult CycleOnce(
        IReadOnlyList<GithubIssue> issues,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        string? githubOwner = null,
        bool syncValidatedWorkflows = false)
    {
        var resumedParent = ResumeReleasedParent(issues, repo, project);
        if (resumedParent is not null)
        {
            return resumedParent;
        }

        var stalledWorkflow = SweepStalledWorkflow(repo, githubOwner, syncValidatedWorkflows);
        if (stalledWorkflow is not null)
        {
            return stalledWorkflow;
        }

        var competingImplementationArtifacts = GetCompetingImplementationArtifacts();
        RemoveSupersededRecoveryJobs(issues);
        RemoveSupersededSummaryJobs();
        RemoveSupersededImplementationJobs();

        if (!queueStore.HasReadyJobs())
        {
            var prioritizedReplay = TryPrioritizeReadyGithubReplay(githubOwner, repo, syncValidatedWorkflows);
            if (prioritizedReplay is not null)
            {
                return prioritizedReplay;
            }

            if (HasSchedulableWork(issues))
            {
                var seeded = SeedNext(issues, repo, project);
                return new CycleResult("seed", seeded, null, []);
            }

            return new CycleResult("waiting", null, null, []);
        }

        var readyLeadJob = queueStore.Peek();
        if (readyLeadJob is not null)
        {
            var prioritizedReplay = TryPrioritizeReadyGithubReplayAheadOfOrdinaryWork(readyLeadJob, githubOwner, repo, syncValidatedWorkflows);
            if (prioritizedReplay is not null)
            {
                return prioritizedReplay;
            }
        }

        var job = queueStore.Dequeue()!;
        var execution = jobExecutor.Execute(RootDirectory, job);
        var workflow = workflowStateStore.Update(job, execution);
        var followUps = PublishFollowUps(job, execution, competingImplementationArtifacts);
        if (TryRequeueTransientProviderFailure(job, execution, ref workflow))
        {
            var githubRetrySync = TrySyncWorkflow(githubOwner, repo, workflow, syncValidatedWorkflows);
            return new CycleResult("retry", job, execution, followUps, workflow, githubRetrySync);
        }

        var executionRecord = executionRecordStore.Append(job, execution, followUps);
        var failureDisposition = ApplyFailurePolicy(job.Issue, workflow);
        if (failureDisposition?.Quarantined == true)
        {
            workflow = workflowStateStore.ReadAll()[job.Issue];
        }
        var githubSync = TrySyncWorkflow(githubOwner, repo, workflow, syncValidatedWorkflows);

        return new CycleResult("consume", job, execution, followUps, workflow, githubSync, executionRecord, failureDisposition);
    }

    private CycleResult? TryPrioritizeReadyGithubReplay(string? githubOwner, string repo, bool syncValidatedWorkflows)
    {
        if (!syncValidatedWorkflows || string.IsNullOrWhiteSpace(githubOwner))
        {
            return null;
        }

        if (ShouldDeferGithubReplayForProviderBackoff())
        {
            return null;
        }

        var now = nowProvider();
        var hasReadyPendingReplay = ReadPendingGithubSync().Any(item =>
            item.NextRetryAt is null || item.NextRetryAt.Value <= now);
        if (!hasReadyPendingReplay)
        {
            return null;
        }

        ReplayPendingGithubSyncs(githubOwner, repo);
        var latestReplay = ReadLatestGithubReplay();
        var replaySummary = latestReplay?.Summary ?? "Replayed pending GitHub updates before resuming queued work.";
        return new CycleResult(
            "replay",
            null,
            null,
            [],
            GithubSync: new GithubSyncResult(
                latestReplay?.AttemptedCount > 0,
                latestReplay?.UpdatedCount > 0,
                replaySummary));
    }

    private CycleResult? TryPrioritizeReadyGithubReplayAheadOfOrdinaryWork(
        SelfBuildJob readyLeadJob,
        string? githubOwner,
        string repo,
        bool syncValidatedWorkflows)
    {
        if (!IsOrdinaryWorkJob(readyLeadJob))
        {
            return null;
        }

        if (!HasOverdueReadyPendingGithubSync())
        {
            return null;
        }

        return TryPrioritizeReadyGithubReplay(githubOwner, repo, syncValidatedWorkflows);
    }

    private bool HasOverdueReadyPendingGithubSync()
    {
        var now = nowProvider();
        return ReadPendingGithubSync().Any(item =>
            item.NextRetryAt is not null &&
            item.NextRetryAt.Value <= now &&
            now - item.NextRetryAt.Value >= LongDelayedRetryAttentionThreshold);
    }

    private static bool IsOrdinaryWorkJob(SelfBuildJob job)
    {
        var workType = job.Metadata.GetValueOrDefault("workType");
        if (string.Equals(workType, "recovery", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workType, "operator-escalation", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public CycleResult CycleOnceFromGithub(
        string owner,
        string repo,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false)
    {
        var issues = LoadGithubIssues(owner, repo);
        return CycleOnce(issues, repo, project, owner, syncValidatedWorkflows);
    }

    public RunUntilIdleResult RunUntilIdle(
        IReadOnlyList<GithubIssue> issues,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        string? githubOwner = null,
        bool syncValidatedWorkflows = false,
        int maxCycles = 100)
    {
        var cycles = new List<CycleResult>();
        for (var index = 0; index < maxCycles; index += 1)
        {
            if (!queueStore.HasReadyJobs() && !HasSchedulableWork(issues))
            {
                return new RunUntilIdleResult(cycles, !queueStore.HasAnyJobs(), false);
            }

            cycles.Add(CycleOnce(issues, repo, project, githubOwner, syncValidatedWorkflows));
        }

        return new RunUntilIdleResult(cycles, false, true);
    }

    public RunUntilIdleResult RunUntilIdleFromGithub(
        string owner,
        string repo,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false,
        int maxCycles = 100)
    {
        var cycles = new List<CycleResult>();
        for (var index = 0; index < maxCycles; index += 1)
        {
            var issues = LoadGithubIssues(owner, repo);
            if (!queueStore.HasReadyJobs() && !HasSchedulableWork(issues))
            {
                return new RunUntilIdleResult(cycles, !queueStore.HasAnyJobs(), false);
            }

            cycles.Add(CycleOnce(issues, repo, project, owner, syncValidatedWorkflows));
        }

        return new RunUntilIdleResult(cycles, false, true);
    }

    public PollingRunResult RunPolling(
        IReadOnlyList<GithubIssue> issues,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        int maxPasses = 10,
        int idlePassesBeforeStop = 2,
        int maxCyclesPerPass = 100,
        Action<int, RunUntilIdleResult>? passCompleted = null)
    {
        var passes = new List<RunUntilIdleResult>();
        var requiredIdlePasses = Math.Max(1, idlePassesBeforeStop);
        var consecutiveIdlePasses = 0;

        for (var index = 0; index < maxPasses; index += 1)
        {
            var pass = RunUntilIdle(issues, repo, project, maxCycles: maxCyclesPerPass);
            passes.Add(pass);
            passCompleted?.Invoke(index + 1, pass);

            consecutiveIdlePasses = pass.ReachedIdle
                ? consecutiveIdlePasses + 1
                : 0;

            if (consecutiveIdlePasses >= requiredIdlePasses)
            {
                return new PollingRunResult(passes, consecutiveIdlePasses, true, false);
            }
        }

        return new PollingRunResult(passes, consecutiveIdlePasses, false, true);
    }

    public PollingRunResult RunWatching(
        IReadOnlyList<GithubIssue> issues,
        TimeSpan pollInterval,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        int maxPasses = 10,
        int idlePassesBeforeStop = 2,
        int maxCyclesPerPass = 100,
        Action<TimeSpan>? delayAction = null,
        Action<int, RunUntilIdleResult>? passCompleted = null)
    {
        var passes = new List<RunUntilIdleResult>();
        var requiredIdlePasses = Math.Max(1, idlePassesBeforeStop);
        var consecutiveIdlePasses = 0;
        var pause = delayAction ?? (_ => { });

        for (var index = 0; index < maxPasses; index += 1)
        {
            var pass = RunUntilIdle(issues, repo, project, maxCycles: maxCyclesPerPass);
            passes.Add(pass);
            passCompleted?.Invoke(index + 1, pass);

            consecutiveIdlePasses = pass.ReachedIdle
                ? consecutiveIdlePasses + 1
                : 0;

            if (consecutiveIdlePasses >= requiredIdlePasses)
            {
                return new PollingRunResult(passes, consecutiveIdlePasses, true, false);
            }

            if (index + 1 < maxPasses)
            {
                pause(DetermineWatchDelay(pollInterval, capToPollInterval: false));
            }
        }

        return new PollingRunResult(passes, consecutiveIdlePasses, false, true);
    }

    public TimeSpan GetWatchDelay(TimeSpan pollInterval, bool capToPollInterval = false) =>
        DetermineWatchDelay(pollInterval, capToPollInterval);

    public PollingRunResult RunPollingFromGithub(
        string owner,
        string repo,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false,
        int maxPasses = 10,
        int idlePassesBeforeStop = 2,
        int maxCyclesPerPass = 100,
        Action<int, RunUntilIdleResult, LatestGithubReplaySnapshot?>? passCompleted = null)
    {
        var passes = new List<RunUntilIdleResult>();
        var requiredIdlePasses = Math.Max(1, idlePassesBeforeStop);
        var consecutiveIdlePasses = 0;

        for (var index = 0; index < maxPasses; index += 1)
        {
            LatestGithubReplaySnapshot? latestReplay = null;
            if (syncValidatedWorkflows)
            {
                ReplayPendingGithubSyncs(owner, repo);
                latestReplay = ReadLatestGithubReplay();
            }

            var pass = RunUntilIdleFromGithub(
                owner,
                repo,
                project,
                syncValidatedWorkflows,
                maxCyclesPerPass);

            passes.Add(pass);
            passCompleted?.Invoke(index + 1, pass, latestReplay);

            consecutiveIdlePasses = pass.ReachedIdle && !ReplayCountsAsWork(latestReplay)
                ? consecutiveIdlePasses + 1
                : 0;

            if (consecutiveIdlePasses >= requiredIdlePasses)
            {
                return new PollingRunResult(passes, consecutiveIdlePasses, true, false);
            }
        }

        return new PollingRunResult(passes, consecutiveIdlePasses, false, true);
    }

    public PollingRunResult RunWatchingFromGithub(
        string owner,
        string repo,
        TimeSpan pollInterval,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false,
        int maxPasses = 10,
        int idlePassesBeforeStop = 2,
        int maxCyclesPerPass = 100,
        Action<TimeSpan>? delayAction = null,
        Action<int, RunUntilIdleResult, LatestGithubReplaySnapshot?>? passCompleted = null)
    {
        var passes = new List<RunUntilIdleResult>();
        var requiredIdlePasses = Math.Max(1, idlePassesBeforeStop);
        var consecutiveIdlePasses = 0;
        var pause = delayAction ?? (_ => { });

        for (var index = 0; index < maxPasses; index += 1)
        {
            LatestGithubReplaySnapshot? latestReplay = null;
            if (syncValidatedWorkflows)
            {
                ReplayPendingGithubSyncs(owner, repo);
                latestReplay = ReadLatestGithubReplay();
            }

            var pass = RunUntilIdleFromGithub(
                owner,
                repo,
                project,
                syncValidatedWorkflows,
                maxCyclesPerPass);

            passes.Add(pass);
            passCompleted?.Invoke(index + 1, pass, latestReplay);

            consecutiveIdlePasses = pass.ReachedIdle && !ReplayCountsAsWork(latestReplay)
                ? consecutiveIdlePasses + 1
                : 0;

            if (consecutiveIdlePasses >= requiredIdlePasses)
            {
                return new PollingRunResult(passes, consecutiveIdlePasses, true, false);
            }

            if (index + 1 < maxPasses)
            {
                pause(DetermineWatchDelay(pollInterval, capToPollInterval: true));
            }
        }

        return new PollingRunResult(passes, consecutiveIdlePasses, false, true);
    }

    public GithubSyncResult SyncValidatedWorkflow(string owner, string repo, int issueNumber)
    {
        var workflows = workflowStateStore.ReadAll();
        if (!workflows.TryGetValue(issueNumber, out var workflow))
        {
            return new GithubSyncResult(false, false, $"No workflow state found for issue #{issueNumber}.");
        }

        return TrySyncWorkflow(owner, repo, workflow, syncValidatedWorkflows: true) ??
            new GithubSyncResult(false, false, $"GitHub sync skipped for issue #{issueNumber}.");
    }

    public IReadOnlyList<GithubSyncResult> ReplayPendingGithubSyncs(string owner, string repo)
    {
        var pending = ReadPendingGithubSync()
            .OrderBy(item => item.RecordedAt)
            .ToArray();

        if (pending.Length == 0)
        {
            WriteLatestGithubReplay(0, 0, 0);
            return [];
        }

        if (ShouldDeferGithubReplayForProviderBackoff())
        {
            WriteLatestGithubReplay(0, 0, 0, pending.Length);
            return pending
                .Select(item => new GithubSyncResult(
                    false,
                    false,
                    $"Intentionally deferring GitHub replay for issue #{item.IssueNumber} while provider backoff remains active."))
                .ToArray();
        }

        var results = new List<GithubSyncResult>(pending.Length);
        foreach (var item in pending)
        {
            results.Add(SyncValidatedWorkflow(owner, repo, item.IssueNumber));
        }

        var attemptedCount = results.Count(result => result.Attempted);
        var updatedCount = results.Count(result => result.Updated);
        var failedCount = results.Count(result => result.Attempted && !result.Updated);
        var deferredCount = results.Count(result => !result.Attempted && !result.Updated);
        WriteLatestGithubReplay(
            attemptedCount,
            updatedCount,
            failedCount,
            deferredCount);

        return results;
    }

    public void RecordPendingGithubSyncForTests(int issueNumber, string summary) =>
        RecordPendingGithubSync(issueNumber, summary);

    private bool ShouldDeferGithubReplayForProviderBackoff()
    {
        var runtimeStatusPath = Path.Combine(RootDirectory, ".dragon", "status", "runtime-status.json");
        if (!File.Exists(runtimeStatusPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var nextWakeReason = root.TryGetProperty("nextWakeReason", out var nextWakeReasonProperty) &&
                nextWakeReasonProperty.ValueKind == JsonValueKind.String
                ? nextWakeReasonProperty.GetString()
                : null;
            var delayedRetryUrgency = root.TryGetProperty("delayedRetryUrgency", out var delayedRetryUrgencyProperty) &&
                delayedRetryUrgencyProperty.ValueKind == JsonValueKind.String
                ? delayedRetryUrgencyProperty.GetString()
                : null;

            return string.Equals(nextWakeReason, "delayed-provider-retry", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(delayedRetryUrgency, "alert", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private GithubSyncResult? TrySyncWorkflow(string? githubOwner, string repo, IssueWorkflowState workflow, bool syncValidatedWorkflows)
    {
        if (!syncValidatedWorkflows || string.IsNullOrWhiteSpace(githubOwner))
        {
            return null;
        }

        try
        {
            var result = githubIssueService.SyncWorkflow(githubOwner, repo, workflow, executionRecordStore.Read(workflow.IssueNumber), RootDirectory);
            WriteLatestGithubSync(workflow.IssueNumber, result);
            if (result.Attempted || result.Updated)
            {
                ClearPendingGithubSync(workflow.IssueNumber);
            }
            return result;
        }
        catch (InvalidOperationException ex)
        {
            var result = new GithubSyncResult(
                true,
                false,
                $"GitHub sync failed for issue #{workflow.IssueNumber}: {ex.Message}");
            WriteLatestGithubSync(workflow.IssueNumber, result);
            RecordPendingGithubSync(workflow.IssueNumber, result.Summary);
            return result;
        }
    }

    private LatestGithubSyncSnapshot? ReadLatestGithubSync()
    {
        if (!File.Exists(GithubSyncStatusPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<LatestGithubSyncSnapshot>(
            File.ReadAllText(GithubSyncStatusPath),
            StatusSerializerOptions);
    }

    private LatestGithubReplaySnapshot? ReadLatestGithubReplay()
    {
        if (!File.Exists(GithubReplayStatusPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<LatestGithubReplaySnapshot>(
            File.ReadAllText(GithubReplayStatusPath),
            StatusSerializerOptions);
    }

    private void WriteLatestGithubSync(int issueNumber, GithubSyncResult result)
    {
        var existing = ReadLatestGithubSync();
        if (existing is not null &&
            existing.IssueNumber == issueNumber &&
            existing.Attempted == result.Attempted &&
            existing.Updated == result.Updated &&
            string.Equals(existing.Summary, result.Summary, StringComparison.Ordinal))
        {
            return;
        }

        var directory = Path.GetDirectoryName(GithubSyncStatusPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = new LatestGithubSyncSnapshot(
            issueNumber,
            result.Attempted,
            result.Updated,
            result.Summary,
            DateTimeOffset.UtcNow);

        File.WriteAllText(GithubSyncStatusPath, JsonSerializer.Serialize(snapshot, StatusSerializerOptions));
    }

    private void WriteLatestGithubReplay(int attemptedCount, int updatedCount, int failedCount, int deferredCount = 0)
    {
        var summary = attemptedCount == 0
            ? deferredCount > 0
                ? $"Intentionally deferring replay for {deferredCount} pending GitHub update{(deferredCount == 1 ? string.Empty : "s")} while provider backoff remains active."
                : "No pending GitHub updates needed replay."
            : deferredCount > 0
                ? $"Replayed {attemptedCount} pending GitHub update{(attemptedCount == 1 ? string.Empty : "s")}: {updatedCount} updated, {failedCount} still failing, {deferredCount} deferred."
                : $"Replayed {attemptedCount} pending GitHub update{(attemptedCount == 1 ? string.Empty : "s")}: {updatedCount} updated, {failedCount} still failing.";

        var existing = ReadLatestGithubReplay();
        if (existing is not null &&
            existing.AttemptedCount == attemptedCount &&
            existing.UpdatedCount == updatedCount &&
            existing.FailedCount == failedCount &&
            string.Equals(existing.Summary, summary, StringComparison.Ordinal))
        {
            return;
        }

        var directory = Path.GetDirectoryName(GithubReplayStatusPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = new LatestGithubReplaySnapshot(
            attemptedCount,
            updatedCount,
            failedCount,
            summary,
            DateTimeOffset.UtcNow);

        File.WriteAllText(GithubReplayStatusPath, JsonSerializer.Serialize(snapshot, StatusSerializerOptions));
    }

    private static bool ReplayCountsAsWork(LatestGithubReplaySnapshot? latestReplay) =>
        latestReplay is not null && latestReplay.AttemptedCount > 0;

    private IReadOnlyList<PendingGithubSyncSnapshot> ReadPendingGithubSync()
    {
        if (!File.Exists(PendingGithubSyncPath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<PendingGithubSyncSnapshot>>(
            File.ReadAllText(PendingGithubSyncPath),
            StatusSerializerOptions) ?? [];
    }

    private void RecordPendingGithubSync(int issueNumber, string summary)
    {
        var now = nowProvider();
        var pending = ReadPendingGithubSync().ToList();
        var existing = pending.FirstOrDefault(item => item.IssueNumber == issueNumber);
        pending.RemoveAll(item => item.IssueNumber == issueNumber);
        var attemptCount = existing is null ? 1 : existing.AttemptCount + 1;
        var nextRetryAt = now.Add(ComputePendingGithubSyncRetryDelay(attemptCount));

        pending.Add(existing is null
            ? new PendingGithubSyncSnapshot(issueNumber, summary, now, attemptCount, now, nextRetryAt)
            : existing with
            {
                Summary = summary,
                AttemptCount = attemptCount,
                LastAttemptedAt = now,
                NextRetryAt = nextRetryAt
            });
        WritePendingGithubSync(pending);
    }

    private static TimeSpan ComputePendingGithubSyncRetryDelay(int attemptCount)
    {
        var clampedAttempts = Math.Max(1, attemptCount);
        var delaySeconds = Math.Min(300, 30 * clampedAttempts);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private void ClearPendingGithubSync(int issueNumber)
    {
        var pending = ReadPendingGithubSync()
            .Where(item => item.IssueNumber != issueNumber)
            .ToList();

        WritePendingGithubSync(pending);
    }

    private void WritePendingGithubSync(IReadOnlyList<PendingGithubSyncSnapshot> pending)
    {
        var directory = Path.GetDirectoryName(PendingGithubSyncPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(PendingGithubSyncPath, JsonSerializer.Serialize(pending, StatusSerializerOptions));
    }

    private static IReadOnlyList<PendingGithubSyncSnapshot> AnnotatePendingGithubSync(
        IReadOnlyList<PendingGithubSyncSnapshot> pending,
        int? pollIntervalSeconds)
    {
        if (pending.Count == 0)
        {
            return pending;
        }

        if (pollIntervalSeconds is null || pollIntervalSeconds <= 0)
        {
            return pending;
        }

        var retryDelay = TimeSpan.FromSeconds(pollIntervalSeconds.Value);
        return pending
            .Select(item =>
            {
                var baseline = item.LastAttemptedAt ?? item.RecordedAt;
                return item.NextRetryAt is null
                    ? item with
                    {
                        NextRetryAt = baseline.Add(retryDelay)
                    }
                    : item;
            })
            .ToArray();
    }

    private bool HasSchedulableWork(IReadOnlyList<GithubIssue> issues)
    {
        try
        {
            _ = SelectNextIssue(issues);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private GithubIssue SelectNextIssue(IReadOnlyList<GithubIssue> issues)
    {
        var workflows = workflowStateStore.ReadAll();
        var queuedIssueNumbers = queueStore.ReadAll()
            .Select(job => job.Issue)
            .ToHashSet();
        var latestRecoveryIssuesByParent = issues
            .Where(IsRecoveryIssue)
            .Where(issue => issue.SourceIssueNumber is not null)
            .GroupBy(issue => issue.SourceIssueNumber!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(issue => issue.Number)!.Number);

        return issues
            .Where(IsSchedulableStoryIssue)
            .Where(issue => !queuedIssueNumbers.Contains(issue.Number))
            .Where(issue => !workflows.TryGetValue(issue.Number, out var workflow) || (
                !string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase) &&
                !(workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
            ))
            .Where(issue => !IsRecoveryIssue(issue) ||
                issue.SourceIssueNumber is null ||
                latestRecoveryIssuesByParent.TryGetValue(issue.SourceIssueNumber.Value, out var latestIssueNumber) && latestIssueNumber == issue.Number)
            .OrderByDescending(issue => ShouldPrioritizeRecoveryIssue(issue, workflows))
            .ThenBy(issue => issue.Number)
            .First();
    }

    private FailureDisposition? ApplyFailurePolicy(int issueNumber, IssueWorkflowState workflow)
    {
        if (!string.Equals(workflow.OverallStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var disposition = FailurePolicy.Evaluate(executionRecordStore.Read(issueNumber));
        if (!disposition.Quarantined)
        {
            return disposition;
        }

        workflowStateStore.OverrideOverallStatus(issueNumber, "quarantined", disposition.Reason!);
        queueStore.RemoveAll(job => job.Issue == issueNumber);
        return disposition;
    }

    private bool TryRequeueTransientProviderFailure(SelfBuildJob job, JobExecutionResult execution, ref IssueWorkflowState workflow)
    {
        if (!string.Equals(execution.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
            !execution.Summary.StartsWith("Transient model provider failure", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var currentRetryCount = ReadTransientProviderRetryCount(job);
        if (currentRetryCount >= MaxTransientProviderRequeuesPerJob)
        {
            return false;
        }

        var metadata = job.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        metadata["transientProviderRetryCount"] = (currentRetryCount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        metadata["transientProviderRetryReason"] = execution.Summary;
        if (execution.RetryNotBefore is { } retryNotBefore)
        {
            metadata["retryNotBeforeUtc"] = retryNotBefore.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }

        queueStore.Enqueue(job with { Metadata = metadata });

        workflow = workflowStateStore.ResetStageForRetry(
            job.Issue,
            job.Agent,
            $"Transient model provider pressure detected for {job.Agent}; requeued for retry ({currentRetryCount + 1}/{MaxTransientProviderRequeuesPerJob}).");

        return true;
    }

    private static int ReadTransientProviderRetryCount(SelfBuildJob job)
    {
        return job.Metadata.TryGetValue("transientProviderRetryCount", out var rawValue) &&
            int.TryParse(rawValue, out var parsedValue)
                ? Math.Max(0, parsedValue)
                : 0;
    }

    private CycleResult? SweepStalledWorkflow(string repo, string? githubOwner, bool syncValidatedWorkflows)
    {
        foreach (var workflow in workflowStateStore.ReadAll().Values.OrderBy(item => item.IssueNumber))
        {
            var disposition = FailurePolicy.Evaluate(workflow);
            if (!disposition.Quarantined)
            {
                continue;
            }

            var updatedWorkflow = workflowStateStore.OverrideOverallStatus(workflow.IssueNumber, "quarantined", disposition.Reason!);
            queueStore.RemoveAll(job => job.Issue == workflow.IssueNumber);
            var githubSync = TrySyncWorkflow(githubOwner, repo, updatedWorkflow, syncValidatedWorkflows);
            return new CycleResult("quarantine", null, null, [], updatedWorkflow, githubSync, null, disposition);
        }

        return null;
    }

    private CycleResult? ResumeReleasedParent(
        IReadOnlyList<GithubIssue> issues,
        string repo,
        string project)
    {
        var queuedIssueNumbers = queueStore.ReadAll().Select(job => job.Issue).ToHashSet();
        foreach (var workflow in workflowStateStore.ReadAll().Values.OrderBy(item => item.IssueNumber))
        {
            if (!string.Equals(workflow.OverallStatus, "in_progress", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(workflow.Note, "Recovery child completed; parent returned to active flow.", StringComparison.Ordinal))
            {
                continue;
            }

            if (queuedIssueNumbers.Contains(workflow.IssueNumber))
            {
                continue;
            }

            var issue = issues.FirstOrDefault(candidate => candidate.Number == workflow.IssueNumber);
            if (issue is null)
            {
                continue;
            }

            var agent = RecommendAgent(issue);
            var job = SelfBuildJobFactory.Create(issue, agent, repo, project);
            queueStore.Enqueue(job);
            var updatedWorkflow = workflowStateStore.UpdateNote(workflow.IssueNumber, "Recovery child completed; parent requeued for active flow.");
            return new CycleResult("resume", job, null, [], updatedWorkflow);
        }

        return null;
    }

    private IReadOnlyList<SelfBuildJob> PublishFollowUps(
        SelfBuildJob job,
        JobExecutionResult execution,
        ISet<string>? competingImplementationArtifacts = null)
    {
        if (!string.Equals(execution.Status, "success", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "review", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "test", StringComparison.OrdinalIgnoreCase) ||
            !IsImplementationAction(job.Action))
        {
            return [];
        }

        var requestedFollowUps = execution.RequestedFollowUps ?? [];
        var followUps = new List<SelfBuildJob>
        {
            CreateFollowUpJob(job, execution, "review", "review_issue", execution.JobId),
            CreateFollowUpJob(job, execution, "test", "test_issue", execution.JobId)
        };

        EnqueueDeferredSummaryFollowUp(job, execution, followUps);
        EnqueueOperatorSummaryFollowUp(job, execution, followUps, competingImplementationArtifacts);

        foreach (var requestedFollowUp in requestedFollowUps
            .OrderBy(GetRequestedFollowUpPriorityRank)
            .ThenBy(GetRequestedFollowUpActionRank)
            .ThenBy(followUp => followUp.Agent ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(followUp => followUp.Action ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var followUpAgent = string.IsNullOrWhiteSpace(requestedFollowUp.Agent)
                ? InferPreferredAgent(requestedFollowUp.TargetArtifact)
                : requestedFollowUp.Agent;
            var followUpAction = string.IsNullOrWhiteSpace(requestedFollowUp.Action)
                ? InferFollowUpAction(requestedFollowUp)
                : requestedFollowUp.Action;

            if (string.IsNullOrWhiteSpace(followUpAgent) || string.IsNullOrWhiteSpace(followUpAction))
            {
                continue;
            }

            if (followUps.Any(existing =>
                string.Equals(existing.Agent, followUpAgent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Action, followUpAction, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (IsRedundantSummaryFollowUp(followUps, followUpAgent, followUpAction, requestedFollowUp.TargetArtifact))
            {
                DeferSummaryFollowUp(followUps, followUpAgent, requestedFollowUp);
                continue;
            }

            followUps.Add(CreateFollowUpJob(
                job,
                execution,
                followUpAgent,
                followUpAction,
                execution.JobId,
                requestedFollowUp.Priority,
                requestedFollowUp.Reason,
                requestedFollowUp.Blocking,
                requestedFollowUp.TargetArtifact,
                requestedFollowUp.TargetOutcome));
        }

        foreach (var followUp in followUps)
        {
            queueStore.Enqueue(followUp);
        }

        return followUps;
    }

    private void EnqueueDeferredSummaryFollowUp(
        SelfBuildJob sourceJob,
        JobExecutionResult execution,
        List<SelfBuildJob> followUps)
    {
        if (!sourceJob.Metadata.TryGetValue("deferredSummaryAgent", out var deferredAgent) ||
            !sourceJob.Metadata.TryGetValue("deferredSummaryAction", out var deferredAction) ||
            string.IsNullOrWhiteSpace(deferredAgent) ||
            string.IsNullOrWhiteSpace(deferredAction))
        {
            return;
        }

        if (sourceJob.Metadata.TryGetValue("deferredSummaryTargetArtifact", out var deferredTargetArtifact) &&
            !string.IsNullOrWhiteSpace(deferredTargetArtifact) &&
            !(execution.ChangedPaths ?? []).Any(path =>
                string.Equals(path, deferredTargetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (sourceJob.Metadata.TryGetValue("deferredSummaryTargetArtifact", out deferredTargetArtifact) &&
            !string.IsNullOrWhiteSpace(deferredTargetArtifact) &&
            queueStore.ReadAll().Any(existing =>
                string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), deferredTargetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (followUps.Any(existing =>
            string.Equals(existing.Agent, deferredAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, deferredAction, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        followUps.Add(CreateFollowUpJob(
            sourceJob,
            execution,
            deferredAgent,
            deferredAction,
            execution.JobId,
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryPriority"),
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryReason"),
            false,
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryTargetArtifact"),
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryTargetOutcome")));
    }

    private void EnqueueOperatorSummaryFollowUp(
        SelfBuildJob sourceJob,
        JobExecutionResult execution,
        List<SelfBuildJob> followUps,
        ISet<string>? competingImplementationArtifacts = null)
    {
        var targetArtifact = sourceJob.Metadata.GetValueOrDefault("targetArtifact");
        if (string.IsNullOrWhiteSpace(targetArtifact))
        {
            return;
        }

        if (competingImplementationArtifacts?.Contains(targetArtifact) == true)
        {
            return;
        }

        var changedPaths = (execution.ChangedPaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (changedPaths.Length <= 1 ||
            !changedPaths.Any(path => string.Equals(path, targetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (queueStore.ReadAll().Any(existing =>
            string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), targetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var summaryAgent = InferOperatorSummaryAgent(targetArtifact);
        var changedArtifactRollup = string.Join("|", changedPaths);
        if (followUps.Any(existing =>
            string.Equals(existing.Agent, summaryAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), targetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        followUps.Add(CreateFollowUpJob(
            sourceJob,
            execution,
            summaryAgent,
            "summarize_issue",
            execution.JobId,
            "low",
            "Summarize the broader operator impact after the targeted implementation.",
            false,
            targetArtifact,
            "Summarize the broader operator impact of the targeted implementation.",
            ("changedArtifactRollup", changedArtifactRollup)));
    }

    private static string InferOperatorSummaryAgent(string targetArtifact)
    {
        var preferredAgent = InferPreferredAgent(targetArtifact);
        return string.Equals(preferredAgent, "documentation", StringComparison.OrdinalIgnoreCase)
            ? "documentation"
            : "feedback";
    }

    private static void DeferSummaryFollowUp(
        List<SelfBuildJob> followUps,
        string followUpAgent,
        RequestedFollowUp requestedFollowUp)
    {
        var index = followUps.FindIndex(existing =>
            string.Equals(existing.Agent, followUpAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), requestedFollowUp.TargetArtifact, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return;
        }

        var implementationFollowUp = followUps[index];
        var metadata = new Dictionary<string, string>(implementationFollowUp.Metadata, StringComparer.Ordinal)
        {
            ["deferredSummaryAgent"] = followUpAgent,
            ["deferredSummaryAction"] = "summarize_issue"
        };

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.Priority))
        {
            metadata["deferredSummaryPriority"] = requestedFollowUp.Priority;
        }

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.Reason))
        {
            metadata["deferredSummaryReason"] = requestedFollowUp.Reason;
        }

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.TargetArtifact))
        {
            metadata["deferredSummaryTargetArtifact"] = requestedFollowUp.TargetArtifact;
        }

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.TargetOutcome))
        {
            metadata["deferredSummaryTargetOutcome"] = requestedFollowUp.TargetOutcome;
        }

        followUps[index] = implementationFollowUp with { Metadata = metadata };
    }

    private static bool IsRedundantSummaryFollowUp(
        IReadOnlyList<SelfBuildJob> existingFollowUps,
        string followUpAgent,
        string followUpAction,
        string? targetArtifact)
    {
        if (!string.Equals(followUpAction, "summarize_issue", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(targetArtifact))
        {
            return false;
        }

        return existingFollowUps.Any(existing =>
            string.Equals(existing.Agent, followUpAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), targetArtifact, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetRequestedFollowUpPriorityRank(RequestedFollowUp followUp)
    {
        if (string.Equals(followUp.Priority, "high", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(followUp.Priority, "low", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static int GetRequestedFollowUpActionRank(RequestedFollowUp followUp)
    {
        var action = string.IsNullOrWhiteSpace(followUp.Action)
            ? InferFollowUpAction(followUp)
            : followUp.Action;

        return action?.ToLowerInvariant() switch
        {
            "implement_issue" => 0,
            "summarize_issue" => 1,
            _ => 2
        };
    }

    private void RemoveSupersededRecoveryJobs(IReadOnlyList<GithubIssue> issues)
    {
        var latestRecoveryIssuesByParent = issues
            .Where(IsRecoveryIssue)
            .Where(issue => issue.SourceIssueNumber is not null)
            .GroupBy(issue => issue.SourceIssueNumber!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(issue => issue.Number)!.Number);

        queueStore.RemoveAll(job =>
        {
            if (!string.Equals(job.Metadata.GetValueOrDefault("workType"), "recovery", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(job.Metadata.GetValueOrDefault("sourceIssueNumber"), out var sourceIssueNumber))
            {
                return false;
            }

            return latestRecoveryIssuesByParent.TryGetValue(sourceIssueNumber, out var latestRecoveryIssueNumber) &&
                latestRecoveryIssueNumber != job.Issue;
        });
    }

    private void RemoveSupersededSummaryJobs()
    {
        var jobs = queueStore.ReadAll().ToList();
        var queuedImplementationJobsByTarget = jobs
            .Where(job => string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            .Where(job => !string.IsNullOrWhiteSpace(job.Metadata.GetValueOrDefault("targetArtifact")))
            .GroupBy(job => job.Metadata.GetValueOrDefault("targetArtifact")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(job => GetImplementationPriorityRank(job))
                    .ThenByDescending(job => job.Issue)
                    .ThenByDescending(GetImplementationSpecificityRank)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        if (queuedImplementationJobsByTarget.Count == 0)
        {
            return;
        }

        var removalsByWinnerIssue = new Dictionary<int, List<int>>();
        var keptJobs = new List<SelfBuildJob>();
        foreach (var job in jobs)
        {
            if (!string.Equals(job.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase))
            {
                keptJobs.Add(job);
                continue;
            }

            var targetArtifact = job.Metadata.GetValueOrDefault("targetArtifact");
            if (string.IsNullOrWhiteSpace(targetArtifact) ||
                !queuedImplementationJobsByTarget.TryGetValue(targetArtifact, out var winnerJob))
            {
                keptJobs.Add(job);
                continue;
            }

            if (!removalsByWinnerIssue.TryGetValue(winnerJob.Issue, out var removedIssues))
            {
                removedIssues = [];
                removalsByWinnerIssue[winnerJob.Issue] = removedIssues;
            }

            removedIssues.Add(job.Issue);
        }

        var updatedJobs = keptJobs
            .Select(job =>
            {
                if (!removalsByWinnerIssue.TryGetValue(job.Issue, out var removedIssues) || removedIssues.Count == 0)
                {
                    return job;
                }

                var metadata = new Dictionary<string, string>(job.Metadata, StringComparer.Ordinal)
                {
                    ["supersededSummaryIssues"] = string.Join("|", removedIssues.OrderBy(issue => issue)),
                    ["summaryConflictResolution"] = "Kept same-artifact implementation and pruned superseded summary jobs."
                };

                return job with { Metadata = metadata };
            })
            .ToList();

        queueStore.ReplaceAll(updatedJobs);
    }

    private void RemoveSupersededImplementationJobs()
    {
        var jobs = queueStore.ReadAll().ToList();
        var strongestPriorityByArtifact = jobs
            .Where(job => string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            .Where(job => !string.IsNullOrWhiteSpace(job.Metadata.GetValueOrDefault("targetArtifact")))
            .GroupBy(job => job.Metadata.GetValueOrDefault("targetArtifact")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(job => GetImplementationPriorityRank(job))
                    .ThenByDescending(job => job.Issue)
                    .ThenByDescending(GetImplementationSpecificityRank)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        if (strongestPriorityByArtifact.Count == 0)
        {
            return;
        }

        var removalsByWinnerIssue = new Dictionary<int, List<int>>();
        var keptJobs = new List<SelfBuildJob>();
        foreach (var job in jobs)
        {
            if (!string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            {
                keptJobs.Add(job);
                continue;
            }

            var targetArtifact = job.Metadata.GetValueOrDefault("targetArtifact");
            if (string.IsNullOrWhiteSpace(targetArtifact) ||
                !strongestPriorityByArtifact.TryGetValue(targetArtifact, out var strongestJob) ||
                AreEquivalentImplementationCandidates(job, strongestJob))
            {
                keptJobs.Add(job);
                continue;
            }

            if (job.Issue == strongestJob.Issue)
            {
                keptJobs.Add(job);
                continue;
            }

            if (!removalsByWinnerIssue.TryGetValue(strongestJob.Issue, out var removedIssues))
            {
                removedIssues = [];
                removalsByWinnerIssue[strongestJob.Issue] = removedIssues;
            }

            removedIssues.Add(job.Issue);
        }

        var updatedJobs = keptJobs
            .Select(job =>
            {
                if (!removalsByWinnerIssue.TryGetValue(job.Issue, out var removedIssues) || removedIssues.Count == 0)
                {
                    return job;
                }

                var metadata = new Dictionary<string, string>(job.Metadata, StringComparer.Ordinal)
                {
                    ["supersededImplementationIssues"] = string.Join("|", removedIssues.OrderBy(issue => issue)),
                    ["implementationConflictResolution"] = "Kept newer or higher-specificity same-artifact implementation; pruned weaker duplicates."
                };

                return job with { Metadata = metadata };
            })
            .ToList();

        queueStore.ReplaceAll(updatedJobs);
    }

    private HashSet<string> GetCompetingImplementationArtifacts() =>
        queueStore.ReadAll()
            .Where(job => string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            .Select(job => job.Metadata.GetValueOrDefault("targetArtifact"))
            .Where(targetArtifact => !string.IsNullOrWhiteSpace(targetArtifact))
            .GroupBy(targetArtifact => targetArtifact!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int GetImplementationPriorityRank(SelfBuildJob job)
    {
        var priority = job.Metadata.GetValueOrDefault("requestedPriority");
        if (string.Equals(priority, "high", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(priority, "low", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static int GetImplementationSpecificityRank(SelfBuildJob job)
    {
        var outcome = job.Metadata.GetValueOrDefault("targetOutcome");
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return 0;
        }

        return outcome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static bool AreEquivalentImplementationCandidates(SelfBuildJob left, SelfBuildJob right) =>
        string.Equals(left.Agent, right.Agent, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Action, right.Action, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Metadata.GetValueOrDefault("requestedPriority"), right.Metadata.GetValueOrDefault("requestedPriority"), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Metadata.GetValueOrDefault("targetArtifact"), right.Metadata.GetValueOrDefault("targetArtifact"), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Metadata.GetValueOrDefault("targetOutcome"), right.Metadata.GetValueOrDefault("targetOutcome"), StringComparison.OrdinalIgnoreCase);

    private static SelfBuildJob CreateFollowUpJob(
        SelfBuildJob sourceJob,
        JobExecutionResult execution,
        string agent,
        string action,
        string parentJobId,
        string? requestedPriority = null,
        string? requestedReason = null,
        bool requestedBlocking = false,
        string? targetArtifact = null,
        string? targetOutcome = null,
        params (string Key, string Value)[] additionalMetadata)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["requestedBy"] = "system",
            ["source"] = "dragon-orchestrator-dotnet",
            ["parentJobId"] = parentJobId,
            ["parentIssue"] = sourceJob.Issue.ToString(),
            ["changedPaths"] = string.Join("|", execution.ChangedPaths ?? [])
        };

        if (!string.IsNullOrWhiteSpace(requestedPriority))
        {
            metadata["requestedPriority"] = requestedPriority;
        }

        if (!string.IsNullOrWhiteSpace(requestedReason))
        {
            metadata["requestedReason"] = requestedReason;
        }

        if (requestedBlocking)
        {
            metadata["requestedBlocking"] = "true";
        }

        if (!string.IsNullOrWhiteSpace(targetArtifact))
        {
            metadata["targetArtifact"] = targetArtifact;
        }

        if (!string.IsNullOrWhiteSpace(targetOutcome))
        {
            metadata["targetOutcome"] = targetOutcome;
        }

        var preferredAgent = InferPreferredAgent(targetArtifact);
        if (!string.IsNullOrWhiteSpace(preferredAgent))
        {
            metadata["preferredAgent"] = preferredAgent;
        }

        foreach (var (key, value) in additionalMetadata)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        return new SelfBuildJob(
            agent,
            action,
            sourceJob.Repo,
            sourceJob.Project,
            sourceJob.Issue,
            new SelfBuildJobPayload(
                sourceJob.Payload.Title,
                ["story"],
                sourceJob.Payload.Heading,
                sourceJob.Payload.SourceFile,
                null
            ),
            metadata
        );
    }

    private static string? InferPreferredAgent(string? targetArtifact)
    {
        if (string.IsNullOrWhiteSpace(targetArtifact))
        {
            return null;
        }

        if (targetArtifact.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
            targetArtifact.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return "documentation";
        }

        if (targetArtifact.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
            targetArtifact.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            targetArtifact.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return "refactor";
        }

        return null;
    }

    private static string? InferFollowUpAction(RequestedFollowUp followUp)
    {
        var targetOutcome = followUp.TargetOutcome ?? string.Empty;

        if (ContainsImplementationVerb(targetOutcome))
        {
            return "implement_issue";
        }

        if (!string.IsNullOrWhiteSpace(followUp.TargetArtifact) || !string.IsNullOrWhiteSpace(followUp.TargetOutcome))
        {
            return "summarize_issue";
        }

        return null;
    }

    private static bool ContainsImplementationVerb(string value) =>
        value.Contains("update", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("improve", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("refactor", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("rewrite", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("clean up", StringComparison.OrdinalIgnoreCase);

    private static string RecommendAgent(GithubIssue issue)
    {
        if (IsRecoveryIssue(issue))
        {
            return "developer";
        }

        var title = issue.Title.ToLowerInvariant();

        if (title.Contains("review", StringComparison.Ordinal))
        {
            return "review";
        }

        if (title.Contains("test", StringComparison.Ordinal))
        {
            return "test";
        }

        if (title.Contains("architect agent", StringComparison.Ordinal))
        {
            return "architect";
        }

        if (title.Contains("documentation agent", StringComparison.Ordinal))
        {
            return "documentation";
        }

        if (title.Contains("feedback agent", StringComparison.Ordinal))
        {
            return "feedback";
        }

        if (title.Contains("idea agent", StringComparison.Ordinal))
        {
            return "idea";
        }

        if (title.Contains("repository manager agent", StringComparison.Ordinal))
        {
            return "repository-manager";
        }

        if (title.Contains("refactor agent", StringComparison.Ordinal))
        {
            return "refactor";
        }

        return "developer";
    }

    private static bool IsRecoveryIssue(GithubIssue issue) =>
        issue.Labels.Contains("recovery", StringComparer.OrdinalIgnoreCase) ||
        issue.Title.Contains("[Recovery]", StringComparison.OrdinalIgnoreCase);

    private static bool IsSchedulableStoryIssue(GithubIssue issue) =>
        issue.Labels.Contains("story", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("validated", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("waiting-follow-up", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("superseded", StringComparer.OrdinalIgnoreCase);

    private static bool IsImplementationAction(string action) =>
        string.Equals(action, "implement_issue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, "recover_issue", StringComparison.OrdinalIgnoreCase);

    private static string InferCurrentStage(IssueWorkflowState workflow)
    {
        var latestObserved = workflow.Stages
            .OrderByDescending(stage => stage.Value.ObservedAt)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(latestObserved.Key) ? "unknown" : latestObserved.Key;
    }

    private IReadOnlyList<string> ReadLatestSuccessfulChangedPaths(int issueNumber)
    {
        return executionRecordStore.Read(issueNumber)
            .Where(record =>
                string.Equals(record.Status, "success", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.JobAgent, "review", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.JobAgent, "test", StringComparison.OrdinalIgnoreCase) &&
                record.ChangedPaths.Count > 0)
            .OrderByDescending(record => record.RecordedAt)
            .Select(record => record.ChangedPaths)
            .FirstOrDefault() ?? [];
    }

    private static bool ShouldPrioritizeRecoveryIssue(
        GithubIssue issue,
        IReadOnlyDictionary<int, IssueWorkflowState> workflows)
    {
        if (!IsRecoveryIssue(issue))
        {
            return false;
        }

        if (issue.SourceIssueNumber is null)
        {
            return true;
        }

        if (!workflows.TryGetValue(issue.SourceIssueNumber.Value, out var parentWorkflow))
        {
            return true;
        }

        if (parentWorkflow.ActiveRecoveryIssueNumbers?.Contains(issue.Number) == true)
        {
            return true;
        }

        return string.Equals(parentWorkflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasActionableRecoveryWork(IssueWorkflowState workflow, IReadOnlyList<SelfBuildJob> queuedJobs)
    {
        if (!string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (queuedJobs.Any(job => job.Issue == workflow.IssueNumber))
        {
            return true;
        }

        if (!(workflow.ActiveRecoveryIssueNumbers?.Any() ?? false))
        {
            return false;
        }

        return workflow.ActiveRecoveryIssueNumbers!.Any(recoveryIssueNumber =>
            queuedJobs.Any(job => job.Issue == recoveryIssueNumber));
    }

    private static int CountActionableRecoveryJobs(IssueWorkflowState workflow, IReadOnlyList<SelfBuildJob> queuedJobs)
    {
        if (!string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var parentQueuedJobs = queuedJobs.Count(job => job.Issue == workflow.IssueNumber);
        var childQueuedJobs = (workflow.ActiveRecoveryIssueNumbers ?? [])
            .Sum(recoveryIssueNumber => queuedJobs.Count(job => job.Issue == recoveryIssueNumber));

        return parentQueuedJobs + childQueuedJobs;
    }

    private static LeadQuarantineSnapshot? BuildLeadQuarantine(
        IReadOnlyDictionary<int, IssueWorkflowState> workflows,
        IReadOnlyList<SelfBuildJob> queuedJobs)
    {
        return BuildLeadQuarantine(workflows, queuedJobs, []);
    }

    private static LeadQuarantineSnapshot? BuildLeadQuarantine(
        IReadOnlyDictionary<int, IssueWorkflowState> workflows,
        IReadOnlyList<SelfBuildJob> queuedJobs,
        IReadOnlyList<PendingGithubSyncSnapshot> pendingGithubSync)
    {
        return workflows.Values
            .Where(workflow => HasActionableRecoveryWork(workflow, queuedJobs))
            .Select(workflow =>
            {
                var queuedRecoveryJobs = CountActionableRecoveryJobs(workflow, queuedJobs);
                var recoveryIssueNumber = (workflow.ActiveRecoveryIssueNumbers ?? [])
                    .Where(recoveryIssue => queuedJobs.Any(job => job.Issue == recoveryIssue))
                    .OrderByDescending(recoveryIssue => queuedJobs.Count(job => job.Issue == recoveryIssue))
                    .ThenByDescending(recoveryIssue => recoveryIssue)
                    .FirstOrDefault();
                var hasRecoveryIssue = recoveryIssueNumber != 0;

                workflows.TryGetValue(recoveryIssueNumber, out var recoveryWorkflow);

                return new
                {
                    Workflow = workflow,
                    QueuedRecoveryJobs = queuedRecoveryJobs,
                    RecoveryIssueNumber = hasRecoveryIssue ? recoveryIssueNumber : (int?)null,
                    RecoveryIssueTitle = hasRecoveryIssue ? recoveryWorkflow?.IssueTitle : null,
                    OldestPendingGithubSyncAt = GetOldestPendingGithubSyncAt(
                        workflow.IssueNumber,
                        hasRecoveryIssue ? recoveryIssueNumber : (int?)null,
                        pendingGithubSync)
                };
            })
            .OrderBy(candidate => candidate.OldestPendingGithubSyncAt is null ? 1 : 0)
            .ThenBy(candidate => candidate.OldestPendingGithubSyncAt)
            .ThenByDescending(candidate => candidate.QueuedRecoveryJobs)
            .ThenByDescending(candidate => candidate.RecoveryIssueNumber ?? 0)
            .ThenBy(candidate => candidate.Workflow.IssueNumber)
            .Select(candidate => new LeadQuarantineSnapshot(
                candidate.Workflow.IssueNumber,
                candidate.Workflow.IssueTitle,
                candidate.Workflow.Note,
                candidate.QueuedRecoveryJobs,
                candidate.RecoveryIssueNumber,
                candidate.RecoveryIssueTitle,
                OldestPendingGithubSyncAt: candidate.OldestPendingGithubSyncAt))
            .FirstOrDefault();
    }

    private static DateTimeOffset? GetOldestPendingGithubSyncAt(
        int issueNumber,
        int? recoveryIssueNumber,
        IReadOnlyList<PendingGithubSyncSnapshot> pendingGithubSync)
    {
        return pendingGithubSync
            .Where(item => item.IssueNumber == issueNumber || (recoveryIssueNumber is not null && item.IssueNumber == recoveryIssueNumber.Value))
            .OrderBy(item => item.RecordedAt)
            .Select(item => (DateTimeOffset?)item.RecordedAt)
            .FirstOrDefault();
    }

    private static string DeriveStatusHealth(
        IReadOnlyList<IssueStatusSnapshot> issues,
        LeadQuarantineSnapshot? leadQuarantine,
        LatestGithubReplaySnapshot? latestGithubReplay,
        InterventionTargetSnapshot? interventionTarget,
        DateTimeOffset? nextDelayedRetryAt,
        DateTimeOffset? pendingGithubSyncNextRetryAt,
        DateTimeOffset now)
    {
        if (leadQuarantine is not null)
        {
            return "blocked";
        }

        if (issues.Any(issue => string.Equals(issue.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase)))
        {
            return "attention";
        }

        if (issues.Any(issue => string.Equals(issue.OverallStatus, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "attention";
        }

        if (interventionTarget is not null &&
            interventionTarget.Acknowledged &&
            string.Equals(interventionTarget.Escalation, "critical", StringComparison.OrdinalIgnoreCase))
        {
            return "attention";
        }

        if (nextDelayedRetryAt is not null)
        {
            return nextDelayedRetryAt.Value - now >= LongDelayedRetryAttentionThreshold
                ? "attention"
                : "healthy";
        }

        if (pendingGithubSyncNextRetryAt is not null &&
            now - pendingGithubSyncNextRetryAt.Value >= LongDelayedRetryAttentionThreshold)
        {
            return "attention";
        }

        if (interventionTarget is not null &&
            !string.Equals(interventionTarget.Kind, "idle", StringComparison.OrdinalIgnoreCase))
        {
            return "healthy";
        }

        if (issues.Any(issue => issue.QueuedJobCount > 0 || string.Equals(issue.OverallStatus, "in_progress", StringComparison.OrdinalIgnoreCase)))
        {
            return "healthy";
        }

        if (ReplayCountsAsWork(latestGithubReplay))
        {
            return "healthy";
        }

        return "idle";
    }

    private static string BuildAttentionSummary(
        int queuedJobs,
        IReadOnlyList<IssueStatusSnapshot> issues,
        string health,
        LeadQuarantineSnapshot? leadQuarantine,
        LatestGithubReplaySnapshot? latestGithubReplay,
        InterventionTargetSnapshot? interventionTarget,
        DateTimeOffset? nextDelayedRetryAt,
        DateTimeOffset? pendingGithubSyncNextRetryAt,
        DateTimeOffset now)
    {
        var rollup = BuildStatusRollupFromIssues(issues);
        var leadRecoveryAge = FormatLeadQuarantineAge(leadQuarantine);
        var delayedRetryAge = nextDelayedRetryAt is null
            ? (TimeSpan?)null
            : nextDelayedRetryAt.Value - now;
        var pendingGithubRetryAge = pendingGithubSyncNextRetryAt is null
            ? (TimeSpan?)null
            : now - pendingGithubSyncNextRetryAt.Value;

        return health switch
        {
            "blocked" when leadQuarantine is not null =>
                $"{rollup.ActionableQuarantinedIssues} quarantined issue(s) still have queued recovery work. Lead recovery: issue #{leadQuarantine.IssueNumber}" +
                $"{(leadQuarantine.RecoveryIssueNumber is not null ? $" via recovery #{leadQuarantine.RecoveryIssueNumber.Value}" : string.Empty)}" +
                $"{(leadRecoveryAge is not null ? $" ({leadRecoveryAge})" : string.Empty)}.",
            "blocked" => $"{rollup.ActionableQuarantinedIssues} quarantined issue(s) still have queued recovery work.",
            "attention" when interventionTarget is not null &&
                interventionTarget.Acknowledged &&
                string.Equals(interventionTarget.Escalation, "critical", StringComparison.OrdinalIgnoreCase) =>
                $"Critical intervention target remains acknowledged but unresolved. {interventionTarget.Summary}",
            "attention" when delayedRetryAge is not null =>
                $"Provider retry remains delayed for {FormatElapsed(delayedRetryAge.Value)} before the next execution window.",
            "attention" when pendingGithubRetryAge is not null =>
                $"GitHub writeback retry has been overdue for {FormatElapsed(pendingGithubRetryAge.Value)} and still has not drained.",
            "attention" when rollup.QuarantinedIssues > 0 => $"{rollup.InactiveQuarantinedIssues} quarantined issue(s) need intervention, {rollup.ActionableQuarantinedIssues} currently actionable.",
            "attention" => $"{rollup.FailedIssues} failed issue(s) need review.",
            "healthy" when queuedJobs == 0 && ReplayCountsAsWork(latestGithubReplay) =>
                $"{latestGithubReplay!.AttemptedCount} GitHub update(s) were replayed on the latest pass and the worker is waiting for a quiet confirmation pass.",
            "healthy" when delayedRetryAge is not null =>
                $"Waiting for the next provider retry window in {FormatElapsed(delayedRetryAge.Value)}.",
            "healthy" => $"{queuedJobs} queued job(s), {rollup.InProgressIssues} issue(s) in progress.",
            _ => "No queued work and no active issue workflows."
        };
    }

    private TimeSpan DetermineWatchDelay(TimeSpan pollInterval, bool capToPollInterval)
    {
        var delayedRetryWait = queueStore.GetNextReadyDelay();
        if (delayedRetryWait is null || delayedRetryWait <= TimeSpan.Zero)
        {
            return pollInterval;
        }

        return capToPollInterval
            ? delayedRetryWait.Value <= pollInterval ? delayedRetryWait.Value : pollInterval
            : delayedRetryWait.Value;
    }

    private static string? DeriveDelayedRetryUrgency(DateTimeOffset? nextDelayedRetryAt, DateTimeOffset now)
    {
        if (nextDelayedRetryAt is null)
        {
            return null;
        }

        var remaining = nextDelayedRetryAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "normal";
        }

        if (remaining >= LongDelayedRetryAttentionThreshold)
        {
            return "alert";
        }

        if (remaining >= TimeSpan.FromMinutes(5))
        {
            return "caution";
        }

        return "normal";
    }

    private static string? DeriveNextWakeReason(DateTimeOffset? nextPollAt, DateTimeOffset? nextDelayedRetryAt)
    {
        if (nextDelayedRetryAt is not null &&
            (nextPollAt is null || (nextPollAt.Value - nextDelayedRetryAt.Value).Duration() <= TimeSpan.FromSeconds(1)))
        {
            return "delayed-provider-retry";
        }

        return nextPollAt is null
            ? null
            : "poll-interval";
    }

    private static StatusRollup BuildStatusRollup(IReadOnlyDictionary<int, IssueWorkflowState> workflows, IReadOnlyList<SelfBuildJob> queuedJobs)
    {
        var issues = workflows.Values
            .Select(workflow => new IssueStatusSnapshot(
                workflow.IssueNumber,
                workflow.IssueTitle,
                workflow.OverallStatus,
                InferCurrentStage(workflow),
                queuedJobs.Count(job => job.Issue == workflow.IssueNumber),
                workflow.Note,
                null,
                null,
                null))
            .ToArray();

        var baseRollup = BuildStatusRollupFromIssues(issues);
        var actionableQuarantinedIssues = workflows.Values.Count(workflow => HasActionableRecoveryWork(workflow, queuedJobs));

        return baseRollup with
        {
            ActionableQuarantinedIssues = actionableQuarantinedIssues,
            InactiveQuarantinedIssues = baseRollup.QuarantinedIssues - actionableQuarantinedIssues
        };
    }

    private static StatusRollup BuildStatusRollupFromIssues(IReadOnlyList<IssueStatusSnapshot> issues)
    {
        var quarantinedIssues = issues
            .Where(issue => string.Equals(issue.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var actionableQuarantinedIssues = quarantinedIssues.Count(issue => issue.QueuedJobCount > 0);

        return new StatusRollup(
            issues.Count(issue => string.Equals(issue.OverallStatus, "failed", StringComparison.OrdinalIgnoreCase)),
            quarantinedIssues.Length,
            actionableQuarantinedIssues,
            quarantinedIssues.Length - actionableQuarantinedIssues,
            issues.Count(issue => string.Equals(issue.OverallStatus, "in_progress", StringComparison.OrdinalIgnoreCase)),
            issues.Count(issue => string.Equals(issue.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        );
    }

    private static LatestActivitySnapshot? BuildLatestActivity(IReadOnlyList<IssueStatusSnapshot> issues)
    {
        var latest = issues
            .Where(issue => issue.LatestExecutionRecordedAt is not null)
            .OrderByDescending(issue => issue.LatestExecutionRecordedAt)
            .FirstOrDefault();

        if (latest is null)
        {
            return null;
        }

        return new LatestActivitySnapshot(
            latest.IssueNumber,
            latest.IssueTitle,
            latest.CurrentStage,
            latest.LatestExecutionSummary,
            latest.LatestExecutionRecordedAt!.Value
        );
    }

    private static string? BuildPendingGithubSyncSummary(IReadOnlyList<PendingGithubSyncSnapshot> pendingGithubSync, DateTimeOffset now)
    {
        if (pendingGithubSync.Count == 0)
        {
            return null;
        }

        var oldest = pendingGithubSync
            .OrderBy(item => item.RecordedAt)
            .First();

        return pendingGithubSync.Count == 1
            ? $"1 GitHub update is waiting for retry: issue #{oldest.IssueNumber} ({FormatElapsed(now - oldest.RecordedAt)} old)."
            : $"{pendingGithubSync.Count} GitHub updates are waiting for retry. Oldest: issue #{oldest.IssueNumber} ({FormatElapsed(now - oldest.RecordedAt)} old).";
    }

    private static string? BuildPendingGithubSyncRetryState(DateTimeOffset? nextRetryAt, DateTimeOffset now)
    {
        if (nextRetryAt is null)
        {
            return null;
        }

        var remaining = nextRetryAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "ready now";
        }

        return $"next retry in {FormatElapsed(remaining)}";
    }

    private static string? BuildReplayPriorityReason(
        DateTimeOffset? nextDelayedRetryAt,
        string? pendingGithubSyncRetryState,
        int pendingGithubSyncRetryOverdueMinutes)
    {
        if (nextDelayedRetryAt is not null)
        {
            return "provider-backoff";
        }

        if (pendingGithubSyncRetryOverdueMinutes >= (int)LongDelayedRetryAttentionThreshold.TotalMinutes)
        {
            return "overdue-github-writeback-retry";
        }

        if (string.Equals(pendingGithubSyncRetryState, "ready now", StringComparison.OrdinalIgnoreCase))
        {
            return "ready-github-writeback-retry";
        }

        return null;
    }

    private static string? BuildReplayPrioritySummary(string? replayPriorityReason) =>
        replayPriorityReason switch
        {
            "provider-backoff" => "Provider backoff is delaying GitHub writeback replay.",
            "overdue-github-writeback-retry" => "Overdue GitHub writeback replay is being prioritized before ordinary implementation.",
            "ready-github-writeback-retry" => "GitHub writeback replay is ready to run.",
            _ => null
        };

    private static string? BuildWaitSignal(string? replayPrioritySummary, string? nextWakeReason)
    {
        if (!string.IsNullOrWhiteSpace(replayPrioritySummary))
        {
            return replayPrioritySummary;
        }

        return nextWakeReason switch
        {
            "poll-interval" => "Routine poll wait.",
            _ => null
        };
    }

    private static InterventionTargetSnapshot BuildInterventionTarget(
        LeadQuarantineSnapshot? leadQuarantine,
        LeadJobSnapshot? leadJob,
        IReadOnlyList<PendingGithubSyncSnapshot> pendingGithubSync)
    {
        if (leadQuarantine is not null &&
            string.Equals(leadQuarantine.State, "sync-drift", StringComparison.OrdinalIgnoreCase))
        {
            var pendingIssueNumber = leadQuarantine.RecoveryIssueNumber ?? leadQuarantine.IssueNumber;
            return new InterventionTargetSnapshot(
                "github-replay-drift",
                leadQuarantine.Summary ?? $"Replay queued GitHub updates for issue #{pendingIssueNumber}.",
                leadQuarantine.IssueNumber,
                leadQuarantine.RecoveryIssueNumber,
                pendingIssueNumber,
                null,
                null,
                leadQuarantine.OldestPendingGithubSyncAt,
                BuildInterventionTargetAgeSummary(leadQuarantine.OldestPendingGithubSyncAt),
                BuildInterventionTargetEscalation(leadQuarantine.OldestPendingGithubSyncAt));
        }

        if (leadQuarantine is not null)
        {
            return new InterventionTargetSnapshot(
                "recovery-work",
                leadQuarantine.Summary ?? $"Continue recovery work for issue #{leadQuarantine.IssueNumber}.",
                leadQuarantine.IssueNumber,
                leadQuarantine.RecoveryIssueNumber,
                null,
                null,
                null,
                leadQuarantine.OldestPendingGithubSyncAt,
                BuildInterventionTargetAgeSummary(leadQuarantine.OldestPendingGithubSyncAt),
                BuildInterventionTargetEscalation(leadQuarantine.OldestPendingGithubSyncAt));
        }

        if (pendingGithubSync.Count > 0)
        {
            var oldest = pendingGithubSync
                .OrderBy(item => item.RecordedAt)
                .First();

            return new InterventionTargetSnapshot(
                "github-replay-drift",
                $"Replay queued GitHub update for issue #{oldest.IssueNumber}.",
                oldest.IssueNumber,
                null,
                oldest.IssueNumber,
                null,
                null,
                oldest.RecordedAt,
                BuildInterventionTargetAgeSummary(oldest.RecordedAt),
                BuildInterventionTargetEscalation(oldest.RecordedAt));
        }

        if (leadJob is not null)
        {
            var isOperatorEscalation = string.Equals(leadJob.WorkType, "operator-escalation", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(leadJob.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(leadJob.Priority, "high", StringComparison.OrdinalIgnoreCase));
            var kind = isOperatorEscalation
                ? "operator-escalation"
                : string.Equals(leadJob.Action, "implement_issue", StringComparison.OrdinalIgnoreCase)
                    ? "implementation"
                    : "queued-work";
            var summary = isOperatorEscalation
                ? leadJob.TargetOutcome is not null
                    ? $"Escalate issue #{leadJob.IssueNumber}: {leadJob.TargetOutcome}."
                    : $"Escalate persistent critical intervention for issue #{leadJob.IssueNumber}."
                : leadJob.TargetOutcome is not null
                    ? $"Advance issue #{leadJob.IssueNumber}: {leadJob.TargetOutcome}."
                    : $"Advance issue #{leadJob.IssueNumber} via {leadJob.Action}.";

            return new InterventionTargetSnapshot(
                kind,
                summary,
                leadJob.IssueNumber,
                null,
                null,
                leadJob.TargetArtifact,
                leadJob.TargetOutcome,
                null,
                null,
                isOperatorEscalation ? "critical" : "fresh");
        }

        return new InterventionTargetSnapshot("idle", "No immediate intervention target.");
    }

    private static string? BuildInterventionTargetAgeSummary(DateTimeOffset? observedAt)
    {
        if (observedAt is null)
        {
            return null;
        }

        var elapsed = DateTimeOffset.UtcNow >= observedAt.Value
            ? DateTimeOffset.UtcNow - observedAt.Value
            : TimeSpan.Zero;

        return $"{FormatElapsed(elapsed)} old";
    }

    private static string? BuildInterventionTargetEscalation(DateTimeOffset? observedAt)
    {
        if (observedAt is null)
        {
            return null;
        }

        var elapsed = DateTimeOffset.UtcNow >= observedAt.Value
            ? DateTimeOffset.UtcNow - observedAt.Value
            : TimeSpan.Zero;

        if (elapsed >= TimeSpan.FromHours(1))
        {
            return "critical";
        }

        if (elapsed >= TimeSpan.FromMinutes(15))
        {
            return "warning";
        }

        return "fresh";
    }

    private static string? BuildDefaultWorkerActivity(
        string workerState,
        InterventionTargetSnapshot? interventionTarget,
        LeadJobSnapshot? leadJob,
        LatestGithubReplaySnapshot? latestGithubReplay,
        int pendingGithubSyncRetryOverdueMinutes)
    {
        if (string.Equals(workerState, "waiting", StringComparison.OrdinalIgnoreCase) &&
            leadJob is not null &&
            leadJob.Delayed &&
            leadJob.RetryNotBeforeUtc is { } retryNotBeforeUtc)
        {
            return $"Waiting for provider retry window on issue #{leadJob.IssueNumber} until {retryNotBeforeUtc:O}.";
        }

        if (interventionTarget is null)
        {
            return null;
        }

        return interventionTarget.Kind switch
        {
            "github-replay-drift" => workerState switch
            {
                "running" when ReplayWasDeferred(latestGithubReplay) => "Deferring pending GitHub replay while provider backoff remains in effect.",
                "waiting" when ReplayWasDeferred(latestGithubReplay) => "Waiting to replay pending GitHub updates after provider backoff clears.",
                "complete" when ReplayWasDeferred(latestGithubReplay) => "Completed the current run with GitHub replay intentionally deferred during provider backoff.",
                "running" when pendingGithubSyncRetryOverdueMinutes > 0 => "Prioritizing overdue GitHub writeback replay before ordinary implementation work.",
                "waiting" when pendingGithubSyncRetryOverdueMinutes > 0 => "Waiting to prioritize overdue GitHub writeback replay on the next GitHub pass.",
                "complete" when pendingGithubSyncRetryOverdueMinutes > 0 => "Completed the current run with overdue GitHub writeback replay still pending.",
                "running" => "Replaying pending GitHub updates before the next GitHub pass.",
                "waiting" => "Waiting to replay pending GitHub updates on the next GitHub pass.",
                "complete" => "Completed the current run with pending GitHub updates still queued for replay.",
                _ => interventionTarget.Summary
            },
            "recovery-work" => workerState switch
            {
                "running" => "Draining queued recovery work before ordinary implementation.",
                "waiting" => "Waiting to continue queued recovery work on the next pass.",
                "complete" => "Completed the current run with recovery work still prioritized.",
                _ => interventionTarget.Summary
            },
            "implementation" => workerState switch
            {
                "running" => "Advancing the lead implementation target.",
                "waiting" => "Waiting to advance the lead implementation target on the next pass.",
                "complete" => "Completed the current run with queued implementation still remaining.",
                _ => interventionTarget.Summary
            },
            "operator-escalation" => workerState switch
            {
                "running" when interventionTarget.Acknowledged => "Tracking an already-acknowledged operator escalation while the critical target remains unresolved.",
                "waiting" when interventionTarget.Acknowledged => "Waiting to continue tracking an already-acknowledged operator escalation on the next pass.",
                "complete" when interventionTarget.Acknowledged => "Completed the current run while continuing to track an acknowledged operator escalation.",
                "running" => "Preparing an operator-facing escalation summary for a persistent critical target.",
                "waiting" => "Waiting to prepare an operator-facing escalation summary on the next pass.",
                "complete" => "Completed the current run with operator escalation follow-up still queued.",
                _ => interventionTarget.Summary
            },
            "queued-work" => workerState switch
            {
                "running" => "Advancing the next queued worker follow-up.",
                "waiting" => "Waiting to advance the next queued worker follow-up.",
                "complete" => "Completed the current run with queued follow-up work still remaining.",
                _ => interventionTarget.Summary
            },
            "idle" => workerState switch
            {
                "complete" => "Completed the current run with no immediate intervention target.",
                "waiting" => "No immediate intervention target is queued for the next pass.",
                _ => null
            },
            _ => interventionTarget.Summary
        };
    }

    private static bool ReplayWasDeferred(LatestGithubReplaySnapshot? latestGithubReplay) =>
        latestGithubReplay is not null &&
        latestGithubReplay.AttemptedCount == 0 &&
        latestGithubReplay.UpdatedCount == 0 &&
        latestGithubReplay.FailedCount == 0 &&
        latestGithubReplay.Summary.Contains("provider backoff", StringComparison.OrdinalIgnoreCase);

    internal static string? BuildInterventionEscalationNote(InterventionTargetSnapshot? interventionTarget)
    {
        if (interventionTarget is null || string.IsNullOrWhiteSpace(interventionTarget.Escalation))
        {
            return null;
        }

        return interventionTarget.Escalation switch
        {
            "critical" when interventionTarget.Acknowledged && interventionTarget.AcknowledgedStreak > 1
                => $"Escalation: global intervention target remains critical after acknowledgment for {interventionTarget.AcknowledgedStreak} consecutive status snapshots. {interventionTarget.Summary}",
            "critical" when interventionTarget.Acknowledged
                => $"Escalation: global intervention target remains critical after acknowledgment. {interventionTarget.Summary}",
            "critical"
                => $"Escalation: global intervention target is critical. {interventionTarget.Summary}",
            "warning"
                => $"Escalation: global intervention target is aging and should be reviewed soon. {interventionTarget.Summary}",
            _ => null
        };
    }

    private static string BuildInterventionTargetSignature(InterventionTargetSnapshot interventionTarget) =>
        string.Join(
            "|",
            interventionTarget.Kind,
            interventionTarget.IssueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            interventionTarget.RecoveryIssueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            interventionTarget.PendingGithubSyncIssueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            interventionTarget.TargetArtifact ?? string.Empty,
            interventionTarget.TargetOutcome ?? string.Empty);

    private static string? FormatLeadQuarantineAge(LeadQuarantineSnapshot? leadQuarantine)
    {
        if (leadQuarantine?.OldestPendingGithubSyncAt is null)
        {
            return null;
        }

        var elapsed = DateTimeOffset.UtcNow >= leadQuarantine.OldestPendingGithubSyncAt.Value
            ? DateTimeOffset.UtcNow - leadQuarantine.OldestPendingGithubSyncAt.Value
            : TimeSpan.Zero;

        return $"{FormatElapsed(elapsed)} old";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{Math.Floor(elapsed.TotalHours)}h {elapsed.Minutes}m";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{Math.Floor(elapsed.TotalMinutes)}m {elapsed.Seconds}s";
        }

        return $"{Math.Max(0, elapsed.Seconds)}s";
    }

    private static LeadQuarantineSnapshot? AnnotateLeadQuarantine(
        LeadQuarantineSnapshot? leadQuarantine,
        IReadOnlyList<PendingGithubSyncSnapshot> pendingGithubSync)
    {
        if (leadQuarantine is null)
        {
            return null;
        }

        var hasPendingGithubSync = pendingGithubSync.Any(item =>
            item.IssueNumber == leadQuarantine.IssueNumber ||
            (leadQuarantine.RecoveryIssueNumber is not null && item.IssueNumber == leadQuarantine.RecoveryIssueNumber.Value));

        if (hasPendingGithubSync)
        {
            return leadQuarantine with
            {
                State = "sync-drift",
                Summary = leadQuarantine.RecoveryIssueNumber is not null
                    ? $"Recovery for issue #{leadQuarantine.IssueNumber} is active, but GitHub updates for recovery #{leadQuarantine.RecoveryIssueNumber.Value} are still queued for retry."
                    : $"Recovery for issue #{leadQuarantine.IssueNumber} is active, but GitHub updates are still queued for retry."
            };
        }

        if (leadQuarantine.RecoveryIssueNumber is not null)
        {
            return leadQuarantine with
            {
                State = "recovery-active",
                Summary = $"Recovery issue #{leadQuarantine.RecoveryIssueNumber.Value} is actively draining work for parent issue #{leadQuarantine.IssueNumber}."
            };
        }

        return leadQuarantine with
        {
            State = "parent-active",
            Summary = $"Parent issue #{leadQuarantine.IssueNumber} still has queued recovery work."
        };
    }

    private static RecentLoopSignalSnapshot BuildRecentLoopSignal(
        int queuedJobs,
        string health,
        LatestActivitySnapshot? latestActivity,
        LeadQuarantineSnapshot? leadQuarantine,
        LatestGithubReplaySnapshot? latestGithubReplay,
        InterventionTargetSnapshot? interventionTarget,
        int pendingGithubSyncRetryOverdueMinutes)
    {
        if (string.Equals(health, "blocked", StringComparison.OrdinalIgnoreCase) && leadQuarantine is not null)
        {
            var recoverySuffix = leadQuarantine.RecoveryIssueNumber is not null
                ? $" via recovery #{leadQuarantine.RecoveryIssueNumber.Value}"
                : string.Empty;
            var leadRecoveryAge = FormatLeadQuarantineAge(leadQuarantine);
            return new RecentLoopSignalSnapshot(
                "blocked",
                $"Loop is blocked by recovery work for issue #{leadQuarantine.IssueNumber}{recoverySuffix}" +
                $"{(leadRecoveryAge is not null ? $" with oldest writeback drift {leadRecoveryAge}" : string.Empty)}.");
        }

        if (string.Equals(health, "attention", StringComparison.OrdinalIgnoreCase) && latestActivity is not null)
        {
            if (string.Equals(interventionTarget?.Kind, "github-replay-drift", StringComparison.OrdinalIgnoreCase) &&
                pendingGithubSyncRetryOverdueMinutes > 0)
            {
                return new RecentLoopSignalSnapshot(
                    "repairing",
                    $"Loop is prioritizing overdue GitHub writeback replay after issue #{latestActivity.IssueNumber}.");
            }

            return new RecentLoopSignalSnapshot("failing", $"Recent activity needs review after issue #{latestActivity.IssueNumber}.");
        }

        if (ReplayWasDeferred(latestGithubReplay))
        {
            return new RecentLoopSignalSnapshot(
                "waiting",
                "Loop is intentionally deferring pending GitHub replay while provider backoff remains active.");
        }

        if (queuedJobs == 0 && ReplayCountsAsWork(latestGithubReplay))
        {
            return new RecentLoopSignalSnapshot(
                "repairing",
                latestGithubReplay!.Summary);
        }

        if (queuedJobs > 0 &&
            string.Equals(interventionTarget?.Kind, "operator-escalation", StringComparison.OrdinalIgnoreCase) &&
            latestActivity is not null)
        {
            return interventionTarget!.Acknowledged
                ? new RecentLoopSignalSnapshot("monitoring", $"Loop is tracking acknowledged operator escalation after issue #{latestActivity.IssueNumber}.")
                : new RecentLoopSignalSnapshot("escalating", $"Loop is actively escalating operator follow-up after issue #{latestActivity.IssueNumber}.");
        }

        if (queuedJobs > 0 &&
            string.Equals(interventionTarget?.Kind, "operator-escalation", StringComparison.OrdinalIgnoreCase))
        {
            return interventionTarget!.Acknowledged
                ? new RecentLoopSignalSnapshot("monitoring", "Loop is tracking acknowledged operator escalation.")
                : new RecentLoopSignalSnapshot("escalating", "Loop is actively escalating operator follow-up.");
        }

        if (queuedJobs > 0)
        {
            return latestActivity is not null
                ? new RecentLoopSignalSnapshot("draining", $"Loop is actively draining queued work after issue #{latestActivity.IssueNumber}.")
                : new RecentLoopSignalSnapshot("draining", "Loop is actively draining queued work.");
        }

        if (latestActivity is null)
        {
            return new RecentLoopSignalSnapshot("idle", "No recorded executions yet.");
        }

        return new RecentLoopSignalSnapshot("idle", $"Loop reached idle after issue #{latestActivity.IssueNumber}.");
    }

    public static LatestPassSummary BuildLatestPassSummary(int passNumber, RunUntilIdleResult result, LatestGithubReplaySnapshot? latestGithubReplay = null)
    {
        var seededCycles = result.Cycles.Count(cycle => string.Equals(cycle.Mode, "seed", StringComparison.OrdinalIgnoreCase));
        var consumedCycles = result.Cycles.Count(cycle => string.Equals(cycle.Mode, "consume", StringComparison.OrdinalIgnoreCase));
        var operatorEscalationQueuedCount = result.Cycles.Sum(cycle => cycle.FollowUps.Count(IsOperatorEscalationJob));
        var operatorEscalationConsumedCount = result.Cycles.Count(cycle => cycle.Job is not null && IsOperatorEscalationJob(cycle.Job));

        return new LatestPassSummary(
            passNumber,
            result.Cycles.Count,
            seededCycles,
            consumedCycles,
            result.ReachedIdle,
            result.ReachedMaxCycles,
            latestGithubReplay?.AttemptedCount ?? 0,
            latestGithubReplay?.UpdatedCount ?? 0,
            latestGithubReplay?.FailedCount ?? 0,
            latestGithubReplay?.Summary,
            operatorEscalationQueuedCount,
            operatorEscalationConsumedCount
        );
    }

    private static bool IsOperatorEscalationJob(SelfBuildJob job) =>
        string.Equals(job.Metadata.GetValueOrDefault("workType"), "operator-escalation", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ReadRetryNotBeforeUtc(SelfBuildJob job)
    {
        return job.Metadata.TryGetValue("retryNotBeforeUtc", out var rawValue) &&
            DateTimeOffset.TryParse(rawValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out var retryNotBeforeUtc)
                ? retryNotBeforeUtc
                : null;
    }
}

public sealed record CycleResult(
    string Mode,
    SelfBuildJob? Job,
    JobExecutionResult? Execution,
    IReadOnlyList<SelfBuildJob> FollowUps,
    IssueWorkflowState? Workflow = null,
    GithubSyncResult? GithubSync = null,
    ExecutionRecord? ExecutionRecord = null,
    FailureDisposition? FailureDisposition = null
);

public sealed record RunUntilIdleResult(
    IReadOnlyList<CycleResult> Cycles,
    bool ReachedIdle,
    bool ReachedMaxCycles
);

public sealed record PollingRunResult(
    IReadOnlyList<RunUntilIdleResult> Passes,
    int ConsecutiveIdlePasses,
    bool ReachedIdleThreshold,
    bool ReachedMaxPasses
);

public sealed record StatusSnapshot(
    DateTimeOffset GeneratedAt,
    string Source,
    string LastCommand,
    string WorkerMode,
    string WorkerState,
    string? WorkerCompletionReason,
    DateTimeOffset? NextPollAt,
    int? PollIntervalSeconds,
    int IdleStreak,
    int IdleTarget,
    int? IdlePassesRemaining,
    int? PassBudgetRemaining,
    int? CurrentPassNumber,
    int? MaxPasses,
    string Health,
    string AttentionSummary,
    StatusRollup Rollup,
    LeadJobSnapshot? LeadJob,
    LeadQuarantineSnapshot? LeadQuarantine,
    LatestActivitySnapshot? LatestActivity,
    RecentLoopSignalSnapshot RecentLoopSignal,
    string QueueDirection,
    int QueueDelta,
    DateTimeOffset? QueueComparedAt,
    StatusRollupDelta RollupDelta,
    int QueuedJobs,
    IReadOnlyList<IssueStatusSnapshot> Issues,
    LatestPassSummary? LatestPass = null,
    LatestGithubSyncSnapshot? LatestGithubSync = null,
    LatestGithubReplaySnapshot? LatestGithubReplay = null,
    int PendingGithubSyncCount = 0,
    IReadOnlyList<PendingGithubSyncSnapshot>? PendingGithubSync = null,
    string? WorkerActivity = null,
    string? PendingGithubSyncSummary = null,
    InterventionTargetSnapshot? InterventionTarget = null,
    string? InterventionEscalationNote = null,
    int InterventionEscalationStreak = 0,
    string? NextWakeReason = null,
    DateTimeOffset? NextDelayedRetryAt = null,
    string? DelayedRetryUrgency = null,
    string? DelayedRetrySummary = null,
    string? WaitSignal = null,
    DateTimeOffset? PendingGithubSyncNextRetryAt = null,
    string? PendingGithubSyncRetryState = null,
    int PendingGithubSyncRetryOverdueMinutes = 0,
    string? ReplayPriorityReason = null,
    string? ReplayPrioritySummary = null
);

public sealed record LatestPassSummary(
    int PassNumber,
    int CycleCount,
    int SeededCycles,
    int ConsumedCycles,
    bool ReachedIdle,
    bool ReachedMaxCycles,
    int GithubReplayAttemptedCount = 0,
    int GithubReplayUpdatedCount = 0,
    int GithubReplayFailedCount = 0,
    string? GithubReplaySummary = null,
    int OperatorEscalationQueuedCount = 0,
    int OperatorEscalationConsumedCount = 0
);

public sealed record StatusRollup(
    int FailedIssues,
    int QuarantinedIssues,
    int ActionableQuarantinedIssues,
    int InactiveQuarantinedIssues,
    int InProgressIssues,
    int ValidatedIssues
);

public sealed record StatusRollupDelta(
    int FailedIssues,
    int QuarantinedIssues,
    int InProgressIssues,
    int ValidatedIssues
);

public sealed record LeadJobSnapshot(
    int IssueNumber,
    string IssueTitle,
    string Agent,
    string Action,
    string? TargetArtifact,
    string? TargetOutcome,
    string? Priority,
    bool Blocking,
    string? WorkType,
    DateTimeOffset? RetryNotBeforeUtc = null,
    bool Delayed = false
);

public sealed record LeadQuarantineSnapshot(
    int IssueNumber,
    string IssueTitle,
    string? Note,
    int QueuedRecoveryJobs,
    int? RecoveryIssueNumber,
    string? RecoveryIssueTitle,
    string? State = null,
    string? Summary = null,
    DateTimeOffset? OldestPendingGithubSyncAt = null
);

public sealed record LatestActivitySnapshot(
    int IssueNumber,
    string IssueTitle,
    string CurrentStage,
    string? Summary,
    DateTimeOffset RecordedAt
);

public sealed record RecentLoopSignalSnapshot(
    string Mode,
    string Summary
);

public sealed record LatestGithubSyncSnapshot(
    int IssueNumber,
    bool Attempted,
    bool Updated,
    string Summary,
    DateTimeOffset RecordedAt
);

public sealed record LatestGithubReplaySnapshot(
    int AttemptedCount,
    int UpdatedCount,
    int FailedCount,
    string Summary,
    DateTimeOffset RecordedAt
);

public sealed record PendingGithubSyncSnapshot(
    int IssueNumber,
    string Summary,
    DateTimeOffset RecordedAt,
    int AttemptCount = 1,
    DateTimeOffset? LastAttemptedAt = null,
    DateTimeOffset? NextRetryAt = null
);

public sealed record InterventionTargetSnapshot(
    string Kind,
    string Summary,
    int? IssueNumber = null,
    int? RecoveryIssueNumber = null,
    int? PendingGithubSyncIssueNumber = null,
    string? TargetArtifact = null,
    string? TargetOutcome = null,
    DateTimeOffset? ObservedAt = null,
    string? AgeSummary = null,
    string? Escalation = null,
    bool Acknowledged = false,
    int AcknowledgedStreak = 0
);

public static class StatusSnapshotTrend
{
    public static StatusSnapshot Apply(StatusSnapshot current, StatusSnapshot? previous)
    {
        var interventionEscalationStreak = ComputeInterventionEscalationStreak(current, previous);
        var interventionAcknowledgedStreak = ComputeInterventionAcknowledgedStreak(current, previous);
        var interventionTarget = current.InterventionTarget is null
            ? null
            : current.InterventionTarget with { AcknowledgedStreak = interventionAcknowledgedStreak };
        var interventionEscalationNote = BuildPersistedInterventionEscalationNote(current.InterventionEscalationNote, interventionTarget, interventionEscalationStreak);

        if (previous is null)
        {
            return current with
            {
                QueueDirection = "unknown",
                QueueDelta = 0,
                QueueComparedAt = null,
                RollupDelta = new StatusRollupDelta(0, 0, 0, 0),
                InterventionTarget = interventionTarget,
                InterventionEscalationStreak = interventionEscalationStreak,
                InterventionEscalationNote = interventionEscalationNote
            };
        }

        var delta = current.QueuedJobs - previous.QueuedJobs;
        var direction = delta switch
        {
            > 0 => "up",
            < 0 => "down",
            _ => "flat"
        };

        return current with
        {
            QueueDirection = direction,
            QueueDelta = delta,
            QueueComparedAt = previous.GeneratedAt,
            RollupDelta = new StatusRollupDelta(
                current.Rollup.FailedIssues - previous.Rollup.FailedIssues,
                current.Rollup.QuarantinedIssues - previous.Rollup.QuarantinedIssues,
                current.Rollup.InProgressIssues - previous.Rollup.InProgressIssues,
                current.Rollup.ValidatedIssues - previous.Rollup.ValidatedIssues
            ),
            InterventionTarget = interventionTarget,
            InterventionEscalationStreak = interventionEscalationStreak,
            InterventionEscalationNote = interventionEscalationNote
        };
    }

    private static int ComputeInterventionEscalationStreak(StatusSnapshot current, StatusSnapshot? previous)
    {
        if (!string.Equals(current.InterventionTarget?.Escalation, "critical", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (previous is null ||
            !string.Equals(previous.InterventionTarget?.Escalation, "critical", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var sameKind = string.Equals(current.InterventionTarget?.Kind, previous.InterventionTarget?.Kind, StringComparison.OrdinalIgnoreCase);
        var sameIssue = current.InterventionTarget?.IssueNumber == previous.InterventionTarget?.IssueNumber;
        var sameRecovery = current.InterventionTarget?.RecoveryIssueNumber == previous.InterventionTarget?.RecoveryIssueNumber;
        var samePendingSync = current.InterventionTarget?.PendingGithubSyncIssueNumber == previous.InterventionTarget?.PendingGithubSyncIssueNumber;

        if (sameKind && sameIssue && sameRecovery && samePendingSync)
        {
            return Math.Max(1, previous.InterventionEscalationStreak) + 1;
        }

        return 1;
    }

    private static int ComputeInterventionAcknowledgedStreak(StatusSnapshot current, StatusSnapshot? previous)
    {
        if (current.InterventionTarget is null || !current.InterventionTarget.Acknowledged)
        {
            return 0;
        }

        if (previous?.InterventionTarget is null || !previous.InterventionTarget.Acknowledged)
        {
            return 1;
        }

        var sameKind = string.Equals(current.InterventionTarget.Kind, previous.InterventionTarget.Kind, StringComparison.OrdinalIgnoreCase);
        var sameIssue = current.InterventionTarget.IssueNumber == previous.InterventionTarget.IssueNumber;
        var sameRecovery = current.InterventionTarget.RecoveryIssueNumber == previous.InterventionTarget.RecoveryIssueNumber;
        var samePendingSync = current.InterventionTarget.PendingGithubSyncIssueNumber == previous.InterventionTarget.PendingGithubSyncIssueNumber;

        if (sameKind && sameIssue && sameRecovery && samePendingSync)
        {
            return Math.Max(1, previous.InterventionTarget.AcknowledgedStreak) + 1;
        }

        return 1;
    }

    private static string? BuildPersistedInterventionEscalationNote(
        string? currentNote,
        InterventionTargetSnapshot? interventionTarget,
        int interventionEscalationStreak)
    {
        var baseNote = SelfBuildLoop.BuildInterventionEscalationNote(interventionTarget) ?? currentNote;

        if (string.IsNullOrWhiteSpace(baseNote))
        {
            return baseNote;
        }

        if (!string.Equals(interventionTarget?.Escalation, "critical", StringComparison.OrdinalIgnoreCase) ||
            interventionEscalationStreak <= 1)
        {
            return baseNote;
        }

        return $"{baseNote} Persisting across {interventionEscalationStreak} consecutive status snapshots.";
    }
}

public sealed record IssueStatusSnapshot(
    int IssueNumber,
    string IssueTitle,
    string OverallStatus,
    string CurrentStage,
    int QueuedJobCount,
    string? WorkflowNote,
    string? LatestExecutionSummary,
    string? LatestExecutionNotes,
    DateTimeOffset? LatestExecutionRecordedAt
);

public sealed record ReleasedQuarantineIssue(
    int IssueNumber,
    string IssueTitle,
    string Agent,
    string Action,
    IReadOnlyList<string> ChangedPaths,
    GithubSyncResult? GithubSync
);

public sealed record QuarantineReleaseResult(
    IReadOnlyList<ReleasedQuarantineIssue> ReleasedIssues)
{
    public int ReleasedCount => ReleasedIssues.Count;
}
