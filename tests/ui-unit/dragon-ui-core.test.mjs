import fs from "node:fs";
import path from "node:path";
import { beforeEach, describe, expect, test, vi } from "vitest";
import {
  buildRealIdeas,
  formatStatus,
  getUiState,
  renderDashboard,
  renderIdeaDetail,
  renderQueueAndProjects,
  resetUiState,
  setDetailTab,
  setWorkspaceTab,
} from "../../ui/dragon-ui/app-core.mjs";

const indexHtml = fs.readFileSync(path.resolve("ui/dragon-ui/index.html"), "utf8");
const sampleSnapshot = JSON.parse(fs.readFileSync(path.resolve("ui/dragon-ui/sample-status.json"), "utf8"));

function mountUi() {
  document.documentElement.innerHTML = indexHtml;
  for (const element of document.querySelectorAll("script")) {
    element.remove();
  }

  Element.prototype.scrollIntoView = vi.fn();
}

describe("dragon-ui core rendering", () => {
  beforeEach(() => {
    mountUi();
    resetUiState();
  });

  test("buildRealIdeas maps backend issues into project cards", () => {
    const ideas = buildRealIdeas({
      issues: [
        {
          issueNumber: 1,
          issueTitle: "Alpha",
          overallStatus: "in_progress",
          currentStage: "review",
          queuedJobCount: 0,
          latestExecutionSummary: "Review completed",
        },
        {
          issueNumber: 2,
          issueTitle: "Beta",
          overallStatus: "failed",
          currentStage: "test",
          queuedJobCount: 0,
          workflowNote: "Needs recovery",
        },
      ],
    });

    expect(ideas).toHaveLength(2);
    expect(ideas[0]).toMatchObject({ id: "1", status: "printing", phase: "review" });
    expect(ideas[1].status).toBe("blocked");
    expect(ideas[1].blockers).toContain("Needs recovery");
  });

  test("renderDashboard fills loop metrics, project cards, and forecast cards", () => {
    renderDashboard(sampleSnapshot);

    expect(document.getElementById("status-loop-health").textContent).toBeTruthy();
    expect(document.getElementById("status-services").textContent).toMatch(/\d\/\d healthy/);
    expect(document.querySelectorAll("#active-projects-list .project-card").length).toBeGreaterThan(0);
    expect(document.querySelectorAll("#forecast-list .forecast-card").length).toBeGreaterThan(0);
    expect(getUiState().currentIdeas.length).toBeGreaterThan(0);
  });

  test("renderQueueAndProjects builds queue rows and project cards from the current snapshot", () => {
    renderDashboard(sampleSnapshot);
    renderQueueAndProjects();

    expect(document.querySelectorAll("#queue-table-body tr[data-idea-id]").length).toBeGreaterThan(0);
    expect(document.querySelectorAll("#project-grid .project-card").length).toBe(getUiState().currentIdeas.length);
  });

  test("renderIdeaDetail shows the selected project summary and activity", () => {
    renderDashboard(sampleSnapshot);
    const firstIdea = getUiState().currentIdeas[0];
    resetUiState({
      currentIdeas: getUiState().currentIdeas,
      selectedIdeaId: firstIdea.id,
      currentSnapshot: sampleSnapshot,
    });

    renderIdeaDetail();

    expect(document.getElementById("idea-detail-title").textContent).toBe(firstIdea.name);
    expect(document.getElementById("idea-detail-status").textContent).toBe(formatStatus(firstIdea.status));
    expect(document.querySelectorAll("#idea-activity-list .activity-card").length).toBeGreaterThan(0);
  });

  test("workspace tabs toggle a single active section", () => {
    setWorkspaceTab("submit-idea", { skipScroll: true });

    expect(document.getElementById("submit-idea").classList.contains("active")).toBe(true);
    expect(document.getElementById("dashboard").classList.contains("active")).toBe(false);
    expect(document.querySelector('.nav-link[href="#submit-idea"]').classList.contains("active")).toBe(true);
  });

  test("detail tabs toggle the active inner panel", () => {
    setDetailTab("board");

    expect(document.querySelector('.tab[data-tab="board"]').classList.contains("active")).toBe(true);
    expect(document.querySelector('.tab-panel[data-panel="board"]').classList.contains("active")).toBe(true);
    expect(document.querySelector('.tab-panel[data-panel="backlog"]').classList.contains("active")).toBe(false);
  });
});
