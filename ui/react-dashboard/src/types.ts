export type ServiceResponse = {
  name: string;
  status: string;
  summary: string;
};

export type DashboardTelemetryResponse = {
  status: string;
  processorLoadPercent?: number | null;
  memoryUsedPercent?: number | null;
  summary?: string | null;
};

export type DashboardResponse = {
  health: string;
  attentionSummary: string;
  servicesHealthyLabel: string;
  telemetry?: DashboardTelemetryResponse | null;
  waitSignal?: string | null;
  recentLoopSummary?: string | null;
  queueSummary: string;
  activeProjectCount: number;
  sourceStatus: string;
  services: ServiceResponse[];
  leadWorkLabel?: string | null;
};

export type AgentMetricResponse = {
  agent: string;
  totalExecutions: number;
  successCount: number;
  failureCount: number;
  successRate: number;
  errorFrequency: number;
  averageDurationMilliseconds: number;
  averageQualityScore: number;
  averageRetryCount: number;
  averageProcessorLoadPercent?: number | null;
  averageMemoryUsedPercent?: number | null;
  averageDiskUsedPercent?: number | null;
  lastRecordedAt?: string | null;
  summary: string;
};

export type AgentPerformanceResponse = {
  generatedAt: string;
  summary: string;
  agents: AgentMetricResponse[];
};

export type AuditLogEntryResponse = {
  id: string;
  actor: string;
  action: string;
  project: string;
  issueNumber?: number | null;
  details: string;
  source?: string | null;
  recordedAt: string;
};

export type AuditLogResponse = {
  generatedAt: string;
  summary: string;
  entries: AuditLogEntryResponse[];
};

export type ContinuousMonitoringFindingResponse = {
  id: string;
  category: string;
  severity: string;
  status: string;
  project: string;
  issueNumber?: number | null;
  summary: string;
  recommendation: string;
  triggerAutomatedUpdate: boolean;
  recordedAt: string;
  lastObservedAt: string;
};

export type ContinuousMonitoringResponse = {
  generatedAt: string;
  summary: string;
  findings: ContinuousMonitoringFindingResponse[];
};

export type IdeaListItemResponse = {
  id: string;
  title: string;
  status: string;
  sourceOverallStatus: string;
  phase: string;
  queuePositionLabel: string;
  etaLabel: string;
  summary: string;
  isActive: boolean;
  isBlocked: boolean;
  canFix: boolean;
  latestExecutionRecordedAt?: string | null;
};

export type StageActivityResponse = {
  stage: string;
  status: string;
  observedAt?: string | null;
  summary?: string | null;
};

export type PanelItemResponse = {
  id: string;
  title: string;
  status: string;
  summary?: string | null;
};

export type ListPanelResponse = {
  state: string;
  summary: string;
  items: PanelItemResponse[];
};

export type BoardColumnResponse = {
  id: string;
  title: string;
  cards: PanelItemResponse[];
};

export type BoardPanelResponse = {
  state: string;
  summary: string;
  columns: BoardColumnResponse[];
};

export type ActivityEntryResponse = {
  id: string;
  title: string;
  status: string;
  summary: string;
  recordedAt?: string | null;
};

export type ActivityPanelResponse = {
  state: string;
  summary: string;
  entries: ActivityEntryResponse[];
};

export type IdeaDetailResponse = {
  id: string;
  title: string;
  status: string;
  sourceOverallStatus: string;
  phase: string;
  queuePositionLabel: string;
  etaLabel: string;
  summary: string;
  blockers: string[];
  preferredStackLabel: string;
  canFix: boolean;
  activity: StageActivityResponse[];
  backlogPanel: ListPanelResponse;
  boardPanel: BoardPanelResponse;
  activityPanel: ActivityPanelResponse;
  latestExecutionRecordedAt?: string | null;
};

export type IdeaFixResponse = {
  id: string;
  title: string;
  agent: string;
  action: string;
  queued: boolean;
  message: string;
  operatorInput?: string | null;
};

export type ResourceState<TData> = {
  data: TData | null;
  loading: boolean;
  error: string | null;
};
