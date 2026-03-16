#!/usr/bin/env node

const fs = require("fs/promises");
const path = require("path");
const { execFileSync } = require("child_process");

const ROOT = path.resolve(__dirname, "..");
const ISSUES_JSON = path.join(ROOT, "planning", "issues.json");
const API_ROOT = "https://api.github.com";

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

async function githubRequest(token, method, apiPath, body) {
  const response = await fetch(`${API_ROOT}${apiPath}`, {
    method,
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${token}`,
      "User-Agent": "idea-engine-publisher",
      "X-GitHub-Api-Version": "2022-11-28",
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const message = data?.message || text || `HTTP ${response.status}`;
    const error = new Error(`${method} ${apiPath} failed: ${message}`);
    error.status = response.status;
    error.data = data;
    throw error;
  }

  return data;
}

function git(...args) {
  return execFileSync("git", args, {
    cwd: ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
}

async function ensureRepo(token, owner, repo, isPrivate) {
  try {
    const existing = await githubRequest(token, "GET", `/repos/${owner}/${repo}`);
    return { repo: existing, created: false };
  } catch (error) {
    if (error.status !== 404) {
      throw error;
    }
  }

  const created = await githubRequest(token, "POST", "/user/repos", {
    name: repo,
    private: isPrivate,
    has_issues: true,
    auto_init: false,
  });

  return { repo: created, created: true };
}

function ensureOrigin(owner, repo) {
  const remoteUrl = `git@github.com:${owner}/${repo}.git`;

  try {
    const current = git("remote", "get-url", "origin");
    if (current !== remoteUrl) {
      git("remote", "set-url", "origin", remoteUrl);
    }
  } catch {
    git("remote", "add", "origin", remoteUrl);
  }

  return remoteUrl;
}

async function ensureLabel(token, owner, repo, name, color, description) {
  try {
    await githubRequest(token, "POST", `/repos/${owner}/${repo}/labels`, {
      name,
      color,
      description,
    });
  } catch (error) {
    if (error.status !== 422) {
      throw error;
    }
  }
}

async function loadDraftIssues() {
  const content = await fs.readFile(ISSUES_JSON, "utf8");
  return JSON.parse(content);
}

async function listExistingIssueTitles(token, owner, repo) {
  const issues = await githubRequest(
    token,
    "GET",
    `/repos/${owner}/${repo}/issues?state=all&per_page=100`
  );

  return new Set(
    issues
      .filter((issue) => !issue.pull_request)
      .map((issue) => issue.title)
  );
}

async function createIssues(token, owner, repo, drafts) {
  const existingTitles = await listExistingIssueTitles(token, owner, repo);
  let created = 0;
  let skipped = 0;

  for (const draft of drafts) {
    if (existingTitles.has(draft.issueTitle)) {
      skipped += 1;
      continue;
    }

    await githubRequest(token, "POST", `/repos/${owner}/${repo}/issues`, {
      title: draft.issueTitle,
      body: draft.body,
      labels: ["codex", "planning"],
    });
    created += 1;
  }

  return { created, skipped };
}

async function main() {
  const token = requireEnv("GITHUB_TOKEN");
  const repo = requireEnv("GITHUB_REPO");
  const visibility = (process.env.GITHUB_VISIBILITY || "private").toLowerCase();
  const isPrivate = visibility !== "public";
  const owner = process.env.GITHUB_OWNER || "tmassey1979";
  const drafts = await loadDraftIssues();

  const { created: repoCreated } = await ensureRepo(token, owner, repo, isPrivate);
  const remoteUrl = ensureOrigin(owner, repo);

  git("push", "-u", "origin", "main");

  await ensureLabel(token, owner, repo, "codex", "1d76db", "Codex-driven planning item");
  await ensureLabel(
    token,
    owner,
    repo,
    "planning",
    "5319e7",
    "Generated planning issue"
  );

  const issueResult = await createIssues(token, owner, repo, drafts);

  console.log(`Owner: ${owner}`);
  console.log(`Repo: ${repo}`);
  console.log(`Visibility: ${isPrivate ? "private" : "public"}`);
  console.log(`Remote: ${remoteUrl}`);
  console.log(`Repo created: ${repoCreated ? "yes" : "no"}`);
  console.log(`Issues created: ${issueResult.created}`);
  console.log(`Issues skipped: ${issueResult.skipped}`);
}

main().catch((error) => {
  console.error(error.stack || String(error));
  process.exit(1);
});
