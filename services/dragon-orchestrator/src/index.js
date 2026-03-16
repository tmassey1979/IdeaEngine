const { execFileSync } = require("child_process");
const fs = require("fs");
const path = require("path");

const {
  createJobPublisher
} = require("../../../sdk/dragon-agent-sdk/src/index");
const {
  discoverAgents,
  runJob,
  workspaceRoot
} = require("../../../runner/dragon-agent-runner/src/index");

const PLANNER_RULES = [
  {
    name: "architecture-doc",
    match: ({ heading, title }) =>
      /(architecture|core system principles|system architecture|registry architecture)/i.test(
        `${heading} ${title}`
      ),
    targetPath: "docs/generated/architecture-notes.md",
    sectionTitle: "Architecture Notes"
  },
  {
    name: "sdk-doc",
    match: ({ heading, title }) => /(sdk|agent interface|agent context|agent result|developer agent)/i.test(`${heading} ${title}`),
    targetPath: "docs/generated/sdk-notes.md",
    sectionTitle: "SDK Notes"
  },
  {
    name: "operations-doc",
    match: ({ heading, title }) =>
      /(pipeline|workflow|loop|review|test|compliance|security|validation)/i.test(
        `${heading} ${title}`
      ),
    targetPath: "docs/generated/operations-notes.md",
    sectionTitle: "Operations Notes"
  },
  {
    name: "registry-doc",
    match: ({ heading, title }) =>
      /(registry|capability|discovery|node|cluster|health monitoring)/i.test(
        `${heading} ${title}`
      ),
    targetPath: "docs/generated/agent-registry.md",
    sectionTitle: "Agent Registry"
  }
];

function buildCapabilityCatalog(rootDir = workspaceRoot()) {
  return Array.from(discoverAgents(rootDir).values())
    .map((agent) => ({
      id: agent.id,
      name: agent.name,
      description: agent.description,
      version: agent.version
    }))
    .sort((left, right) => left.id.localeCompare(right.id));
}

function selectNextIssue(issues) {
  return [...issues]
    .filter((issue) => issue.state === "OPEN" || issue.state === "open")
    .filter((issue) => issue.labels.includes("story"))
    .sort((left, right) => left.number - right.number)[0];
}

function recommendAgentForIssue(issue, capabilityCatalog) {
  const title = issue.title.toLowerCase();
  const available = new Set(capabilityCatalog.map((capability) => capability.id));

  if (title.includes("review") && available.has("review")) {
    return "review";
  }

  if (title.includes("test") && available.has("test")) {
    return "test";
  }

  if (title.includes("refactor") && available.has("refactor")) {
    return "refactor";
  }

  if (title.includes("architect") && available.has("architect")) {
    return "architect";
  }

  if (available.has("developer")) {
    return "developer";
  }

  return capabilityCatalog[0]?.id || null;
}

function createSelfBuildJob({ issue, agent, repo, project }) {
  const payload = {
    title: issue.title,
    labels: issue.labels,
    heading: issue.heading || null,
    sourceFile: issue.sourceFile || null
  };

  if (agent === "developer") {
    payload.operations = planDeveloperOperations(issue);
  }

  return {
    agent,
    action: "implement_issue",
    repo,
    project,
    issue: issue.number,
    payload,
    metadata: {
      requestedBy: "system",
      source: "dragon-orchestrator",
      issueNumber: issue.number
    }
  };
}

function planDeveloperOperations(issue) {
  const title = issue.title.toLowerCase();
  const body = String(issue.body || "");
  const heading = String(issue.heading || "");
  const rule = PLANNER_RULES.find((candidate) => candidate.match({ heading, title }));

  if (title.includes("core system principles")) {
    return [
      {
        type: "append_text",
        path: "docs/ARCHITECTURE.md",
        content:
          "\n## Core System Principles\n\n- Agents are plugins loaded dynamically by the runner.\n- The runner supports CLI and service execution modes.\n- Jobs flow through an event-driven queue contract.\n"
      }
    ];
  }

  if (title.includes("registry architecture") || title.includes("capability catalog")) {
    return [
      {
        type: "write_file",
        path: "docs/AGENT_REGISTRY.md",
        content: [
          "# Agent Registry",
          "",
          "This document captures the current runtime capability catalog and the direction for registry-driven routing.",
          "",
          "## Current Capabilities",
          "",
          "- architect",
          "- developer",
          "- review",
          "- test",
          "- refactor"
        ].join("\n")
      }
    ];
  }

  if (title.includes("developer agent")) {
    return [
      {
        type: "append_text",
        path: "docs/SDK.md",
        content:
          "\n## Developer Operations\n\nThe developer agent supports bounded `write_file`, `append_text`, and `replace_text` operations for deterministic self-improvement tasks.\n"
      }
    ];
  }

  if (rule) {
    return [
      {
        type: "append_text",
        path: rule.targetPath,
        content: renderPlannedSection({
          sectionTitle: rule.sectionTitle,
          issue
        })
      }
    ];
  }

  return [
    {
      type: "append_text",
      path: "docs/BACKLOG_EXECUTION.md",
      content: [
        "",
        `## Issue #${issue.number}: ${issue.title}`,
        "",
        body ? body.split("\n").slice(0, 6).join("\n") : "Planned by the orchestrator for incremental self-build work."
      ].join("\n")
    }
  ];
}

