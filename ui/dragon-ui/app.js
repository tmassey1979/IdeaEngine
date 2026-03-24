const LIVE_STATUS_ENDPOINTS = buildLiveStatusEndpoints();

const IDEA_DATA = [
  {
    id: "pi-engine",
    name: "Pi-hosted Dragon Idea Engine",
    status: "printing",
    queuePosition: "Now building",
    phase: "Platform wiring",
    eta: "Mar 27",
    stack: "ASP.NET Core, Docker, PostgreSQL, TypeScript UI",
    summary:
      "Core single-Pi autonomous engine with orchestrator, queueing, validation, and operator visibility.",
    blockers: ["Host CPU and memory telemetry still needs a real backend feed."],
    backlog: [
      { title: "Finish dashboard-first UI shell", state: "In progress", owner: "documentation" },
      { title: "Wire real host telemetry into dashboard health", state: "Queued", owner: "developer" },
      { title: "Expand structured validation for more scaffold profiles", state: "Queued", owner: "refactor" },
    ],
    board: {
      Backlog: [{ title: "Host telemetry endpoint", meta: "backend" }],
      "In Progress": [{ title: "Dashboard-first shell", meta: "ui" }],
      Review: [{ title: "Idea queue states + ETA copy", meta: "product" }],
      Done: [{ title: "Status simplification direction", meta: "ux" }],
    },
    activity: [
      "Reshaped milestone around Pi MVP stories and added UI redesign stories.",
      "System status now feeds a compact dashboard summary instead of driving the whole page.",
      "Profile-aware validation continues to improve backend throughput behind the scenes.",
    ],
    projectHealth: "attention",
  },
  {
    id: "service-ops",
    name: "Field Service Ops Assistant",
    status: "queued",
    queuePosition: "#1",
    phase: "Approved for MVP slicing",
    eta: "Mar 29",
    stack: "React, TypeScript, ASP.NET Core, PostgreSQL",
    summary:
      "Dispatch board and invoice-ready workflow for small service teams that need a calm admin experience.",
    blockers: ["Waiting for the Pi engine platform slice to finish its current milestone work."],
    backlog: [
      { title: "Draft MVP stories from intake", state: "Queued", owner: "idea" },
      { title: "Create project skeleton", state: "Queued", owner: "refactor" },
      { title: "Map auth and billing requirements", state: "Queued", owner: "architect" },
    ],
    board: {
      Backlog: [{ title: "Queue MVP stories", meta: "planning" }],
      "In Progress": [],
      Review: [],
      Done: [{ title: "Approval granted", meta: "operator" }],
    },
    activity: [
      "Idea approved and placed at the front of the queue.",
      "Preferred stack captured during intake.",
      "MVP forecast is based on current throughput plus active platform work.",
    ],
    projectHealth: "steady",
  },
  {
    id: "client-portal",
    name: "Customer Self-Service Portal",
    status: "under-review",
    queuePosition: "Pending approval",
    phase: "Reviewing scope",
    eta: "Needs approval",
    stack: "Next.js, TypeScript, ASP.NET Core APIs",
    summary:
      "Customer portal for requests, job history, and invoices once core service ops workflows are stable.",
    blockers: ["Scope is larger than the current MVP budget and needs trimming."],
    backlog: [
      { title: "Reduce MVP to request + history baseline", state: "Review", owner: "idea" },
      { title: "Decide auth boundary", state: "Review", owner: "architect" },
    ],
    board: {
      Backlog: [],
      "In Progress": [{ title: "Approval review", meta: "operator" }],
      Review: [{ title: "Scope reduction", meta: "planning" }],
      Done: [],
    },
    activity: [
      "Idea is in review while the MVP line is tightened.",
      "Technical direction looks good, but scope is still broad.",
    ],
    projectHealth: "steady",
  },
  {
    id: "invoice-ai",
    name: "Invoice Summarizer Assistant",
    status: "approved",
    queuePosition: "#2",
    phase: "Approved and waiting",
    eta: "Apr 1",
    stack: "TypeScript worker, OpenAI, queue-driven processing",
    summary:
      "AI-assisted invoice summary and note cleanup for field teams after technician notes are submitted.",
    blockers: ["Depends on the service ops core data model landing first."],
    backlog: [
      { title: "Define note-to-invoice prompt contract", state: "Queued", owner: "architect" },
      { title: "Build worker scaffold", state: "Queued", owner: "refactor" },
    ],
    board: {
      Backlog: [{ title: "Worker scaffold", meta: "pipeline" }],
      "In Progress": [],
      Review: [],
      Done: [{ title: "Idea approved", meta: "operator" }],
    },
    activity: [
      "Idea approved and queued behind the service ops project.",
    ],
    projectHealth: "steady",
  },
  {
    id: "parts-market",
    name: "Parts Marketplace",
    status: "rejected",
    queuePosition: "Not queued",
    phase: "Rejected for MVP",
    eta: "Not scheduled",
    stack: "Marketplace stack undecided",
    summary:
      "Large multi-sided marketplace concept that does not fit the current Pi MVP window.",
    blockers: ["Rejected because it does not fit the next-three-days MVP target."],
    backlog: [
      { title: "Re-scope into smaller standalone idea later", state: "Deferred", owner: "operator" },
    ],
    board: {
      Backlog: [],
      "In Progress": [],
      Review: [],
      Done: [{ title: "Rejected for current milestone", meta: "operator" }],
    },
    activity: [
      "Idea was rejected for the current milestone due to size and complexity.",
    ],
    projectHealth: "steady",
  },
];

