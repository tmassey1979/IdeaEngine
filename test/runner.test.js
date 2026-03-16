const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const os = require("os");
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
  createAgent,
  createAgentContext,
  createAgentResult,
  createCredentialsManager,
  createGitClient,
  createJob,
  createJobPublisher,
  createWorkspaceManager,
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

  assert.equal(result.success, true);
  assert.equal(result.artifacts.agent, "developer");
  assert.equal(result.artifacts.target, "42");
});

test("developer agent can apply structured file operations", async () => {
  const tempRoot = makeTempDir("tmp-developer-edit-");
  fs.writeFileSync(path.join(tempRoot, "README.md"), "hello\n", "utf8");

  const result = await runJob(
    {
      jobId: "job-dev-edit",
      agent: "developer",
      action: "implement_issue",
      issue: 42,
      createdAt: new Date().toISOString(),
      payload: {
        operations: [
          {
            type: "append_text",
            path: "README.md",
            content: "world\n"
          },
          {
            type: "write_file",
            path: "notes/summary.txt",
            content: "done\n"
          }
        ]
      },
      metadata: {}
    },
    {
      rootDir: tempRoot,
      catalogRoot: workspaceRoot()
    }
  );

  assert.equal(result.status, JOB_STATUS.SUCCESS);
  assert.equal(result.result.artifacts.changedFiles.includes("README.md"), true);
  assert.equal(fs.readFileSync(path.join(tempRoot, "README.md"), "utf8"), "hello\nworld\n");
  assert.equal(fs.readFileSync(path.join(tempRoot, "notes", "summary.txt"), "utf8"), "done\n");

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("developer agent fails invalid replace operations", async () => {
  const tempRoot = makeTempDir("tmp-developer-fail-");
  fs.writeFileSync(path.join(tempRoot, "README.md"), "hello\n", "utf8");

  const result = await runJob(
    {
      jobId: "job-dev-fail",
      agent: "developer",
      action: "implement_issue",
      issue: 42,
      createdAt: new Date().toISOString(),
      payload: {
        operations: [
          {
            type: "replace_text",
            path: "README.md",
            search: "missing",
            replace: "found"
          }
        ]
      },
      metadata: {}
    },
    {
      rootDir: tempRoot,
      catalogRoot: workspaceRoot()
    }
  );

  assert.equal(result.status, JOB_STATUS.RETRY);
  assert.match(result.errors[0], /could not find the target text/);

  fs.rmSync(tempRoot, { recursive: true, force: true });
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
  assert.equal(result.result.success, true);
  assert.equal(result.result.artifacts.target, "42");
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

test("createAgent supports codex interface fields", async () => {
  const agent = createAgent({
    name: "sample",
    description: "Sample agent",
    version: "1.2.3",
    async execute() {
      return createAgentResult({ success: true });
    }
  });

  assert.equal(agent.name, "sample");
  assert.equal(agent.version, "1.2.3");
  const result = await agent.execute({});
  assert.equal(result.success, true);
});

test("credentials manager resolves project before global", () => {
  const credentials = createCredentialsManager({
    projectCredentials: { github: "project-token" },
    globalCredentials: { github: "global-token", npm: "npm-token" }
  });

  assert.equal(credentials.get("github"), "project-token");
  assert.equal(credentials.get("npm"), "npm-token");
});

test("workspace manager creates isolated issue workspaces", () => {
  const tempRoot = makeTempDir("tmp-workspace-");
  const workspace = createWorkspaceManager({ rootDir: tempRoot });
  const job = createJob({
    agent: "developer",
    action: "implement_issue",
    repo: "dragon-crm",
    issue: 42
  });

  const workspacePath = workspace.ensureJobWorkspace(job);
  assert.match(workspacePath, /workspaces[\\/]dragon-crm[\\/]issue-42$/);
  assert.equal(fs.existsSync(workspacePath), true);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("job publisher writes queue entries", () => {
  const tempRoot = makeTempDir("tmp-publisher-");
  const publisher = createJobPublisher({ rootDir: tempRoot, queue: "dragon.jobs" });
  const publishResult = publisher.publish({
    agent: "review",
    action: "review_pr",
    repo: "dragon-crm"
  });

  assert.equal(publishResult.queue, "dragon.jobs");
  const contents = fs.readFileSync(publishResult.path, "utf8");
  assert.match(contents, /"agent":"review"/);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("agent context exposes codex runtime helpers", () => {
  const tempRoot = makeTempDir("tmp-context-");
  const agent = createAgent({
    name: "developer",
    description: "Developer",
    async execute() {
      return createAgentResult({ success: true });
    }
  });
  const job = createJob({
    agent: "developer",
    action: "implement_issue",
    repo: "dragon-crm",
    issue: 7,
    payload: { branch: "feature/login" }
  });

  const context = createAgentContext({
    agent,
    job,
    rootDir: tempRoot,
    projectCredentials: { github: "project" },
    globalCredentials: { github: "global" }
  });

  assert.equal(context.payload.branch, "feature/login");
  assert.equal(context.repo, "dragon-crm");
  assert.equal(context.credentials.get("github"), "project");
  assert.match(context.workspace.path, /workspaces[\\/]dragon-crm[\\/]issue-7$/);

  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("git client supports branch and commit helpers", () => {
  const repoDir = makeTempDir("tmp-git-");
  execGit(["init"], repoDir);
  execGit(["config", "user.name", "Dragon Test"], repoDir);
  execGit(["config", "user.email", "dragon@example.com"], repoDir);
  fs.writeFileSync(path.join(repoDir, "README.md"), "# temp\n", "utf8");
  execGit(["add", "README.md"], repoDir);
  execGit(["commit", "-m", "chore: seed"], repoDir);

  const git = createGitClient({ cwd: repoDir });
  git.createBranch("feature/test");
  fs.writeFileSync(path.join(repoDir, "notes.txt"), "hello\n", "utf8");
  execGit(["add", "notes.txt"], repoDir);
  git.commit("feat: notes");
  const branch = execGit(["branch", "--show-current"], repoDir).trim();

  assert.equal(branch, "feature/test");

  fs.rmSync(repoDir, { recursive: true, force: true });
});

function execGit(args, cwd) {
  const { execFileSync } = require("child_process");
  return execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  });
}

function makeTempDir(prefix) {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}
