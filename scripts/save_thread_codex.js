#!/usr/bin/env node

const fs = require("fs/promises");
const path = require("path");
const https = require("https");

const SHARE_URL =
  "https://chatgpt.com/share/69b808f3-3e40-8001-b31f-26e66ab44bb5";
const OUTPUT_ROOT = path.resolve(__dirname, "..", "codex");
const SECTIONS_DIR = path.join(OUTPUT_ROOT, "sections");
const ARCHIVE_DIR = path.join(OUTPUT_ROOT, "archive");

const CURRENT_TURNS = [
  40, 42, 44, 46, 48, 50, 52, 54, 56, 60, 62, 64, 66, 68, 70, 72, 74, 76, 78,
  80, 82,
];

const ARCHIVED_TURNS = [
  {
    turn: 58,
    note: "Superseded later in the thread by PLATFORM_DISTRIBUTION_AND_COLLABORATION_MODEL (turn 60).",
  },
];

function fetchText(url, redirects = 0) {
  return new Promise((resolve, reject) => {
    https
      .get(url, (response) => {
        const status = response.statusCode || 0;

        if (
          [301, 302, 303, 307, 308].includes(status) &&
          response.headers.location
        ) {
          if (redirects >= 5) {
            reject(new Error(`Too many redirects while fetching ${url}`));
            return;
          }

          const nextUrl = new URL(response.headers.location, url).toString();
          resolve(fetchText(nextUrl, redirects + 1));
          return;
        }

        if (status < 200 || status >= 300) {
          reject(new Error(`Request failed for ${url}: HTTP ${status}`));
          return;
        }

        let body = "";
        response.setEncoding("utf8");
        response.on("data", (chunk) => {
          body += chunk;
        });
        response.on("end", () => resolve(body));
      })
      .on("error", reject);
  });
}

function decodeConversationPage(html) {
  const match = html.match(
    /streamController\.enqueue\("((?:\\.|[^"\\])*)"\);/
  );

  if (!match) {
    throw new Error("Could not locate embedded conversation payload.");
  }

  const payload = JSON.parse(`"${match[1]}"`);
  const tokens = Function(`return (${payload.trim()});`)();
  const memo = new Map();

  function resolveRef(value) {
    if (typeof value === "number" && Number.isInteger(value)) {
      if (value === -5) {
        return undefined;
      }
      if (value >= 0) {
        return resolve(value);
      }
    }
    return value;
  }

  function resolve(index) {
    if (memo.has(index)) {
      return memo.get(index);
    }

    const value = tokens[index];

    if (Array.isArray(value)) {
      const output = [];
      memo.set(index, output);
      for (const item of value) {
        output.push(resolveRef(item));
      }
      return output;
    }

    if (value && typeof value === "object") {
      const output = {};
      memo.set(index, output);
      for (const [key, item] of Object.entries(value)) {
        const resolvedKey = /^_\d+$/.test(key)
          ? resolveRef(Number(key.slice(1)))
          : key;
        output[resolvedKey] = resolveRef(item);
      }
      return output;
    }

    memo.set(index, value);
    return value;
  }

  return resolve(0).loaderData["routes/share.$shareId.($action)"].serverResponse
    .data;
}

function getVisibleTurns(conversation) {
  const turns = [];
  let visibleTurn = 0;

  for (const item of conversation.linear_conversation || []) {
    const message = item.message;
    if (!message) {
      continue;
    }

    const role = message.author?.role;
    if (!["user", "assistant"].includes(role)) {
      continue;
    }

    const content = message.content || {};
    let text = "";

    if (Array.isArray(content.parts)) {
      text = content.parts.join("\n");
    } else if (typeof content.text === "string") {
      text = content.text;
    }

    text = (text || "").trim();
    if (!text) {
      continue;
    }

    visibleTurn += 1;
    turns.push({
      visibleTurn,
      role,
      text,
      createTime: message.create_time || null,
    });
  }

  return turns;
}

