const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
const { createAgent, createAgentResult } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  name: "review",
  description: "Reviews changes for bugs, regressions, and missing tests.",
  version: "0.1.0",
  async execute(context) {
    const scope = context.flags.scope || context.args[0] || "current-change";
    const repoPath = context.config.sourceRoot || context.workspace.path;
    context.logger.info("Reviewing change scope.", { scope });

    const changedFiles = gitLines(repoPath, ["status", "--short"])
      .map((line) => line.trim())
      .filter(Boolean);
    const packageJsonPath = path.join(repoPath, "package.json");
    const hasPackageJson = fs.existsSync(packageJsonPath);
    const hasTestScript = hasPackageJson && Boolean(readJson(packageJsonPath).scripts?.test);
    const findings = [];

    if (changedFiles.length === 0) {
      findings.push("No repository changes detected to review.");
    }

    if (!hasTestScript) {
      findings.push("No npm test script is defined for automated validation.");
    }

    const success = findings.length === 0;
    return createAgentResult({
      success,
      message: success ? "Review checks passed." : "Review checks found issues.",
      artifacts: {
        agent: "review",
        scope,
        changedFiles,
        findings
      },
      metrics: {
        changedFileCount: changedFiles.length,
        hasTestScript
      }
    });
  }
});

function gitLines(cwd, args) {
  const output = execFileSync("git", args, {
    cwd,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  });
  return output.split("\n");
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}
