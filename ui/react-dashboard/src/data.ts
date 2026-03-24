export const workspaceTabs = [
  { id: "dashboard", label: "Dashboard" },
  { id: "intervention", label: "Intervention" },
  { id: "submit-idea", label: "Submit Idea" },
  { id: "idea-queue", label: "Idea Queue" },
  { id: "projects", label: "Projects" },
  { id: "idea-detail", label: "Idea Detail" },
] as const;

export const detailTabs = [
  { id: "backlog", label: "Backlog" },
  { id: "board", label: "Board" },
  { id: "activity", label: "Activity" },
] as const;

export const pageSizeOptions = [10, 20, 40, 50, 100] as const;

export const wizardSteps = [
  {
    id: "define",
    label: "Define the idea",
    description: "Name, problem, users, and MVP outcome.",
  },
  {
    id: "lane",
    label: "Choose build lane",
    description: "Route this project to core software now and leave room for future packs.",
  },
  {
    id: "constraints",
    label: "Set constraints",
    description: "Must-haves, timing, stack preferences, and approval notes.",
  },
] as const;

export const buildLanes = [
  {
    id: "core-software",
    title: "Core Software",
    state: "Phase 1 default",
    description: "Best for apps, APIs, workers, dashboards, internal tools, and the self-building loop.",
    active: true,
  },
  {
    id: "game-dev",
    title: "Game Dev",
    state: "Future pack",
    description: "For projects that will eventually need gameplay systems, content pipelines, and engine-specific agents.",
    active: false,
  },
  {
    id: "hardware-design",
    title: "Hardware Design",
    state: "Future pack",
    description: "For ideas that will later need component selection, schematic capture, board layout, and routing specialists.",
    active: false,
  },
] as const;
