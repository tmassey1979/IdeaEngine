const { createAgent, createAgentResult } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  name: "review",
  description: "Reviews changes for bugs, regressions, and missing tests.",
  version: "0.1.0",
  async execute(context) {
    const scope = context.flags.scope || context.args[0] || "current-change";
    context.logger.info("Reviewing change scope.", { scope });

    return createAgentResult({
      success: true,
      message: "Review scope prepared.",
      artifacts: {
        agent: "review",
        scope,
        nextStep: "Report findings ordered by severity."
      }
    });
  }
});