function renderPlannedSection({ sectionTitle, issue }) {
  const body = String(issue.body || "").trim();
  const excerpt = body
    ? body.split("\n").slice(0, 8).join("\n")
    : "Planned automatically from backlog context.";

  return [
    "",
    `## ${issue.title}`,
    "",
    `Source heading: ${issue.heading || "n/a"}`,
    `Source file: ${issue.sourceFile || "n/a"}`,
    "",
    excerpt,
    ""
  ].join("\n");
}

function publishNextJob({
  issues,
  rootDir = workspaceRoot(),
  catalogRoot = workspaceRoot(),
  repo = "IdeaEngine",
  project = "DragonIdeaEngine",
  queue = "dragon.jobs"
}) {
  const capabilityCatalog = buildCapabilityCatalog(catalogRoot);
  const nextIssue = selectNextIssue(issues);

  if (!nextIssue) {
    return {
      published: false,
      reason: "No open story issues were available."
    };
  }

  const agent = recommendAgentForIssue(nextIssue, capabilityCatalog);
  if (!agent) {
    return {
      published: false,
      reason: "No agent capabilities are registered."
    };
  }

  const publisher = createJobPublisher({ rootDir, queue });
  const job = createSelfBuildJob({
    issue: nextIssue,
    agent,
    repo,
    project
  });
  const publishResult = publisher.publish(job);

  return {
    published: true,
    job,
    issue: nextIssue,
    agent,
    queue,
    capabilityCatalog,
    publishResult
  };
}

function queuePath(rootDir = workspaceRoot(), queue = "dragon.jobs") {
  return path.join(rootDir, ".dragon", "queues", `${queue}.ndjson`);
}

function readQueueJobs({ rootDir = workspaceRoot(), queue = "dragon.jobs" }) {
  const filePath = queuePath(rootDir, queue);
  if (!fs.existsSync(filePath)) {
    return [];
  }

  return fs
    .readFileSync(filePath, "utf8")
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function dequeueNextJob({ rootDir = workspaceRoot(), queue = "dragon.jobs" }) {
  const filePath = queuePath(rootDir, queue);
  const jobs = readQueueJobs({ rootDir, queue });
  const nextJob = jobs.shift() || null;

  if (jobs.length > 0) {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, `${jobs.map((job) => JSON.stringify(job)).join("\n")}\n`, "utf8");
  } else if (fs.existsSync(filePath)) {
    fs.unlinkSync(filePath);
  }

  return nextJob;
}

async function executeSelfBuildStep({
  owner,
  repo,
  rootDir = workspaceRoot(),
  catalogRoot = workspaceRoot(),
  project = "DragonIdeaEngine",
  queue = "dragon.jobs",
  issues
}) {
  const selection = publishNextJob({
    issues: issues || listGithubIssues({ owner, repo }),
    rootDir,
    catalogRoot,
    repo,
    project,
    queue
  });

  if (!selection.published) {
    return {
      executed: false,
      ...selection
    };
  }

  const execution = await runJob(selection.job, {
    rootDir,
    catalogRoot,
    queue
  });
  const followUps = publishFollowUpJobs({
    execution,
    issue: selection.issue,
    repo,
    project,
    rootDir,
    queue
  });
  const executionRecord = persistExecutionRecord({
    rootDir,
    issue: selection.issue,
    initialJob: selection.job,
    execution,
    followUps
  });

  return {
    executed: true,
    selection,
    execution,
    followUps,
    executionRecord
  };
}

