import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";
import App from "./App";

const dashboardPayload = {
  health: "healthy",
  attentionSummary: "0 queued job(s), 1 issue(s) in progress.",
  servicesHealthyLabel: "2/2 healthy",
  telemetry: {
    status: "healthy",
    processorLoadPercent: 18,
    memoryUsedPercent: 32,
    summary: "Telemetry ready",
  },
  waitSignal: "Routine poll wait",
  recentLoopSummary: "Loop is healthy.",
  queueSummary: "1 queued job(s) | 0 failed | 0 quarantined",
  activeProjectCount: 1,
  sourceStatus: "status-http",
  services: [
    { name: "orchestrator", status: "healthy", summary: "Ready" },
    { name: "queue", status: "healthy", summary: "Ready" },
  ],
  leadWorkLabel: "#44 implement_issue",
};

const agentPerformancePayload = {
  generatedAt: "2026-03-25T12:00:00Z",
  summary: "12 tracked agent metric set(s)",
  agents: Array.from({ length: 12 }, (_, index) => ({
    agent: `agent-${index + 1}`,
    totalExecutions: 12 - index,
    successCount: 10 - Math.min(index, 5),
    failureCount: index < 2 ? 1 : 0,
    successRate: index < 2 ? 91 : 100,
    errorFrequency: index < 2 ? 0.09 : 0,
    averageDurationMilliseconds: 800 + index * 25,
    averageQualityScore: 4.8 - index * 0.1,
    averageRetryCount: index < 2 ? 0.5 : 0,
    averageProcessorLoadPercent: 22,
    averageMemoryUsedPercent: 38,
    averageDiskUsedPercent: 41,
    lastRecordedAt: "2026-03-25T11:45:00Z",
    summary: `Summary for agent ${index + 1}`,
  })),
};

const auditLogPayload = {
  generatedAt: "2026-03-25T12:00:00Z",
  summary: "12 audit event(s)",
  entries: Array.from({ length: 12 }, (_, index) => ({
    id: `audit-${index + 1}`,
    actor: index === 0 ? "continuous-monitor" : "operator",
    action: index === 0 ? "queue_fix" : "release_quarantine",
    project: "DragonIdeaEngine",
    issueNumber: index < 2 ? index + 1 : null,
    details: `Audit event ${index + 1}`,
    source: "ui",
    recordedAt: "2026-03-25T11:45:00Z",
  })),
};

const continuousMonitoringPayload = {
  generatedAt: "2026-03-25T12:00:00Z",
  summary: "12 monitoring finding(s)",
  findings: Array.from({ length: 12 }, (_, index) => ({
    id: `finding-${index + 1}`,
    category: index === 0 ? "new_vulnerability_discovery" : "technology_deprecations",
    severity: index === 0 ? "critical" : "warning",
    status: "active",
    project: "DragonIdeaEngine",
    issueNumber: index < 2 ? index + 1 : null,
    summary: `Finding ${index + 1}`,
    recommendation: `Recommendation ${index + 1}`,
    triggerAutomatedUpdate: index === 0,
    recordedAt: "2026-03-25T11:30:00Z",
    lastObservedAt: "2026-03-25T11:45:00Z",
  })),
};

const ideasPayload = Array.from({ length: 12 }, (_, index) => ({
  id: String(index + 1),
  title: `Project ${index + 1}`,
  status: index === 0 ? "printing" : index === 1 ? "blocked" : index < 4 ? "queued" : index === 11 ? "done" : "review",
  sourceOverallStatus: index === 1 ? "quarantined" : index === 0 ? "in_progress" : index === 11 ? "validated" : index < 4 ? "pending" : "failed",
  phase: index === 0 ? "Review" : index === 1 ? "Test" : index === 11 ? "Test" : "Developer",
  queuePositionLabel: index < 3 ? `${index + 1} queued job(s)` : "Not exposed",
  etaLabel: "Not exposed yet",
  summary: `Summary ${index + 1}`,
  isActive: index === 0 || (index >= 3 && index !== 11),
  isBlocked: index === 1,
  canFix: index === 1,
  latestExecutionRecordedAt: "2026-03-24T16:00:00Z",
}));

