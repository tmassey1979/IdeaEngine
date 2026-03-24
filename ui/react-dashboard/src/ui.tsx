import { useEffect } from "react";
import { pageSizeOptions } from "./data";
import type { ActivityEntryResponse, ActivityPanelResponse, BoardPanelResponse, ListPanelResponse } from "./types";
import { formatStatus, formatTimestamp, hasActivityEntries, hasBoardCards, hasListItems, paginate, usePagination } from "./view";

export function SectionStateCard(props: { message: string; tone?: "default" | "error" }) {
  return <div className={`empty-state${props.tone === "error" ? " error-state" : ""}`}>{props.message}</div>;
}

export function PaginationControls(props: {
  label: string;
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  startIndex: number;
  endIndex: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}) {
  const hasItems = props.totalCount > 0;

  return (
    <div className="pagination-bar" aria-label={`${props.label} pagination`}>
      <div className="pagination-summary">
        {hasItems ? `Showing ${props.startIndex + 1}-${props.endIndex} of ${props.totalCount}` : "No entries"}
      </div>
      <div className="pagination-controls">
        <label className="page-size-control">
          <span>Rows</span>
          <select
            aria-label={`${props.label} page size`}
            value={props.pageSize}
            onChange={(event) => props.onPageSizeChange(Number(event.target.value))}
          >
            {pageSizeOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          className="button tertiary"
          onClick={() => props.onPageChange(Math.max(1, props.page - 1))}
          disabled={props.page <= 1}
        >
          Previous
        </button>
        <span className="page-indicator">
          Page {props.page} / {props.totalPages}
        </span>
        <button
          type="button"
          className="button tertiary"
          onClick={() => props.onPageChange(Math.min(props.totalPages, props.page + 1))}
          disabled={props.page >= props.totalPages}
        >
          Next
        </button>
      </div>
    </div>
  );
}

export function DragonMark() {
  return (
    <svg viewBox="0 0 64 64" role="presentation">
      <defs>
        <linearGradient id="brand-dragon-fill-react" x1="15" y1="10" x2="50" y2="55" gradientUnits="userSpaceOnUse">
          <stop stopColor="#ff9c86" />
          <stop offset="0.58" stopColor="#f03a24" />
          <stop offset="1" stopColor="#8b0000" />
        </linearGradient>
      </defs>
      <path
        d="M49 15c-5-2-11-2-16 1-5 2-8 7-11 11l-8-1 5 5-4 6 8-1c1 6 5 11 11 13 4 1 10 1 15-1-3-1-5-3-6-6l7 1-4-6 5-4-8-1c1-6-1-12-4-17z"
        fill="url(#brand-dragon-fill-react)"
      />
      <path
        d="M41 19c-6 1-11 5-14 11l6 1-5 5 7-1c0 5 3 9 7 12-1-4 0-8 3-10 3-3 4-7 4-12 0-2-1-4-2-6l-4 2-2-2z"
        fill="#180303"
        fillOpacity="0.82"
      />
      <circle cx="40" cy="25" r="2.2" fill="#fff3ef" />
    </svg>
  );
}

export function HeroSigil() {
  return (
    <div className="hero-emblem" aria-hidden="true">
      <div className="hero-emblem-ring" />
      <svg viewBox="0 0 220 220" role="presentation">
        <defs>
          <linearGradient id="hero-dragon-sigil-react" x1="48" y1="34" x2="170" y2="187" gradientUnits="userSpaceOnUse">
            <stop stopColor="#ffbaa9" />
            <stop offset="0.55" stopColor="#f03a24" />
            <stop offset="1" stopColor="#870000" />
          </linearGradient>
        </defs>
        <path
          d="M166 58c-16-8-36-9-54-2-17 7-30 21-40 37l-27-4 18 18-13 20 24-4c3 21 17 39 37 48 16 6 35 7 54 0-11-4-19-12-24-23l24 3-14-18 18-14-26-4c6-18 4-38-3-57z"
          fill="url(#hero-dragon-sigil-react)"
        />
        <path
          d="M132 70c-19 3-35 15-45 33l20 3-15 15 21-3c0 15 8 29 19 39-3-13 1-26 10-34 9-10 14-22 14-38 0-7-2-13-5-19l-12 6-7-5z"
          fill="#180303"
          fillOpacity="0.84"
        />
        <path
          d="M83 89c7-14 17-25 31-31"
          stroke="#ffd1c7"
          strokeOpacity="0.4"
          strokeWidth="5"
          strokeLinecap="round"
        />
        <circle cx="131" cy="86" r="5.5" fill="#fff2ef" />
      </svg>
    </div>
  );
}

export function ListPanelView(props: { panel: ListPanelResponse }) {
  const pagination = usePagination(props.panel.items.length);
  const slice = paginate(props.panel.items, pagination.page, pagination.pageSize);

  useEffect(() => {
    pagination.setPage(1);
  }, [pagination.setPage, props.panel.items.length, props.panel.summary]);

  if (!hasListItems(props.panel)) {
    return <SectionStateCard message={props.panel.summary} />;
  }

  return (
    <div className="panel-item-list">
      <p className="panel-summary">{props.panel.summary}</p>
      <PaginationControls
        label="Backlog"
        totalCount={props.panel.items.length}
        page={slice.page}
        pageSize={pagination.pageSize}
        totalPages={slice.totalPages}
        startIndex={slice.startIndex}
        endIndex={slice.endIndex}
        onPageChange={pagination.setPage}
        onPageSizeChange={pagination.setPageSize}
      />
      {slice.items.map((item) => (
        <article key={item.id} className="detail-list-card">
          <div className="project-card-header">
            <strong>{item.title}</strong>
            <span className={`pill ${item.status.toLowerCase()}`}>{formatStatus(item.status)}</span>
          </div>
          <p className="subtle">{item.summary ?? "No additional summary is available."}</p>
        </article>
      ))}
    </div>
  );
}

export function BoardPanelView(props: { panel: BoardPanelResponse }) {
  if (!hasBoardCards(props.panel)) {
    return <SectionStateCard message={props.panel.summary} />;
  }

  return (
    <div className="board-shell">
      <p className="panel-summary">{props.panel.summary}</p>
      <div className="board-grid">
        {props.panel.columns.map((column) => (
          <section key={column.id} className="board-column">
            <div className="board-column-head">
              <strong>{column.title}</strong>
              <span className="chip">{column.cards.length}</span>
            </div>
            <div className="board-card-list">
              {column.cards.length === 0 ? (
                <div className="board-empty">No cards</div>
              ) : (
                column.cards.map((card) => (
                  <article key={card.id} className="board-card">
                    <strong>{card.title}</strong>
                    <p className="subtle">{card.summary ?? "No additional summary is available."}</p>
                  </article>
                ))
              )}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}

export function ActivityPanelView(props: { panel: ActivityPanelResponse }) {
  const pagination = usePagination(props.panel.entries.length);
  const slice = paginate(props.panel.entries, pagination.page, pagination.pageSize);

  useEffect(() => {
    pagination.setPage(1);
  }, [pagination.setPage, props.panel.entries.length, props.panel.summary]);

  if (!hasActivityEntries(props.panel)) {
    return <SectionStateCard message={props.panel.summary} />;
  }

  return (
    <div className="activity-list">
      <p className="panel-summary">{props.panel.summary}</p>
      <PaginationControls
        label="Activity"
        totalCount={props.panel.entries.length}
        page={slice.page}
        pageSize={pagination.pageSize}
        totalPages={slice.totalPages}
        startIndex={slice.startIndex}
        endIndex={slice.endIndex}
        onPageChange={pagination.setPage}
        onPageSizeChange={pagination.setPageSize}
      />
      {slice.items.map((entry: ActivityEntryResponse) => (
        <article key={entry.id} className="activity-card">
          <div className="activity-card-head">
            <strong>{entry.title}</strong>
            <span className="subtle">{formatTimestamp(entry.recordedAt)}</span>
          </div>
          <p className="subtle">
            <span className={`pill ${entry.status.toLowerCase()}`}>{formatStatus(entry.status)}</span>
          </p>
          <p className="subtle">{entry.summary}</p>
        </article>
      ))}
    </div>
  );
}
