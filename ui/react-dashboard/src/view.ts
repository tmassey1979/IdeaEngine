import { useEffect, useState } from "react";
import type { ActivityPanelResponse, BoardPanelResponse, ListPanelResponse, ResourceState } from "./types";

export type PaginationState = ReturnType<typeof usePagination>;

export function createResourceState<TData>(): ResourceState<TData> {
  return {
    data: null,
    loading: true,
    error: null,
  };
}

export function formatStatus(value: string): string {
  return value
    .replace(/-/g, " ")
    .replace(/\b\w/g, (segment) => segment.toUpperCase());
}

export function formatTimestamp(value?: string | null): string {
  if (!value) {
    return "Unknown time";
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : parsed.toLocaleString([], { dateStyle: "medium", timeStyle: "short" });
}

export function paginate<TItem>(items: TItem[], page: number, pageSize: number) {
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const safePage = Math.min(page, totalPages);
  const startIndex = (safePage - 1) * pageSize;
  const endIndex = Math.min(items.length, startIndex + pageSize);

  return {
    page: safePage,
    totalPages,
    startIndex,
    endIndex,
    items: items.slice(startIndex, endIndex),
  };
}

export function usePagination(totalCount: number) {
  const [pageSize, setPageSize] = useState<number>(10);
  const [page, setPage] = useState<number>(1);

  useEffect(() => {
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    if (page > totalPages) {
      setPage(totalPages);
    }
  }, [page, pageSize, totalCount]);

  return {
    page,
    pageSize,
    setPage,
    setPageSize(nextPageSize: number) {
      setPageSize(nextPageSize);
      setPage(1);
    },
  };
}

export function hasBoardCards(panel: BoardPanelResponse): boolean {
  return panel.columns.some((column) => column.cards.length > 0);
}

export function hasListItems(panel: ListPanelResponse): boolean {
  return panel.items.length > 0;
}

export function hasActivityEntries(panel: ActivityPanelResponse): boolean {
  return panel.entries.length > 0;
}
