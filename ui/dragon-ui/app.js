async function loadStatusSnapshot() {
  const response = await fetch("sample-status.json");
  if (!response.ok) {
    throw new Error(`Unable to load sample status (${response.status})`);
  }

  return response.json();
}

async function loadPreviousStatusSnapshot() {
  const response = await fetch("sample-status.previous.json");
  if (!response.ok) {
    return null;
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

function formatDelta(value) {
  if (value > 0) {
    return `+${value}`;
  }

  return String(value ?? 0);
}

function withFallbackTrend(currentSnapshot, previousSnapshot) {
  if (!previousSnapshot) {
    return {
      ...currentSnapshot,
      comparisonMode: "backend",
    };
  }

  if ((currentSnapshot.queueDirection && currentSnapshot.queueDirection !== "unknown") || currentSnapshot.queueComparedAt) {
    return {
      ...currentSnapshot,
      comparisonMode: "backend",
    };
  }

  const queueDelta = (currentSnapshot.queuedJobs ?? 0) - (previousSnapshot.queuedJobs ?? 0);
  const queueDirection = queueDelta > 0 ? "up" : queueDelta < 0 ? "down" : "flat";
  const currentRollup = currentSnapshot.rollup ?? {};
  const previousRollup = previousSnapshot.rollup ?? {};

  return {
    ...currentSnapshot,
    comparisonMode: "fallback",
    queueDirection,
    queueDelta,
    queueComparedAt: previousSnapshot.generatedAt ?? null,
    rollupDelta: {
      failedIssues: (currentRollup.failedIssues ?? 0) - (previousRollup.failedIssues ?? 0),
      quarantinedIssues: (currentRollup.quarantinedIssues ?? 0) - (previousRollup.quarantinedIssues ?? 0),
      inProgressIssues: (currentRollup.inProgressIssues ?? 0) - (previousRollup.inProgressIssues ?? 0),
      validatedIssues: (currentRollup.validatedIssues ?? 0) - (previousRollup.validatedIssues ?? 0),
    },
  };
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
  const queueDirection = document.getElementById("status-queue-direction");
  const queueComparedAt = document.getElementById("status-queue-compared-at");
  const comparisonMode = document.getElementById("status-comparison-mode");
  const attentionSummary = document.getElementById("status-attention-summary");
  const failed = document.getElementById("status-rollup-failed");
  const quarantined = document.getElementById("status-rollup-quarantined");
  const inProgress = document.getElementById("status-rollup-in-progress");
  const validated = document.getElementById("status-rollup-validated");
  const failedDelta = document.getElementById("status-rollup-delta-failed");
  const quarantinedDelta = document.getElementById("status-rollup-delta-quarantined");
  const inProgressDelta = document.getElementById("status-rollup-delta-in-progress");
  const validatedDelta = document.getElementById("status-rollup-delta-validated");
  const latestIssue = document.getElementById("status-latest-issue");
  const latestStage = document.getElementById("status-latest-stage");
  const latestSummary = document.getElementById("status-latest-summary");
  const loopMode = document.getElementById("status-loop-mode");
  const loopSummary = document.getElementById("status-loop-summary");
  const queueDelta = document.getElementById("status-queue-delta");
  chip.textContent = `${snapshot.issues.length} issues loaded from sample-status.json`;
  health.textContent = snapshot.health ?? "unknown";
  source.textContent = snapshot.source ?? "unknown";
  generatedAt.textContent = formatTimestamp(snapshot.generatedAt);
  queueDirection.textContent = snapshot.queueDirection ?? "unknown";
  queueComparedAt.textContent = snapshot.queueComparedAt ? formatTimestamp(snapshot.queueComparedAt) : "No prior snapshot";
  comparisonMode.textContent = snapshot.comparisonMode ?? "backend";
  attentionSummary.textContent = snapshot.attentionSummary ?? "No summary available";
  failed.textContent = String(snapshot.rollup?.failedIssues ?? 0);
  quarantined.textContent = String(snapshot.rollup?.quarantinedIssues ?? 0);
  inProgress.textContent = String(snapshot.rollup?.inProgressIssues ?? 0);
  validated.textContent = String(snapshot.rollup?.validatedIssues ?? 0);
  failedDelta.textContent = formatDelta(snapshot.rollupDelta?.failedIssues ?? 0);
  quarantinedDelta.textContent = formatDelta(snapshot.rollupDelta?.quarantinedIssues ?? 0);
  inProgressDelta.textContent = formatDelta(snapshot.rollupDelta?.inProgressIssues ?? 0);
  validatedDelta.textContent = formatDelta(snapshot.rollupDelta?.validatedIssues ?? 0);
  latestIssue.textContent = snapshot.latestActivity
    ? `#${snapshot.latestActivity.issueNumber} ${snapshot.latestActivity.issueTitle}`
    : "No recent execution";
  latestStage.textContent = snapshot.latestActivity?.currentStage ?? "unknown";
  latestSummary.textContent = snapshot.latestActivity?.summary ?? "No recent execution summary";
  loopMode.textContent = snapshot.recentLoopSignal?.mode ?? "unknown";
  loopSummary.textContent = snapshot.recentLoopSignal?.summary ?? "No recent loop summary";
  queueDelta.textContent = String(snapshot.queueDelta ?? 0);

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
    const snapshot = withFallbackTrend(
      await loadStatusSnapshot(),
      await loadPreviousStatusSnapshot()
    );
    renderStatusSnapshot(snapshot);
  } catch (error) {
    const chip = document.getElementById("status-chip");
    const feed = document.getElementById("status-feed");
    const health = document.getElementById("status-health");
    const source = document.getElementById("status-source");
    const generatedAt = document.getElementById("status-generated-at");
    const queueDirection = document.getElementById("status-queue-direction");
    const queueComparedAt = document.getElementById("status-queue-compared-at");
    const comparisonMode = document.getElementById("status-comparison-mode");
    const attentionSummary = document.getElementById("status-attention-summary");
    const failed = document.getElementById("status-rollup-failed");
    const quarantined = document.getElementById("status-rollup-quarantined");
    const inProgress = document.getElementById("status-rollup-in-progress");
    const validated = document.getElementById("status-rollup-validated");
    const failedDelta = document.getElementById("status-rollup-delta-failed");
    const quarantinedDelta = document.getElementById("status-rollup-delta-quarantined");
    const inProgressDelta = document.getElementById("status-rollup-delta-in-progress");
    const validatedDelta = document.getElementById("status-rollup-delta-validated");
    const latestIssue = document.getElementById("status-latest-issue");
    const latestStage = document.getElementById("status-latest-stage");
    const latestSummary = document.getElementById("status-latest-summary");
    const loopMode = document.getElementById("status-loop-mode");
    const loopSummary = document.getElementById("status-loop-summary");
    const queueDelta = document.getElementById("status-queue-delta");
    chip.textContent = "Sample snapshot unavailable";
    health.textContent = "unavailable";
    source.textContent = "unavailable";
    generatedAt.textContent = "Could not load sample payload";
    queueDirection.textContent = "unavailable";
    queueComparedAt.textContent = "Unavailable";
    comparisonMode.textContent = "unavailable";
    attentionSummary.textContent = "Could not load status summary";
    failed.textContent = "0";
    quarantined.textContent = "0";
    inProgress.textContent = "0";
    validated.textContent = "0";
    failedDelta.textContent = "0";
    quarantinedDelta.textContent = "0";
    inProgressDelta.textContent = "0";
    validatedDelta.textContent = "0";
    latestIssue.textContent = "No recent execution";
    latestStage.textContent = "unknown";
    latestSummary.textContent = "Could not load status summary";
    loopMode.textContent = "unavailable";
    loopSummary.textContent = "Could not load loop summary";
    queueDelta.textContent = "0";
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
