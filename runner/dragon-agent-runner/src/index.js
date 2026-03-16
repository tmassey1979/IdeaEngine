const fs = require("fs");
const path = require("path");
const {
  createJob,
  createAgentContext,
  createJobResult,
  DEFAULT_RETRY_POLICY,
  JOB_STATUS
} = require("../../../sdk/dragon-agent-sdk/src/index");

const DEADLETTER_QUEUE = "dragon.deadletter";

function discoverAgents(rootDir = workspaceRoot()) {
  const agentsDir = path.join(rootDir, "agents");
  if (!fs.existsSync(agentsDir)) {
    return new Map();
  }

  const agents = new Map();
  for (const entry of fs.readdirSync(agentsDir, { withFileTypes: true })) {
    if (!entry.isDirectory()) {
      continue;
    }

    const modulePath = path.join(agentsDir, entry.name, "src", "index.js");
    if (!fs.existsSync(modulePath)) {
      continue;
    }

    const plugin = require(modulePath);
    agents.set(plugin.id, plugin);
  }

  return agents;
}

async function runAgent(agentId, options = {}) {
  const agents = discoverAgents(options.rootDir);
  const agent = agents.get(agentId);

  if (!agent) {
    const availableAgents = Array.from(agents.keys()).sort();
    throw new Error(
      `Unknown agent "${agentId}". Available agents: ${availableAgents.join(", ") || "none"}.`
    );
  }

  const rootDir = options.rootDir || workspaceRoot();
  const job =
    options.job ||
    createJob({
      agent: agent.name || agent.id,
      action: options.mode === "service" ? "run" : "cli_run",
      repo: options.flags?.repo || null,
      issue: options.flags?.issue ? Number(options.flags.issue) : null,
      payload: {
        args: options.args || [],
        flags: options.flags || {}
      }
    });

  const context = createAgentContext({
    agent,
    job,
    args: options.args || [],
    flags: options.flags || {},
    rootDir,
    config: options.config || {},
    projectCredentials: options.projectCredentials || {},
    globalCredentials: options.globalCredentials || {}
  });

  context.mode = options.mode || "cli";
  context.execution = options.execution || null;
  return agent.execute(context);
}

async function runJob(jobInput, options = {}) {
  const job = createJob(jobInput);
  const startedAt = Date.now();
  const execution = createExecutionContext(job, options);

  try {
    const result = await runAgent(job.agent, {
      ...options,
      mode: "service",
      args: options.args || [],
      flags: options.flags || {},
      job,
      execution
    });

    return createObservedResult(
      createJobResult({
        job,
        startedAt,
        result,
        logs: buildExecutionLogs(job, execution, [
          {
            level: "info",
            message: `Job routed to agent "${job.agent}".`
          }
        ])
      }),
      execution
    );
  } catch (error) {
    const lifecycle = buildFailureLifecycle(job, execution, error, options);
    return createObservedResult(
      createJobResult({
        job,
        startedAt,
        status: lifecycle.status,
        result: lifecycle.result,
        logs: buildExecutionLogs(job, execution, lifecycle.logs),
        errors: [error.message]
      }),
      execution
    );
  }
}

function createExecutionContext(job, options = {}) {
  const attempts = Number(job.metadata?.attempts || options.attempts || 0);
  const agentVersion = options.agentVersion || "0.1.0";

  return {
    attempts,
    agentVersion,
    queue: options.queue || "stdin",
    metrics: {
      queue: options.queue || "stdin",
      attempt: attempts + 1
    }
  };
}

function buildFailureLifecycle(job, execution, error, options = {}) {
  const retryPolicy = options.retryPolicy || DEFAULT_RETRY_POLICY;
  const shouldRetry = execution.attempts < retryPolicy.maxRetries;

  if (shouldRetry) {
    return {
      status: JOB_STATUS.RETRY,
      result: {
        retry: {
          nextAttempt: execution.attempts + 1,
          delaySeconds: retryDelaySeconds(execution.attempts, retryPolicy)
        }
      },
      logs: [
        {
          level: "warn",
          message: `Job failed and is scheduled for retry ${execution.attempts + 1}.`
        }
      ]
    };
  }

  const deadletterEntry = persistDeadLetter(job, error, options.rootDir);
  return {
    status: JOB_STATUS.DEADLETTER,
    result: {
      deadletter: deadletterEntry
    },
    logs: [
      {
        level: "error",
        message: `Job exhausted retries and was routed to ${DEADLETTER_QUEUE}.`
      }
    ]
  };
}

function retryDelaySeconds(attempts, retryPolicy = DEFAULT_RETRY_POLICY) {
  return retryPolicy.scheduleSeconds[Math.min(attempts, retryPolicy.scheduleSeconds.length - 1)];
}

function persistDeadLetter(job, error, rootDir = workspaceRoot()) {
  const queueDir = path.join(rootDir, ".dragon", "queues");
  fs.mkdirSync(queueDir, { recursive: true });

  const deadletterPath = path.join(queueDir, "dragon.deadletter.ndjson");
  const entry = {
    queue: DEADLETTER_QUEUE,
    failedAt: new Date().toISOString(),
    job,
    error: error.message
  };

  fs.appendFileSync(deadletterPath, `${JSON.stringify(entry)}\n`, "utf8");
  return {
    queue: DEADLETTER_QUEUE,
    path: deadletterPath
  };
}

function buildExecutionLogs(job, execution, extraLogs = []) {
  return [
    {
      level: "info",
      message: "Job execution started.",
      jobId: job.jobId,
      agentVersion: execution.agentVersion,
      queue: execution.queue,
      attempt: execution.metrics.attempt
    },
    ...extraLogs
  ];
}

function createObservedResult(result, execution) {
  return {
    ...result,
    metrics: {
      ...execution.metrics,
      durationMs: result.duration
    },
    observedAt: new Date().toISOString(),
    agentVersion: execution.agentVersion
  };
}

function parseArgv(argv) {
  const args = [];
  const flags = {};

  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) {
      args.push(token);
      continue;
    }

    const [rawKey, inlineValue] = token.slice(2).split("=", 2);
    const nextToken = argv[index + 1];
    if (inlineValue !== undefined) {
      flags[rawKey] = inlineValue;
      continue;
    }

    if (nextToken && !nextToken.startsWith("--")) {
      flags[rawKey] = nextToken;
      index += 1;
      continue;
    }

    flags[rawKey] = true;
  }

  return { args, flags };
}

function workspaceRoot() {
  return path.resolve(__dirname, "../../..");
}

module.exports = {
  DEADLETTER_QUEUE,
  buildFailureLifecycle,
  discoverAgents,
  parseArgv,
  persistDeadLetter,
  retryDelaySeconds,
  runAgent,
  runJob,
  workspaceRoot
};
