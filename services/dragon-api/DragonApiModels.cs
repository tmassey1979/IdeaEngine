namespace Dragon.Api;

public sealed record DashboardResponse(
    string Health,
    string AttentionSummary,
    string ServicesHealthyLabel,
    DashboardTelemetryResponse? Telemetry,
    string? WaitSignal,
    string? RecentLoopSummary,
    string QueueSummary,
    int ActiveProjectCount,
    string SourceStatus,
    IReadOnlyList<ServiceResponse> Services,
    string? LeadWorkLabel
);

public sealed record DashboardTelemetryResponse(
    string Status,
    double? ProcessorLoadPercent,
    double? MemoryUsedPercent,
    string? Summary
);

public sealed record ServiceResponse(
    string Name,
    string Status,
    string Summary
);

public sealed record IdeaListItemResponse(
    string Id,
    string Title,
    string Status,
    string SourceOverallStatus,
    string Phase,
    string QueuePositionLabel,
    string EtaLabel,
    string Summary,
    bool IsActive,
    bool IsBlocked,
    bool CanFix,
    DateTimeOffset? LatestExecutionRecordedAt
);

public sealed record IdeaDetailResponse(
    string Id,
    string Title,
    string Status,
    string SourceOverallStatus,
    string Phase,
    string QueuePositionLabel,
    string EtaLabel,
    string Summary,
    IReadOnlyList<string> Blockers,
    string PreferredStackLabel,
    bool CanFix,
    IReadOnlyList<StageActivityResponse> Activity,
    ListPanelResponse BacklogPanel,
    BoardPanelResponse BoardPanel,
    ActivityPanelResponse ActivityPanel,
    DateTimeOffset? LatestExecutionRecordedAt
);

public sealed record IdeaFixRequest(
    string? OperatorInput
);

public sealed record IdeaFixResponse(
    string Id,
    string Title,
    string Agent,
    string Action,
    bool Queued,
    string Message,
    string? OperatorInput
);

public sealed record StageActivityResponse(
    string Stage,
    string Status,
    DateTimeOffset? ObservedAt,
    string? Summary
);

public sealed record ListPanelResponse(
    string State,
    string Summary,
    IReadOnlyList<PanelItemResponse> Items
);

public sealed record BoardPanelResponse(
    string State,
    string Summary,
    IReadOnlyList<BoardColumnResponse> Columns
);

public sealed record BoardColumnResponse(
    string Id,
    string Title,
    IReadOnlyList<PanelItemResponse> Cards
);

public sealed record PanelItemResponse(
    string Id,
    string Title,
    string Status,
    string? Summary
);

public sealed record ActivityPanelResponse(
    string State,
    string Summary,
    IReadOnlyList<ActivityEntryResponse> Entries
);

public sealed record ActivityEntryResponse(
    string Id,
    string Title,
    string Status,
    string Summary,
    DateTimeOffset? RecordedAt
);
