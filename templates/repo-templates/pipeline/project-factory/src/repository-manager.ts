export interface RepositoryPlan {
  name: string;
  visibility: "public" | "private";
  labels: string[];
}

export async function createRepository(plan: RepositoryPlan): Promise<RepositoryPlan> {
  return {
    ...plan,
    labels: [...plan.labels]
  };
}