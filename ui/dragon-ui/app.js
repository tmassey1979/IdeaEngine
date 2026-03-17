async function loadStatusSnapshot() {
  if (globalThis.__DRAGON_STATUS__) {
    return globalThis.__DRAGON_STATUS__;
  }

  const response = await fetch("sample-status.json");
  if (!response.ok) {
    throw new Error(`Unable to load sample status (${response.status})`);
  }

  return response.json();
}

async function loadPreviousStatusSnapshot() {
  if (globalThis.__DRAGON_PREVIOUS_STATUS__) {
    return globalThis.__DRAGON_PREVIOUS_STATUS__;
  }

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

function freshnessInfo(value) {
  if (!value) {
    return { label: "unknown", state: "unknown" };
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return { label: value, state: "unknown" };
  }

  const ageMs = Date.now() - parsed.getTime();
  if (ageMs < 0) {
    return { label: "future", state: "unknown" };
  }

  const ageMinutes = Math.floor(ageMs / 60000);
  if (ageMinutes < 2) {
    return { label: "just now", state: "fresh" };
  }

  if (ageMinutes < 15) {
    return { label: `${ageMinutes}m old`, state: "fresh" };
  }

  if (ageMinutes < 60) {
    return { label: `${ageMinutes}m old`, state: "aging" };
  }

  const ageHours = Math.floor(ageMinutes / 60);
  return { label: `${ageHours}h old`, state: "stale" };
}

function formatDelta(value) {
  if (value > 0) {
    return `+${value}`;
  }

  return String(value ?? 0);
}

function deltaClass(value) {
  if (value > 0) {
    return "positive";
  }

  if (value < 0) {
    return "negative";
  }

  return "";
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

function comparisonNote(snapshot) {
  if (snapshot.comparisonMode === "fallback") {
    return "Comparison values are being derived in the UI from the previous exported snapshot.";
  }

  if (snapshot.comparisonMode === "unavailable") {
    return "Comparison values are unavailable because the current status payload could not be loaded.";
  }

  return "Comparison data is coming directly from the exported backend snapshot.";
}

function comparisonLabel(snapshot) {
  if (snapshot.comparisonMode === "fallback") {
    return "Fallback";
  }

  if (snapshot.comparisonMode === "unavailable") {
    return "Unavailable";
  }

  return "Backend";
}

function workerModeInfo(snapshot) {
  const mode = snapshot.workerMode ?? "status";

  return {
    label: mode,
    state: mode,
  };
}

function workerStateInfo(snapshot) {
  const state = snapshot.workerState ?? "snapshot";

  return {
    label: state,
    state,
  };
}

function workerNote(snapshot) {
  const state = snapshot.workerState ?? "snapshot";
  const nextPollLabel = snapshot.nextPollAt ? formatTimestamp(snapshot.nextPollAt) : "the next scheduled interval";

  if (state === "waiting") {
    return {
      label: "Waiting",
      state: "waiting",
      text: `Worker is paused between passes and is scheduled to poll again at ${nextPollLabel}.`,
    };
  }

  if (state === "complete") {
    return {
      label: "Complete",
      state: "complete",
      text: "Worker finished its current run and is not waiting on another scheduled pass.",
    };
  }

  if (state === "snapshot") {
    return {
      label: "Snapshot",
      state: "snapshot",
      text: "This view reflects a captured status export rather than an actively waiting worker.",
    };
  }

  return {
    label: "Unavailable",
    state: "unavailable",
    text: "Worker state details are unavailable because the current status payload could not be loaded.",
  };
}

function latestPassOutcome(snapshot) {
  const latestPass = snapshot.latestPass;
  if (!latestPass) {
    return "No polling summary";
  }

  if (latestPass.reachedMaxCycles) {
    return "Pass hit max cycle cap";
  }

  if (latestPass.reachedIdle) {
    return "Pass reached idle";
  }

  return "Pass completed with remaining work";
}

function renderIssueCard(issue) {
  const workflowNote = issue.workflowNote ?? "none";
  const summary = issue.latestExecutionSummary ?? "none";
  const notes = issue.latestExecutionNotes ?? "none";
  const cardClass = issue.isPrimary ? "status-card primary" : "status-card";
  const leadBadge = issue.isPrimary ? '<span class="chip">Lead issue</span>' : "";

  return `
    <article class="${cardClass}">
      <div class="status-card-head">
        <div>
          <p class="panel-label">Issue #${issue.issueNumber}</p>
          <h4>${issue.issueTitle}</h4>
        </div>
        <div class="status-card-badges">
          ${leadBadge}
          <span class="badge ${badgeClassForStatus(issue.overallStatus)}">${issue.overallStatus}</span>
        </div>
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
  const lastCommand = document.getElementById("status-last-command");
  const workerMode = document.getElementById("status-worker-mode");
  const workerState = document.getElementById("status-worker-state");
  const generatedAt = document.getElementById("status-generated-at");
  const freshness = document.getElementById("status-freshness");
  const nextPoll = document.getElementById("status-next-poll");
  const queueDirection = document.getElementById("status-queue-direction");
  const queueComparedAt = document.getElementById("status-queue-compared-at");
  const compareAge = document.getElementById("status-compare-age");
  const comparisonMode = document.getElementById("status-comparison-mode");
  const compareNote = document.getElementById("status-compare-note");
  const compareLabel = document.querySelector(".status-compare-label");
  const compareNoteText = document.getElementById("status-compare-note-text");
  const workerNoteNode = document.getElementById("status-worker-note");
  const workerNoteLabel = document.querySelector(".status-worker-label");
  const workerNoteText = document.getElementById("status-worker-note-text");
  const attentionSummary = document.getElementById("status-attention-summary");
  const failed = document.getElementById("status-rollup-failed");
  const quarantined = document.getElementById("status-rollup-quarantined");
  const inProgress = document.getElementById("status-rollup-in-progress");
  const validated = document.getElementById("status-rollup-validated");
  const failedDelta = document.getElementById("status-rollup-delta-failed");
  const quarantinedDelta = document.getElementById("status-rollup-delta-quarantined");
  const inProgressDelta = document.getElementById("status-rollup-delta-in-progress");
  const validatedDelta = document.getElementById("status-rollup-delta-validated");
  const latestActivityGroup = document.getElementById("status-latest-activity-group");
  const latestPassGroup = document.getElementById("status-latest-pass-group");
  const latestIssue = document.getElementById("status-latest-issue");
  const latestStage = document.getElementById("status-latest-stage");
  const latestSummary = document.getElementById("status-latest-summary");
  const latestPassNumber = document.getElementById("status-latest-pass-number");
  const latestPassCycles = document.getElementById("status-latest-pass-cycles");
  const latestPassOutcomeNode = document.getElementById("status-latest-pass-outcome");
  const loopMode = document.getElementById("status-loop-mode");
  const loopSummary = document.getElementById("status-loop-summary");
  const queueDelta = document.getElementById("status-queue-delta");
  chip.textContent = `${snapshot.issues.length} issues loaded from sample-status.json`;
  feed.className = "status-feed";
  health.textContent = snapshot.health ?? "unknown";
  source.textContent = snapshot.source ?? "unknown";
  lastCommand.textContent = snapshot.lastCommand ?? snapshot.source ?? "unknown";
  const workerModeState = workerModeInfo(snapshot);
  workerMode.textContent = workerModeState.label;
  workerMode.className = `worker-mode ${workerModeState.state}`;
  const workerStateValue = workerStateInfo(snapshot);
  workerState.textContent = workerStateValue.label;
  workerState.className = `worker-state ${workerStateValue.state}`;
  generatedAt.textContent = formatTimestamp(snapshot.generatedAt);
  const freshnessState = freshnessInfo(snapshot.generatedAt);
  freshness.textContent = freshnessState.label;
  freshness.className = `snapshot-freshness ${freshnessState.state}`;
  nextPoll.textContent = snapshot.nextPollAt ? formatTimestamp(snapshot.nextPollAt) : "No next poll scheduled";
  queueDirection.textContent = snapshot.queueDirection ?? "unknown";
  queueDirection.className = `queue-trend ${snapshot.queueDirection ?? "unknown"}`;
  queueComparedAt.textContent = snapshot.queueComparedAt ? formatTimestamp(snapshot.queueComparedAt) : "No prior snapshot";
  const compareAgeState = freshnessInfo(snapshot.queueComparedAt);
  compareAge.textContent = snapshot.queueComparedAt ? compareAgeState.label : "n/a";
  compareAge.className = `snapshot-freshness ${snapshot.queueComparedAt ? compareAgeState.state : "unknown"}`;
  comparisonMode.textContent = snapshot.comparisonMode ?? "backend";
  comparisonMode.className = `comparison-mode ${snapshot.comparisonMode ?? "backend"}`;
  compareLabel.textContent = comparisonLabel(snapshot);
  compareNoteText.textContent = comparisonNote(snapshot);
  compareNote.className = `status-compare-note ${snapshot.comparisonMode ?? "backend"}`;
  const workerNoteState = workerNote(snapshot);
  workerNoteLabel.textContent = workerNoteState.label;
  workerNoteText.textContent = workerNoteState.text;
  workerNoteNode.className = `status-worker-note ${workerNoteState.state}`;
  attentionSummary.textContent = snapshot.attentionSummary ?? "No summary available";
  failed.textContent = String(snapshot.rollup?.failedIssues ?? 0);
  quarantined.textContent = String(snapshot.rollup?.quarantinedIssues ?? 0);
  inProgress.textContent = String(snapshot.rollup?.inProgressIssues ?? 0);
  validated.textContent = String(snapshot.rollup?.validatedIssues ?? 0);
  failedDelta.textContent = formatDelta(snapshot.rollupDelta?.failedIssues ?? 0);
  quarantinedDelta.textContent = formatDelta(snapshot.rollupDelta?.quarantinedIssues ?? 0);
  inProgressDelta.textContent = formatDelta(snapshot.rollupDelta?.inProgressIssues ?? 0);
  validatedDelta.textContent = formatDelta(snapshot.rollupDelta?.validatedIssues ?? 0);
  failedDelta.className = deltaClass(snapshot.rollupDelta?.failedIssues ?? 0);
  quarantinedDelta.className = deltaClass(snapshot.rollupDelta?.quarantinedIssues ?? 0);
  inProgressDelta.className = deltaClass(snapshot.rollupDelta?.inProgressIssues ?? 0);
  validatedDelta.className = deltaClass(snapshot.rollupDelta?.validatedIssues ?? 0);
  latestIssue.textContent = snapshot.latestActivity
    ? `#${snapshot.latestActivity.issueNumber} ${snapshot.latestActivity.issueTitle}`
    : "No recent execution";
  latestStage.textContent = snapshot.latestActivity?.currentStage ?? "unknown";
  latestSummary.textContent = snapshot.latestActivity?.summary ?? "No recent execution summary";
  latestActivityGroup.className = "status-activity";
  latestPassGroup.className = "status-activity";
  latestPassNumber.textContent = snapshot.latestPass ? `Pass ${snapshot.latestPass.passNumber}` : "No pass recorded";
  latestPassCycles.textContent = snapshot.latestPass
    ? `${snapshot.latestPass.cycleCount} cycles, ${snapshot.latestPass.seededCycles} seed, ${snapshot.latestPass.consumedCycles} consume`
    : "0 cycles";
  latestPassOutcomeNode.textContent = latestPassOutcome(snapshot);
  if (snapshot.recentLoopSignal?.mode === "failing" || snapshot.recentLoopSignal?.mode === "blocked") {
    latestActivityGroup.classList.add("alert");
    latestPassGroup.classList.add("alert");
    feed.classList.add("deemphasized");
  } else if (snapshot.recentLoopSignal?.mode === "draining") {
    latestActivityGroup.classList.add("caution");
    latestPassGroup.classList.add("caution");
  }
  loopMode.textContent = snapshot.recentLoopSignal?.mode ?? "unknown";
  loopSummary.textContent = snapshot.recentLoopSignal?.summary ?? "No recent loop summary";
  queueDelta.textContent = String(snapshot.queueDelta ?? 0);
  queueDelta.className = `queue-trend ${snapshot.queueDirection ?? "unknown"}`;

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

  feed.innerHTML = snapshot.issues
    .map((issue, index) => renderIssueCard({
      ...issue,
      isPrimary: index === 0 && snapshot.recentLoopSignal?.mode !== "failing" && snapshot.recentLoopSignal?.mode !== "blocked",
    }))
    .join("");
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
    const lastCommand = document.getElementById("status-last-command");
    const workerMode = document.getElementById("status-worker-mode");
    const workerState = document.getElementById("status-worker-state");
    const generatedAt = document.getElementById("status-generated-at");
    const freshness = document.getElementById("status-freshness");
    const nextPoll = document.getElementById("status-next-poll");
    const queueDirection = document.getElementById("status-queue-direction");
    const queueComparedAt = document.getElementById("status-queue-compared-at");
    const compareAge = document.getElementById("status-compare-age");
    const comparisonMode = document.getElementById("status-comparison-mode");
    const compareNote = document.getElementById("status-compare-note");
    const compareLabel = document.querySelector(".status-compare-label");
    const compareNoteText = document.getElementById("status-compare-note-text");
    const workerNoteNode = document.getElementById("status-worker-note");
    const workerNoteLabel = document.querySelector(".status-worker-label");
    const workerNoteText = document.getElementById("status-worker-note-text");
    const attentionSummary = document.getElementById("status-attention-summary");
    const failed = document.getElementById("status-rollup-failed");
    const quarantined = document.getElementById("status-rollup-quarantined");
    const inProgress = document.getElementById("status-rollup-in-progress");
    const validated = document.getElementById("status-rollup-validated");
    const failedDelta = document.getElementById("status-rollup-delta-failed");
    const quarantinedDelta = document.getElementById("status-rollup-delta-quarantined");
    const inProgressDelta = document.getElementById("status-rollup-delta-in-progress");
    const validatedDelta = document.getElementById("status-rollup-delta-validated");
    const latestActivityGroup = document.getElementById("status-latest-activity-group");
    const latestPassGroup = document.getElementById("status-latest-pass-group");
    const latestIssue = document.getElementById("status-latest-issue");
    const latestStage = document.getElementById("status-latest-stage");
    const latestSummary = document.getElementById("status-latest-summary");
    const latestPassNumber = document.getElementById("status-latest-pass-number");
    const latestPassCycles = document.getElementById("status-latest-pass-cycles");
    const latestPassOutcomeNode = document.getElementById("status-latest-pass-outcome");
    const loopMode = document.getElementById("status-loop-mode");
    const loopSummary = document.getElementById("status-loop-summary");
    const queueDelta = document.getElementById("status-queue-delta");
    chip.textContent = "Sample snapshot unavailable";
    feed.className = "status-feed";
    health.textContent = "unavailable";
    source.textContent = "unavailable";
    lastCommand.textContent = "unavailable";
    workerMode.textContent = "unavailable";
    workerMode.className = "worker-mode unavailable";
    workerState.textContent = "unavailable";
    workerState.className = "worker-state unavailable";
    generatedAt.textContent = "Could not load sample payload";
    freshness.textContent = "unavailable";
    freshness.className = "snapshot-freshness unavailable";
    nextPoll.textContent = "Unavailable";
    queueDirection.textContent = "unavailable";
    queueDirection.className = "queue-trend unavailable";
    queueComparedAt.textContent = "Unavailable";
    compareAge.textContent = "unavailable";
    compareAge.className = "snapshot-freshness unavailable";
    comparisonMode.textContent = "unavailable";
    comparisonMode.className = "comparison-mode unavailable";
    compareLabel.textContent = "Unavailable";
    compareNoteText.textContent = "Comparison values are unavailable because the current status payload could not be loaded.";
    compareNote.className = "status-compare-note unavailable";
    workerNoteLabel.textContent = "Unavailable";
    workerNoteText.textContent = "Worker state details are unavailable because the current status payload could not be loaded.";
    workerNoteNode.className = "status-worker-note unavailable";
    attentionSummary.textContent = "Could not load status summary";
    failed.textContent = "0";
    quarantined.textContent = "0";
    inProgress.textContent = "0";
    validated.textContent = "0";
    failedDelta.textContent = "0";
    quarantinedDelta.textContent = "0";
    inProgressDelta.textContent = "0";
    validatedDelta.textContent = "0";
    failedDelta.className = "";
    quarantinedDelta.className = "";
    inProgressDelta.className = "";
    validatedDelta.className = "";
    latestIssue.textContent = "No recent execution";
    latestStage.textContent = "unknown";
    latestSummary.textContent = "Could not load status summary";
    latestActivityGroup.className = "status-activity";
    latestPassGroup.className = "status-activity";
    latestPassNumber.textContent = "Unavailable";
    latestPassCycles.textContent = "Unavailable";
    latestPassOutcomeNode.textContent = "Could not load pass summary";
    loopMode.textContent = "unavailable";
    loopSummary.textContent = "Could not load loop summary";
    queueDelta.textContent = "0";
    queueDelta.className = "queue-trend unavailable";
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
