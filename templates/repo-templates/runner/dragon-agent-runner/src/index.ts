export async function runAgentRunner(mode: "cli" | "service"): Promise<void> {
  if (mode === "service") {
    console.log("Starting dragon-agent-runner in service mode.");
    return;
  }

  console.log("Starting dragon-agent-runner in CLI mode.");
}