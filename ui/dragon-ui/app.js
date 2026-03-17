async function loadStatusSnapshot() {
  const response = await fetch("sample-status.json");
  if (!response.ok) {
    throw new Error(`Unable to load sample status (${response.status})`);
  }

  return response.json();
}

function formatTimestamp(value) {
  if (!value) {
    return "No execution recorded yet";
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

function badgeClassForStatus(status) {
  return status === "quarantined" ? "warn" : "good";
}

function renderIssueCard(issue) {
  const workflowNote = issue.workflowNote ?? "none";
  const summary = issue.latestExecutionSummary ?? "none";
  const notes = issue.latestExecutionNotes ?? "none";

  return `
    <article class="status-card">
      <div class="status-card-head">
        <div>
          <p class="panel-label">Issue #${issue.issueNumber}</p>
          <h4>${issue.issueTitle}</h4>
        </div>
        <span class="badge ${badgeClassForStatus(issue.overallStatus)}">${issue.overallStatus}</span>
      </div>
      <div class="status-meta-grid">
        <div>
          <span class="status-meta-label">Current stage</span>
          <strong>${issue.currentStage}</strong>
        </div>
        <div>
          <span class="status-meta-label">Queued jobs</span>
          <strong>${issue.queuedJobCount}</strong>
        </div>
      </div>
      <p class="status-line"><span>Workflow note</span>${workflowNote}</p>
      <p class="status-line"><span>Latest summary</span>${summary}</p>
      <p class="status-line"><span>Execution notes</span>${notes}</p>
      <p class="status-timestamp">${formatTimestamp(issue.latestExecutionRecordedAt)}</p>
    </article>
  `;
}

function renderStatusSnapshot(snapshot) {
  document.querySelectorAll('[data-status-stat="queued"]').forEach((node) => {
    node.textContent = String(snapshot.queuedJobs);
  });

  document.querySelectorAll('[data-status-stat="issues"]').forEach((node) => {
    node.textContent = String(snapshot.issues.length);
  });

  document.querySelectorAll('[data-status-stat="health"]').forEach((node) => {
    node.textContent = snapshot.health ?? "unknown";
  });

  const chip = document.getElementById("status-chip");
  const feed = document.getElementById("status-feed");
  const health = document.getElementById("status-health");
  const source = document.getElementById("status-source");
  const generatedAt = document.getElementById("status-generated-at");
  const attentionSummary = document.getElementById("status-attention-summary");
  chip.textContent = `${snapshot.issues.length} issues loaded from sample-status.json`;
  health.textContent = snapshot.health ?? "unknown";
  source.textContent = snapshot.source ?? "unknown";
  generatedAt.textContent = formatTimestamp(snapshot.generatedAt);
  attentionSummary.textContent = snapshot.attentionSummary ?? "No summary available";

  if (!snapshot.issues.length) {
    feed.innerHTML = `
      <article class="status-card">
        <p class="panel-label">Idle snapshot</p>
        <h4>No active issue workflows</h4>
        <p>The backend status shape loaded successfully, but there are no tracked issues in the sample payload.</p>
      </article>
    `;
    return;
  }

  feed.innerHTML = snapshot.issues.map(renderIssueCard).join("");
}

async function bootStatusMock() {
  try {
    const snapshot = await loadStatusSnapshot();
    renderStatusSnapshot(snapshot);
  } catch (error) {
    const chip = document.getElementById("status-chip");
    const feed = document.getElementById("status-feed");
    const health = document.getElementById("status-health");
    const source = document.getElementById("status-source");
    const generatedAt = document.getElementById("status-generated-at");
    const attentionSummary = document.getElementById("status-attention-summary");
    chip.textContent = "Sample snapshot unavailable";
    health.textContent = "unavailable";
    source.textContent = "unavailable";
    generatedAt.textContent = "Could not load sample payload";
    attentionSummary.textContent = "Could not load status summary";
    feed.innerHTML = `
      <article class="status-card">
        <p class="panel-label">Status load failed</p>
        <h4>Could not load sample status</h4>
        <p>${error instanceof Error ? error.message : "Unknown error"}</p>
      </article>
    `;
  }
}

bootStatusMock();
