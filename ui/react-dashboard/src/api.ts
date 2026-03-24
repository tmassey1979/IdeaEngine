import type { DashboardResponse, IdeaDetailResponse, IdeaFixResponse, IdeaListItemResponse } from "./types";

async function fetchJson<TData>(path: string): Promise<TData> {
  const response = await fetch(path, {
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Request failed (${response.status})`);
  }

  return (await response.json()) as TData;
}

async function postJson<TData>(path: string, body: unknown): Promise<TData> {
  const response = await fetch(path, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`Request failed (${response.status})`);
  }

  return (await response.json()) as TData;
}

export function loadDashboard(): Promise<DashboardResponse> {
  return fetchJson<DashboardResponse>("/api/dashboard");
}

export function loadIdeas(): Promise<IdeaListItemResponse[]> {
  return fetchJson<IdeaListItemResponse[]>("/api/ideas");
}

export function loadIdeaDetail(id: string): Promise<IdeaDetailResponse> {
  return fetchJson<IdeaDetailResponse>(`/api/ideas/${encodeURIComponent(id)}`);
}

export function fixIdea(id: string, operatorInput?: string): Promise<IdeaFixResponse> {
  return postJson<IdeaFixResponse>(`/api/ideas/${encodeURIComponent(id)}/fix`, {
    operatorInput: operatorInput?.trim() || null,
  });
}

export async function fixIdeas(ids: string[], operatorInput?: string): Promise<IdeaFixResponse[]> {
  const uniqueIds = Array.from(new Set(ids));
  return await Promise.all(uniqueIds.map((id) => fixIdea(id, operatorInput)));
}
