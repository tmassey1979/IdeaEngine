const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { createAgent, createAgentResult } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  name: "test",
  description: "Builds and runs validation for proposed changes.",
  version: "0.1.0",
  async execute(context) {
    const suite = context.flags.suite || context.args[0] || "default";
    const repoPath = context.config.sourceRoot || context.workspace.path;
    context.logger.info("Selecting test suite.", { suite });

    const packageJsonPath = path.join(repoPath, "package.json");
    if (!fs.existsSync(packageJsonPath)) {
      return createAgentResult({
        success: false,
        message: "No package.json found for test execution.",
        artifacts: {
          agent: "test",
          suite
        }
      });
    }

    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, "utf8"));
    if (!packageJson.scripts?.test) {
      return createAgentResult({
        success: false,
        message: "No npm test script is defined.",
        artifacts: {
          agent: "test",
          suite
        }
      });
    }

    const run = runCommand(repoPath, "npm", ["test"]);
    return createAgentResult({
      success: run.success,
      message: run.success ? "Tests passed." : "Tests failed.",
      artifacts: {
        agent: "test",
        suite,
        stdout: truncate(run.stdout),
        stderr: truncate(run.stderr)
      },
      metrics: {
        exitCode: run.exitCode
      }
    });
  }
});

function runCommand(cwd, command, args) {
  try {
    const stdout = execFileSync(command, args, {
      cwd,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    });
    return {
      success: true,
      exitCode: 0,
      stdout,
      stderr: ""
    };
  } catch (error) {
    return {
      success: false,
      exitCode: typeof error.status === "number" ? error.status : 1,
      stdout: error.stdout?.toString?.() || "",
      stderr: error.stderr?.toString?.() || error.message
    };
  }
}

function truncate(value) {
  return String(value || "").slice(0, 2000);
}
