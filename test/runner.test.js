const test = require("node:test");
const assert = require("node:assert/strict");
const path = require("path");

const {
  discoverAgents,
  parseArgv,
  runAgent,
  workspaceRoot
} = require("../runner/dragon-agent-runner/src/index");

test("discoverAgents finds starter plugins", () => {
  const agents = discoverAgents(workspaceRoot());
  assert.deepEqual(Array.from(agents.keys()).sort(), [
    "architect",
    "developer",
    "refactor",
    "review",
    "test"
  ]);
});

test("parseArgv separates args and flags", () => {
  assert.deepEqual(parseArgv(["developer", "crm", "--issue", "42", "--service"]), {
    args: ["developer", "crm"],
    flags: {
      issue: "42",
      service: true
    }
  });
});

test("runAgent executes a plugin", async () => {
  const result = await runAgent("developer", {
    rootDir: workspaceRoot(),
    args: [],
    flags: { issue: "42" }
  });

  assert.equal(result.agent, "developer");
  assert.equal(result.target, "42");
});
