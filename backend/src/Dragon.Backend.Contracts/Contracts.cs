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
    string SourceFile
);

public sealed record GithubIssue(
    int Number,
    string Title,
    string State,
    IReadOnlyList<string> Labels,
    string Body = "",
    string? Heading = null,
    string? SourceFile = null,
    int? SourceIssueNumber = null
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
    IReadOnlyList<RequestedFollowUp>? RequestedFollowUps = null
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
    IReadOnlyList<string> FollowUpAgents
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
    string Agent,
    string Action,
    string? Priority = null,
    string? Reason = null
);

public sealed record AgentModelProviderDescriptor(
    string Name,
    string Transport,
    string DefaultModel,
    string ApiKeyEnvironmentVariable,
    string Notes
);
