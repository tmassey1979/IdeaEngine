import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { spawnSync } from "node:child_process";

const mode = process.argv[2] ?? "status";
const repoRoot = resolve(import.meta.dirname, "..");
const currentSnapshotPath = resolve(repoRoot, "ui/dragon-ui/sample-status.json");
const previousSnapshotPath = resolve(repoRoot, "ui/dragon-ui/sample-status.previous.json");
const currentSnapshotScriptPath = resolve(repoRoot, "ui/dragon-ui/sample-status.js");
const previousSnapshotScriptPath = resolve(repoRoot, "ui/dragon-ui/sample-status.previous.js");

function writeSnapshotScript(jsonPath, scriptPath, globalName, fallbackValue) {
  const serialized = existsSync(jsonPath)
    ? readFileSync(jsonPath, "utf8")
    : JSON.stringify(fallbackValue);

  writeFileSync(
    scriptPath,
    `globalThis.${globalName} = ${serialized};\n`,
    "utf8"
  );
}

mkdirSync(dirname(currentSnapshotPath), { recursive: true });

if (existsSync(currentSnapshotPath)) {
  copyFileSync(currentSnapshotPath, previousSnapshotPath);
}

writeSnapshotScript(
  previousSnapshotPath,
  previousSnapshotScriptPath,
  "__DRAGON_PREVIOUS_STATUS__",
  null
);

const cliArgs =
  mode === "run"
    ? [
        "run",
        "--project",
        "backend/src/Dragon.Backend.Cli",
        "--",
        "run-until-idle",
        "--root",
        ".",
        "--status-out",
        "ui/dragon-ui/sample-status.json",
      ]
    : [
        "run",
        "--project",
        "backend/src/Dragon.Backend.Cli",
        "--",
        "status",
        "--root",
        ".",
        "--out",
        "ui/dragon-ui/sample-status.json",
      ];

const result = spawnSync("dotnet", cliArgs, {
  cwd: repoRoot,
  stdio: "inherit",
});

if (typeof result.status === "number") {
  if (result.status === 0) {
    writeSnapshotScript(
      currentSnapshotPath,
      currentSnapshotScriptPath,
      "__DRAGON_STATUS__",
      null
    );
  }

  process.exit(result.status);
}

process.exit(1);
