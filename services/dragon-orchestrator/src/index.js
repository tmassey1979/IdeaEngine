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
  return {
    agent,
    action: "implement_issue",
    repo,
    project,
    issue: issue.number,
    payload: {
      title: issue.title,
      labels: issue.labels
    },
    metadata: {
      requestedBy: "system",
      source: "dragon-orchestrator",
      issueNumber: issue.number
    }
  };
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
      "number,title,state,labels"
    ],
    {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    }
  );

  const issues = JSON.parse(raw);
  return issues.map((issue) => ({
    number: issue.number,
    title: issue.title,
    state: issue.state,
    labels: issue.labels.map((label) => label.name)
  }));
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
  createSelfBuildJob,
  executeSelfBuildStep,
  listGithubIssues,
  persistExecutionRecord,
  publishFollowUpJobs,
  publishNextJob,
  recommendAgentForIssue,
  runSelfBuildCycle,
  selectNextIssue
};
