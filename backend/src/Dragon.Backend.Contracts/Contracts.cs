namespace Dragon.Backend.Contracts;

public sealed record DeveloperOperation(
    string Type,
    string Path,
    string? Content = null,
    string? SearchText = null,
    string? ReplaceWith = null
);

public sealed record BacklogStoryMetadata(
    string Title,
    string Heading,
    string SourceFile,
    IReadOnlyList<string>? TechnicalDetails = null
);

public sealed record GithubIssue(
    int Number,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    string Body = "",
    string? Heading = null,
    string? SourceFile = null,
    int? SourceIssueNumber = null,
    IReadOnlyList<string>? TechnicalDetails = null
);

public sealed record SelfBuildJobPayload(
    string Title,
    IReadOnlyList<string> Labels,
    string? Heading,
    string? SourceFile,
    IReadOnlyList<DeveloperOperation>? Operations
);

public sealed record SelfBuildJob(
    string Agent,
    string Action,
    string Repo,
    string Project,
    int Issue,
    SelfBuildJobPayload Payload,
    IReadOnlyDictionary<string, string> Metadata
);

public sealed record JobExecutionResult(
    string JobId,
    string Agent,
    string Status,
    string Summary,
    DateTimeOffset ObservedAt,
    IReadOnlyList<string>? ChangedPaths = null,
    IReadOnlyList<RequestedFollowUp>? RequestedFollowUps = null,
    DateTimeOffset? RetryNotBefore = null,
    long? DurationMilliseconds = null
);

public sealed record WorkflowStageState(
    string Status,
    string? JobId,
    DateTimeOffset? ObservedAt,
    string? Summary
);

public sealed record IssueWorkflowState(
    int IssueNumber,
    string IssueTitle,
    string OverallStatus,
    IReadOnlyDictionary<string, WorkflowStageState> Stages,
    DateTimeOffset UpdatedAt,
    string? Note = null,
    int? SourceIssueNumber = null,
    IReadOnlyList<int>? ActiveRecoveryIssueNumbers = null
);

public sealed record FailureDisposition(
    bool Quarantined,
    string? Reason
);

public sealed record GithubSyncResult(
    bool Attempted,
    bool Updated,
    string Summary
);

public sealed record ExecutionRecord(
    int IssueNumber,
    string IssueTitle,
    string JobAgent,
    string JobAction,
    string JobId,
    string Status,
    string Summary,
    DateTimeOffset RecordedAt,
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<string> FollowUpAgents,
    string Notes = "",
    long? DurationMilliseconds = null,
    int RetryCount = 0,
    double? QualityScore = null,
    string? HostTelemetryStatus = null,
    double? ProcessorLoadPercent = null,
    double? MemoryUsedPercent = null,
    double? DiskUsedPercent = null
);

public sealed record AgentModelMessage(
    string Role,
    string Content
);

public sealed record AgentModelRequest(
    string Agent,
    string Purpose,
    string Model,
    string? Instructions,
    IReadOnlyList<AgentModelMessage> Messages,
    IReadOnlyDictionary<string, string>? Metadata = null,
    bool Background = false
);

public sealed record AgentModelResponse(
    string Provider,
    string Model,
    string ResponseId,
    string OutputText,
    string? FinishReason = null
);

public sealed record AgentStructuredResult(
    string Summary,
    string? Recommendation = null,
    IReadOnlyList<string>? Artifacts = null,
    IReadOnlyList<DeveloperOperation>? Operations = null,
    IReadOnlyList<RequestedFollowUp>? FollowUps = null
);

public sealed record RequestedFollowUp(
    string? Agent,
    string? Action,
    string? Priority = null,
    string? Reason = null,
    bool Blocking = false,
    string? TargetArtifact = null,
    string? TargetOutcome = null
);

public sealed record AgentModelProviderDescriptor(
    string Name,
    string Transport,
    string DefaultModel,
    string ApiKeyEnvironmentVariable,
    string Notes
);

public sealed record BackendDashboardReadModel(
    string Health,
    string AttentionSummary,
    string WorkerMode,
    string WorkerState,
    int QueuedJobs,
    IReadOnlyDictionary<string, int> Rollup,
    string? WaitSignal,
    string? RecentLoopSummary,
    BackendLeadJobReadModel? LeadJob,
    BackendTelemetryReadModel? HostTelemetry,
    IReadOnlyList<BackendServiceReadModel> Services,
    string SourceStatus
);

public sealed record BackendLeadJobReadModel(
    int IssueNumber,
    string IssueTitle,
    string Agent,
    string Action,
    string? TargetArtifact,
    string? ImplementationProfile
);

