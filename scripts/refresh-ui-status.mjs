import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { spawnSync } from "node:child_process";

const mode = process.argv[2] ?? "status";
const repoRoot = resolve(import.meta.dirname, "..");
const currentSnapshotPath = resolve(repoRoot, "ui/dragon-ui/sample-status.json");
const previousSnapshotPath = resolve(repoRoot, "ui/dragon-ui/sample-status.previous.json");

mkdirSync(dirname(currentSnapshotPath), { recursive: true });

if (existsSync(currentSnapshotPath)) {
  copyFileSync(currentSnapshotPath, previousSnapshotPath);
}

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
  process.exit(result.status);
}

process.exit(1);
