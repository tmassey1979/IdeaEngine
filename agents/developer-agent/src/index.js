const { createAgent } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  id: "developer",
  description: "Implements scoped backlog work items.",
  async run(context) {
    const target = context.flags.issue || context.args[0] || "unscoped-work";
    context.logger.info("Preparing development plan.", { target });

    return {
      agent: "developer",
      target,
      nextStep: "Implement the requested slice and verify it with tests."
    };
  }
});
