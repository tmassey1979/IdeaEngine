const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const path = require("path");

const {
  DEADLETTER_QUEUE,
  retryDelaySeconds,
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
  assert.equal(result.agentVersion, "0.1.0");
  assert.equal(result.metrics.attempt, 1);
});

test("default retry policy matches codex defaults", () => {
  assert.deepEqual(DEFAULT_RETRY_POLICY, {
    maxRetries: 3,
    retryDelay: "exponential",
    scheduleSeconds: [10, 30, 90]
  });
});

test("retry delay follows codex schedule", () => {
  assert.equal(retryDelaySeconds(0), 10);
  assert.equal(retryDelaySeconds(1), 30);
  assert.equal(retryDelaySeconds(10), 90);
});

test("runJob returns retry before deadletter", async () => {
  const result = await runJob({
    jobId: "retry-job",
    agent: "missing-agent",
    action: "implement_issue",
    createdAt: new Date().toISOString(),
    metadata: {
      attempts: 1
    }
  });

  assert.equal(result.status, JOB_STATUS.RETRY);
  assert.equal(result.result.retry.nextAttempt, 2);
  assert.equal(result.result.retry.delaySeconds, 30);
});

test("runJob deadletters after retries are exhausted", async () => {
  const rootDir = workspaceRoot();
  const deadletterPath = path.join(rootDir, ".dragon", "queues", "dragon.deadletter.ndjson");
  fs.rmSync(path.dirname(deadletterPath), { recursive: true, force: true });

  const result = await runJob(
    {
      jobId: "deadletter-job",
      agent: "missing-agent",
      action: "implement_issue",
      createdAt: new Date().toISOString(),
      metadata: {
        attempts: 3
      }
    },
    { rootDir }
  );

  assert.equal(result.status, JOB_STATUS.DEADLETTER);
  assert.equal(result.result.deadletter.queue, DEADLETTER_QUEUE);
  assert.equal(fs.existsSync(deadletterPath), true);
  assert.match(fs.readFileSync(deadletterPath, "utf8"), /deadletter-job/);
});
