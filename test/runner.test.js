const test = require("node:test");
const assert = require("node:assert/strict");
const path = require("path");

const {
  runJob,
  discoverAgents,
  parseArgv,
  runAgent,
  workspaceRoot
} = require("../runner/dragon-agent-runner/src/index");
const {
  createJob,
  DEFAULT_RETRY_POLICY,
  JOB_STATUS,
  validateJob
} = require("../sdk/dragon-agent-sdk/src/index");

test("discoverAgents finds starter plugins", () => {
  const agents = discoverAgents(workspaceRoot());
  assert.deepEqual(Array.from(agents.keys()).sort(), [
    "architect",
    "developer",
    "refactor",
    "review",
    "test"
  ]);
});

test("parseArgv separates args and flags", () => {
  assert.deepEqual(parseArgv(["developer", "crm", "--issue", "42", "--service"]), {
    args: ["developer", "crm"],
    flags: {
      issue: "42",
      service: true
    }
  });
});

test("runAgent executes a plugin", async () => {
  const result = await runAgent("developer", {
    rootDir: workspaceRoot(),
    args: [],
    flags: { issue: "42" }
  });

  assert.equal(result.agent, "developer");
  assert.equal(result.target, "42");
});

test("createJob applies schema defaults", () => {
  const job = createJob({
    agent: "developer",
    action: "implement_issue"
  });

  assert.equal(job.priority, "normal");
  assert.deepEqual(job.payload, {});
  assert.deepEqual(job.metadata, {});
  assert.equal(typeof job.jobId, "string");
});

test("validateJob rejects missing required fields", () => {
  assert.throws(() => validateJob({}), /jobId must be a non-empty string/);
});

test("createJob rejects invalid issue values", () => {
  assert.throws(
    () =>
      createJob({
        agent: "developer",
        action: "implement_issue",
        issue: -1
      }),
    /issue must be a non-negative integer/
  );
});

test("runJob returns structured success results", async () => {
  const result = await runJob({
    jobId: "job-42",
    agent: "developer",
    action: "implement_issue",
    issue: 42,
    createdAt: new Date().toISOString(),
    payload: {},
    metadata: {}
  });

  assert.equal(result.jobId, "job-42");
  assert.equal(result.status, JOB_STATUS.SUCCESS);
  assert.equal(result.agent, "developer");
  assert.equal(result.result.target, "unscoped-work");
});

test("default retry policy matches codex defaults", () => {
  assert.deepEqual(DEFAULT_RETRY_POLICY, {
    maxRetries: 3,
    retryDelay: "exponential",
    scheduleSeconds: [10, 30, 90]
  });
});
