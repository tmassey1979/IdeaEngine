import { useEffect, useState } from "react";
import { fixIdea, fixIdeas, loadDashboard, loadIdeaDetail, loadIdeas } from "./api";
import { buildLanes, detailTabs, wizardSteps, workspaceTabs } from "./data";
import type { DashboardResponse, IdeaDetailResponse, IdeaFixResponse, IdeaListItemResponse, ResourceState } from "./types";
import { ActivityPanelView, BoardPanelView, DragonMark, HeroSigil, ListPanelView, PaginationControls, SectionStateCard } from "./ui";
import { createResourceState, formatStatus, formatTimestamp, paginate, usePagination } from "./view";
import "./styles.css";

type WorkspaceTabId = (typeof workspaceTabs)[number]["id"];
type DetailTabId = (typeof detailTabs)[number]["id"];

const DEFAULT_TAB: WorkspaceTabId = "dashboard";

function readHashTab(): WorkspaceTabId {
  const hash = globalThis.location?.hash?.replace(/^#/, "");
  return workspaceTabs.some((tab) => tab.id === hash)
    ? (hash as WorkspaceTabId)
    : DEFAULT_TAB;
}

export default function App() {
  const [activeTab, setActiveTab] = useState<WorkspaceTabId>(readHashTab());
  const [detailTab, setDetailTab] = useState<DetailTabId>("backlog");
  const [selectedIdeaId, setSelectedIdeaId] = useState<string | null>(null);
  const [servicePanelOpen, setServicePanelOpen] = useState(false);
  const [selectedInterventionIds, setSelectedInterventionIds] = useState<string[]>([]);
  const [submitNotice, setSubmitNotice] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);
  const [fixDraft, setFixDraft] = useState("");
  const [fixState, setFixState] = useState<{
    issueId: string | null;
    loading: boolean;
    error: string | null;
    response: IdeaFixResponse | null;
  }>({
    issueId: null,
    loading: false,
    error: null,
    response: null,
  });
  const [batchFixState, setBatchFixState] = useState<{
    loading: boolean;
    error: string | null;
    responses: IdeaFixResponse[];
  }>({
    loading: false,
    error: null,
    responses: [],
  });
  const [dashboardState, setDashboardState] = useState<ResourceState<DashboardResponse>>(createResourceState);
  const [ideasState, setIdeasState] = useState<ResourceState<IdeaListItemResponse[]>>(createResourceState);
  const [detailState, setDetailState] = useState<ResourceState<IdeaDetailResponse>>({
    data: null,
    loading: false,
    error: null,
  });

  const activePagination = usePagination((ideasState.data ?? []).filter((idea) => idea.isActive).length);
  const queuedPreviewPagination = usePagination((ideasState.data ?? []).filter((idea) => idea.status === "queued").length);
  const forecastPagination = usePagination((ideasState.data ?? []).length);
  const queuePagination = usePagination((ideasState.data ?? []).filter((idea) => idea.status !== "done").length);
  const projectPagination = usePagination((ideasState.data ?? []).filter((idea) => idea.status !== "done").length);
  const quarantinePagination = usePagination((ideasState.data ?? []).filter((idea) => idea.sourceOverallStatus === "quarantined").length);

  useEffect(() => {
    const handleHashChange = () => {
      setActiveTab(readHashTab());
    };

    handleHashChange();
    globalThis.addEventListener("hashchange", handleHashChange);

    if (!globalThis.location?.hash) {
      globalThis.history.replaceState(null, "", `#${DEFAULT_TAB}`);
    }

    return () => {
      globalThis.removeEventListener("hashchange", handleHashChange);
    };
  }, []);

  useEffect(() => {
    const intervalId = globalThis.setInterval(() => {
      setRefreshToken((value) => value + 1);
    }, 10_000);

    return () => {
      globalThis.clearInterval(intervalId);
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setDashboardState(createResourceState());

    loadDashboard()
      .then((dashboard) => {
        if (!cancelled) {
          setDashboardState({ data: dashboard, loading: false, error: null });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setDashboardState({
            data: null,
            loading: false,
            error: error instanceof Error ? error.message : "Dashboard data is unavailable.",
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [refreshToken]);

  useEffect(() => {
    let cancelled = false;
    setIdeasState(createResourceState());

    loadIdeas()
      .then((ideas) => {
        if (!cancelled) {
          setIdeasState({ data: ideas, loading: false, error: null });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setIdeasState({
            data: null,
            loading: false,
            error: error instanceof Error ? error.message : "Idea data is unavailable.",
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [refreshToken]);

  useEffect(() => {
    const ideas = ideasState.data ?? [];
    if (ideas.length === 0) {
      setSelectedIdeaId(null);
      return;
    }

    if (!selectedIdeaId || !ideas.some((idea) => idea.id === selectedIdeaId)) {
      setSelectedIdeaId(ideas[0].id);
    }
  }, [ideasState.data, selectedIdeaId]);

  useEffect(() => {
    if (!selectedIdeaId) {
      setDetailState({ data: null, loading: false, error: null });
      return;
    }

    let cancelled = false;
    setDetailState({ data: null, loading: true, error: null });

    loadIdeaDetail(selectedIdeaId)
      .then((detail) => {
        if (!cancelled) {
          setDetailState({ data: detail, loading: false, error: null });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setDetailState({
            data: null,
            loading: false,
            error: error instanceof Error ? error.message : "Idea detail is unavailable.",
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [selectedIdeaId, refreshToken]);

  const ideas = ideasState.data ?? [];
  const activeIdeas = ideas.filter((idea) => idea.isActive);
  const projectIdeas = ideas.filter((idea) => idea.status !== "done");
  const queueIdeas = ideas.filter((idea) => idea.status !== "done");
  const queuedIdeas = ideas.filter((idea) => idea.status === "queued");
  const quarantinedIdeas = ideas.filter((idea) => idea.sourceOverallStatus === "quarantined");
  const quarantinedIdeaIds = quarantinedIdeas.map((idea) => idea.id);
  const quarantinedIdeaKey = quarantinedIdeaIds.join(",");
  const selectedIdea = ideas.find((idea) => idea.id === selectedIdeaId) ?? null;
  const activeSlice = paginate(activeIdeas, activePagination.page, activePagination.pageSize);
  const queuedPreviewSlice = paginate(queuedIdeas, queuedPreviewPagination.page, queuedPreviewPagination.pageSize);
  const forecastSlice = paginate(ideas, forecastPagination.page, forecastPagination.pageSize);
  const queueSlice = paginate(queueIdeas, queuePagination.page, queuePagination.pageSize);
  const projectSlice = paginate(projectIdeas, projectPagination.page, projectPagination.pageSize);
  const quarantineSlice = paginate(quarantinedIdeas, quarantinePagination.page, quarantinePagination.pageSize);

  useEffect(() => {
    setSelectedInterventionIds((current) => {
      const next = current.filter((id) => quarantinedIdeaIds.includes(id));
      return next.length === current.length ? current : next;
    });
  }, [quarantinedIdeaKey]);

  function navigateToTab(nextTab: WorkspaceTabId) {
    setActiveTab(nextTab);

    if (globalThis.location?.hash !== `#${nextTab}`) {
      globalThis.location.hash = nextTab;
    }
  }

  function openIdeaDetail(ideaId: string) {
    setSelectedIdeaId(ideaId);
    navigateToTab("idea-detail");
  }

  async function queueFix(ideaId: string, operatorInput?: string) {
    setFixState({
      issueId: ideaId,
      loading: true,
      error: null,
      response: null,
    });

    try {
      const response = await fixIdea(ideaId, operatorInput);
      setFixState({
        issueId: ideaId,
        loading: false,
        error: null,
        response,
      });
      setRefreshToken((value) => value + 1);
      if (selectedIdeaId === ideaId) {
        setFixDraft("");
      }
    } catch (error: unknown) {
      setFixState({
        issueId: ideaId,
        loading: false,
        error: error instanceof Error ? error.message : "Issue fix request failed.",
        response: null,
      });
    }
  }

  async function queueBatchFix(ideaIds: string[], operatorInput?: string) {
    const uniqueIds = Array.from(new Set(ideaIds));
    if (uniqueIds.length === 0) {
      setBatchFixState({
        loading: false,
        error: "Select at least one quarantined issue first.",
        responses: [],
      });
      return;
    }

    setBatchFixState({
      loading: true,
      error: null,
      responses: [],
    });

    try {
      const responses = await fixIdeas(uniqueIds, operatorInput);
      setBatchFixState({
        loading: false,
        error: null,
        responses,
      });
      setSelectedInterventionIds([]);
      setRefreshToken((value) => value + 1);
    } catch (error: unknown) {
      setBatchFixState({
        loading: false,
        error: error instanceof Error ? error.message : "Bulk issue fix request failed.",
        responses: [],
      });
    }
  }

  function toggleInterventionSelection(ideaId: string) {
    setSelectedInterventionIds((current) =>
      current.includes(ideaId) ? current.filter((id) => id !== ideaId) : [...current, ideaId],
    );
  }

  function selectVisibleInterventions() {
    setSelectedInterventionIds((current) => Array.from(new Set([...current, ...quarantineSlice.items.map((idea) => idea.id)])));
  }

  function clearInterventionSelection() {
    setSelectedInterventionIds([]);
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark" aria-hidden="true">
            <DragonMark />
          </div>
          <div>
            <p className="eyebrow">Dragon Idea Engine</p>
            <h1>Idea Foundry</h1>
          </div>
        </div>

        <div className="nav-shell">
          <p className="panel-label nav-label">Workspace tabs</p>
          <nav className="nav" aria-label="Primary">
            {workspaceTabs.map((tab) => (
              <button
                key={tab.id}
                type="button"
                className={`nav-link${activeTab === tab.id ? " active" : ""}`}
                onClick={() => navigateToTab(tab.id)}
              >
                {tab.label}
              </button>
            ))}
          </nav>
        </div>

        <section className="sidebar-card">
          <p className="panel-label">What this view should answer</p>
          <ul className="sidebar-list">
            <li>What are we building right now?</li>
            <li>Which ideas are approved and waiting?</li>
            <li>When should each idea reach MVP?</li>
            <li>Is the system healthy enough to trust?</li>
            <li>Which build lane should this idea follow?</li>
            <li>Which agent microservice needs attention or scale?</li>
          </ul>
        </section>

        <section className="sidebar-card">
          <p className="panel-label">Current UI work</p>
          <ul className="sidebar-list compact">
            <li>#349 Dashboard-first shell</li>
            <li>#352 Idea queue with approval + ETA</li>
            <li>#353 Submission wizard</li>
            <li>#350 Idea detail backlog + board</li>
            <li>#354 System telemetry for health</li>
          </ul>
        </section>
      </aside>

      <main className="main-content">
        {activeTab === "dashboard" && (
          <>
            <section className="hero panel">
              <div className="hero-copy">
                <p className="eyebrow">Dashboard</p>
                <h2>Ideas come in, route into the right build lane, and move toward MVP in full view.</h2>
                <p>
                  Phase one stays focused on the self-building loop, but the intake flow can already tag each
                  project for the software lane now and future domain packs later while every agent runs as its own
                  containerized microservice.
                </p>
              </div>
              <HeroSigil />
              <div className="hero-actions">
                <button type="button" className="button primary" onClick={() => navigateToTab("submit-idea")}>
                  Submit Idea
                </button>
                <button type="button" className="button secondary" onClick={() => navigateToTab("idea-queue")}>
                  Open Idea Queue
                </button>
              </div>
            </section>

            <section className="health-strip">
              <button
                type="button"
                className="metric-card metric-card-button emphasis"
                onClick={() => navigateToTab("intervention")}
              >
                <p className="metric-label">Loop health</p>
                <p className="metric-value">{dashboardState.data?.health ?? (dashboardState.loading ? "Loading" : "Unavailable")}</p>
                <p className="metric-meta">
                  {dashboardState.data?.attentionSummary ?? dashboardState.error ?? "Waiting for dashboard data"}
                </p>
              </button>
              <button
                type="button"
                className="metric-card metric-button"
                aria-expanded={servicePanelOpen}
                aria-controls="service-health-panel"
                onClick={() => setServicePanelOpen((value) => !value)}
              >
                <p className="metric-label">Services</p>
                <p className="metric-value">
                  {dashboardState.data?.servicesHealthyLabel ?? (dashboardState.loading ? "Checking" : "Unavailable")}
                </p>
                <p className="metric-meta">
                  {dashboardState.data?.services.find((service) => service.status !== "healthy")?.summary ??
                    dashboardState.data?.sourceStatus ??
                    "Dragon API is unavailable."}
                </p>
              </button>
              <article className="metric-card">
                <p className="metric-label">CPU</p>
                <p className="metric-value">
                  {typeof dashboardState.data?.telemetry?.processorLoadPercent === "number"
                    ? `${Math.round(dashboardState.data.telemetry.processorLoadPercent)}%`
                    : dashboardState.loading
                      ? "Pending"
                      : "Unavailable"}
                </p>
                <p className="metric-meta">Pi telemetry target</p>
              </article>
              <article className="metric-card">
                <p className="metric-label">Memory</p>
                <p className="metric-value">
                  {typeof dashboardState.data?.telemetry?.memoryUsedPercent === "number"
                    ? `${Math.round(dashboardState.data.telemetry.memoryUsedPercent)}%`
                    : dashboardState.loading
                      ? "Pending"
                      : "Unavailable"}
                </p>
                <p className="metric-meta">{dashboardState.data?.telemetry?.summary ?? "Host telemetry unavailable"}</p>
              </article>
            </section>

            {servicePanelOpen && (
              <section id="service-health-panel" className="panel service-panel">
                <div className="panel-head">
                  <div>
                    <p className="panel-label">Services</p>
                    <h3>Service health detail</h3>
                  </div>
                  <span className="chip">{dashboardState.data?.servicesHealthyLabel ?? "Unavailable"}</span>
                </div>
                {dashboardState.error ? (
                  <SectionStateCard tone="error" message={`Service health is unavailable right now. ${dashboardState.error}`} />
                ) : dashboardState.loading ? (
                  <SectionStateCard message="Loading service health..." />
                ) : !dashboardState.data?.services?.length ? (
                  <SectionStateCard message="No service health records are currently available." />
                ) : (
                  <div className="service-list">
                    {dashboardState.data.services.map((service) => (
                      <article key={service.name} className="service-card">
                        <div className="project-card-header">
                          <div>
                            <h4>{service.name}</h4>
                            <p className="subtle">{service.summary}</p>
                          </div>
                          <span className={`pill ${service.status.toLowerCase()}`}>{formatStatus(service.status)}</span>
                        </div>
                      </article>
                    ))}
                  </div>
                )}
              </section>
            )}

            <section className="dashboard-grid">
              <article className="panel">
                <div className="panel-head">
                  <div>
                    <p className="panel-label">Printing now</p>
                    <h3>Active projects</h3>
                  </div>
                  <span className="chip">{dashboardState.data?.activeProjectCount ?? activeIdeas.length} active</span>
                </div>
                {ideasState.error ? (
                  <SectionStateCard tone="error" message={`Active project data is unavailable right now. ${ideasState.error}`} />
                ) : (
                  <>
                    <PaginationControls
                      label="Active projects"
                      totalCount={activeIdeas.length}
                      page={activeSlice.page}
                      pageSize={activePagination.pageSize}
                      totalPages={activeSlice.totalPages}
                      startIndex={activeSlice.startIndex}
                      endIndex={activeSlice.endIndex}
                      onPageChange={activePagination.setPage}
                      onPageSizeChange={activePagination.setPageSize}
                    />
                    <div className="card-stack">
                      {activeSlice.items.length === 0 ? (
                        <SectionStateCard message={ideasState.loading ? "Loading active projects..." : "No active projects are available."} />
                      ) : (
                        activeSlice.items.map((idea) => (
                          <button key={idea.id} type="button" className="project-card" onClick={() => openIdeaDetail(idea.id)}>
                            <div className="project-card-header">
                              <div>
                                <h4>{idea.title}</h4>
                                <p className="subtle">{idea.summary}</p>
                              </div>
                              <span className={`pill ${idea.status}`}>{formatStatus(idea.status)}</span>
                            </div>
                            <div className="meta-row">
                              <span>Phase: {idea.phase}</span>
                              <span>Queue: {idea.queuePositionLabel}</span>
                            </div>
                          </button>
                        ))
                      )}
                    </div>
                  </>
                )}
              </article>

              <article className="panel">
                <div className="panel-head">
                  <div>
                    <p className="panel-label">Queue</p>
                    <h3>Approved ideas waiting for build</h3>
                  </div>
                  <span className="chip">{queuedIdeas.length} queued</span>
                </div>
                {ideasState.error ? (
                  <SectionStateCard tone="error" message={`Queue preview is unavailable right now. ${ideasState.error}`} />
                ) : queuedIdeas.length === 0 ? (
                  <SectionStateCard message={ideasState.loading ? "Loading queue preview..." : "No queued ideas are currently waiting for build."} />
                ) : (
                  <>
                    <PaginationControls
                      label="Queue preview"
                      totalCount={queuedIdeas.length}
                      page={queuedPreviewSlice.page}
                      pageSize={queuedPreviewPagination.pageSize}
                      totalPages={queuedPreviewSlice.totalPages}
                      startIndex={queuedPreviewSlice.startIndex}
                      endIndex={queuedPreviewSlice.endIndex}
                      onPageChange={queuedPreviewPagination.setPage}
                      onPageSizeChange={queuedPreviewPagination.setPageSize}
                    />
                    <div className="card-stack compact">
                      {queuedPreviewSlice.items.map((idea) => (
                        <button key={idea.id} type="button" className="project-card" onClick={() => openIdeaDetail(idea.id)}>
                          <div className="project-card-header">
                            <div>
                              <h4>{idea.title}</h4>
                              <p className="subtle">{idea.summary}</p>
                            </div>
                            <span className={`pill ${idea.status}`}>{formatStatus(idea.status)}</span>
                          </div>
                          <div className="meta-row">
                            <span>Phase: {idea.phase}</span>
                            <span>Queue: {idea.queuePositionLabel}</span>
                          </div>
                        </button>
                      ))}
                    </div>
                  </>
                )}
              </article>
            </section>

            <section className="dashboard-grid secondary">
              <article className="panel">
                <div className="panel-head">
                  <div>
                    <p className="panel-label">MVP forecast</p>
                    <h3>Expected delivery windows</h3>
                  </div>
                  <span className="chip">Paged to 10 by default</span>
                </div>
                {ideasState.error ? (
                  <SectionStateCard tone="error" message={`Forecast data is unavailable right now. ${ideasState.error}`} />
                ) : (
                  <>
                    <PaginationControls
                      label="Forecast"
                      totalCount={ideas.length}
                      page={forecastSlice.page}
                      pageSize={forecastPagination.pageSize}
                      totalPages={forecastSlice.totalPages}
                      startIndex={forecastSlice.startIndex}
                      endIndex={forecastSlice.endIndex}
                      onPageChange={forecastPagination.setPage}
                      onPageSizeChange={forecastPagination.setPageSize}
                    />
                    <div className="forecast-list">
                      {forecastSlice.items.map((idea) => (
                        <div key={idea.id} className="forecast-card">
                          <div className="forecast-row">
                            <div>
                              <strong>{idea.title}</strong>
                              <p className="subtle">{idea.summary}</p>
                            </div>
                            <strong>{idea.etaLabel}</strong>
                          </div>
                        </div>
                      ))}
                      {ideas.length === 0 && <SectionStateCard message={ideasState.loading ? "Loading forecast..." : "No forecast data is available."} />}
                    </div>
                  </>
                )}
              </article>

              <article className="panel">
                <div className="panel-head">
                  <div>
                    <p className="panel-label">Agent fabric</p>
                    <h3>Operational signals</h3>
                  </div>
                  <span className="chip">{dashboardState.data?.queueSummary ?? "Queue loading"}</span>
                </div>
                <div className="signal-list">
                  <div className="signal-row">
                    <span>Agent topology</span>
                    <strong>Independent containerized services</strong>
                  </div>
                  <div className="signal-row">
                    <span>Scale mode</span>
                    <strong>Ingress can fan out more agents</strong>
                  </div>
                  <div className="signal-row">
                    <span>Worker wait signal</span>
                    <strong>{dashboardState.data?.waitSignal ?? "Waiting for dashboard data"}</strong>
                  </div>
                  <div className="signal-row">
                    <span>Recent loop summary</span>
                    <strong>{dashboardState.data?.recentLoopSummary ?? "No recent loop summary yet"}</strong>
                  </div>
                  <div className="signal-row">
                    <span>Lead work</span>
                    <strong>{dashboardState.data?.leadWorkLabel ?? "No lead work recorded"}</strong>
                  </div>
                  <div className="signal-row">
                    <span>Snapshot source</span>
                    <strong>{dashboardState.data?.sourceStatus ?? "Unavailable"}</strong>
                  </div>
                </div>
              </article>
            </section>

            <section className="panel">
              <div className="panel-head">
                <div>
                  <p className="panel-label">Intervention queue</p>
                  <h3>Quarantined issues</h3>
                </div>
                <span className="chip">{quarantinedIdeas.length} quarantined</span>
              </div>
              {ideasState.error ? (
                <SectionStateCard tone="error" message={`Quarantine data is unavailable right now. ${ideasState.error}`} />
              ) : quarantinedIdeas.length === 0 ? (
                <SectionStateCard message={ideasState.loading ? "Loading quarantined issues..." : "No quarantined issues are currently recorded."} />
              ) : (
                <>
                  <PaginationControls
                    label="Quarantined issues"
                    totalCount={quarantinedIdeas.length}
                    page={quarantineSlice.page}
                    pageSize={quarantinePagination.pageSize}
                    totalPages={quarantineSlice.totalPages}
                    startIndex={quarantineSlice.startIndex}
                    endIndex={quarantineSlice.endIndex}
                    onPageChange={quarantinePagination.setPage}
                    onPageSizeChange={quarantinePagination.setPageSize}
                  />
                  <div className="triage-list">
                    {quarantineSlice.items.map((idea) => (
                      <article key={idea.id} className="triage-card">
                        <div className="project-card-header">
                          <div>
                            <h4>{idea.title}</h4>
                            <p className="subtle">{idea.summary}</p>
                          </div>
                          <span className={`pill ${idea.status}`}>{formatStatus(idea.status)}</span>
                        </div>
                        <div className="triage-meta">
                          <span>Phase: {idea.phase}</span>
                          <span>Last update: {formatTimestamp(idea.latestExecutionRecordedAt)}</span>
                        </div>
                        <div className="triage-actions">
                          <button type="button" className="button secondary" onClick={() => openIdeaDetail(idea.id)}>
                            Open Detail
                          </button>
                          <button
                            type="button"
                            className="button primary"
                            disabled={fixState.loading && fixState.issueId === idea.id}
                            onClick={() => queueFix(idea.id)}
                          >
                            {fixState.loading && fixState.issueId === idea.id ? "Queueing Fix..." : "Quick Fix"}
                          </button>
                        </div>
                        {fixState.issueId === idea.id && fixState.error && (
                          <SectionStateCard tone="error" message={`Fix request failed. ${fixState.error}`} />
                        )}
                        {fixState.issueId === idea.id && fixState.response && (
                          <SectionStateCard message={fixState.response.message} />
                        )}
                      </article>
                    ))}
                  </div>
                </>
              )}
            </section>
          </>
        )}

        {activeTab === "intervention" && (
          <section className="panel">
            <div className="panel-head">
              <div>
                <p className="panel-label">Intervention</p>
                <h3>Quarantined issues that need operator-directed recovery</h3>
              </div>
              <span className="chip">{quarantinedIdeas.length} quarantined</span>
            </div>

            <div className="signal-list intervention-summary">
              <div className="signal-row">
                <span>Loop health</span>
                <strong>{dashboardState.data?.health ?? "Unavailable"}</strong>
              </div>
              <div className="signal-row">
                <span>Attention summary</span>
                <strong>{dashboardState.data?.attentionSummary ?? dashboardState.error ?? "Unavailable"}</strong>
              </div>
              <div className="signal-row">
                <span>Selected issues</span>
                <strong>{selectedInterventionIds.length}</strong>
              </div>
            </div>

            {ideasState.error ? (
              <SectionStateCard tone="error" message={`Intervention data is unavailable right now. ${ideasState.error}`} />
            ) : quarantinedIdeas.length === 0 ? (
              <SectionStateCard message={ideasState.loading ? "Loading quarantined issues..." : "No quarantined issues are currently recorded."} />
            ) : (
              <>
                <PaginationControls
                  label="Intervention queue"
                  totalCount={quarantinedIdeas.length}
                  page={quarantineSlice.page}
                  pageSize={quarantinePagination.pageSize}
                  totalPages={quarantineSlice.totalPages}
                  startIndex={quarantineSlice.startIndex}
                  endIndex={quarantineSlice.endIndex}
                  onPageChange={quarantinePagination.setPage}
                  onPageSizeChange={quarantinePagination.setPageSize}
                />

                <div className="triage-actions bulk-actions">
                  <button type="button" className="button tertiary" onClick={selectVisibleInterventions}>
                    Select Visible
                  </button>
                  <button type="button" className="button tertiary" onClick={clearInterventionSelection}>
                    Clear Selection
                  </button>
                  <button
                    type="button"
                    className="button secondary"
                    disabled={batchFixState.loading || selectedInterventionIds.length === 0}
                    onClick={() => queueBatchFix(selectedInterventionIds)}
                  >
                    {batchFixState.loading ? "Queueing Selected..." : `Queue Selected (${selectedInterventionIds.length})`}
                  </button>
                  <button
                    type="button"
                    className="button primary"
                    disabled={batchFixState.loading || quarantinedIdeas.length === 0}
                    onClick={() => queueBatchFix(quarantinedIdeas.map((idea) => idea.id))}
                  >
                    {batchFixState.loading ? "Queueing All..." : `Queue All (${quarantinedIdeas.length})`}
                  </button>
                </div>

                {batchFixState.error && <SectionStateCard tone="error" message={`Bulk fix request failed. ${batchFixState.error}`} />}
                {batchFixState.responses.length > 0 && (
                  <SectionStateCard
                    message={`Queued ${batchFixState.responses.length} issue fix request(s): ${batchFixState.responses.map((response) => `#${response.id}`).join(", ")}`}
                  />
                )}

                <div className="triage-list">
                  {quarantineSlice.items.map((idea) => {
                    const isSelected = selectedInterventionIds.includes(idea.id);
                    return (
                      <article key={idea.id} className={`triage-card${isSelected ? " selected" : ""}`}>
                        <div className="triage-select-row">
                          <label className="triage-checkbox">
                            <input
                              type="checkbox"
                              checked={isSelected}
                              onChange={() => toggleInterventionSelection(idea.id)}
                              aria-label={`Select issue ${idea.id}`}
                            />
                            <span>Select</span>
                          </label>
                          <span className={`pill ${idea.status}`}>{formatStatus(idea.status)}</span>
                        </div>
                        <div className="project-card-header">
                          <div>
                            <h4>{idea.title}</h4>
                            <p className="subtle">{idea.summary}</p>
                          </div>
                        </div>
                        <div className="triage-meta">
                          <span>Issue: #{idea.id}</span>
                          <span>Phase: {idea.phase}</span>
                          <span>Last update: {formatTimestamp(idea.latestExecutionRecordedAt)}</span>
                        </div>
                        <div className="triage-actions">
                          <button type="button" className="button tertiary" onClick={() => openIdeaDetail(idea.id)}>
                            Open Detail
                          </button>
                          <button
                            type="button"
                            className="button secondary"
                            disabled={fixState.loading && fixState.issueId === idea.id}
                            onClick={() => queueFix(idea.id)}
                          >
                            {fixState.loading && fixState.issueId === idea.id ? "Queueing..." : "Queue Fix"}
                          </button>
                        </div>
                        {fixState.issueId === idea.id && fixState.error && (
                          <SectionStateCard tone="error" message={`Fix request failed. ${fixState.error}`} />
                        )}
                        {fixState.issueId === idea.id && fixState.response && (
                          <SectionStateCard message={fixState.response.message} />
                        )}
                      </article>
                    );
                  })}
                </div>
              </>
            )}
          </section>
        )}

        {activeTab === "submit-idea" && (
          <section className="panel wizard-panel">
            <div className="panel-head">
              <div>
                <p className="panel-label">Submit Idea</p>
                <h3>Guided intake with future pack routing</h3>
              </div>
              <span className="chip">UI work #353</span>
            </div>

            <div className="wizard-steps">
              {wizardSteps.map((step, index) => (
                <div key={step.id} className={`wizard-step${index === 0 ? " active" : ""}`}>
                  <span>{index + 1}</span>
                  <div>
                    <strong>{step.label}</strong>
                    <p>{step.description}</p>
                  </div>
                </div>
              ))}
            </div>

            <div className="lane-intro">
              <div>
                <p className="panel-label">Build lanes</p>
                <h4>Submit once, route cleanly</h4>
              </div>
              <p className="subtle">
                Packs ship later, but the intake should already know whether a project belongs in the standard
                software lane or a future specialist lane like game dev or hardware.
              </p>
            </div>

            <div className="lane-grid">
              {buildLanes.map((lane) => (
                <article key={lane.id} className={`lane-card${lane.active ? " active" : ""}`}>
                  <span className="lane-state">{lane.state}</span>
                  <h4>{lane.title}</h4>
                  <p>{lane.description}</p>
                </article>
              ))}
            </div>

            <form
              className="wizard-grid"
              onSubmit={(event) => {
                event.preventDefault();
                setSubmitNotice("Submit Idea is intentionally UI-only in this milestone. The form is ready, but persistence is not wired yet.");
              }}
            >
              <label>
                <span>Idea name</span>
                <input type="text" placeholder="Enter idea name" />
              </label>
              <label>
                <span>Build lane</span>
                <select defaultValue="Core software">
                  <option>Core software</option>
                  <option>Game dev lane (future pack)</option>
                  <option>Hardware design lane (future pack)</option>
                </select>
              </label>
              <label>
                <span>Primary platform</span>
                <select defaultValue="Select platform">
                  <option>Select platform</option>
                  <option>Web application</option>
                  <option>Worker / automation</option>
                  <option>API / service</option>
                </select>
              </label>
              <label className="wide">
                <span>Problem to solve</span>
                <textarea placeholder="Describe the problem, target users, and the MVP outcome you want." />
              </label>
              <label>
                <span>Frontend preference</span>
                <select defaultValue="Select frontend preference">
                  <option>Select frontend preference</option>
                  <option>React + TypeScript</option>
                  <option>Next.js</option>
                  <option>None / backend only</option>
                </select>
              </label>
              <label>
                <span>Backend stack</span>
                <select defaultValue="Select backend preference">
                  <option>Select backend preference</option>
                  <option>ASP.NET Core + PostgreSQL</option>
                  <option>Node.js + PostgreSQL</option>
                  <option>Python worker</option>
                </select>
              </label>
              <label>
                <span>Deployment target</span>
                <select defaultValue="Select deployment target">
                  <option>Select deployment target</option>
                  <option>Pi-hosted Docker stack</option>
                  <option>Cloud container host</option>
                  <option>Undecided</option>
                </select>
              </label>
              <label>
                <span>Languages</span>
                <input type="text" placeholder="TypeScript, C#, Python..." />
              </label>
              <label>
                <span>Pack signals</span>
                <input type="text" placeholder="Gameplay, sensors, pcb, realtime, ai art..." />
              </label>
              <label>
                <span>Integrations</span>
                <input type="text" placeholder="Auth, payments, email, webhooks..." />
              </label>
              <label className="wide">
                <span>MVP must-have scope</span>
                <textarea placeholder="List only the must-have capabilities for MVP." />
              </label>
              <div className="form-actions wide">
                <button type="submit" className="button primary">
                  Queue For Review
                </button>
                <span className="subtle">This screen remains usable even when read APIs are degraded.</span>
              </div>
            </form>

            {submitNotice && <SectionStateCard message={submitNotice} />}
          </section>
        )}

        {activeTab === "idea-queue" && (
          <section className="panel">
            <div className="panel-head">
              <div>
                <p className="panel-label">Idea Queue</p>
                <h3>Submitted ideas, approvals, and build order</h3>
              </div>
              <span className="chip">{ideas.length} total</span>
            </div>
            {ideasState.error ? (
              <SectionStateCard tone="error" message={`Idea queue is unavailable right now. ${ideasState.error}`} />
            ) : (
              <>
                <PaginationControls
                  label="Idea queue"
                  totalCount={ideas.length}
                  page={queueSlice.page}
                  pageSize={queuePagination.pageSize}
                  totalPages={queueSlice.totalPages}
                  startIndex={queueSlice.startIndex}
                  endIndex={queueSlice.endIndex}
                  onPageChange={queuePagination.setPage}
                  onPageSizeChange={queuePagination.setPageSize}
                />
                <div className="queue-table-wrap">
                  <table className="queue-table">
                    <thead>
                      <tr>
                        <th>Idea</th>
                        <th>Status</th>
                        <th>Queue</th>
                        <th>Current phase</th>
                        <th>MVP ETA</th>
                      </tr>
                    </thead>
                    <tbody>
                      {queueSlice.items.length === 0 ? (
                        <tr>
                          <td colSpan={5}>
                            <SectionStateCard message={ideasState.loading ? "Loading idea queue..." : "No ideas are available."} />
                          </td>
                        </tr>
                      ) : (
                        queueSlice.items.map((idea) => (
                          <tr key={idea.id} onClick={() => openIdeaDetail(idea.id)}>
                            <td><strong>{idea.title}</strong></td>
                            <td><span className={`pill ${idea.status}`}>{formatStatus(idea.status)}</span></td>
                            <td>{idea.queuePositionLabel}</td>
                            <td>{idea.phase}</td>
                            <td>{idea.etaLabel}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </section>
        )}

        {activeTab === "projects" && (
          <section className="panel">
            <div className="panel-head">
              <div>
                <p className="panel-label">Projects</p>
                <h3>Ideas already in active delivery</h3>
              </div>
              <span className="chip">{projectIdeas.length} visible</span>
            </div>
            {ideasState.error ? (
              <SectionStateCard tone="error" message={`Project data is unavailable right now. ${ideasState.error}`} />
            ) : (
              <>
                <PaginationControls
                  label="Projects"
                  totalCount={projectIdeas.length}
                  page={projectSlice.page}
                  pageSize={projectPagination.pageSize}
                  totalPages={projectSlice.totalPages}
                  startIndex={projectSlice.startIndex}
                  endIndex={projectSlice.endIndex}
                  onPageChange={projectPagination.setPage}
                  onPageSizeChange={projectPagination.setPageSize}
                />
                <div className="project-grid">
                  {projectSlice.items.length === 0 ? (
                    <SectionStateCard message={ideasState.loading ? "Loading projects..." : "No active delivery projects are available."} />
                  ) : (
                    projectSlice.items.map((idea) => (
                      <button key={idea.id} type="button" className="project-card" onClick={() => openIdeaDetail(idea.id)}>
                        <div className="project-card-header">
                          <div>
                            <h4>{idea.title}</h4>
                            <p className="subtle">{idea.summary}</p>
                          </div>
                          <span className={`pill ${idea.status}`}>{formatStatus(idea.status)}</span>
                        </div>
                        <div className="meta-row">
                          <span>Phase: {idea.phase}</span>
                          <span>Queue: {idea.queuePositionLabel}</span>
                        </div>
                      </button>
                    ))
                  )}
                </div>
              </>
            )}
          </section>
        )}

        {activeTab === "idea-detail" && (
          <section className="panel detail-panel">
            <div className="panel-head">
              <div>
                <p className="panel-label">Idea Detail</p>
                <h3>{detailState.data?.title ?? selectedIdea?.title ?? "Select an idea"}</h3>
              </div>
              <span className="chip">UI work #350</span>
            </div>

            <div className="detail-summary">
              <article>
                <span>Status</span>
                <strong>{detailState.data ? formatStatus(detailState.data.status) : "-"}</strong>
              </article>
              <article>
                <span>Queue position</span>
                <strong>{detailState.data?.queuePositionLabel ?? "-"}</strong>
              </article>
              <article>
                <span>Current phase</span>
                <strong>{detailState.data?.phase ?? "-"}</strong>
              </article>
              <article>
                <span>MVP ETA</span>
                <strong>{detailState.data?.etaLabel ?? "-"}</strong>
              </article>
            </div>

            <div className="detail-body">
              <div className="detail-sidebar">
                <div className="detail-card">
                  <p className="panel-label">Summary</p>
                  <p>{detailState.data?.summary ?? "Pick an idea to explore its current delivery plan and execution board."}</p>
                </div>
                <div className="detail-card">
                  <p className="panel-label">Preferred stack</p>
                  <p>{detailState.data?.preferredStackLabel ?? "Not exposed yet"}</p>
                </div>
                <div className="detail-card">
                  <p className="panel-label">Blockers</p>
                  {detailState.data?.blockers?.length ? (
                    <ul className="sidebar-list compact">
                      {detailState.data.blockers.map((blocker) => (
                        <li key={blocker}>{blocker}</li>
                      ))}
                    </ul>
                  ) : (
                    <p className="subtle">No blockers are available.</p>
                  )}
                </div>
                {detailState.data?.canFix && (
                  <div className="detail-card">
                    <p className="panel-label">Fix workflow</p>
                    <p className="subtle">
                      Queue an implementation agent to work this blocked issue. Add operator notes if you want to steer the fix.
                    </p>
                    <label className="fix-input">
                      <span>Operator guidance</span>
                      <textarea
                        className="fix-notes"
                        value={fixDraft}
                        onChange={(event) => setFixDraft(event.target.value)}
                        placeholder="Describe what to change, what failed, or what to avoid."
                      />
                    </label>
                    <div className="triage-actions">
                      <button
                        type="button"
                        className="button secondary"
                        disabled={fixState.loading && fixState.issueId === detailState.data.id}
                        onClick={() => queueFix(detailState.data!.id)}
                      >
                        {fixState.loading && fixState.issueId === detailState.data.id ? "Queueing Fix..." : "Quick Fix"}
                      </button>
                      <button
                        type="button"
                        className="button primary"
                        disabled={(fixState.loading && fixState.issueId === detailState.data.id) || fixDraft.trim().length === 0}
                        onClick={() => queueFix(detailState.data!.id, fixDraft)}
                      >
                        Queue Guided Fix
                      </button>
                    </div>
                    {fixState.issueId === detailState.data.id && fixState.error && (
                      <SectionStateCard tone="error" message={`Fix request failed. ${fixState.error}`} />
                    )}
                    {fixState.issueId === detailState.data.id && fixState.response && (
                      <SectionStateCard message={fixState.response.message} />
                    )}
                  </div>
                )}
              </div>

              <div className="detail-main">
                <div className="tab-row">
                  {detailTabs.map((tab) => (
                    <button
                      key={tab.id}
                      type="button"
                      className={`tab${detailTab === tab.id ? " active" : ""}`}
                      onClick={() => setDetailTab(tab.id)}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>

                {detailState.error ? (
                  <SectionStateCard tone="error" message={`Idea detail is unavailable right now. ${detailState.error}`} />
                ) : detailState.loading ? (
                  <SectionStateCard message="Loading idea detail..." />
                ) : detailState.data ? (
                  <div className="tab-panel active">
                    {detailTab === "backlog" && <ListPanelView panel={detailState.data.backlogPanel} />}
                    {detailTab === "board" && <BoardPanelView panel={detailState.data.boardPanel} />}
                    {detailTab === "activity" && <ActivityPanelView panel={detailState.data.activityPanel} />}
                  </div>
                ) : (
                  <SectionStateCard message="Select an idea from the queue or projects view." />
                )}
              </div>
            </div>
          </section>
        )}
      </main>
    </div>
  );
}
