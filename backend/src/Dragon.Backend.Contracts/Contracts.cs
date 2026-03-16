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
    string? SourceFile = null
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
    DateTimeOffset ObservedAt
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
    DateTimeOffset UpdatedAt
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