function getMarkdownBody(text) {
  const firstHeadingIndex = text.search(/^#\s+/m);
  return (firstHeadingIndex >= 0 ? text.slice(firstHeadingIndex) : text).trim();
}

function normalizeArtifacts(text) {
  return text
    .replace(/cite[^]+/g, "")
    .replace(/entity\[[^\]]*\]/g, "")
    .replace(/[ \t]+\n/g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function slugifyHeading(heading) {
  return heading
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .replace(/-{2,}/g, "-");
}

function buildSectionEntry(turn, order) {
  const body = normalizeArtifacts(getMarkdownBody(turn.text));
  const headings = [...body.matchAll(/^#\s+([^\n]+)/gm)].map((match) =>
    match[1].trim()
  );
  const title = headings[0] || `turn-${turn.visibleTurn}`;
  const slug = slugifyHeading(title);
  const fileName = `${String(order).padStart(2, "0")}-${slug}.md`;

  return {
    ...turn,
    body,
    headings,
    title,
    slug,
    fileName,
  };
}

function buildFileHeader({ visibleTurn, title, createTime, note }) {
  const lines = [
    "> Extracted from the shared ChatGPT conversation:",
    `> ${SHARE_URL}`,
    `> Assistant turn: ${visibleTurn}`,
  ];

  if (createTime) {
    lines.push(
      `> Original timestamp: ${new Date(createTime * 1000).toISOString()}`
    );
  }

  if (note) {
    lines.push(`> Note: ${note}`);
  }

  return `${lines.join("\n")}\n\n`;
}

async function writeSectionFile(dir, entry, note) {
  const filePath = path.join(dir, entry.fileName);
  const content = `${buildFileHeader({ ...entry, note })}${entry.body}\n`;
  await fs.writeFile(filePath, content, "utf8");
  return filePath;
}

function buildIndex(currentEntries, archivedEntries) {
  const currentLines = currentEntries.map(
    (entry) =>
      `- [${entry.title}](./sections/${entry.fileName}) - assistant turn ${entry.visibleTurn}`
  );

  const archivedLines = archivedEntries.map(
    ({ entry, note }) =>
      `- [${entry.title}](./archive/${entry.fileName}) - assistant turn ${entry.visibleTurn} (${note})`
  );

  return `# Codex Index

This directory was extracted from the shared ChatGPT thread below and saved into repo-local markdown files:

- ${SHARE_URL}

## Current Codex Files

${currentLines.join("\n")}

## Archived / Superseded Files

${archivedLines.join("\n")}
`;
}

function buildMasterCodex(currentEntries) {
  const header = `# Dragon Idea Engine Master Codex

This file compiles the current codex sections extracted from the shared ChatGPT thread below.

- Source: ${SHARE_URL}
- Generated by: \`scripts/save_thread_codex.js\`

## Included Sections

${currentEntries
  .map(
    (entry) => `- ${entry.title} (\`sections/${entry.fileName}\`, assistant turn ${entry.visibleTurn})`
  )
  .join("\n")}
`;

  const compiledSections = currentEntries
    .map(
      (entry) =>
        `\n<!-- BEGIN sections/${entry.fileName} -->\n\n${entry.body}\n\n<!-- END sections/${entry.fileName} -->`
    )
    .join("\n");

  return `${header}${compiledSections}\n`;
}

async function main() {
  const html = await fetchText(SHARE_URL);
  const conversation = decodeConversationPage(html);
  const turns = getVisibleTurns(conversation);
  const turnMap = new Map(turns.map((turn) => [turn.visibleTurn, turn]));

  const currentEntries = CURRENT_TURNS.map((turnNumber, index) => {
    const turn = turnMap.get(turnNumber);
    if (!turn) {
      throw new Error(`Could not find assistant turn ${turnNumber}.`);
    }
    return buildSectionEntry(turn, index + 1);
  });

  const archivedEntries = ARCHIVED_TURNS.map(({ turn, note }, index) => {
    const turnEntry = turnMap.get(turn);
    if (!turnEntry) {
      throw new Error(`Could not find archived assistant turn ${turn}.`);
    }

    return {
      entry: buildSectionEntry(turnEntry, index + 1),
      note,
    };
  });

  await fs.mkdir(OUTPUT_ROOT, { recursive: true });
  await fs.rm(SECTIONS_DIR, { recursive: true, force: true });
  await fs.rm(ARCHIVE_DIR, { recursive: true, force: true });
  await fs.mkdir(SECTIONS_DIR, { recursive: true });
  await fs.mkdir(ARCHIVE_DIR, { recursive: true });

  for (const entry of currentEntries) {
    await writeSectionFile(SECTIONS_DIR, entry);
  }

  for (const { entry, note } of archivedEntries) {
    await writeSectionFile(ARCHIVE_DIR, entry, note);
  }

  await fs.writeFile(
    path.join(OUTPUT_ROOT, "INDEX.md"),
    buildIndex(currentEntries, archivedEntries),
    "utf8"
  );

  await fs.writeFile(
    path.join(OUTPUT_ROOT, "MASTER_CODEX.md"),
    buildMasterCodex(currentEntries),
    "utf8"
  );

  console.log(`Saved ${currentEntries.length} current codex files.`);
  console.log(`Saved ${archivedEntries.length} archived codex files.`);
  console.log(`Output directory: ${OUTPUT_ROOT}`);
}

main().catch((error) => {
  console.error(error.stack || String(error));
  process.exit(1);
});
