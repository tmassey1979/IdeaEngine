const { createAgent } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  id: "refactor",
  description: "Improves structure without changing intended behavior.",
  async run(context) {
    const target = context.flags.target || context.args[0] || "current-module";
    context.logger.info("Preparing refactor guidance.", { target });

    return {
      agent: "refactor",
      target,
      nextStep: "Simplify structure while keeping behavior stable."
    };
  }
});
