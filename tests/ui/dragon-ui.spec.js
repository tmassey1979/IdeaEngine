const { test, expect } = require("@playwright/test");

const workspaceTabs = [
  { name: "Dashboard", id: "dashboard", heading: /Ideas come in, route into the right build lane/i },
  { name: "Submit Idea", id: "submit-idea", heading: /Guided intake with future pack routing/i },
  { name: "Idea Queue", id: "idea-queue", heading: /Submitted ideas, approvals, and build order/i },
  { name: "Projects", id: "projects", heading: /Ideas already in active delivery/i },
  { name: "Idea Detail", id: "idea-detail", heading: /Idea Detail|Summary|Preferred stack/i },
];

test("workspace tabs only show one section at a time", async ({ page }) => {
  await page.goto("/");
  const nav = page.getByLabel("Primary");

  for (const tab of workspaceTabs) {
    await nav.getByRole("link", { name: tab.name }).click();

    for (const panel of workspaceTabs) {
      const locator = page.locator(`#${panel.id}`);
      if (panel.id === tab.id) {
        await expect(locator).toBeVisible();
        await expect(locator).toContainText(tab.heading);
      } else {
        await expect(locator).toBeHidden();
      }
    }
  }
});

test("hero actions and project cards route into the correct workspace tab", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("link", { name: "Open Idea Queue" }).click();
  await expect(page.locator("#idea-queue")).toBeVisible();
  await expect(page.locator("#dashboard")).toBeHidden();

  await page.getByLabel("Primary").getByRole("link", { name: "Dashboard" }).click();
  const firstProjectCard = page.locator("#active-projects-list .project-card").first();
  await firstProjectCard.focus();
  await page.keyboard.press("Enter");
  await expect(page.locator("#idea-detail")).toBeVisible();
  await expect(page.locator("#dashboard")).toBeHidden();
});

test("loads a requested workspace tab from the hash", async ({ page }) => {
  await page.goto("/index.html#submit-idea");

  await expect(page.locator("#submit-idea")).toBeVisible();
  await expect(page.locator("#dashboard")).toBeHidden();
});

test("detail sub-tabs switch between backlog, board, and activity", async ({ page }) => {
  await page.goto("/index.html#idea-detail");

  await expect(page.locator('.tab-panel[data-panel="backlog"]')).toBeVisible();
  await page.getByRole("button", { name: "Board" }).click();
  await expect(page.locator('.tab-panel[data-panel="board"]')).toBeVisible();
  await expect(page.locator('.tab-panel[data-panel="backlog"]')).toBeHidden();

  await page.getByRole("button", { name: "Activity" }).click();
  await expect(page.locator('.tab-panel[data-panel="activity"]')).toBeVisible();
  await expect(page.locator('.tab-panel[data-panel="board"]')).toBeHidden();
});

test("dashboard status cards render live or sample status values", async ({ page }) => {
  await page.goto("/");

  await expect(page.locator("#status-loop-health")).not.toHaveText(/Loading/i);
  await expect(page.locator("#status-services")).toContainText("healthy");
  await expect(page.locator("#status-source")).toContainText(/Live backend status|Sample status/i);
});
