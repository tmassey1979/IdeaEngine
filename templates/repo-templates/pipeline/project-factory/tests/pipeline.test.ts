import { buildWorkflow } from "../src/workflow-engine";
import { generateProjectSlice } from "../src/code-generator";

export function pipelineSmokeTest(): boolean {
  return buildWorkflow().length > 0 && generateProjectSlice("sample").length > 0;
}