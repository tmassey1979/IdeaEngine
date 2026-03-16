const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const JOB_STATUS = {
  QUEUED: "queued",
  RUNNING: "running",
  SUCCESS: "success",
  FAILED: "failed",
  RETRY: "retry",
  DEADLETTER: "deadletter"
};

const DEFAULT_RETRY_POLICY = {
  maxRetries: 3,
  retryDelay: "exponential",
  scheduleSeconds: [10, 30, 90]
};

function createAgent(definition) {
  const name = definition.name || definition.id;
  const execute = definition.execute || definition.run;

  requireNonEmptyString(name, "agent name");
  requireNonEmptyString(definition.description || "", "description");
  if (typeof execute !== "function") {
    throw new Error(`Agent "${name}" must provide an execute or run function.`);
  }

  const agent = {
    id: name,
    name,
    description: definition.description,
    version: definition.version || "0.1.0",
    registerArgs: definition.registerArgs || (() => {}),
    async execute(context) {
      return execute(context);
    },
    async run(context) {
      return execute(context);
    }
  };

  return agent;
}

function createLogger(scope, defaults = {}) {
  return {
    child(childScope, childDefaults = {}) {
      return createLogger(`${scope}:${childScope}`, { ...defaults, ...childDefaults });
    },
    info(message, data) {
      writeLog("info", scope, message, data, defaults);
    },
    warn(message, data) {
      writeLog("warn", scope, message, data, defaults);
    },
    error(message, data) {
      writeLog("error", scope, message, data, defaults);
    }
  };
}

function writeLog(level, scope, message, data, defaults = {}) {
  const entry = {
    time: new Date().toISOString(),
    level,
    scope,
    ...defaults,
    message,
    ...(data === undefined ? {} : { data })
  };

  process.stderr.write(`${JSON.stringify(entry)}\n`);
}

function createJob(input = {}) {
  const job = {
    jobId: input.jobId || crypto.randomUUID(),
    agent: input.agent,
    action: input.action || "run",
    repo: input.repo || null,
    project: input.project || null,
    issue: input.issue ?? null,
    priority: input.priority || "normal",
    createdAt: input.createdAt || new Date().toISOString(),
    payload: isPlainObject(input.payload) ? input.payload : {},
    metadata: isPlainObject(input.metadata) ? input.metadata : {}
  };

  validateJob(job);
  return job;
}

function validateJob(job) {
  if (!isPlainObject(job)) {
    throw new Error("Job must be an object.");
  }

  requireNonEmptyString(job.jobId, "jobId");
  requireNonEmptyString(job.agent, "agent");
  requireNonEmptyString(job.action, "action");
  requireIsoDate(job.createdAt, "createdAt");

  if (job.repo !== null && job.repo !== undefined) {
    requireNonEmptyString(job.repo, "repo");
  }

  if (job.project !== null && job.project !== undefined) {
    requireNonEmptyString(job.project, "project");
  }

  if (job.issue !== null && job.issue !== undefined) {
    if (!Number.isInteger(job.issue) || job.issue < 0) {
      throw new Error("issue must be a non-negative integer when provided.");
    }
  }

  if (!isPlainObject(job.payload)) {
    throw new Error("payload must be an object.");
  }

  if (!isPlainObject(job.metadata)) {
    throw new Error("metadata must be an object.");
  }
}

function createJobResult({
  job,
  agent,
  status = JOB_STATUS.SUCCESS,
  startedAt,
  result = {},
  logs = [],
  errors = []
}) {
  if (!Object.values(JOB_STATUS).includes(status)) {
    throw new Error(`Unknown job status "${status}".`);
  }

  return {
    jobId: job.jobId,
    status,
    agent: agent || job.agent,
    duration: Math.max(0, Date.now() - startedAt),
    result,
    logs,
    errors
  };
}

function createAgentResult({
  success,
  message,
  artifacts,
  metrics = {}
}) {
  if (typeof success !== "boolean") {
    throw new Error("Agent result requires a boolean success field.");
  }

  return {
    success,
    ...(message ? { message } : {}),
    ...(artifacts === undefined ? {} : { artifacts }),
    metrics
  };
}

function createCredentialsManager({ projectCredentials = {}, globalCredentials = {} } = {}) {
  return {
    get(name) {
      if (Object.prototype.hasOwnProperty.call(projectCredentials, name)) {
        return projectCredentials[name];
      }

      return globalCredentials[name];
    }
  };
}

