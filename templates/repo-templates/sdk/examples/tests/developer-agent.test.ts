import { developerAgent } from "../developer-agent";

export async function exampleAgentSmokeTest(): Promise<boolean> {
  const result = await developerAgent.execute({ mode: "cli" });
  return result.success;
}