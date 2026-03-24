export type WorkflowStage = "architecture" | "implementation" | "review" | "test" | "documentation";

export function buildWorkflow(): WorkflowStage[] {
  return ["architecture", "implementation", "review", "test", "documentation"];
}