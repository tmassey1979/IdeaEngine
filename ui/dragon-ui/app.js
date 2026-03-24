const LIVE_STATUS_ENDPOINTS = buildLiveStatusEndpoints();
const TAB_IDS = ["backlog", "board", "activity"];

let selectedIdeaId = null;
let currentSnapshot = null;
let currentIdeas = [];

function buildLiveStatusEndpoints() {
  const endpoints = [];

  if (globalThis.location?.protocol === "http:" || globalThis.location?.protocol === "https:") {
    endpoints.push(new URL("/status", globalThis.location.href).toString());
  }

  endpoints.push("http://127.0.0.1:5078/status", "http://localhost:5078/status");
  return [...new Set(endpoints)];
}

async function fetchJsonWithTimeout(url, timeoutMs = 1500) {
  const controller = new AbortController();
  const timeout = globalThis.setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(url, {
      headers: { Accept: "application/json" },
      signal: controller.signal,
    });

    if (!response.ok) {
      throw new Error(`Unable to load status (${response.status})`);
    }

    return response.json();
  } finally {
    globalThis.clearTimeout(timeout);
  }
}

async function loadLiveStatusSnapshot() {
  for (const endpoint of LIVE_STATUS_ENDPOINTS) {
    try {
      const snapshot = await fetchJsonWithTimeout(endpoint);
      return { snapshot, endpoint };
    } catch {
    }
  }

  return null;
}

async function loadStatusSnapshot() {
  const liveStatus = await loadLiveStatusSnapshot();
  if (liveStatus) {
    return {
      ...liveStatus.snapshot,
      uiPayloadSource: "live-http",
      uiStatusEndpoint: liveStatus.endpoint,
    };
  }

  if (globalThis.__DRAGON_STATUS__) {
    return {
      ...globalThis.__DRAGON_STATUS__,
      uiPayloadSource: "sample-script",
    };
  }

  const snapshot = await fetchJsonWithTimeout("sample-status.json");
  return {
    ...snapshot,
    uiPayloadSource: "sample-json",
  };
}

function text(id, value) {
  const node = document.getElementById(id);
  if (node) {
    node.textContent = value;
  }
}

function healthLabel(snapshot) {
  return typeof snapshot.health === "string" && snapshot.health.trim()
    ? snapshot.health.replace(/-/g, " ")
    : "unknown";
}

function sourceLabel(snapshot) {
  switch (snapshot.uiPayloadSource) {
    case "live-http":
      return `Live backend status (${snapshot.uiStatusEndpoint})`;
    case "sample-script":
      return "Sample status script";
    case "sample-json":
      return "Sample status JSON";
    default:
      return "Unknown source";
  }
}

function servicesLabel(snapshot) {
  const services = Array.isArray(snapshot.services) ? snapshot.services : [];
  if (services.length === 0) {
    return "Unknown";
  }

  const healthyCount = services.filter((service) => service.status === "healthy").length;
  return `${healthyCount}/${services.length} healthy`;
}

function serviceNote(snapshot) {
  const services = Array.isArray(snapshot.services) ? snapshot.services : [];
  if (services.length === 0) {
    return "Service health is not exposed yet.";
  }

  const attentionService = services.find((service) => service.status === "attention");
  return attentionService?.summary || "All reported services look healthy.";
}

function leadWorkLabel(snapshot) {
  const leadJob = snapshot.leadJob;
  if (!leadJob) {
    return "No queued work";
  }

  const profile = leadJob.implementationProfile ? ` (${leadJob.implementationProfile})` : "";
  return `#${leadJob.issueNumber} ${leadJob.action || "work"}${profile}`;
}

function writebackLabel(snapshot) {
  const state = snapshot.pendingGithubSyncRetryState || "not scheduled";
  const count = snapshot.pendingGithubSyncCount ?? 0;
  return `${state} · ${count} pending`;
}

