const LIVE_STATUS_ENDPOINTS = buildLiveStatusEndpoints();

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

function ageLabel(value) {
  if (!value) {
    return "n/a";
  }

  return freshnessInfo(value).label;
}

function leadQuarantineUrgency(snapshot) {
  const leadQuarantine = snapshot.leadQuarantine;
  if (!leadQuarantine || leadQuarantine.state !== "sync-drift" || !leadQuarantine.oldestPendingGithubSyncAt) {
    return "normal";
  }

  const freshness = freshnessInfo(leadQuarantine.oldestPendingGithubSyncAt);
  if (freshness.state === "stale") {
    return "alert";
  }

  if (freshness.state === "aging" || freshness.state === "fresh") {
    return "caution";
  }

  return "normal";
}

function interventionTargetUrgency(snapshot) {
  const escalation = snapshot.interventionTarget?.escalation;
  if (escalation === "critical") {
    return "alert";
  }

  if (escalation === "warning") {
    return "caution";
  }

  const kind = snapshot.interventionTarget?.kind;
  if (kind === "github-replay-drift") {
    return "alert";
  }

  if (kind === "recovery-work") {
    return "caution";
  }

  return "normal";
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

function statusChipLabel(snapshot) {
  switch (snapshot.uiPayloadSource) {
    case "live-http":
      return `Live snapshot from ${snapshot.uiStatusEndpoint ?? "/status"}`;
    case "sample-script":
      return `${snapshot.issues.length} issues loaded from local sample script`;
    case "sample-json":
      return `${snapshot.issues.length} issues loaded from sample-status.json`;
    default:
      return `${snapshot.issues.length} issues loaded`;
  }
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

function workerCompletionInfo(snapshot) {
  const reason = snapshot.workerCompletionReason;
  const state = snapshot.workerState ?? "snapshot";

  if (!reason) {
    if (state === "waiting" || state === "running") {
      return { label: "active", state: "active" };
    }

    if (state === "complete") {
      return { label: "complete", state: "complete" };
    }

    if (state === "snapshot") {
      return { label: "not recorded", state: "snapshot" };
    }

    return { label: "unavailable", state: "unavailable" };
  }

  switch (reason) {
    case "idle_target_reached":
      return { label: "idle target reached", state: "complete" };
    case "idle_run_completed":
      return { label: "idle run completed", state: "complete" };
    case "max_passes_reached":
      return { label: "pass cap reached", state: "capped" };
    case "max_cycles_reached":
      return { label: "cycle cap reached", state: "capped" };
    default:
      return { label: reason.replaceAll("_", " "), state: "snapshot" };
  }
}

function githubSyncInfo(snapshot) {
  const sync = snapshot.latestGithubSync;
  if (!sync) {
    return { label: "not recorded", state: "snapshot" };
  }

  if (sync.updated) {
    return { label: "updated", state: "complete" };
  }

  if (sync.attempted) {
    return { label: "degraded", state: "capped" };
  }

  return { label: "skipped", state: "snapshot" };
}

function pendingGithubSyncInfo(snapshot) {
  const count = snapshot.pendingGithubSyncCount ?? 0;
  if (count > 0) {
    return { label: String(count), state: "waiting" };
  }

  return { label: "0", state: "complete" };
}

function workerNote(snapshot) {
  const state = snapshot.workerState ?? "snapshot";
  const completion = workerCompletionInfo(snapshot);
  const nextPollLabel = snapshot.nextPollAt ? formatTimestamp(snapshot.nextPollAt) : "the next scheduled interval";
  const cadenceLabel = snapshot.pollIntervalSeconds
    ? `every ${snapshot.pollIntervalSeconds} second${snapshot.pollIntervalSeconds === 1 ? "" : "s"}`
    : null;
  const currentPassNumber = snapshot.currentPassNumber ?? snapshot.latestPass?.passNumber ?? 0;
  const maxPasses = snapshot.maxPasses;
  const passProgressLabelText = typeof maxPasses === "number" && maxPasses > 0
    ? ` Current pass progress: ${currentPassNumber} / ${maxPasses}.`
    : currentPassNumber > 0
      ? ` Current pass: ${currentPassNumber}.`
      : "";
  const idleTarget = snapshot.idleTarget ?? 0;
  const idlePassesRemaining = snapshot.idlePassesRemaining;
  const passBudgetRemaining = snapshot.passBudgetRemaining;
  const idleProgressLabel = idleTarget > 0
    ? ` Current idle progress: ${snapshot.idleStreak ?? 0} / ${idleTarget}.`
    : snapshot.idleStreak
      ? ` Current idle streak: ${snapshot.idleStreak}.`
      : "";
  const idleRemainingLabelText = typeof idlePassesRemaining === "number"
    ? ` Idle passes remaining: ${idlePassesRemaining}.`
    : "";
  const passBudgetLabel = typeof passBudgetRemaining === "number"
    ? ` Remaining pass budget: ${passBudgetRemaining}.`
    : "";
  const githubSyncLabel = snapshot.latestGithubSync
    ? ` Latest GitHub sync for issue #${snapshot.latestGithubSync.issueNumber}: ${snapshot.latestGithubSync.summary}.`
    : "";
  const githubReplayLabel = snapshot.latestGithubReplay
    ? ` ${snapshot.latestGithubReplay.summary}`
    : "";
  const pendingGithubSyncLabel = snapshot.pendingGithubSyncSummary
    ? ` ${snapshot.pendingGithubSyncSummary}`
    : "";
  const interventionTargetLabel = snapshot.interventionTarget && snapshot.interventionTarget.kind !== "idle"
    ? ` Lead intervention target: ${snapshot.interventionTarget.summary}`
    : "";
  const leadQuarantineAgeLabel = snapshot.leadQuarantine?.oldestPendingGithubSyncAt
    ? ` Oldest recovery writeback drift: ${ageLabel(snapshot.leadQuarantine.oldestPendingGithubSyncAt)}.`
    : "";

  if (state === "waiting") {
    return {
      label: "Waiting",
      state: "waiting",
      text: cadenceLabel
        ? `Worker is paused between passes, polling ${cadenceLabel}, and is scheduled to poll again at ${nextPollLabel}.${passProgressLabelText}${idleProgressLabel}${idleRemainingLabelText}${passBudgetLabel}${githubSyncLabel}${githubReplayLabel}${pendingGithubSyncLabel}${interventionTargetLabel}${leadQuarantineAgeLabel}`
        : `Worker is paused between passes and is scheduled to poll again at ${nextPollLabel}.${passProgressLabelText}${idleProgressLabel}${idleRemainingLabelText}${passBudgetLabel}${githubSyncLabel}${githubReplayLabel}${pendingGithubSyncLabel}${interventionTargetLabel}${leadQuarantineAgeLabel}`,
    };
  }

  if (state === "running") {
    return {
      label: "Running",
      state: "running",
      text: cadenceLabel
        ? `Worker is actively processing the current pass and will continue polling ${cadenceLabel} after this pass completes.${passProgressLabelText}${idleProgressLabel}${idleRemainingLabelText}${passBudgetLabel}${githubSyncLabel}${githubReplayLabel}${pendingGithubSyncLabel}${interventionTargetLabel}${leadQuarantineAgeLabel}`
        : `Worker is actively processing the current pass.${passProgressLabelText}${idleProgressLabel}${idleRemainingLabelText}${passBudgetLabel}${githubSyncLabel}${githubReplayLabel}${pendingGithubSyncLabel}${interventionTargetLabel}${leadQuarantineAgeLabel}`,
    };
  }

  if (state === "complete") {
    return {
      label: "Complete",
      state: "complete",
      text: `Worker finished its current run and is not waiting on another scheduled pass.${completion.label !== "complete" ? ` Stop reason: ${completion.label}.` : ""}${passProgressLabelText}${idleRemainingLabelText}${githubSyncLabel}${githubReplayLabel}${pendingGithubSyncLabel}${interventionTargetLabel}${leadQuarantineAgeLabel}`,
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

function pollCadenceLabel(snapshot) {
  if (!snapshot.pollIntervalSeconds) {
    return "Not scheduled";
  }

  const seconds = snapshot.pollIntervalSeconds;
  if (seconds < 60) {
    return `${seconds}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  if (remainder === 0) {
    return `${minutes}m`;
  }

  return `${minutes}m ${remainder}s`;
}

function idleStreakLabel(snapshot) {
  const idleStreak = snapshot.idleStreak ?? 0;
  const idleTarget = snapshot.idleTarget ?? 0;

  if (idleTarget > 0) {
    return `${idleStreak} / ${idleTarget}`;
  }

  return String(idleStreak);
}

function idleRemainingLabel(snapshot) {
  if (typeof snapshot.idlePassesRemaining !== "number") {
    return "n/a";
  }

  return String(snapshot.idlePassesRemaining);
}

function passBudgetLabel(snapshot) {
  if (typeof snapshot.passBudgetRemaining !== "number") {
    return "n/a";
  }

  return String(snapshot.passBudgetRemaining);
}

function passProgressLabel(snapshot) {
  const currentPassNumber = snapshot.currentPassNumber ?? snapshot.latestPass?.passNumber ?? 0;
  const maxPasses = snapshot.maxPasses;

  if (typeof maxPasses === "number" && maxPasses > 0) {
    return `${currentPassNumber} / ${maxPasses}`;
  }

  if (currentPassNumber > 0) {
    return String(currentPassNumber);
  }

  return "n/a";
}

function workerProgressLabel(snapshot) {
  return `pass ${passProgressLabel(snapshot)} · idle ${idleStreakLabel(snapshot)} · remaining ${idleRemainingLabel(snapshot)} · budget ${passBudgetLabel(snapshot)}`;
}

function workerProgressState(snapshot) {
  const state = snapshot.workerState ?? "snapshot";

  if (snapshot.recentLoopSignal?.mode === "repairing") {
    return "waiting";
  }

  if (state === "waiting") {
    const idleTarget = snapshot.idleTarget ?? 0;
    const idleStreak = snapshot.idleStreak ?? 0;
    if (idleTarget > 0 && idleStreak >= idleTarget) {
      return "ready";
    }

    return "waiting";
  }

  if (state === "complete") {
    return "complete";
  }

  if (state === "snapshot") {
    return "snapshot";
  }

  return "unavailable";
}

function latestPassOutcome(snapshot) {
  const latestPass = snapshot.latestPass;
  if (!latestPass) {
    return "No polling summary";
  }

  if ((latestPass.githubReplayAttemptedCount ?? 0) > 0 && latestPass.cycleCount === 0) {
    return "Pass repaired GitHub drift";
  }

  if (latestPass.reachedMaxCycles) {
    return "Pass hit max cycle cap";
  }

  if (latestPass.reachedIdle) {
    return "Pass reached idle";
  }

  return "Pass completed with remaining work";
}

function latestPassOutcomeState(label) {
  switch (label) {
    case "Pass repaired GitHub drift":
      return "active";
    case "Pass reached idle":
      return "idle";
    case "Pass completed with remaining work":
      return "active";
    case "Pass hit max cycle cap":
      return "capped";
    case "Could not load pass summary":
      return "unavailable";
    default:
      return "unknown";
  }
}

function latestPassMix(snapshot) {
  const latestPass = snapshot.latestPass;
  if (!latestPass) {
    return "No pass mix";
  }

  if (latestPass.seededCycles === 0 && latestPass.consumedCycles === 0) {
    if ((latestPass.githubReplayAttemptedCount ?? 0) > 0) {
      return "Repair-only";
    }
    return "Idle";
  }

  if (latestPass.seededCycles > 0 && latestPass.consumedCycles === 0) {
    return "Seed-heavy";
  }

  if (latestPass.consumedCycles > 0 && latestPass.seededCycles === 0) {
    return "Consume-heavy";
  }

  if (latestPass.seededCycles === latestPass.consumedCycles) {
    return "Balanced";
  }

  return latestPass.seededCycles > latestPass.consumedCycles ? "Seed-leaning" : "Consume-leaning";
}

function latestPassMixState(label) {
  switch (label) {
    case "Seed-heavy":
    case "Seed-leaning":
      return "seed";
    case "Repair-only":
    case "Consume-heavy":
    case "Consume-leaning":
      return "consume";
    case "Balanced":
      return "balanced";
    case "Idle":
      return "idle";
    default:
      return "unknown";
  }
}

function latestPassCycleState(snapshot) {
  const latestPass = snapshot.latestPass;
  if (!latestPass) {
    return { label: "0 cycles", state: "unknown" };
  }

  const label = `${latestPass.cycleCount} cycle${latestPass.cycleCount === 1 ? "" : "s"}`;
  if (latestPass.cycleCount === 0) {
    if ((latestPass.githubReplayAttemptedCount ?? 0) > 0) {
      return { label, state: "active" };
    }
    return { label, state: "idle" };
  }

  if (latestPass.reachedMaxCycles) {
    return { label, state: "capped" };
  }

  if (latestPass.reachedIdle) {
    return { label, state: "idle" };
  }

  return { label, state: "active" };
}

function latestPassReplayState(snapshot) {
  const latestPass = snapshot.latestPass;
  if (!latestPass) {
    return { label: "No replay", state: "unknown" };
  }

  if ((latestPass.githubReplayAttemptedCount ?? 0) === 0) {
    return { label: "No replay", state: "idle" };
  }

  const attempted = latestPass.githubReplayAttemptedCount ?? 0;
  const updated = latestPass.githubReplayUpdatedCount ?? 0;
  const failed = latestPass.githubReplayFailedCount ?? 0;
  const label = `Replay ${updated}/${attempted}`;

  if (failed > 0 && updated > 0) {
    return { label, state: "partial" };
  }

  if (failed > 0) {
    return { label, state: "failed" };
  }

  return { label, state: "recovered" };
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
  const workerCompletion = document.getElementById("status-worker-completion");
  const pollCadence = document.getElementById("status-poll-cadence");
  const workerProgress = document.getElementById("status-worker-progress");
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
  const quarantineDetail = document.getElementById("status-rollup-quarantine-detail");
  const inProgress = document.getElementById("status-rollup-in-progress");
  const validated = document.getElementById("status-rollup-validated");
  const failedDelta = document.getElementById("status-rollup-delta-failed");
  const quarantinedDelta = document.getElementById("status-rollup-delta-quarantined");
  const inProgressDelta = document.getElementById("status-rollup-delta-in-progress");
  const validatedDelta = document.getElementById("status-rollup-delta-validated");
  const latestActivityGroup = document.getElementById("status-latest-activity-group");
  const leadJobGroup = document.getElementById("status-lead-job-group");
  const leadJobIssue = document.getElementById("status-lead-job-issue");
  const leadJobWorkType = document.getElementById("status-lead-job-work-type");
  const leadJobAgent = document.getElementById("status-lead-job-agent");
  const leadJobAction = document.getElementById("status-lead-job-action");
  const leadJobBlocking = document.getElementById("status-lead-job-blocking");
  const leadJobTarget = document.getElementById("status-lead-job-target");
  const leadJobOutcome = document.getElementById("status-lead-job-outcome");
  const leadJobPriority = document.getElementById("status-lead-job-priority");
  const interventionTargetGroup = document.getElementById("status-intervention-target-group");
  const interventionTargetIssue = document.getElementById("status-intervention-target-issue");
  const interventionTargetKind = document.getElementById("status-intervention-target-kind");
  const interventionTargetRecovery = document.getElementById("status-intervention-target-recovery");
  const interventionTargetArtifact = document.getElementById("status-intervention-target-artifact");
  const interventionTargetAge = document.getElementById("status-intervention-target-age");
  const interventionTargetEscalation = document.getElementById("status-intervention-target-escalation");
  const interventionTargetSummary = document.getElementById("status-intervention-target-summary");
  const leadQuarantineGroup = document.getElementById("status-lead-quarantine-group");
  const leadQuarantineIssue = document.getElementById("status-lead-quarantine-issue");
  const leadQuarantineState = document.getElementById("status-lead-quarantine-state");
  const leadQuarantineRecovery = document.getElementById("status-lead-quarantine-recovery");
  const leadQuarantineJobs = document.getElementById("status-lead-quarantine-jobs");
  const leadQuarantineAge = document.getElementById("status-lead-quarantine-age");
  const leadQuarantineNote = document.getElementById("status-lead-quarantine-note");
  const leadQuarantineSummary = document.getElementById("status-lead-quarantine-summary");
  const latestPassGroup = document.getElementById("status-latest-pass-group");
  const latestIssue = document.getElementById("status-latest-issue");
  const latestStage = document.getElementById("status-latest-stage");
  const latestSummary = document.getElementById("status-latest-summary");
  const latestPassNumber = document.getElementById("status-latest-pass-number");
  const latestPassCycles = document.getElementById("status-latest-pass-cycles");
  const latestPassWork = document.getElementById("status-latest-pass-work");
  const latestPassReplay = document.getElementById("status-latest-pass-replay");
  const latestPassMixNode = document.getElementById("status-latest-pass-mix");
  const latestPassOutcomeNode = document.getElementById("status-latest-pass-outcome");
  const pendingGithubGroup = document.getElementById("status-pending-github-group");
  const pendingGithubSummary = document.getElementById("status-pending-github-summary");
  const pendingGithubList = document.getElementById("status-pending-github-list");
  const loopMode = document.getElementById("status-loop-mode");
  const loopSummary = document.getElementById("status-loop-summary");
  const queueDelta = document.getElementById("status-queue-delta");
  chip.textContent = statusChipLabel(snapshot);
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
  document.getElementById("status-worker-activity").textContent = snapshot.workerActivity ?? "Not recorded";
  const workerCompletionValue = workerCompletionInfo(snapshot);
  workerCompletion.textContent = workerCompletionValue.label;
  workerCompletion.className = `worker-completion ${workerCompletionValue.state}`;
  const githubSync = document.getElementById("status-github-sync");
  const githubSyncValue = githubSyncInfo(snapshot);
  githubSync.textContent = githubSyncValue.label;
  githubSync.className = `worker-completion ${githubSyncValue.state}`;
  const pendingGithubSync = document.getElementById("status-pending-github-sync");
  const pendingGithubSyncValue = pendingGithubSyncInfo(snapshot);
  pendingGithubSync.textContent = pendingGithubSyncValue.label;
  pendingGithubSync.className = `worker-progress ${pendingGithubSyncValue.state}`;
  pollCadence.textContent = pollCadenceLabel(snapshot);
  workerProgress.textContent = workerProgressLabel(snapshot);
  workerProgress.className = `worker-progress ${workerProgressState(snapshot)}`;
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
  const leadQuarantineUrgencyState = leadQuarantineUrgency(snapshot);
  const interventionTargetUrgencyState = interventionTargetUrgency(snapshot);
  workerNoteLabel.textContent = workerNoteState.label;
  workerNoteText.textContent = workerNoteState.text;
  const workerUrgencyState = interventionTargetUrgencyState !== "normal" ? interventionTargetUrgencyState : leadQuarantineUrgencyState;
  workerNoteNode.className = `status-worker-note ${workerNoteState.state}${workerUrgencyState !== "normal" ? ` drift-${workerUrgencyState}` : ""}`;
  attentionSummary.textContent = snapshot.attentionSummary ?? "No summary available";
  failed.textContent = String(snapshot.rollup?.failedIssues ?? 0);
  quarantined.textContent = String(snapshot.rollup?.quarantinedIssues ?? 0);
  quarantineDetail.textContent = `${snapshot.rollup?.actionableQuarantinedIssues ?? 0} actionable / ${snapshot.rollup?.inactiveQuarantinedIssues ?? 0} inactive`;
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
  leadJobIssue.textContent = snapshot.leadJob
    ? `#${snapshot.leadJob.issueNumber} ${snapshot.leadJob.issueTitle}`
    : "No queued work";
  leadJobWorkType.textContent = snapshot.leadJob?.workType ?? "story";
  leadJobAgent.textContent = snapshot.leadJob?.agent ?? "unknown";
  leadJobAction.textContent = snapshot.leadJob?.action ?? "unknown";
  leadJobBlocking.textContent = snapshot.leadJob?.blocking ? "yes" : "no";
  leadJobTarget.textContent = snapshot.leadJob?.targetArtifact ?? "none";
  leadJobOutcome.textContent = snapshot.leadJob?.targetOutcome ?? "none";
  leadJobPriority.textContent = snapshot.leadJob?.priority ?? "normal";
  leadJobGroup.className = "status-activity";
  interventionTargetIssue.textContent = snapshot.interventionTarget?.issueNumber
    ? `#${snapshot.interventionTarget.issueNumber}`
    : snapshot.interventionTarget?.kind === "idle"
      ? "No immediate target"
      : "No issue target";
  interventionTargetKind.textContent = snapshot.interventionTarget?.kind ?? "idle";
  interventionTargetRecovery.textContent = snapshot.interventionTarget?.recoveryIssueNumber
    ? `#${snapshot.interventionTarget.recoveryIssueNumber}`
    : "none";
  interventionTargetArtifact.textContent = snapshot.interventionTarget?.targetArtifact ?? "none";
  interventionTargetAge.textContent = snapshot.interventionTarget?.ageSummary ?? "n/a";
  interventionTargetEscalation.textContent = snapshot.interventionTarget?.escalation ?? "normal";
  interventionTargetSummary.textContent = snapshot.interventionTarget?.summary ?? "No immediate intervention target.";
  interventionTargetGroup.className = snapshot.interventionTarget && snapshot.interventionTarget.kind !== "idle"
    ? `status-activity ${interventionTargetUrgencyState === "alert" ? "alert" : interventionTargetUrgencyState === "caution" ? "caution" : ""}`.trim()
    : "status-activity";
  leadQuarantineIssue.textContent = snapshot.leadQuarantine
    ? `#${snapshot.leadQuarantine.issueNumber} ${snapshot.leadQuarantine.issueTitle}`
    : "No actionable quarantine";
  leadQuarantineState.textContent = snapshot.leadQuarantine?.state ?? "inactive";
  leadQuarantineRecovery.textContent = snapshot.leadQuarantine?.recoveryIssueNumber
    ? `#${snapshot.leadQuarantine.recoveryIssueNumber} ${snapshot.leadQuarantine.recoveryIssueTitle ?? ""}`.trim()
    : "none";
  leadQuarantineJobs.textContent = String(snapshot.leadQuarantine?.queuedRecoveryJobs ?? 0);
  leadQuarantineAge.textContent = ageLabel(snapshot.leadQuarantine?.oldestPendingGithubSyncAt);
  leadQuarantineNote.textContent = snapshot.leadQuarantine?.note ?? "No recovery hold recorded";
  leadQuarantineSummary.textContent = snapshot.leadQuarantine?.summary ?? "No active recovery blocker";
  leadQuarantineGroup.className = snapshot.leadQuarantine
    ? `status-activity ${leadQuarantineUrgencyState === "alert" ? "alert" : leadQuarantineUrgencyState === "caution" ? "caution" : "caution"}`
    : "status-activity";
  latestIssue.textContent = snapshot.latestActivity
    ? `#${snapshot.latestActivity.issueNumber} ${snapshot.latestActivity.issueTitle}`
    : "No recent execution";
  latestStage.textContent = snapshot.latestActivity?.currentStage ?? "unknown";
  latestSummary.textContent = snapshot.latestActivity?.summary ?? "No recent execution summary";
  latestActivityGroup.className = "status-activity";
  latestPassGroup.className = "status-activity";
  latestPassNumber.textContent = snapshot.latestPass ? `Pass ${snapshot.latestPass.passNumber}` : "No pass recorded";
  const latestPassCycle = latestPassCycleState(snapshot);
  latestPassCycles.textContent = latestPassCycle.label;
  latestPassCycles.className = `cycle-mix ${latestPassCycle.state}`;
  latestPassWork.textContent = snapshot.latestPass
    ? snapshot.latestPass.githubReplayAttemptedCount > 0
      ? `${snapshot.latestPass.seededCycles} seed, ${snapshot.latestPass.consumedCycles} consume, replay ${snapshot.latestPass.githubReplayUpdatedCount}/${snapshot.latestPass.githubReplayAttemptedCount}`
      : `${snapshot.latestPass.seededCycles} seed, ${snapshot.latestPass.consumedCycles} consume`
    : "0 seed, 0 consume";
  const latestPassReplayValue = latestPassReplayState(snapshot);
  latestPassReplay.textContent = latestPassReplayValue.label;
  latestPassReplay.className = `pass-replay ${latestPassReplayValue.state}`;
  const latestPassMixLabel = latestPassMix(snapshot);
  latestPassMixNode.textContent = latestPassMixLabel;
  latestPassMixNode.className = `pass-mix ${latestPassMixState(latestPassMixLabel)}`;
  const latestPassOutcomeLabel = latestPassOutcome(snapshot);
  latestPassOutcomeNode.textContent = latestPassOutcomeLabel;
  latestPassOutcomeNode.className = `pass-outcome ${latestPassOutcomeState(latestPassOutcomeLabel)}`;
  pendingGithubGroup.className = "status-activity";
  const pendingGithubItems = snapshot.pendingGithubSync ?? [];
  pendingGithubSummary.textContent = pendingGithubItems.length
    ? `${pendingGithubItems.length} issue update${pendingGithubItems.length === 1 ? "" : "s"} waiting`
    : "No pending GitHub updates";
  pendingGithubList.innerHTML = pendingGithubItems.length
    ? pendingGithubItems
        .slice(0, 5)
        .map((item) => `
          <div class="status-line">
            <span>#${item.issueNumber}</span>
            <strong>${item.summary}</strong>
            <p class="status-timestamp">
              Attempt ${item.attemptCount ?? 1}
              ${item.lastAttemptedAt ? ` • last tried ${formatTimestamp(item.lastAttemptedAt)}` : ""}
              ${item.nextRetryAt ? ` • next retry ${formatTimestamp(item.nextRetryAt)}` : ""}
              ${item.recordedAt ? ` • first queued ${formatTimestamp(item.recordedAt)}` : ""}
            </p>
          </div>
        `)
        .join("")
    : '<p class="status-line">No pending GitHub sync backlog.</p>';
  if (snapshot.recentLoopSignal?.mode === "failing" || snapshot.recentLoopSignal?.mode === "blocked") {
    latestActivityGroup.classList.add("alert");
    latestPassGroup.classList.add("alert");
    if (pendingGithubItems.length) {
      pendingGithubGroup.classList.add("alert");
    }
    feed.classList.add("deemphasized");
  } else if (snapshot.recentLoopSignal?.mode === "draining" || snapshot.recentLoopSignal?.mode === "repairing") {
    latestActivityGroup.classList.add("caution");
    latestPassGroup.classList.add("caution");
    if (pendingGithubItems.length) {
      pendingGithubGroup.classList.add("caution");
    }
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
    const workerCompletion = document.getElementById("status-worker-completion");
    const pollCadence = document.getElementById("status-poll-cadence");
    const workerProgress = document.getElementById("status-worker-progress");
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
    const quarantineDetail = document.getElementById("status-rollup-quarantine-detail");
    const inProgress = document.getElementById("status-rollup-in-progress");
    const validated = document.getElementById("status-rollup-validated");
    const failedDelta = document.getElementById("status-rollup-delta-failed");
    const quarantinedDelta = document.getElementById("status-rollup-delta-quarantined");
    const inProgressDelta = document.getElementById("status-rollup-delta-in-progress");
    const validatedDelta = document.getElementById("status-rollup-delta-validated");
    const latestActivityGroup = document.getElementById("status-latest-activity-group");
    const leadJobGroup = document.getElementById("status-lead-job-group");
    const leadJobIssue = document.getElementById("status-lead-job-issue");
    const leadJobWorkType = document.getElementById("status-lead-job-work-type");
    const leadJobAgent = document.getElementById("status-lead-job-agent");
    const leadJobAction = document.getElementById("status-lead-job-action");
    const leadJobBlocking = document.getElementById("status-lead-job-blocking");
    const leadJobTarget = document.getElementById("status-lead-job-target");
    const leadJobOutcome = document.getElementById("status-lead-job-outcome");
    const leadJobPriority = document.getElementById("status-lead-job-priority");
    const interventionTargetGroup = document.getElementById("status-intervention-target-group");
    const interventionTargetIssue = document.getElementById("status-intervention-target-issue");
    const interventionTargetKind = document.getElementById("status-intervention-target-kind");
    const interventionTargetRecovery = document.getElementById("status-intervention-target-recovery");
    const interventionTargetArtifact = document.getElementById("status-intervention-target-artifact");
    const interventionTargetAge = document.getElementById("status-intervention-target-age");
    const interventionTargetEscalation = document.getElementById("status-intervention-target-escalation");
    const interventionTargetSummary = document.getElementById("status-intervention-target-summary");
    const leadQuarantineGroup = document.getElementById("status-lead-quarantine-group");
    const leadQuarantineIssue = document.getElementById("status-lead-quarantine-issue");
    const leadQuarantineState = document.getElementById("status-lead-quarantine-state");
    const leadQuarantineRecovery = document.getElementById("status-lead-quarantine-recovery");
    const leadQuarantineJobs = document.getElementById("status-lead-quarantine-jobs");
    const leadQuarantineAge = document.getElementById("status-lead-quarantine-age");
    const leadQuarantineNote = document.getElementById("status-lead-quarantine-note");
    const leadQuarantineSummary = document.getElementById("status-lead-quarantine-summary");
    const latestPassGroup = document.getElementById("status-latest-pass-group");
    const latestIssue = document.getElementById("status-latest-issue");
    const latestStage = document.getElementById("status-latest-stage");
    const latestSummary = document.getElementById("status-latest-summary");
    const latestPassNumber = document.getElementById("status-latest-pass-number");
    const latestPassCycles = document.getElementById("status-latest-pass-cycles");
    const latestPassWork = document.getElementById("status-latest-pass-work");
    const latestPassReplay = document.getElementById("status-latest-pass-replay");
    const latestPassMixNode = document.getElementById("status-latest-pass-mix");
    const latestPassOutcomeNode = document.getElementById("status-latest-pass-outcome");
    const pendingGithubSummary = document.getElementById("status-pending-github-summary");
    const pendingGithubList = document.getElementById("status-pending-github-list");
    const loopMode = document.getElementById("status-loop-mode");
    const loopSummary = document.getElementById("status-loop-summary");
    const queueDelta = document.getElementById("status-queue-delta");
    chip.textContent = "Status snapshot unavailable";
    feed.className = "status-feed";
    health.textContent = "unavailable";
    source.textContent = "unavailable";
    lastCommand.textContent = "unavailable";
    workerMode.textContent = "unavailable";
    workerMode.className = "worker-mode unavailable";
    workerState.textContent = "unavailable";
    workerState.className = "worker-state unavailable";
    document.getElementById("status-worker-activity").textContent = "unavailable";
    workerCompletion.textContent = "unavailable";
    workerCompletion.className = "worker-completion unavailable";
    document.getElementById("status-github-sync").textContent = "unavailable";
    document.getElementById("status-github-sync").className = "worker-completion unavailable";
    document.getElementById("status-pending-github-sync").textContent = "unavailable";
    document.getElementById("status-pending-github-sync").className = "worker-progress unavailable";
    pollCadence.textContent = "Unavailable";
    workerProgress.textContent = "Unavailable";
    workerProgress.className = "worker-progress unavailable";
    generatedAt.textContent = "Could not load status payload";
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
    quarantineDetail.textContent = "0 actionable / 0 inactive";
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
    leadJobIssue.textContent = "No queued work";
    leadJobWorkType.textContent = "unavailable";
    leadJobAgent.textContent = "unavailable";
    leadJobAction.textContent = "unavailable";
    leadJobBlocking.textContent = "unavailable";
    leadJobTarget.textContent = "unavailable";
    leadJobOutcome.textContent = "unavailable";
    leadJobPriority.textContent = "unavailable";
    leadJobGroup.className = "status-activity";
    interventionTargetIssue.textContent = "Unavailable";
    interventionTargetKind.textContent = "unavailable";
    interventionTargetRecovery.textContent = "unavailable";
    interventionTargetArtifact.textContent = "unavailable";
    interventionTargetAge.textContent = "unavailable";
    interventionTargetEscalation.textContent = "unavailable";
    interventionTargetSummary.textContent = "Intervention target unavailable";
    interventionTargetGroup.className = "status-activity";
    leadQuarantineIssue.textContent = "Unavailable";
    leadQuarantineState.textContent = "unavailable";
    leadQuarantineRecovery.textContent = "unavailable";
    leadQuarantineJobs.textContent = "unavailable";
    leadQuarantineAge.textContent = "unavailable";
    leadQuarantineNote.textContent = "Recovery status unavailable";
    leadQuarantineSummary.textContent = "Recovery summary unavailable";
    leadQuarantineGroup.className = "status-activity";
    latestIssue.textContent = "No recent execution";
    latestStage.textContent = "unknown";
    latestSummary.textContent = "Could not load status summary";
    latestActivityGroup.className = "status-activity";
    latestPassGroup.className = "status-activity";
    latestPassNumber.textContent = "Unavailable";
    latestPassCycles.textContent = "Unavailable";
    latestPassCycles.className = "cycle-mix unavailable";
    latestPassWork.textContent = "Unavailable";
    latestPassReplay.textContent = "Unavailable";
    latestPassReplay.className = "pass-replay unavailable";
    latestPassMixNode.textContent = "Unavailable";
    latestPassMixNode.className = "pass-mix unavailable";
    latestPassOutcomeNode.textContent = "Could not load pass summary";
    latestPassOutcomeNode.className = "pass-outcome unavailable";
    pendingGithubSummary.textContent = "unavailable";
    pendingGithubList.innerHTML = '<p class="status-line">Pending GitHub sync details are unavailable.</p>';
    loopMode.textContent = "unavailable";
    loopSummary.textContent = "Could not load loop summary";
    queueDelta.textContent = "0";
    queueDelta.className = "queue-trend unavailable";
    feed.innerHTML = `
      <article class="status-card">
        <p class="panel-label">Status load failed</p>
        <h4>Could not load status snapshot</h4>
        <p>${error instanceof Error ? error.message : "Unknown error"}</p>
      </article>
    `;
  }
}

bootStatusMock();
