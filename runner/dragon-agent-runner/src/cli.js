#!/usr/bin/env node

const readline = require("readline");
const {
  discoverAgents,
  parseArgv,
  runAgent,
  runJob
} = require("./index");
const {
  createJob,
  createJobResult,
  JOB_STATUS
} = require("../../../sdk/dragon-agent-sdk/src/index");

async function main() {
  const { args, flags } = parseArgv(process.argv.slice(2));

  if (flags.help || args[0] === "help") {
    printHelp();
    return;
  }

  if (flags.service) {
    await runServiceMode(flags);
    return;
  }

  const agentId = args[0];
  if (!agentId) {
    printHelp();
    process.exitCode = 1;
    return;
  }

  const result = await runAgent(agentId, {
    mode: "cli",
    args: args.slice(1),
    flags
  });

  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
}

async function runServiceMode(serviceFlags = {}) {
  const rl = readline.createInterface({
    input: process.stdin,
    crlfDelay: Infinity
  });

  for await (const line of rl) {
    const trimmed = line.trim();
    if (!trimmed) {
      continue;
    }

    let parsed;
    try {
      parsed = JSON.parse(trimmed);
    } catch (error) {
      process.stdout.write(
        `${JSON.stringify(
          createInvalidJobResult({
            jobId: null,
            message: `Invalid JSON: ${error.message}`
          })
        )}\n`
      );
      continue;
    }

    const args = Array.isArray(parsed.args) ? parsed.args : [];
    const flags = isObject(parsed.flags) ? parsed.flags : {};

    try {
      const job = createJob({
        ...parsed,
        payload: {
          ...(isObject(parsed.payload) ? parsed.payload : {}),
          args,
          flags
        }
      });
      const result = await runJob(job, {
        args,
        flags,
        queue: serviceFlags.queue || "stdin"
      });
      process.stdout.write(`${JSON.stringify(result)}\n`);
    } catch (error) {
      process.stdout.write(
        `${JSON.stringify(
          createInvalidJobResult({
            jobId: parsed.jobId || null,
            message: error.message
          })
        )}\n`
      );
    }
  }
}

function printHelp() {
  const agentList = Array.from(discoverAgents().values())
    .sort((left, right) => left.id.localeCompare(right.id))
    .map((agent) => `  - ${agent.id}: ${agent.description}`)
    .join("\n");

  process.stdout.write(
    [
      "dragon-agent-runner",
      "",
      "Usage:",
      "  dragon-agent-runner <agent> [args...] [--flag value]",
      "  dragon-agent-runner --service",
      "  dragon-agent-runner --service --queue rabbitmq",
      "",
      "Available agents:",
      agentList || "  - none"
    ].join("\n") + "\n"
  );
}

function createInvalidJobResult({ jobId, message }) {
  const fallbackJob = {
    jobId: jobId || "invalid-job",
    agent: "unknown"
  };

  return createJobResult({
    job: fallbackJob,
    agent: "runner",
    status: JOB_STATUS.FAILED,
    startedAt: Date.now(),
    result: {},
    errors: [message]
  });
}

function isObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

main().catch((error) => {
  process.stderr.write(`${error.stack || String(error)}\n`);
  process.exit(1);
});
