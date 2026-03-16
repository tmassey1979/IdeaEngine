#!/usr/bin/env node

const { parseArgv } = require("../../../runner/dragon-agent-runner/src/index");
const {
  buildCapabilityCatalog,
  executeSelfBuildStep,
  listGithubIssues,
  runSelfBuildCycle
} = require("./index");

async function main() {
  const { args, flags } = parseArgv(process.argv.slice(2));
  const command = args[0] || "run-once";

  if (flags.help || command === "help") {
    printHelp();
    return;
  }

  if (command === "capabilities") {
    process.stdout.write(`${JSON.stringify(buildCapabilityCatalog(), null, 2)}\n`);
    return;
  }

  if (command === "backlog") {
    const issues = listGithubIssues({
      owner: requiredFlag(flags, "owner"),
      repo: requiredFlag(flags, "repo")
    });
    process.stdout.write(`${JSON.stringify(issues, null, 2)}\n`);
    return;
  }

  if (command === "run-once") {
    const result = runSelfBuildCycle({
      owner: requiredFlag(flags, "owner"),
      repo: requiredFlag(flags, "repo"),
      project: flags.project || "DragonIdeaEngine",
      queue: flags.queue || "dragon.jobs"
    });
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
    return;
  }

  if (command === "execute-once") {
    const result = await executeSelfBuildStep({
      owner: requiredFlag(flags, "owner"),
      repo: requiredFlag(flags, "repo"),
      project: flags.project || "DragonIdeaEngine",
      queue: flags.queue || "dragon.jobs"
    });
    process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
    return;
  }

  throw new Error(`Unknown orchestrator command "${command}".`);
}

function requiredFlag(flags, name) {
  const value = flags[name];
  if (!value) {
    throw new Error(`Missing required flag --${name}`);
  }

  return value;
}

function printHelp() {
  process.stdout.write(
    [
      "dragon-orchestrator",
      "",
      "Usage:",
      "  dragon-orchestrator run-once --owner <owner> --repo <repo> [--project <project>] [--queue <queue>]",
      "  dragon-orchestrator execute-once --owner <owner> --repo <repo> [--project <project>] [--queue <queue>]",
      "  dragon-orchestrator backlog --owner <owner> --repo <repo>",
      "  dragon-orchestrator capabilities"
    ].join("\n") + "\n"
  );
}

main().catch((error) => {
  process.stderr.write(`${error.stack || String(error)}\n`);
  process.exit(1);
});