function providerRetryLabel(snapshot) {
  if (!snapshot.nextDelayedRetryAt) {
    return "No delayed retry scheduled";
  }

  return `Next retry ${formatTimestamp(snapshot.nextDelayedRetryAt)}`;
}

function formatTimestamp(value) {
  if (!value) {
    return "Not scheduled";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString([], {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function cpuLabel(snapshot) {
  const host = snapshot.hostTelemetry;
  if (!host) {
    return "Unavailable";
  }

  if (typeof host.processorLoadPercent === "number") {
    return `${Math.round(host.processorLoadPercent)}%`;
  }

  if (typeof host.processorCount === "number") {
    return `${host.processorCount} cores`;
  }

  return host.status === "unavailable" ? "Unavailable" : "Partial";
}

function memoryLabel(snapshot) {
  const host = snapshot.hostTelemetry;
  if (!host) {
    return "Unavailable";
  }

  if (typeof host.memoryUsedPercent === "number") {
    return `${Math.round(host.memoryUsedPercent)}%`;
  }

  return host.status === "unavailable" ? "Unavailable" : "Partial";
}

function memoryNote(snapshot) {
  const host = snapshot.hostTelemetry;
  if (!host) {
    return "Host telemetry is not exposed yet.";
  }

  if (typeof host.memoryAvailableMb === "number" && typeof host.memoryTotalMb === "number") {
    return `${host.memoryAvailableMb} MB free of ${host.memoryTotalMb} MB`;
  }

  return host.summary || "Host telemetry unavailable in this runtime";
}

function mapIssueStatus(issue) {
  const overallStatus = (issue.overallStatus || "").toLowerCase();
  if (overallStatus === "validated") {
    return "done";
  }

  if (overallStatus === "failed" || overallStatus === "quarantined") {
    return "blocked";
  }

  if (issue.queuedJobCount > 0 && overallStatus !== "in_progress") {
    return "queued";
  }

  if (overallStatus === "in_progress") {
    return "printing";
  }

  return "review";
}

function phaseLabel(issue) {
  return issue.currentStage
    ? issue.currentStage.replace(/_/g, " ")
    : "unknown";
}

function queuePositionLabel(issue) {
  if (issue.queuedJobCount > 0) {
    return `${issue.queuedJobCount} queued job(s)`;
  }

  return "Not exposed";
}

function buildRealIdeas(snapshot) {
  const issues = Array.isArray(snapshot.issues) ? snapshot.issues : [];
  return issues.map((issue) => {
    const blockers = [];
    if (issue.workflowNote) {
      blockers.push(issue.workflowNote);
    }

    if ((issue.overallStatus || "").toLowerCase() === "failed") {
      blockers.push("Execution failed and needs recovery or operator review.");
    }

    return {
      id: String(issue.issueNumber),
      issueNumber: issue.issueNumber,
      name: issue.issueTitle,
      status: mapIssueStatus(issue),
      queuePosition: queuePositionLabel(issue),
      phase: phaseLabel(issue),
      eta: "Not exposed yet",
      stack: "Not exposed yet",
      summary: issue.latestExecutionSummary || issue.workflowNote || "No idea-level summary is available yet.",
      blockers,
      latestExecutionRecordedAt: issue.latestExecutionRecordedAt || null,
      latestExecutionSummary: issue.latestExecutionSummary || null,
    };
  });
}

function formatStatus(value) {
  return value
    .replace(/-/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function buildProjectCard(idea) {
  return `
    <button class="project-card" data-idea-id="${idea.id}">
      <div class="project-card-header">
        <div>
          <h4>${idea.name}</h4>
          <p class="subtle">${idea.summary}</p>
        </div>
        <span class="pill ${idea.status}">${formatStatus(idea.status)}</span>
      </div>
      <div class="meta-row">
        <span>Phase: ${idea.phase}</span>
        <span>Queue: ${idea.queuePosition}</span>
      </div>
    </button>
  `;
}

function buildForecastCard(idea) {
  return `
    <div class="forecast-card">
      <div class="forecast-row">
        <div>
          <strong>${idea.name}</strong>
          <p>MVP ETA is not exposed yet.</p>
        </div>
        <strong>Unknown</strong>
      </div>
    </div>
  `;
}

function renderEmptyState(containerId, message) {
  const container = document.getElementById(containerId);
  if (container) {
    container.innerHTML = `<div class="empty-state">${message}</div>`;
  }
}

function renderDashboard(snapshot) {
  currentIdeas = buildRealIdeas(snapshot);
  const activeIdeas = currentIdeas.filter((idea) => ["printing", "blocked", "review"].includes(idea.status));

  text("status-loop-health", healthLabel(snapshot));
  text("status-triage", snapshot.triageSummary || snapshot.attentionSummary || "No active triage summary");
  text("status-services", servicesLabel(snapshot));
  text("status-service-note", serviceNote(snapshot));
  text("status-source", sourceLabel(snapshot));
  text("status-cpu", cpuLabel(snapshot));
  text("status-memory", memoryLabel(snapshot));
  text("status-memory-note", memoryNote(snapshot));
  text("status-wait-signal", snapshot.waitSignal || "Routine poll wait");
  text("status-loop-summary", snapshot.recentLoopSignal?.summary || "No recent loop summary");
  text("status-lead-work", leadWorkLabel(snapshot));
  text("status-writeback", writebackLabel(snapshot));
  text("status-provider-retry", providerRetryLabel(snapshot));
  text("status-active-projects", `${activeIdeas.length} active`);
  text("status-approved-ideas", "Not exposed");
  text("status-mvp-window", "Needs velocity + idea model");
  text("status-queue-depth", `${snapshot.queuedJobs ?? 0} queued job(s)`);

  if (activeIdeas.length > 0) {
    document.getElementById("active-projects-list").innerHTML = activeIdeas.map(buildProjectCard).join("");
  } else {
    renderEmptyState("active-projects-list", "No active idea/project data is available in the current status payload.");
  }

  renderEmptyState(
    "queue-preview-list",
    "Idea approval and queue ordering are not exposed by the backend yet, so this view cannot show real queue position data."
  );

  if (currentIdeas.length > 0) {
    document.getElementById("forecast-list").innerHTML = currentIdeas.slice(0, 3).map(buildForecastCard).join("");
  } else {
    renderEmptyState("forecast-list", "MVP forecast requires idea-level throughput and velocity data that is not wired yet.");
  }
}

function renderQueueAndProjects() {
  const tbody = document.getElementById("queue-table-body");
  if (currentIdeas.length === 0) {
    tbody.innerHTML = `
      <tr>
        <td colspan="5">
          <div class="empty-state">No real idea queue data is exposed yet. We currently have workflow issues, not submitted idea approval records.</div>
        </td>
      </tr>
    `;
  } else {
    tbody.innerHTML = currentIdeas.map(
      (idea) => `
        <tr data-idea-id="${idea.id}">
          <td><strong>${idea.name}</strong></td>
          <td><span class="pill ${idea.status}">${formatStatus(idea.status)}</span></td>
          <td>${idea.queuePosition}</td>
          <td>${idea.phase}</td>
          <td>${idea.eta}</td>
        </tr>
      `,
    ).join("");
  }

  const projectGrid = document.getElementById("project-grid");
  if (currentIdeas.length > 0) {
    projectGrid.innerHTML = currentIdeas.map(buildProjectCard).join("");
  } else {
    projectGrid.innerHTML = `<div class="empty-state">No project-level workflow issues were found in the current status payload.</div>`;
  }
}

function renderIdeaDetail() {
  const idea = currentIdeas.find((entry) => entry.id === selectedIdeaId) || currentIdeas[0];

  if (!idea) {
    text("idea-detail-title", "No idea selected");
    text("idea-detail-status", "Unavailable");
    text("idea-detail-queue", "Unavailable");
    text("idea-detail-phase", "Unavailable");
    text("idea-detail-eta", "Unavailable");
    text("idea-detail-summary", "Idea detail requires idea/project data from the backend.");
    text("idea-detail-stack", "Not exposed yet");
    renderEmptyState("idea-backlog-list", "Backlog data is not exposed yet.");
    renderEmptyState("idea-board", "Agile board data is not exposed yet.");
    renderEmptyState("idea-activity-list", "Activity data is not exposed yet.");
    document.getElementById("idea-detail-blockers").innerHTML = "<li>No idea/project selected.</li>";
    return;
  }

  text("idea-detail-title", idea.name);
  text("idea-detail-status", formatStatus(idea.status));
  text("idea-detail-queue", idea.queuePosition);
  text("idea-detail-phase", idea.phase);
  text("idea-detail-eta", idea.eta);
  text("idea-detail-summary", idea.summary);
  text("idea-detail-stack", idea.stack);

  document.getElementById("idea-detail-blockers").innerHTML = (idea.blockers.length > 0
    ? idea.blockers
    : ["Idea-level blockers are not explicitly exposed yet."]).map((blocker) => `<li>${blocker}</li>`).join("");

  renderEmptyState(
    "idea-backlog-list",
    "Backlog items are not exposed yet. We need idea-to-story grouping and idea-level backlog data from the backend."
  );

  renderEmptyState(
    "idea-board",
    "Agile board data is not exposed yet. We need story states grouped under a parent idea/project."
  );

  const activityEntries = [];
  if (idea.latestExecutionSummary) {
    activityEntries.push(`
      <article class="activity-card">
        <div class="activity-card-head">
          <strong>Latest execution</strong>
          <span class="subtle">${idea.latestExecutionRecordedAt ? formatTimestamp(idea.latestExecutionRecordedAt) : "Unknown time"}</span>
        </div>
        <p class="subtle">${idea.latestExecutionSummary}</p>
      </article>
    `);
  }

  activityEntries.push(`
    <article class="activity-card">
      <div class="activity-card-head">
        <strong>Missing data</strong>
        <span class="subtle">Needs backend work</span>
      </div>
      <p class="subtle">Queue position, MVP ETA, stack preferences, backlog grouping, and agile board state are not yet modeled as idea-level data.</p>
    </article>
  `);

  document.getElementById("idea-activity-list").innerHTML = activityEntries.join("");
}

function bindIdeaSelection() {
  document.addEventListener("click", (event) => {
    const selectionTarget = event.target.closest("[data-idea-id]");
    if (selectionTarget) {
      selectedIdeaId = selectionTarget.getAttribute("data-idea-id");
      renderIdeaDetail();
      document.getElementById("idea-detail").scrollIntoView({ behavior: "smooth", block: "start" });
      return;
    }

    const tab = event.target.closest(".tab");
    if (!tab) {
      return;
    }

    const nextTab = tab.getAttribute("data-tab");
    for (const tabButton of document.querySelectorAll(".tab")) {
      tabButton.classList.toggle("active", tabButton === tab);
    }

    for (const panel of document.querySelectorAll(".tab-panel")) {
      panel.classList.toggle("active", panel.getAttribute("data-panel") === nextTab);
    }
  });
}

async function bootstrap() {
  currentSnapshot = await loadStatusSnapshot();
  currentIdeas = buildRealIdeas(currentSnapshot);
  selectedIdeaId = currentIdeas[0]?.id ?? null;
  renderDashboard(currentSnapshot);
  renderQueueAndProjects();
  renderIdeaDetail();
  bindIdeaSelection();
}

bootstrap().catch((error) => {
  text("status-loop-health", "Unavailable");
  text("status-triage", error instanceof Error ? error.message : "Unable to load status");
  currentIdeas = [];
  renderQueueAndProjects();
  renderIdeaDetail();
  bindIdeaSelection();
});
