const crypto = require("crypto");

function createAgent({ id, description, run }) {
  if (!id) {
    throw new Error("Agent id is required.");
  }

  if (typeof run !== "function") {
    throw new Error(`Agent "${id}" must provide a run function.`);
  }

  return {
    id,
    description: description || "",
    run
  };
}

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

function createLogger(scope) {
  return {
    info(message, data) {
      writeLog("info", scope, message, data);
    },
    warn(message, data) {
      writeLog("warn", scope, message, data);
    },
    error(message, data) {
      writeLog("error", scope, message, data);
    }
  };
}

function writeLog(level, scope, message, data) {
  const entry = {
    time: new Date().toISOString(),
    level,
    scope,
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

module.exports = {
  createAgent,
  createJob,
  createJobResult,
  createLogger,
  DEFAULT_RETRY_POLICY,
  JOB_STATUS,
  validateJob
};
