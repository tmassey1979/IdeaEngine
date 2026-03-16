const fs = require("fs");
const path = require("path");
const {
  createJob,
  createJobResult,
  createLogger,
  JOB_STATUS
} = require("../../../sdk/dragon-agent-sdk/src/index");

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

  const logger = createLogger(`agent:${agentId}`);
  return agent.run({
    mode: options.mode || "cli",
    args: options.args || [],
    flags: options.flags || {},
    job: options.job || null,
    logger
  });
}

async function runJob(jobInput, options = {}) {
  const job = createJob(jobInput);
  const startedAt = Date.now();

  try {
    const result = await runAgent(job.agent, {
      ...options,
      mode: "service",
      args: options.args || [],
      flags: options.flags || {},
      job
    });

    return createJobResult({
      job,
      startedAt,
      result,
      logs: [
        {
          level: "info",
          message: `Job routed to agent "${job.agent}".`
        }
      ]
    });
  } catch (error) {
    return createJobResult({
      job,
      startedAt,
      status: JOB_STATUS.FAILED,
      result: {},
      errors: [error.message]
    });
  }
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
  discoverAgents,
  parseArgv,
  runAgent,
  runJob,
  workspaceRoot
};
