const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const os = require("os");
const path = require("path");

const {
  buildCapabilityCatalog,
  consumeQueuedJob,
  createSelfBuildJob,
  cycleOnce,
  dequeueNextJob,
  executeSelfBuildStep,
  persistExecutionRecord,
  planDeveloperOperations,
  publishFollowUpJobs,
  publishNextJob,
  readQueueJobs,
  recommendAgentForIssue,
  selectNextIssue,
  syncIssueWorkflowToGithub,
  updateIssueWorkflowState
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
    issue: { number: 72, title: "Implement runner", labels: ["story"], body: "" },
    agent: "developer",
    repo: "IdeaEngine",
    project: "DragonIdeaEngine"
  });

  assert.equal(job.agent, "developer");
  assert.equal(job.issue, 72);
  assert.equal(job.metadata.source, "dragon-orchestrator");
  assert.equal(Array.isArray(job.payload.operations), true);
});

test("planDeveloperOperations adds a core principles update", () => {
  const operations = planDeveloperOperations({
    number: 22,
    title: "[Story] Dragon Idea Engine Master Codex: Core System Principles",
    body: ""
  });

  assert.equal(operations[0].path, "docs/ARCHITECTURE.md");
  assert.match(operations[0].content, /Core System Principles/);
});

test("publishNextJob emits the next queued self-build job", () => {
  const tempRoot = makeTempDir("tmp-orchestrator-");
  const result = publishNextJob({
    rootDir: tempRoot,
    repo: "IdeaEngine",
    project: "DragonIdeaEngine",
    issues: [
      { number: 102, title: "Review docs", state: "OPEN", labels: ["story"] },
      { number: 87, title: "Implement developer workflow", state: "OPEN", labels: ["story"], body: "" }
    ]
  });

  assert.equal(result.published, true);
  assert.equal(result.issue.number, 87);
  assert.equal(result.agent, "developer");
  assert.equal(fs.existsSync(result.publishResult.path), true);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("publishFollowUpJobs queues review and test after success", () => {
  const tempRoot = makeTempDir("tmp-followups-");
  const followUps = publishFollowUpJobs({
    execution: {
      status: "success",
      agent: "developer",
      jobId: "job-1"
    },
    issue: {
      number: 88,
      title: "Implement thing"
    },
    repo: "IdeaEngine",
    project: "DragonIdeaEngine",
    rootDir: tempRoot,
    queue: "dragon.jobs"
  });

  assert.equal(followUps.length, 2);
  assert.deepEqual(
    followUps.map((item) => item.agent),
    ["review", "test"]
  );

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("persistExecutionRecord stores execution state", () => {
  const tempRoot = makeTempDir("tmp-record-");
  const saved = persistExecutionRecord({
    rootDir: tempRoot,
    issue: { number: 22, title: "Core System Principles" },
    initialJob: { agent: "developer" },
    execution: { status: "success", jobId: "job-22" },
    followUps: [{ agent: "review" }]
  });

  assert.equal(fs.existsSync(saved.path), true);
  assert.match(fs.readFileSync(saved.path, "utf8"), /job-22/);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("executeSelfBuildStep runs one build step and queues follow-ups", async () => {
  const tempRoot = makeTempDir("tmp-execute-");
  const result = await executeSelfBuildStep({
    owner: "tmassey1979",
    repo: "IdeaEngine",
    rootDir: tempRoot,
    catalogRoot: workspaceRoot(),
    issues: [
      {
        number: 22,
        title: "[Story] Dragon Idea Engine Master Codex: Core System Principles",
        state: "OPEN",
        labels: ["story"]
      }
    ]
  });

  assert.equal(result.executed, true);
  assert.equal(result.selection.issue.number, 22);
  assert.equal(result.execution.status, "success");
  assert.equal(result.followUps.length, 2);
  assert.equal(fs.existsSync(result.executionRecord.path), true);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("dequeueNextJob removes the oldest queued job", () => {
  const tempRoot = makeTempDir("tmp-dequeue-");
  publishNextJob({
    rootDir: tempRoot,
    repo: "IdeaEngine",
    project: "DragonIdeaEngine",
    issues: [
      { number: 21, title: "First", state: "OPEN", labels: ["story"] },
      { number: 22, title: "Second", state: "OPEN", labels: ["story"] }
    ]
  });

  const job = dequeueNextJob({ rootDir: tempRoot });
  assert.equal(job.issue, 21);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("consumeQueuedJob executes queued work and updates workflow state", async () => {
  const tempRoot = makeTempDir("tmp-consume-");
  publishNextJob({
    rootDir: tempRoot,
    repo: "IdeaEngine",
    project: "DragonIdeaEngine",
    issues: [{ number: 22, title: "Core", state: "OPEN", labels: ["story"] }]
  });

  const consumed = await consumeQueuedJob({
    rootDir: tempRoot,
    catalogRoot: workspaceRoot()
  });

  assert.equal(consumed.consumed, true);
  assert.equal(consumed.execution.status, "success");
  assert.equal(consumed.followUps.length, 2);
  assert.equal(consumed.workflow.issue.stages.developer.status, "success");

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("updateIssueWorkflowState marks issues validated when stages succeed", () => {
  const tempRoot = makeTempDir("tmp-state-");

  updateIssueWorkflowState({
    rootDir: tempRoot,
    issueNumber: 22,
    issueTitle: "Core",
    agent: "developer",
    execution: { status: "success", jobId: "job-dev", observedAt: new Date().toISOString() },
    followUps: []
  });
  const workflow = updateIssueWorkflowState({
    rootDir: tempRoot,
    issueNumber: 22,
    issueTitle: "Core",
    agent: "review",
    execution: { status: "success", jobId: "job-review", observedAt: new Date().toISOString() },
    followUps: []
  });
  const finalWorkflow = updateIssueWorkflowState({
    rootDir: tempRoot,
    issueNumber: 22,
    issueTitle: "Core",
    agent: "test",
    execution: { status: "success", jobId: "job-test", observedAt: new Date().toISOString() },
    followUps: []
  });

  assert.equal(workflow.issue.overall, "in_progress");
  assert.equal(finalWorkflow.issue.overall, "validated");

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("updateIssueWorkflowState marks issues failed when a stage fails", () => {
  const tempRoot = makeTempDir("tmp-state-failed-");
  const workflow = updateIssueWorkflowState({
    rootDir: tempRoot,
    issueNumber: 22,
    issueTitle: "Core",
    agent: "review",
    execution: { status: "failed", jobId: "job-review", observedAt: new Date().toISOString() },
    followUps: []
  });

  assert.equal(workflow.issue.overall, "failed");

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("syncIssueWorkflowToGithub closes validated issues", () => {
  const calls = [];
  const result = syncIssueWorkflowToGithub({
    workflow: {
      issueNumber: 22,
      overall: "validated",
      stages: {
        developer: { status: "success" },
        review: { status: "success" },
        test: { status: "success" }
      }
    },
    owner: "tmassey1979",
    repo: "IdeaEngine",
    ghBin: "gh",
    exec(bin, args) {
      calls.push({ bin, args });
      return "closed";
    }
  });

  assert.equal(result.synced, true);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].args[0], "issue");
  assert.equal(calls[1].args[1], "close");
});

test("syncIssueWorkflowToGithub skips non-validated issues", () => {
  const result = syncIssueWorkflowToGithub({
    workflow: {
      issueNumber: 22,
      overall: "in_progress",
      stages: {}
    },
    owner: "tmassey1979",
    repo: "IdeaEngine"
  });

  assert.equal(result.synced, false);
});

test("cycleOnce consumes queued jobs before seeding new ones", async () => {
  const tempRoot = makeTempDir("tmp-cycle-");
  publishNextJob({
    rootDir: tempRoot,
    repo: "IdeaEngine",
    project: "DragonIdeaEngine",
    issues: [{ number: 77, title: "Seeded", state: "OPEN", labels: ["story"] }]
  });

  const cycle = await cycleOnce({
    owner: "tmassey1979",
    repo: "IdeaEngine",
    rootDir: tempRoot,
    catalogRoot: workspaceRoot(),
    issues: [{ number: 88, title: "Later", state: "OPEN", labels: ["story"] }]
  });

  assert.equal(cycle.mode, "consume");
  assert.equal(cycle.result.job.issue, 77);
  assert.equal(readQueueJobs({ rootDir: tempRoot }).length, 2);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

function makeTempDir(prefix) {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}