async function consumeQueuedJob({
  owner,
  repo,
  rootDir = workspaceRoot(),
  catalogRoot = workspaceRoot(),
  queue = "dragon.jobs",
  ghBin = resolveGhBin(),
  exec = execFileSync
}) {
  const job = dequeueNextJob({ rootDir, queue });
  if (!job) {
    return {
      consumed: false,
      reason: "No queued jobs were available."
    };
  }

  const execution = await runJob(job, {
    rootDir,
    catalogRoot,
    queue
  });
  const followUps = job.agent === "developer"
    ? publishFollowUpJobs({
        execution,
        issue: {
          number: job.issue,
          title: job.payload?.title || `Issue ${job.issue}`
        },
        repo: job.repo,
        project: job.project,
        rootDir,
        queue
      })
    : [];
  const workflow = updateIssueWorkflowState({
    rootDir,
    issueNumber: job.issue,
    issueTitle: job.payload?.title || `Issue ${job.issue}`,
    agent: job.agent,
    execution,
    followUps
  });
  const githubSync = syncIssueWorkflowToGithub({
    workflow: workflow.issue,
    owner,
    repo,
    ghBin,
    exec
  });
  const executionRecord = persistExecutionRecord({
    rootDir,
    issue: {
      number: job.issue,
      title: job.payload?.title || `Issue ${job.issue}`
    },
    initialJob: job,
    execution,
    followUps,
    workflow: workflow.issue,
    githubSync
  });

  return {
    consumed: true,
    job,
    execution,
    followUps,
    workflow,
    githubSync,
    executionRecord
  };
}

async function cycleOnce({
  owner,
  repo,
  rootDir = workspaceRoot(),
  catalogRoot = workspaceRoot(),
  project = "DragonIdeaEngine",
  queue = "dragon.jobs",
  issues
}) {
  const queuedJobs = readQueueJobs({ rootDir, queue });
  if (queuedJobs.length > 0) {
    return {
      mode: "consume",
      result: await consumeQueuedJob({
        owner,
        repo,
        rootDir,
        catalogRoot,
        queue
      })
    };
  }

  return {
    mode: "seed",
    result: await executeSelfBuildStep({
      owner,
      repo,
      rootDir,
      catalogRoot,
      project,
      queue,
      issues
    })
  };
}

function publishFollowUpJobs({ execution, issue, repo, project, rootDir, queue }) {
  if (execution.status !== "success") {
    return [];
  }

  const publisher = createJobPublisher({ rootDir, queue });
  const jobs = [];

  for (const agent of ["review", "test"]) {
    const followUpJob = {
      agent,
      action: agent === "review" ? "review_issue" : "test_issue",
      repo,
      project,
      issue: issue.number,
      payload: {
        title: issue.title,
        previousAgent: execution.agent,
        previousJobId: execution.jobId
      },
      metadata: {
        requestedBy: "system",
        source: "dragon-orchestrator",
        parentJobId: execution.jobId,
        parentIssue: issue.number
      }
    };

    jobs.push({
      agent,
      job: followUpJob,
      publishResult: publisher.publish(followUpJob)
    });
  }

  return jobs;
}

function persistExecutionRecord({ rootDir, issue, initialJob, execution, followUps }) {
  const runsDir = path.join(rootDir, ".dragon", "runs");
  fs.mkdirSync(runsDir, { recursive: true });
  const recordPath = path.join(runsDir, `issue-${issue.number}.json`);
  const record = {
    recordedAt: new Date().toISOString(),
    issue,
    initialJob,
    execution,
    followUps
  };
  fs.writeFileSync(recordPath, JSON.stringify(record, null, 2));

  return {
    path: recordPath
  };
}

function syncIssueWorkflowToGithub({
  workflow,
  owner,
  repo,
  ghBin = resolveGhBin(),
  exec = execFileSync
}) {
  if (!owner || !repo) {
    return {
      synced: false,
      reason: "GitHub owner/repo not provided."
    };
  }

  if (workflow.overall !== "validated") {
    return {
      synced: false,
      reason: `Workflow is ${workflow.overall}, not validated.`
    };
  }

  const body = [
    "Validated automatically by Dragon Idea Engine.",
    "",
    `Stages completed: ${Object.keys(workflow.stages)
      .sort()
      .map((stage) => `${stage}=${workflow.stages[stage].status}`)
      .join(", ")}`
  ].join("\n");

  exec(
    ghBin,
    ["issue", "comment", String(workflow.issueNumber), "--repo", `${owner}/${repo}`, "--body", body],
    {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    }
  );
  const closed = exec(
    ghBin,
    [
      "issue",
      "close",
      String(workflow.issueNumber),
      "--repo",
      `${owner}/${repo}`,
      "--comment",
      "Closing automatically after developer, review, and test stages all succeeded."
    ],
    {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    }
  ).trim();

  return {
    synced: true,
    closed
  };
}

