import { expect, test } from "@playwright/test";

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

async function routeApi(page: import("@playwright/test").Page) {
  await page.route("**/api/dashboard", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(dashboardPayload) });
  });

  await page.route("**/api/ideas/1", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detailPayload) });
  });

  await page.route("**/api/ideas/2", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(quarantinedDetailPayload) });
  });

  await page.route("**/api/ideas", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(ideasPayload) });
  });

  await page.route("**/api/agent-performance", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(agentPerformancePayload) });
  });

  await page.route("**/api/audit-log**", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(auditLogPayload) });
  });

  await page.route("**/api/continuous-monitoring**", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(continuousMonitoringPayload) });
  });

  await page.route("**/api/ideas/2/fix", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: "2",
        title: "Project 2",
        agent: "developer",
        action: "implement_issue",
        queued: true,
        message: "Queued developer to work issue #2 with operator guidance.",
        operatorInput: "Focus on the failing test path first.",
      }),
    });
  });
}

test("react dashboard switches tabs and opens idea detail", async ({ page }) => {
  await routeApi(page);
  await page.goto("/");

  await expect(page.getByText(/Ideas come in, route into the right build lane/i)).toBeVisible();
  await page.getByRole("navigation", { name: "Primary" }).getByRole("button", { name: "Idea Queue" }).click();
  await expect(page.getByText(/Submitted ideas, approvals, and build order/i)).toBeVisible();

  await page.locator("tbody tr").first().click();
  await expect(page.getByRole("heading", { name: /Project 1/i })).toBeVisible();
  await expect(page.getByText(/Review notes still need fixes/i)).toBeVisible();
});

test("services card expands service health detail", async ({ page }) => {
  await routeApi(page);
  await page.goto("/");

  await page.getByRole("button", { name: /Services/i }).click();
  await expect(page.getByRole("heading", { name: /Service health detail/i })).toBeVisible();
  await expect(page.locator(".service-card").filter({ hasText: "orchestrator" })).toBeVisible();
  await expect(page.locator(".service-card").filter({ hasText: "queue" })).toBeVisible();
});

test("dashboard renders monitoring panels with paged data", async ({ page }) => {
  await routeApi(page);
  await page.goto("/");

  await expect(page.getByRole("heading", { name: /Execution quality and throughput/i })).toBeVisible();
  await expect(page.getByText("agent-10")).toBeVisible();
  await expect(page.getByText("agent-11")).toHaveCount(0);
  await expect(page.getByText("Finding 10")).toBeVisible();
  await expect(page.getByText("Finding 11")).toHaveCount(0);
  await expect(page.getByText("Audit event 10")).toBeVisible();
  await expect(page.getByText("Audit event 11")).toHaveCount(0);

  await page.getByLabel("Agent performance page size").selectOption("20");
  await expect(page.getByText("agent-11")).toBeVisible();
});

test("loop health opens the intervention workspace and supports grouped fix queueing", async ({ page }) => {
  await routeApi(page);
  await page.goto("/");

  await page.getByRole("button", { name: /Loop health/i }).click();
  await expect(page.getByRole("heading", { name: /Quarantined issues that need operator-directed recovery/i })).toBeVisible();
  await page.getByRole("button", { name: "Select Visible" }).click();
  await page.getByRole("button", { name: /Queue Selected \(1\)/i }).click();
  await expect(page.getByText(/Queued 1 issue fix request\(s\): #2/i)).toBeVisible();
});

test("queue pagination defaults to 10 rows and can be changed to 20", async ({ page }) => {
  await routeApi(page);
  await page.goto("/#idea-queue");

  await expect(page.locator("tbody tr")).toHaveCount(10);
  await expect(page.getByText("Project 12")).toHaveCount(0);
  await page.getByLabel("Idea queue page size").selectOption("20");
  await expect(page.locator("tbody tr")).toHaveCount(11);
  await expect(page.getByText("Project 12")).toHaveCount(0);
});

test("detail backlog pagination defaults to 10 rows and can be changed to 20", async ({ page }) => {
  await routeApi(page);
  await page.goto("/#idea-queue");

  await page.locator("tbody tr").first().click();
  await expect(page.getByText("Backlog Item 10")).toBeVisible();
  await expect(page.getByText("Backlog Item 11")).toHaveCount(0);

  await page.getByLabel("Backlog page size").selectOption("20");
  await expect(page.getByText("Backlog Item 11")).toBeVisible();
});

test("submit idea shows the non-persistent message", async ({ page }) => {
  await routeApi(page);
  await page.goto("/#submit-idea");

  await page.getByRole("button", { name: "Queue For Review" }).click();
  await expect(page.getByText(/intentionally UI-only/i)).toBeVisible();
});

test("degraded api state keeps the shell visible", async ({ page }) => {
  await page.route("**/api/**", async (route) => {
    await route.fulfill({ status: 503, body: "" });
  });

  await page.goto("/");

  await expect(page.getByRole("navigation", { name: "Primary" })).toBeVisible();
  await expect(page.getByText(/Request failed \(503\)/i).first()).toBeVisible();
});

test("quarantined issue detail can queue a guided fix", async ({ page }) => {
  await routeApi(page);
  await page.goto("/");

  const triageCard = page.locator(".triage-card").filter({ hasText: "Project 2" });
  await expect(triageCard).toBeVisible();
  await triageCard.getByRole("button", { name: "Open Detail" }).click();

  await expect(page.getByRole("heading", { name: /Project 2/i })).toBeVisible();
  await page.getByPlaceholder("Describe what to change, what failed, or what to avoid.").fill("Focus on the failing test path first.");
  await page.getByRole("button", { name: "Queue Guided Fix" }).click();
  await expect(page.getByText(/Queued developer to work issue #2 with operator guidance/i)).toBeVisible();
});

test.describe("mobile", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("mobile nav still switches workspaces", async ({ page }) => {
    await routeApi(page);
    await page.goto("/");

    await page.getByRole("navigation", { name: "Primary" }).getByRole("button", { name: "Projects" }).click();
    await expect(page.getByText(/Ideas already in active delivery/i)).toBeVisible();
  });
});
