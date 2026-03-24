import { runAgentRunner } from "../src/index";

export async function runnerSmokeTest(): Promise<void> {
  await runAgentRunner("cli");
}