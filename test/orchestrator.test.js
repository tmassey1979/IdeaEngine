const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const path = require("path");

const {
  buildCapabilityCatalog,
  createSelfBuildJob,
  publishNextJob,
  recommendAgentForIssue,
  selectNextIssue
} = require("../services/dragon-orchestrator/src/index");
const { workspaceRoot } = require("../runner/dragon-agent-runner/src/index");

test("capability catalog is built from registered agents", () => {
  const catalog = buildCapabilityCatalog(workspaceRoot());
  assert.equal(catalog.some((entry) => entry.id === "developer"), true);
  assert.equal(catalog.some((entry) => entry.id === "architect"), true);
});

test("selectNextIssue chooses the lowest-number open story", () => {
  const issue = selectNextIssue([
    { number: 99, title: "Later", state: "OPEN", labels: ["story"] },
    { number: 40, title: "Epic", state: "OPEN", labels: ["epic"] },
    { number: 12, title: "First story", state: "OPEN", labels: ["story"] }
  ]);

  assert.equal(issue.number, 12);
});

test("recommendAgentForIssue maps issue title to best-fit agent", () => {
  const catalog = buildCapabilityCatalog(workspaceRoot());
  assert.equal(
    recommendAgentForIssue(
      { title: "[Story] Something: Write tests", labels: ["story"] },
      catalog
    ),
    "test"
  );
  assert.equal(
    recommendAgentForIssue(
      { title: "[Story] Something: Review workflow", labels: ["story"] },
      catalog
    ),
    "review"
  );
});

test("createSelfBuildJob builds a developer job from an issue", () => {
  const job = createSelfBuildJob({
    issue: { number: 72, title: "Implement runner", labels: ["story"] },
    agent: "developer",
    repo: "IdeaEngine",
    project: "DragonIdeaEngine"
  });

  assert.equal(job.agent, "developer");
  assert.equal(job.issue, 72);
  assert.equal(job.metadata.source, "dragon-orchestrator");
});

test("publishNextJob emits the next queued self-build job", () => {
  const tempRoot = fs.mkdtempSync(path.join(workspaceRoot(), "tmp-orchestrator-"));
  const result = publishNextJob({
    rootDir: tempRoot,
    repo: "IdeaEngine",
    project: "DragonIdeaEngine",
    issues: [
      { number: 102, title: "Review docs", state: "OPEN", labels: ["story"] },
      { number: 87, title: "Implement developer workflow", state: "OPEN", labels: ["story"] }
    ]
  });

  assert.equal(result.published, true);
  assert.equal(result.issue.number, 87);
  assert.equal(result.agent, "developer");
  assert.equal(fs.existsSync(result.publishResult.path), true);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});