const detailPayload = {
  id: "1",
  title: "Project 1",
  status: "printing",
  sourceOverallStatus: "in_progress",
  phase: "Review",
  queuePositionLabel: "Not exposed",
  etaLabel: "Not exposed yet",
  summary: "Summary 1",
  blockers: ["Review notes still need fixes."],
  preferredStackLabel: "React + TypeScript",
  canFix: false,
  activity: [{ stage: "Review", status: "failed", observedAt: "2026-03-24T16:00:00Z", summary: "Review failed." }],
  backlogPanel: {
    state: "ready",
    summary: "Backlog proxy",
    items: Array.from({ length: 12 }, (_, index) => ({
      id: `backlog-${index + 1}`,
      title: `Backlog Item ${index + 1}`,
      status: index === 0 ? "failed" : "ready",
      summary: `Backlog summary ${index + 1}`,
    })),
  },
  boardPanel: {
    state: "ready",
    summary: "Board proxy",
    columns: [{ id: "blocked", title: "Blocked", cards: [{ id: "review", title: "Review", status: "failed", summary: "Review failed." }] }],
  },
  activityPanel: {
    state: "ready",
    summary: "Activity proxy",
    entries: Array.from({ length: 12 }, (_, index) => ({
      id: `job-${index + 1}`,
      title: `Activity ${index + 1}`,
      status: index === 0 ? "failed" : "ready",
      summary: `Activity summary ${index + 1}`,
      recordedAt: "2026-03-24T16:00:00Z",
    })),
  },
  latestExecutionRecordedAt: "2026-03-24T16:00:00Z",
};

const quarantinedDetailPayload = {
  id: "2",
  title: "Project 2",
  status: "blocked",
  sourceOverallStatus: "quarantined",
  phase: "Test",
  queuePositionLabel: "Not exposed",
  etaLabel: "Not exposed yet",
  summary: "Summary 2",
  blockers: ["Issue is quarantined and needs intervention before normal flow can resume."],
  preferredStackLabel: "ASP.NET Core + .NET 9",
  canFix: true,
  activity: [{ stage: "Test", status: "failed", observedAt: "2026-03-24T16:00:00Z", summary: "dotnet test failed:" }],
  backlogPanel: detailPayload.backlogPanel,
  boardPanel: detailPayload.boardPanel,
  activityPanel: detailPayload.activityPanel,
  latestExecutionRecordedAt: "2026-03-24T16:00:00Z",
};

