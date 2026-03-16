const { createAgent } = require("../../../sdk/dragon-agent-sdk/src/index");

module.exports = createAgent({
  id: "architect",
  description: "Creates architecture-oriented implementation guidance.",
  async run(context) {
    const subject = context.flags.subject || context.args[0] || "system";
    context.logger.info("Generating architecture guidance.", { subject });

    return {
      agent: "architect",
      subject,
      nextStep: "Draft the architecture and identify implementation slices."
    };
  }
});
