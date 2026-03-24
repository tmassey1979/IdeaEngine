export interface RoutedTask {
  agent: string;
  action: string;
  issue: number;
}

export function routeTask(labels: string[]): RoutedTask {
  if (labels.includes("documentation")) {
    return { agent: "documentation", action: "implement_issue", issue: 0 };
  }

  return { agent: "developer", action: "implement_issue", issue: 0 };
}