const fs = require("fs");
const path = require("path");
const {
  createAgent,
  createAgentResult
} = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  name: "developer",
  description: "Implements scoped backlog work items.",
  version: "0.1.0",
  async execute(context) {
    const target = context.flags.issue || context.job.issue || context.args[0] || "unscoped-work";
    const operations = Array.isArray(context.payload.operations) ? context.payload.operations : [];
    const repoPath = context.config.sourceRoot || context.workspace.path;
    context.logger.info("Preparing development plan.", { target });

    if (operations.length > 0) {
      const changedFiles = applyOperations(repoPath, operations);
      return createAgentResult({
        success: true,
        message: "Development changes applied.",
        artifacts: {
          agent: "developer",
          target: String(target),
          changedFiles
        },
        metrics: {
          operationCount: operations.length,
          changedFileCount: changedFiles.length
        }
      });
    }

    return createAgentResult({
      success: true,
      message: "Development slice prepared.",
      artifacts: {
        agent: "developer",
        target: String(target),
        nextStep: "Implement the requested slice and verify it with tests."
      },
      metrics: {
        targetType: typeof target
      }
    });
  }
});

function applyOperations(rootDir, operations) {
  const changedFiles = [];

  for (const operation of operations) {
    const relativePath = normalizeRelativePath(operation.path);
    const filePath = path.join(rootDir, relativePath);
    const fileDir = path.dirname(filePath);
    fs.mkdirSync(fileDir, { recursive: true });

    if (operation.type === "write_file") {
      fs.writeFileSync(filePath, String(operation.content || ""), "utf8");
      changedFiles.push(relativePath);
      continue;
    }

    const current = fs.existsSync(filePath) ? fs.readFileSync(filePath, "utf8") : "";

    if (operation.type === "append_text") {
      fs.writeFileSync(filePath, `${current}${String(operation.content || "")}`, "utf8");
      changedFiles.push(relativePath);
      continue;
    }

    if (operation.type === "replace_text") {
      const search = String(operation.search || "");
      if (!search) {
        throw new Error(`replace_text requires a non-empty search value for ${relativePath}.`);
      }

      if (!current.includes(search)) {
        throw new Error(`replace_text could not find the target text in ${relativePath}.`);
      }

      const next = current.replace(search, String(operation.replace || ""));
      fs.writeFileSync(filePath, next, "utf8");
      changedFiles.push(relativePath);
      continue;
    }

    throw new Error(`Unsupported developer operation type "${operation.type}".`);
  }

  return Array.from(new Set(changedFiles));
}

function normalizeRelativePath(filePath) {
  const normalized = path.normalize(String(filePath || ""));
  if (!normalized || normalized.startsWith("..") || path.isAbsolute(normalized)) {
    throw new Error(`Invalid operation path "${filePath}".`);
  }

  return normalized;
}
