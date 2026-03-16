#!/usr/bin/env node

const fs = require('fs/promises');
const path = require('path');
const { execFileSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const BACKLOG_JSON = path.join(ROOT, 'planning', 'backlog.json');
const API_ROOT = 'https://api.github.com';
const WRITE_THROTTLE_MS = Number(process.env.GITHUB_WRITE_THROTTLE_MS || '1200');
const MAX_RETRIES = Number(process.env.GITHUB_MAX_RETRIES || '10');

function requireEnv(name) {
  const value = process.env[name];
  if (!value) throw new Error(`Missing required environment variable: ${name}`);
  return value;
}

async function githubRequest(token, method, apiPath, body) {
  for (let attempt = 1; attempt <= MAX_RETRIES; attempt += 1) {
    if (body && (method === 'POST' || method === 'PATCH') && WRITE_THROTTLE_MS > 0) {
      await sleep(WRITE_THROTTLE_MS);
    }

    const response = await fetch(`${API_ROOT}${apiPath}`, {
      method,
      headers: {
        Accept: 'application/vnd.github+json',
        Authorization: `Bearer ${token}`,
        'User-Agent': 'idea-engine-publisher',
        'X-GitHub-Api-Version': '2022-11-28',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });

    const text = await response.text();
    const data = text ? JSON.parse(text) : null;
    if (response.ok) {
      return data;
    }

    if (shouldRetry(response.status, data) && attempt < MAX_RETRIES) {
      const delayMs = retryDelayMs(response, data, attempt);
      console.warn(
        `${method} ${apiPath} hit GitHub rate limiting (attempt ${attempt}/${MAX_RETRIES}); retrying in ${Math.ceil(delayMs / 1000)}s.`
      );
      await sleep(delayMs);
      continue;
    }

    const message = data?.message || text || `HTTP ${response.status}`;
    const error = new Error(`${method} ${apiPath} failed: ${message}`);
    error.status = response.status;
    error.data = data;
    throw error;
  }
}

function shouldRetry(status, data) {
  if (![403, 429].includes(status)) return false;
  const message = String(data?.message || '').toLowerCase();
  return message.includes('secondary rate limit') || message.includes('rate limit');
}

function retryDelayMs(response, data, attempt) {
  const retryAfterSeconds = Number(response.headers.get('retry-after'));
  if (Number.isFinite(retryAfterSeconds) && retryAfterSeconds > 0) {
    return retryAfterSeconds * 1000;
  }

  const message = String(data?.message || '').toLowerCase();
  if (message.includes('secondary rate limit') || message.includes('temporarily blocked from content creation')) {
    return Math.min(300000, 60000 * attempt);
  }

  const resetEpochSeconds = Number(response.headers.get('x-ratelimit-reset'));
  if (Number.isFinite(resetEpochSeconds) && resetEpochSeconds > 0) {
    const untilResetMs = resetEpochSeconds * 1000 - Date.now();
    if (untilResetMs > 0) return untilResetMs + 1000;
  }

  return Math.min(120000, 15000 * attempt);
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function git(...args) {
  return execFileSync('git', args, {
    cwd: ROOT,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  }).trim();
}

function ensureOrigin(owner, repo) {
  const remoteUrl = `git@github.com:${owner}/${repo}.git`;
  try {
    const current = git('remote', 'get-url', 'origin');
    if (current !== remoteUrl) git('remote', 'set-url', 'origin', remoteUrl);
  } catch {
    git('remote', 'add', 'origin', remoteUrl);
  }
  return remoteUrl;
}

async function ensureLabel(token, owner, repo, name, color, description) {
  try {
    await githubRequest(token, 'POST', `/repos/${owner}/${repo}/labels`, {
      name,
      color,
      description,
    });
  } catch (error) {
    if (error.status !== 422) throw error;
  }
}

async function loadBacklog() {
  return JSON.parse(await fs.readFile(BACKLOG_JSON, 'utf8'));
}

async function listAllIssues(token, owner, repo) {
  const issues = [];
  for (let page = 1; page < 10; page += 1) {
    const batch = await githubRequest(
      token,
      'GET',
      `/repos/${owner}/${repo}/issues?state=all&per_page=100&page=${page}`
    );
    issues.push(...batch.filter((issue) => !issue.pull_request));
    if (batch.length < 100) break;
  }
  return issues;
}

function buildEpicBody(epic, childIssueRefs = []) {
  const childLines = childIssueRefs.length
    ? childIssueRefs.map((item) => `- [ ] #${item.number} ${item.title}`).join('\n')
    : epic.childStories.map((story) => `- [ ] ${story.title}`).join('\n');

  const details = epic.devNotes.technicalDetails?.length
    ? `- Known technical details:\n${epic.devNotes.technicalDetails.map((d) => `  - ${d}`).join('\n')}`
    : '- Known technical details: none extracted yet';

  return `## User Story\n\n${epic.userStory}\n\n## Summary\n\n${epic.summary}\n\n## Acceptance Criteria\n\n${epic.acceptanceCriteria.map((item) => `- [ ] ${item}`).join('\n')}\n\n## Child Stories\n\n${childLines}\n\n## Dev Notes\n\n- Actor: ${epic.devNotes.actor}\n- Source section: \`${epic.devNotes.sourceSection}\`\n- Planned child stories: ${epic.childStories.length}\n${details}\n`;
}

function buildStoryBody(story, parentIssue) {
  const details = story.devNotes.technicalDetails?.length
    ? `- Known technical details:\n${story.devNotes.technicalDetails.map((d) => `  - ${d}`).join('\n')}`
    : '- Known technical details: none extracted yet';

  return `Parent epic: #${parentIssue.number} ${parentIssue.title}\nSource section: \`${story.sourceFile}\`\n\n## User Story\n\n${story.userStory}\n\n## Description\n\n${story.description}\n\n## Acceptance Criteria\n\n${story.acceptanceCriteria.map((item) => `- [ ] ${item}`).join('\n')}\n\n## Dev Notes\n\n- Parent epic: #${parentIssue.number} ${parentIssue.title}\n- Source section: \`${story.devNotes.sourceSection}\`\n${details}${story.devNotes.rawReference ? `\n\n### Source Excerpt\n\n${story.devNotes.rawReference}` : ''}\n`;
}

async function createOrReuseIssue(token, owner, repo, title, body, labels, existingByTitle) {
  if (existingByTitle.has(title)) {
    return existingByTitle.get(title);
  }

  const created = await githubRequest(token, 'POST', `/repos/${owner}/${repo}/issues`, {
    title,
    body,
    labels,
  });
  existingByTitle.set(title, created);
  return created;
}

async function updateIssueBody(token, owner, repo, issueNumber, body) {
  return githubRequest(token, 'PATCH', `/repos/${owner}/${repo}/issues/${issueNumber}`, { body });
}

async function main() {
  const token = requireEnv('GITHUB_TOKEN');
  const repo = requireEnv('GITHUB_REPO');
  const owner = process.env.GITHUB_OWNER || 'tmassey1979';

  ensureOrigin(owner, repo);
  git('push', '-u', 'origin', 'main');

  await ensureLabel(token, owner, repo, 'epic', '6f42c1', 'Epic backlog item');
  await ensureLabel(token, owner, repo, 'story', '0e8a16', 'Feature-sliced story');
  await ensureLabel(token, owner, repo, 'codex', '1d76db', 'Derived from codex planning');
  await ensureLabel(token, owner, repo, 'backlog', '5319e7', 'Generated planning backlog');

  const backlog = await loadBacklog();
  const existingIssues = await listAllIssues(token, owner, repo);
  const existingByTitle = new Map(existingIssues.map((issue) => [issue.title, issue]));
  const epicMap = new Map();

  for (const epic of backlog.epics) {
    const issue = await createOrReuseIssue(
      token,
      owner,
      repo,
      epic.title,
      buildEpicBody(epic),
      ['epic', 'codex', 'backlog'],
      existingByTitle
    );
    epicMap.set(epic.rawTitle, issue);
  }

  for (const story of backlog.stories) {
    const parentIssue = epicMap.get(story.epicTitle);
    if (!parentIssue) throw new Error(`Missing parent epic for story ${story.title}`);
    await createOrReuseIssue(
      token,
      owner,
      repo,
      story.title,
      buildStoryBody(story, parentIssue),
      ['story', 'codex', 'backlog'],
      existingByTitle
    );
  }

  const refreshedByTitle = new Map((await listAllIssues(token, owner, repo)).map((issue) => [issue.title, issue]));

  for (const epic of backlog.epics) {
    const epicIssue = refreshedByTitle.get(epic.title);
    const childIssueRefs = epic.childStories
      .map((story) => refreshedByTitle.get(story.title))
      .filter(Boolean)
      .map((issue) => ({ number: issue.number, title: issue.title }));

    await updateIssueBody(token, owner, repo, epicIssue.number, buildEpicBody(epic, childIssueRefs));
  }

  console.log(`Published ${backlog.epics.length} epics and ${backlog.stories.length} stories to ${owner}/${repo}.`);
}

main().catch((error) => {
  console.error(error.stack || String(error));
  process.exit(1);
});