const TAB_IDS = ["backlog", "board", "activity"];
let selectedIdeaId = IDEA_DATA[0].id;

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
  const health = (snapshot.health || "").toLowerCase();
  if (health === "healthy") {
    return "Stable";
  }

  if (health === "attention") {
    return "Needs attention";
  }

  if (health === "quarantined") {
    return "Recovery active";
  }

  return "Observing";
}

function serviceNote(snapshot) {
  const counts = snapshot.rollup || {};
  return `${counts.inProgressIssues ?? 0} active issue(s), ${snapshot.queuedJobs ?? 0} queued job(s)`;
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
        <span>${idea.phase}</span>
        <span>MVP ${idea.eta}</span>
        <span>${idea.stack}</span>
      </div>
    </button>
  `;
}

function buildQueuePreviewCard(idea) {
  return `
    <div class="queue-card">
      <div class="queue-card-header">
        <div>
          <h4>${idea.name}</h4>
          <p class="subtle">${idea.phase}</p>
        </div>
        <span class="pill ${idea.status}">${idea.queuePosition}</span>
      </div>
      <div class="meta-row">
        <span>${formatStatus(idea.status)}</span>
        <span>MVP ${idea.eta}</span>
      </div>
    </div>
  `;
}

function buildForecastCard(idea) {
  return `
    <div class="forecast-card">
      <div class="forecast-row">
        <div>
          <strong>${idea.name}</strong>
          <p>${idea.phase}</p>
        </div>
        <strong>${idea.eta}</strong>
      </div>
    </div>
  `;
}

function formatStatus(value) {
  return value
    .replace(/-/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function renderDashboard(snapshot) {
  const activeIdeas = IDEA_DATA.filter((idea) => idea.status === "printing");
  const queuedIdeas = IDEA_DATA.filter((idea) => ["queued", "approved"].includes(idea.status));
  const forecastIdeas = IDEA_DATA.filter((idea) => ["printing", "queued", "approved"].includes(idea.status));

  text("status-loop-health", healthLabel(snapshot));
  text("status-triage", snapshot.triageSummary || snapshot.attentionSummary || "No active triage summary");
  text("status-services", servicesLabel(snapshot));
  text("status-service-note", serviceNote(snapshot));
  text("status-source", sourceLabel(snapshot));
  text("status-wait-signal", snapshot.waitSignal || "Routine poll wait");
  text("status-loop-summary", snapshot.recentLoopSignal?.summary || "No recent loop summary");
  text("status-lead-work", leadWorkLabel(snapshot));
  text("status-writeback", writebackLabel(snapshot));
  text("status-provider-retry", providerRetryLabel(snapshot));
  text("status-active-projects", `${activeIdeas.length} active`);
  text("status-approved-ideas", `${queuedIdeas.length} approved`);
  text("status-mvp-window", `${forecastIdeas[0]?.eta || "TBD"} earliest`);
  text("status-queue-depth", `${snapshot.queuedJobs ?? 0} queued job(s)`);

  document.getElementById("active-projects-list").innerHTML = activeIdeas.map(buildProjectCard).join("");
  document.getElementById("queue-preview-list").innerHTML = queuedIdeas.map(buildQueuePreviewCard).join("");
  document.getElementById("forecast-list").innerHTML = forecastIdeas.map(buildForecastCard).join("");
}

function renderQueueAndProjects() {
  const tbody = document.getElementById("queue-table-body");
  tbody.innerHTML = IDEA_DATA.map(
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

  const projectGrid = document.getElementById("project-grid");
  const projects = IDEA_DATA.filter((idea) => ["printing", "queued", "approved", "under-review"].includes(idea.status));
  projectGrid.innerHTML = projects.map(buildProjectCard).join("");
}

function renderIdeaDetail() {
  const idea = IDEA_DATA.find((entry) => entry.id === selectedIdeaId) || IDEA_DATA[0];

  text("idea-detail-title", idea.name);
  text("idea-detail-status", formatStatus(idea.status));
  text("idea-detail-queue", idea.queuePosition);
  text("idea-detail-phase", idea.phase);
  text("idea-detail-eta", idea.eta);
  text("idea-detail-summary", idea.summary);
  text("idea-detail-stack", idea.stack);

  document.getElementById("idea-detail-blockers").innerHTML = idea.blockers.length
    ? idea.blockers.map((blocker) => `<li>${blocker}</li>`).join("")
    : "<li>No active blockers.</li>";

  document.getElementById("idea-backlog-list").innerHTML = idea.backlog.length
    ? idea.backlog.map(
        (item) => `
          <article class="backlog-card">
            <div class="backlog-card-head">
              <h4>${item.title}</h4>
              <span class="pill">${item.state}</span>
            </div>
            <p class="subtle">Owner: ${item.owner}</p>
          </article>
        `,
      ).join("")
    : `<div class="empty-state">No backlog items available yet.</div>`;

  const board = document.getElementById("idea-board");
  board.innerHTML = Object.entries(idea.board).map(
    ([column, tickets]) => `
      <section class="board-column">
        <h4>${column}</h4>
        ${tickets.length
          ? tickets.map(
              (ticket) => `
                <article class="board-ticket">
                  <p>${ticket.title}</p>
                  <span>${ticket.meta}</span>
                </article>
              `,
            ).join("")
          : `<div class="empty-state">No items</div>`}
      </section>
    `,
  ).join("");

  document.getElementById("idea-activity-list").innerHTML = idea.activity.length
    ? idea.activity.map(
        (entry, index) => `
          <article class="activity-card">
            <div class="activity-card-head">
              <strong>Update ${index + 1}</strong>
              <span class="subtle">${idea.phase}</span>
            </div>
            <p class="subtle">${entry}</p>
          </article>
        `,
      ).join("")
    : `<div class="empty-state">No recent activity.</div>`;
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
  const snapshot = await loadStatusSnapshot();
  renderDashboard(snapshot);
  renderQueueAndProjects();
  renderIdeaDetail();
  bindIdeaSelection();
}

bootstrap().catch((error) => {
  text("status-loop-health", "Unavailable");
  text("status-triage", error instanceof Error ? error.message : "Unable to load status");
  renderQueueAndProjects();
  renderIdeaDetail();
  bindIdeaSelection();
});
