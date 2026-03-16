const { createAgent } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  id: "review",
  description: "Reviews changes for bugs, regressions, and missing tests.",
  async run(context) {
    const scope = context.flags.scope || context.args[0] || "current-change";
    context.logger.info("Reviewing change scope.", { scope });

    return {
      agent: "review",
      scope,
      nextStep: "Report findings ordered by severity."
    };
  }
});
