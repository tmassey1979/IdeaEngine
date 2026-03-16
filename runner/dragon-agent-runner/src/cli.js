#!/usr/bin/env node

const readline = require("readline");
const { discoverAgents, parseArgv, runAgent } = require("./index");

async function main() {
  const { args, flags } = parseArgv(process.argv.slice(2));

  if (flags.help || args[0] === "help") {
    printHelp();
    return;
  }

  if (flags.service) {
    await runServiceMode();
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

async function runServiceMode() {
  const rl = readline.createInterface({
    input: process.stdin,
    crlfDelay: Infinity
  });

  for await (const line of rl) {
    const trimmed = line.trim();
    if (!trimmed) {
      continue;
    }

    const job = JSON.parse(trimmed);
    const result = await runAgent(job.agent, {
      mode: "service",
      args: job.args || [],
      flags: job.flags || {},
      job
    });

    process.stdout.write(`${JSON.stringify({ jobId: job.id || null, result })}\n`);
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
      "",
      "Available agents:",
      agentList || "  - none"
    ].join("\n") + "\n"
  );
}

main().catch((error) => {
  process.stderr.write(`${error.stack || String(error)}\n`);
  process.exit(1);
});