function createWorkspaceManager({ rootDir }) {
  const baseDir = path.join(rootDir, "workspaces");

  return {
    rootDir: baseDir,
    getJobWorkspace(job) {
      const repoName = sanitizePathSegment(job.repo || "shared");
      const issueSegment = job.issue !== null && job.issue !== undefined ? `issue-${job.issue}` : job.jobId;
      return path.join(baseDir, repoName, sanitizePathSegment(issueSegment));
    },
    ensureJobWorkspace(job) {
      const workspacePath = this.getJobWorkspace(job);
      fs.mkdirSync(workspacePath, { recursive: true });
      return workspacePath;
    },
    cloneRepo(repoSource, options = {}) {
      const repoName =
        options.repoName || deriveRepoName(options.job?.repo || repoSource || options.job?.project || "repo");
      const targetDir = options.targetDir || this.ensureJobWorkspace(options.job || fallbackJob(repoName));
      const repoDir = path.join(targetDir, repoName);

      if (fs.existsSync(repoDir)) {
        return repoDir;
      }

      if (repoSource && fs.existsSync(repoSource)) {
        fs.cpSync(repoSource, repoDir, { recursive: true });
        return repoDir;
      }

      if (repoSource) {
        execFileSync("git", ["clone", repoSource, repoDir], { stdio: ["ignore", "pipe", "pipe"] });
        return repoDir;
      }

      fs.mkdirSync(repoDir, { recursive: true });
      return repoDir;
    }
  };
}

function createGitClient({ cwd }) {
  if (!cwd) {
    throw new Error("Git client requires a working directory.");
  }

  return {
    cloneRepo(repoSource, targetDir) {
      execFileSync("git", ["clone", repoSource, targetDir], { cwd, stdio: ["ignore", "pipe", "pipe"] });
      return targetDir;
    },
    createBranch(name) {
      execFileSync("git", ["checkout", "-b", name], { cwd, stdio: ["ignore", "pipe", "pipe"] });
      return name;
    },
    commit(message) {
      execFileSync("git", ["commit", "-m", message, "--allow-empty"], {
        cwd,
        stdio: ["ignore", "pipe", "pipe"]
      });
      return message;
    },
    push(remote = "origin", branch) {
      const args = branch ? ["push", remote, branch] : ["push", remote];
      execFileSync("git", args, { cwd, stdio: ["ignore", "pipe", "pipe"] });
      return { remote, branch: branch || null };
    },
    createPullRequest(options = {}) {
      return {
        provider: "manual",
        title: options.title || "",
        body: options.body || "",
        head: options.head || null,
        base: options.base || "main"
      };
    }
  };
}

function createJobPublisher({ rootDir, queue = "dragon.jobs" }) {
  return {
    publish(jobInput) {
      const job = createJob(jobInput);
      const queueDir = path.join(rootDir, ".dragon", "queues");
      fs.mkdirSync(queueDir, { recursive: true });
      const queuePath = path.join(queueDir, `${sanitizePathSegment(queue)}.ndjson`);
      fs.appendFileSync(queuePath, `${JSON.stringify(job)}\n`, "utf8");
      return {
        queue,
        path: queuePath,
        jobId: job.jobId
      };
    }
  };
}

function createAgentContext({
  agent,
  job,
  args = [],
  flags = {},
  rootDir,
  config = {},
  projectCredentials = {},
  globalCredentials = {}
}) {
  const workspace = createWorkspaceManager({ rootDir });
  const workspacePath = workspace.ensureJobWorkspace(job);
  const logger = createLogger(`agent:${job.agent}`, {
    jobId: job.jobId,
    agent: job.agent
  });
  const credentials = createCredentialsManager({ projectCredentials, globalCredentials });
  const git = createGitClient({ cwd: workspacePath });
  const jobs = createJobPublisher({ rootDir });

  return {
    job,
    payload: job.payload,
    args,
    flags,
    workspace: {
      ...workspace,
      path: workspacePath
    },
    logger,
    credentials,
    repo: job.repo,
    config,
    git,
    jobs,
    agent: {
      name: agent.name,
      version: agent.version
    }
  };
}

function requireNonEmptyString(value, field) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error(`${field} must be a non-empty string.`);
  }
}

function requireIsoDate(value, field) {
  requireNonEmptyString(value, field);
  if (Number.isNaN(Date.parse(value))) {
    throw new Error(`${field} must be a valid ISO-8601 timestamp.`);
  }
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function sanitizePathSegment(value) {
  return String(value).replace(/[^a-zA-Z0-9._-]/g, "-");
}

function deriveRepoName(value) {
  return path.basename(String(value)).replace(/\.git$/, "");
}

function fallbackJob(repoName) {
  return createJob({
    agent: "workspace",
    action: "clone_repo",
    repo: repoName
  });
}

module.exports = {
  createAgent,
  createAgentContext,
  createAgentResult,
  createCredentialsManager,
  createGitClient,
  createJob,
  createJobPublisher,
  createJobResult,
  createLogger,
  createWorkspaceManager,
  DEFAULT_RETRY_POLICY,
  JOB_STATUS,
  validateJob
};
