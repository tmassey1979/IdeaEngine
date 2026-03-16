const { createAgent } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  id: "test",
  description: "Builds and runs validation for proposed changes.",
  async run(context) {
    const suite = context.flags.suite || context.args[0] || "default";
    context.logger.info("Selecting test suite.", { suite });

    return {
      agent: "test",
      suite,
      nextStep: "Run targeted tests and summarize failures."
    };
  }
});
