const { createAgent, createAgentResult } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  name: "test",
  description: "Builds and runs validation for proposed changes.",
  version: "0.1.0",
  async execute(context) {
    const suite = context.flags.suite || context.args[0] || "default";
    context.logger.info("Selecting test suite.", { suite });

    return createAgentResult({
      success: true,
      message: "Test suite selected.",
      artifacts: {
        agent: "test",
        suite,
        nextStep: "Run targeted tests and summarize failures."
      }
    });
  }
});