function installFetchStub() {
  vi.stubGlobal(
    "fetch",
    vi.fn((input: string | URL | Request) => {
      const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;

      if (url.endsWith("/api/dashboard")) {
        return Promise.resolve(new Response(JSON.stringify(dashboardPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas")) {
        return Promise.resolve(new Response(JSON.stringify(ideasPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/1")) {
        return Promise.resolve(new Response(JSON.stringify(detailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/2")) {
        return Promise.resolve(new Response(JSON.stringify(quarantinedDetailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/agent-performance")) {
        return Promise.resolve(new Response(JSON.stringify(agentPerformancePayload), { status: 200 }));
      }

      if (url.includes("/api/audit-log")) {
        return Promise.resolve(new Response(JSON.stringify(auditLogPayload), { status: 200 }));
      }

      if (url.includes("/api/continuous-monitoring")) {
        return Promise.resolve(new Response(JSON.stringify(continuousMonitoringPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/2/fix")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: "2",
              title: "Project 2",
              agent: "developer",
              action: "implement_issue",
              queued: true,
              message: "Queued developer to work issue #2 with operator guidance.",
              operatorInput: "Focus on the failing test path first.",
            }),
            { status: 200 },
          ),
        );
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }),
  );
}

describe("React dashboard", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.useRealTimers();
    window.history.replaceState(null, "", "/");
  });

  beforeEach(() => {
    installFetchStub();
  });

  test("renders degraded dashboard state when APIs fail", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
      }),
    );

    render(<App />);

    await screen.findAllByText(/Request failed \(503\)/i);
    expect(within(screen.getByRole("navigation", { name: "Primary" })).getByRole("button", { name: "Submit Idea" })).toBeInTheDocument();
  });

  test("loads the workspace from the hash route", async () => {
    window.history.replaceState(null, "", "/#projects");

    render(<App />);

    await screen.findByText(/Ideas already in active delivery/i);
  });

  test("refreshes dashboard data every 10 seconds", async () => {
    const fetchMock = vi.fn((input: string | URL | Request) => {
      const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;

      if (url.endsWith("/api/dashboard")) {
        return Promise.resolve(new Response(JSON.stringify(dashboardPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas")) {
        return Promise.resolve(new Response(JSON.stringify(ideasPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/1")) {
        return Promise.resolve(new Response(JSON.stringify(detailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/agent-performance")) {
        return Promise.resolve(new Response(JSON.stringify(agentPerformancePayload), { status: 200 }));
      }

      if (url.includes("/api/audit-log")) {
        return Promise.resolve(new Response(JSON.stringify(auditLogPayload), { status: 200 }));
      }

      if (url.includes("/api/continuous-monitoring")) {
        return Promise.resolve(new Response(JSON.stringify(continuousMonitoringPayload), { status: 200 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });

    vi.stubGlobal("fetch", fetchMock);
    const setIntervalSpy = vi.spyOn(globalThis, "setInterval").mockImplementation(() => {
      return 1 as unknown as ReturnType<typeof setInterval>;
    });
    vi.spyOn(globalThis, "clearInterval").mockImplementation(() => {});

    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);

    const countDashboardCalls = () => fetchMock.mock.calls.filter(([request]) => {
      const url = typeof request === "string" ? request : request instanceof URL ? request.toString() : request.url;
      return url.endsWith("/api/dashboard");
    }).length;

    const initialDashboardCalls = countDashboardCalls();
    const refreshCall = setIntervalSpy.mock.calls.find(([, timeout]) => timeout === 10_000);

    expect(refreshCall).toBeDefined();
    expect(typeof refreshCall?.[0]).toBe("function");

    await act(async () => {
      (refreshCall![0] as () => void)();
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(countDashboardCalls()).toBeGreaterThan(initialDashboardCalls);
  });

  test("keeps dashboard data visible and shows a toast when background refresh fails", async () => {
    let dashboardRequests = 0;
    const fetchMock = vi.fn((input: string | URL | Request) => {
      const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;

      if (url.endsWith("/api/dashboard")) {
        dashboardRequests += 1;

        if (dashboardRequests === 1) {
          return Promise.resolve(new Response(JSON.stringify(dashboardPayload), { status: 200 }));
        }

        return Promise.resolve(new Response(null, { status: 503 }));
      }

      if (url.endsWith("/api/ideas")) {
        return Promise.resolve(new Response(JSON.stringify(ideasPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/1")) {
        return Promise.resolve(new Response(JSON.stringify(detailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/agent-performance")) {
        return Promise.resolve(new Response(JSON.stringify(agentPerformancePayload), { status: 200 }));
      }

      if (url.includes("/api/audit-log")) {
        return Promise.resolve(new Response(JSON.stringify(auditLogPayload), { status: 200 }));
      }

      if (url.includes("/api/continuous-monitoring")) {
        return Promise.resolve(new Response(JSON.stringify(continuousMonitoringPayload), { status: 200 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });

    vi.stubGlobal("fetch", fetchMock);
    const setIntervalSpy = vi.spyOn(globalThis, "setInterval").mockImplementation(() => {
      return 1 as unknown as ReturnType<typeof setInterval>;
    });
    vi.spyOn(globalThis, "clearInterval").mockImplementation(() => {});

    render(<App />);

    await screen.findByText("2/2 healthy");

    const refreshCall = setIntervalSpy.mock.calls.find(([, timeout]) => timeout === 10_000);
    expect(refreshCall).toBeDefined();

    await act(async () => {
      (refreshCall![0] as () => void)();
      await Promise.resolve();
      await Promise.resolve();
    });

    await screen.findByRole("alert");
    expect(screen.getByText(/Dashboard auto-refresh failed\. Request failed \(503\)/i)).toBeInTheDocument();
    expect(screen.getByText("2/2 healthy")).toBeInTheDocument();
    expect(screen.queryByText(/Loading active projects/i)).not.toBeInTheDocument();
  });

  test("renders agent performance, monitoring, and audit panels with paged data", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByRole("heading", { name: /Execution quality and throughput/i });
    expect(screen.getByText("agent-10")).toBeInTheDocument();
    expect(screen.queryByText("agent-11")).not.toBeInTheDocument();
    expect(screen.getByText("Finding 10")).toBeInTheDocument();
    expect(screen.queryByText("Finding 11")).not.toBeInTheDocument();
    expect(screen.getByText("Audit event 10")).toBeInTheDocument();
    expect(screen.queryByText("Audit event 11")).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText(/Agent performance page size/i), "20");
    await waitFor(() => {
      expect(screen.getByText("agent-11")).toBeInTheDocument();
    });
  });

  test("shows a panel-level error when continuous monitoring is unavailable", async () => {
    const fetchMock = vi.fn((input: string | URL | Request) => {
      const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;

      if (url.endsWith("/api/dashboard")) {
        return Promise.resolve(new Response(JSON.stringify(dashboardPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas")) {
        return Promise.resolve(new Response(JSON.stringify(ideasPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/1")) {
        return Promise.resolve(new Response(JSON.stringify(detailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/agent-performance")) {
        return Promise.resolve(new Response(JSON.stringify(agentPerformancePayload), { status: 200 }));
      }

      if (url.includes("/api/audit-log")) {
        return Promise.resolve(new Response(JSON.stringify(auditLogPayload), { status: 200 }));
      }

      if (url.includes("/api/continuous-monitoring")) {
        return Promise.resolve(new Response(null, { status: 503 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });

    vi.stubGlobal("fetch", fetchMock);
    render(<App />);

    await screen.findByText(/Continuous monitoring is unavailable right now\. Request failed \(503\)/i);
    expect(screen.getByRole("heading", { name: /Execution quality and throughput/i })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /Operator and automated control actions/i })).toBeInTheDocument();
  });

  test("opens the intervention workspace from the loop health card", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(screen.getByRole("button", { name: /Loop health/i }));

    await screen.findByRole("heading", { name: /Quarantined issues that need operator-directed recovery/i });
  });

  test("shows service health detail when the services card is clicked", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(screen.getByRole("button", { name: /Services/i }));

    await screen.findByRole("heading", { name: /Service health detail/i });
    expect(screen.getByText("orchestrator")).toBeInTheDocument();
    expect(screen.getByText("queue")).toBeInTheDocument();
    expect(screen.getAllByText("Ready").length).toBeGreaterThan(0);
  });

  test("opens idea detail from the queue and switches detail tabs", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(screen.getByRole("button", { name: "Idea Queue" }));
    const projectRow = await screen.findByText("Project 1");
    await user.click(projectRow.closest("tr")!);

    await screen.findByRole("heading", { name: /Project 1/i });
    expect(screen.getByText(/Review notes still need fixes/i)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Board" }));
    expect(screen.getByText(/Blocked/i)).toBeInTheDocument();
  });

  test("paginates detail backlog with a default page size of 10 and supports changing page size", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(screen.getByRole("button", { name: "Idea Queue" }));
    const projectRow = await screen.findByText("Project 1");
    await user.click(projectRow.closest("tr")!);

    await screen.findByRole("heading", { name: /Project 1/i });
    expect(screen.getByText("Backlog Item 10")).toBeInTheDocument();
    expect(screen.queryByText("Backlog Item 11")).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText(/Backlog page size/i), "20");

    await waitFor(() => {
      expect(screen.getByText("Backlog Item 11")).toBeInTheDocument();
    });
  });

  test("paginates the queue with a default page size of 10 and supports changing page size", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(screen.getByRole("button", { name: "Idea Queue" }));

    await waitFor(() => {
      expect(screen.getAllByRole("row")).toHaveLength(11);
    });
    expect(screen.queryByText("Project 12")).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText(/Idea queue page size/i), "20");

    await waitFor(() => {
      expect(screen.getAllByRole("row")).toHaveLength(12);
    });
    expect(screen.queryByText("Project 12")).not.toBeInTheDocument();
  });

  test("keeps submit idea usable and shows the non-persistent placeholder message", async () => {
    const user = userEvent.setup();
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(within(screen.getByRole("navigation", { name: "Primary" })).getByRole("button", { name: "Submit Idea" }));
    await user.click(screen.getByRole("button", { name: "Queue For Review" }));

    expect(screen.getByText(/intentionally UI-only/i)).toBeInTheDocument();
  });

  test("shows quarantined issues and queues a guided fix with operator input", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: string | URL | Request) => {
      const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
      if (url.endsWith("/api/dashboard")) {
        return Promise.resolve(new Response(JSON.stringify(dashboardPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas")) {
        return Promise.resolve(new Response(JSON.stringify(ideasPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/2")) {
        return Promise.resolve(new Response(JSON.stringify(quarantinedDetailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/agent-performance")) {
        return Promise.resolve(new Response(JSON.stringify(agentPerformancePayload), { status: 200 }));
      }

      if (url.includes("/api/audit-log")) {
        return Promise.resolve(new Response(JSON.stringify(auditLogPayload), { status: 200 }));
      }

      if (url.includes("/api/continuous-monitoring")) {
        return Promise.resolve(new Response(JSON.stringify(continuousMonitoringPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/2/fix")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: "2",
              title: "Project 2",
              agent: "developer",
              action: "implement_issue",
              queued: true,
              message: "Queued developer to work issue #2 with operator guidance.",
              operatorInput: "Focus on the failing test path first.",
            }),
            { status: 200 },
          ),
        );
      }

      if (url.endsWith("/api/ideas/1")) {
        return Promise.resolve(new Response(JSON.stringify(detailPayload), { status: 200 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });

    vi.stubGlobal("fetch", fetchMock);
    render(<App />);

    const card = (await screen.findAllByText("Project 2"))
      .map((element) => element.closest(".triage-card"))
      .find((element): element is HTMLElement => element !== null);
    expect(card).not.toBeNull();
    await user.click(within(card!).getByRole("button", { name: "Open Detail" }));

    await screen.findByRole("heading", { name: /Project 2/i });
    await user.type(screen.getByPlaceholderText(/Describe what to change/i), "Focus on the failing test path first.");
    await user.click(screen.getByRole("button", { name: "Queue Guided Fix" }));

    await screen.findByText(/Queued developer to work issue #2 with operator guidance/i);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/ideas/2/fix",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ operatorInput: "Focus on the failing test path first." }),
      }),
    );
  });

  test("queues selected quarantined issues as a group from the intervention tab", async () => {
    const user = userEvent.setup();
    const bulkIdeasPayload = ideasPayload.map((idea, index) =>
      index === 4
        ? {
            ...idea,
            id: "5",
            title: "Project 5",
            status: "blocked",
            sourceOverallStatus: "quarantined",
            isBlocked: true,
            canFix: true,
          }
        : idea,
    );

    const fetchMock = vi.fn((input: string | URL | Request) => {
      const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;

      if (url.endsWith("/api/dashboard")) {
        return Promise.resolve(new Response(JSON.stringify(dashboardPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas")) {
        return Promise.resolve(new Response(JSON.stringify(bulkIdeasPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/1")) {
        return Promise.resolve(new Response(JSON.stringify(detailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/2")) {
        return Promise.resolve(new Response(JSON.stringify(quarantinedDetailPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/5")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              ...quarantinedDetailPayload,
              id: "5",
              title: "Project 5",
              summary: "Summary 5",
            }),
            { status: 200 },
          ),
        );
      }

      if (url.endsWith("/api/agent-performance")) {
        return Promise.resolve(new Response(JSON.stringify(agentPerformancePayload), { status: 200 }));
      }

      if (url.includes("/api/audit-log")) {
        return Promise.resolve(new Response(JSON.stringify(auditLogPayload), { status: 200 }));
      }

      if (url.includes("/api/continuous-monitoring")) {
        return Promise.resolve(new Response(JSON.stringify(continuousMonitoringPayload), { status: 200 }));
      }

      if (url.endsWith("/api/ideas/2/fix") || url.endsWith("/api/ideas/5/fix")) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: url.includes("/api/ideas/5/fix") ? "5" : "2",
              title: url.includes("/api/ideas/5/fix") ? "Project 5" : "Project 2",
              agent: "developer",
              action: "implement_issue",
              queued: true,
              message: "Queued fix.",
              operatorInput: null,
            }),
            { status: 200 },
          ),
        );
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });

    vi.stubGlobal("fetch", fetchMock);
    render(<App />);

    await screen.findByText(/Ideas come in, route into the right build lane/i);
    await user.click(screen.getByRole("button", { name: /Loop health/i }));

    await screen.findByRole("heading", { name: /Quarantined issues that need operator-directed recovery/i });
    await user.click(screen.getByRole("checkbox", { name: /Select issue 2/i }));
    await user.click(screen.getByRole("checkbox", { name: /Select issue 5/i }));
    await user.click(screen.getByRole("button", { name: /Queue Selected \(2\)/i }));

    await screen.findByText(/Queued 2 issue fix request\(s\): #2, #5/i);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/ideas/2/fix",
      expect.objectContaining({ method: "POST" }),
    );
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/ideas/5/fix",
      expect.objectContaining({ method: "POST" }),
    );
  });
});