function updateIssueWorkflowState({
  rootDir,
  issueNumber,
  issueTitle,
  agent,
  execution,
  followUps
}) {
  const stateDir = path.join(rootDir, ".dragon", "state");
  const statePath = path.join(stateDir, "issues.json");
  fs.mkdirSync(stateDir, { recursive: true });

  const state = fs.existsSync(statePath)
    ? JSON.parse(fs.readFileSync(statePath, "utf8"))
    : {};

  const record = state[issueNumber] || {
    issueNumber,
    title: issueTitle,
    stages: {},
    overall: "queued"
  };

  record.title = issueTitle;
  record.stages[agent] = {
    status: execution.status,
    jobId: execution.jobId,
    observedAt: execution.observedAt
  };
  if (followUps.length > 0) {
    record.followUps = followUps.map((item) => ({
      agent: item.agent,
      jobId: item.publishResult.jobId
    }));
  }

  const stageStatuses = ["developer", "review", "test"].map(
    (stage) => record.stages[stage]?.status || null
  );
  if (stageStatuses.every((status) => status === "success")) {
    record.overall = "validated";
  } else if (stageStatuses.some((status) => status === "failed")) {
    record.overall = "failed";
  } else if (stageStatuses.some((status) => status === "deadletter")) {
    record.overall = "blocked";
  } else if (stageStatuses.some((status) => status === "retry")) {
    record.overall = "retrying";
  } else if (stageStatuses.some(Boolean)) {
    record.overall = "in_progress";
  }

  state[issueNumber] = record;
  fs.writeFileSync(statePath, JSON.stringify(state, null, 2));

  return {
    path: statePath,
    issue: record
  };
}

function listGithubIssues({ owner, repo, ghBin = resolveGhBin() }) {
  const raw = execFileSync(
    ghBin,
    [
      "issue",
      "list",
      "--repo",
      `${owner}/${repo}`,
      "--state",
      "open",
      "--limit",
      "400",
      "--json",
      "number,title,state,labels,body"
    ],
    {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    }
  );

  const backlogIndex = loadBacklogIndex();
  const issues = JSON.parse(raw);
  return issues.map((issue) => {
    const metadata = backlogIndex.get(issue.title) || {};
    return {
      number: issue.number,
      title: issue.title,
      state: issue.state,
      body: issue.body || "",
      heading: metadata.heading || null,
      sourceFile: metadata.sourceFile || null,
      labels: issue.labels.map((label) => label.name)
    };
  });
}

function loadBacklogIndex(rootDir = workspaceRoot()) {
  const backlogPath = path.join(rootDir, "planning", "backlog.json");
  if (!fs.existsSync(backlogPath)) {
    return new Map();
  }

  const backlog = JSON.parse(fs.readFileSync(backlogPath, "utf8"));
  const index = new Map();

  for (const story of backlog.stories || []) {
    index.set(story.title, {
      heading: story.heading || null,
      sourceFile: story.sourceFile || null
    });
  }

  return index;
}

function runSelfBuildCycle({
  owner,
  repo,
  rootDir = workspaceRoot(),
  catalogRoot = workspaceRoot(),
  project = "DragonIdeaEngine",
  queue = "dragon.jobs",
  issues
}) {
  const backlog = issues || listGithubIssues({ owner, repo });
  return publishNextJob({
    issues: backlog,
    rootDir,
    catalogRoot,
    repo,
    project,
    queue
  });
}

function resolveGhBin() {
  return process.env.GH_BIN || "/home/temassey/.local/bin/gh";
}

module.exports = {
  buildCapabilityCatalog,
  consumeQueuedJob,
  createSelfBuildJob,
  cycleOnce,
  dequeueNextJob,
  executeSelfBuildStep,
  listGithubIssues,
  loadBacklogIndex,
  planDeveloperOperations,
  persistExecutionRecord,
  publishFollowUpJobs,
  publishNextJob,
  queuePath,
  readQueueJobs,
  recommendAgentForIssue,
  runSelfBuildCycle,
  selectNextIssue,
  syncIssueWorkflowToGithub,
  updateIssueWorkflowState
};