public sealed record BackendTelemetryReadModel(
    string Status,
    int? ProcessorCount,
    double? ProcessorLoadPercent,
    long? MemoryTotalMb,
    long? MemoryAvailableMb,
    double? MemoryUsedPercent,
    string? Summary
);

public sealed record BackendServiceReadModel(
    string Name,
    string Status,
    string Summary
);

public sealed record BackendIssueReadModel(
    string Id,
    string Title,
    string OverallStatus,
    string CurrentStage,
    int QueuedJobCount,
    string? WorkflowNote,
    string? LatestExecutionSummary,
    DateTimeOffset? LatestExecutionRecordedAt
);

public sealed record BackendIssueDetailReadModel(
    string Id,
    string Title,
    string OverallStatus,
    string CurrentStage,
    int QueuedJobCount,
    string? WorkflowNote,
    string? LatestExecutionSummary,
    DateTimeOffset? LatestExecutionRecordedAt,
    IReadOnlyList<string> Blockers,
    string PreferredStackLabel,
    IReadOnlyList<BackendStageActivityReadModel> Activity,
    BackendListPanelReadModel BacklogPanel,
    BackendBoardPanelReadModel BoardPanel,
    BackendActivityPanelReadModel ActivityPanel
);

public sealed record BackendIssueFixRequest(
    string? OperatorInput,
    string? Actor = null
);

public sealed record BackendIssueFixResponse(
    string Id,
    string Title,
    string Agent,
    string Action,
    bool Queued,
    string Message,
    string? OperatorInput
);

public sealed record BackendStageActivityReadModel(
    string Stage,
    string Status,
    DateTimeOffset? ObservedAt,
    string? Summary
);

public sealed record BackendListPanelReadModel(
    string State,
    string Summary,
    IReadOnlyList<BackendPanelItemReadModel> Items
);

public sealed record BackendBoardPanelReadModel(
    string State,
    string Summary,
    IReadOnlyList<BackendBoardColumnReadModel> Columns
);

public sealed record BackendBoardColumnReadModel(
    string Id,
    string Title,
    IReadOnlyList<BackendPanelItemReadModel> Cards
);

public sealed record BackendPanelItemReadModel(
    string Id,
    string Title,
    string Status,
    string? Summary
);

public sealed record BackendActivityPanelReadModel(
    string State,
    string Summary,
    IReadOnlyList<BackendActivityEntryReadModel> Entries
);

public sealed record BackendActivityEntryReadModel(
    string Id,
    string Title,
    string Status,
    string Summary,
    DateTimeOffset? RecordedAt
);

public sealed record BackendAgentPerformanceReadModel(
    DateTimeOffset GeneratedAt,
    string Summary,
    IReadOnlyList<BackendAgentMetricReadModel> Agents
);

public sealed record BackendAgentMetricReadModel(
    string Agent,
    int TotalExecutions,
    int SuccessCount,
    int FailureCount,
    double SuccessRate,
    double ErrorFrequency,
    double AverageDurationMilliseconds,
    double AverageQualityScore,
    double AverageRetryCount,
    double? AverageProcessorLoadPercent,
    double? AverageMemoryUsedPercent,
    double? AverageDiskUsedPercent,
    DateTimeOffset? LastRecordedAt,
    string Summary
);

public sealed record BackendAuditLogReadModel(
    DateTimeOffset GeneratedAt,
    string Summary,
    IReadOnlyList<BackendAuditLogEntryReadModel> Entries
);

public sealed record BackendAuditLogEntryReadModel(
    string Id,
    string Actor,
    string Action,
    string Project,
    int? IssueNumber,
    string Details,
    string? Source,
    DateTimeOffset RecordedAt
);

public sealed record BackendContinuousMonitoringReadModel(
    DateTimeOffset GeneratedAt,
    string Summary,
    IReadOnlyList<BackendContinuousMonitoringFindingReadModel> Findings
);

public sealed record BackendContinuousMonitoringFindingReadModel(
    string Id,
    string Category,
    string Severity,
    string Status,
    string Project,
    int? IssueNumber,
    string Summary,
    string Recommendation,
    bool TriggerAutomatedUpdate,
    DateTimeOffset RecordedAt,
    DateTimeOffset LastObservedAt
);

public sealed record BackendMonitoringFindingUpsertRequest(
    string Category,
    string Severity,
    string Status,
    string Project,
    int? IssueNumber,
    string Summary,
    string Recommendation,
    bool TriggerAutomatedUpdate = false
);

public sealed record BackendMonitoringFindingUpsertResponse(
    string Id,
    string Category,
    string Severity,
    string Status,
    string Project,
    int? IssueNumber,
    bool TriggerAutomatedUpdate,
    bool AutomatedRemediationQueued,
    string Message
);
