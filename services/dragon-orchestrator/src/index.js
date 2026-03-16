const { execFileSync } = require("child_process");
const path = require("path");

const {
  createJobPublisher
} = require("../../../sdk/dragon-agent-sdk/src/index");
const { discoverAgents, workspaceRoot } = require("../../../runner/dragon-agent-runner/src/index");

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
    issue: nextIssue,
    agent,
    queue,
    capabilityCatalog,
    publishResult
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
  listGithubIssues,
  publishNextJob,
  recommendAgentForIssue,
  runSelfBuildCycle,
  selectNextIssue
};
