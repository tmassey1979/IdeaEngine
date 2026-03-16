#!/usr/bin/env node

const fs = require("fs/promises");
const path = require("path");

const ROOT = path.resolve(__dirname, "..");
const CODEX_SECTIONS_DIR = path.join(ROOT, "codex", "sections");
const ISSUES_DIR = path.join(ROOT, "planning", "issues");
const INDEX_PATH = path.join(ROOT, "planning", "ISSUES.md");
const JSON_PATH = path.join(ROOT, "planning", "issues.json");

function stripQuotedHeader(content) {
  return content.replace(/^(>.*\n)+\n?/, "");
}

function slugify(value) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .replace(/-{2,}/g, "-");
}

function parseSectionMarkdown(content) {
  const markdown = stripQuotedHeader(content).trim();
  const headingMatch = markdown.match(/^#\s+([^\n]+)/m);
  const title = headingMatch ? headingMatch[1].trim() : "Untitled";

  const subheadings = [...markdown.matchAll(/^##\s+([^\n]+)/gm)].map((match) =>
    match[1].trim()
  );

  let summary = "";
  const purposeHeadingMatch = markdown.match(/^##\s+Purpose\s*$/m);
  if (purposeHeadingMatch && purposeHeadingMatch.index != null) {
    const start = purposeHeadingMatch.index + purposeHeadingMatch[0].length;
    const afterPurpose = markdown.slice(start).replace(/^\s+/, "");
    const collectedLines = [];

    for (const line of afterPurpose.split("\n")) {
      if (/^#\s+/.test(line) || /^##\s+/.test(line)) {
        break;
      }
      if (line.trim() === "---") {
        continue;
      }
      collectedLines.push(line);
    }

    summary = collectedLines.join("\n").trim();
  } else {
    const afterTitle = markdown
      .replace(/^#\s+[^\n]+\n+/, "")
      .split(/\n##\s+/)[0]
      .trim();
    summary = afterTitle;
  }

  summary = summary
    .replace(/(^|\n)---(?=\n|$)/g, "$1")
    .replace(/\n{3,}/g, "\n\n")
    .replace(/\s+\n/g, "\n")
    .trim();

  return { title, subheadings, summary };
}

function buildIssueDraft({ index, sourceFile, title, subheadings, summary }) {
  const issueTitle = `Codex: ${title}`;
  const checkItems = subheadings
    .filter((heading) => heading.toLowerCase() !== "purpose")
    .map((heading) => `- [ ] ${heading}`)
    .join("\n");

  const body = [
    `# ${issueTitle}`,
    "",
    `Source section: \`${sourceFile}\``,
    "",
    "## Summary",
    "",
    summary || "Implement the section defined in the codex.",
    "",
    "## Scope Checklist",
    "",
    checkItems || "- [ ] Define implementation scope from the source codex section.",
    "",
    "## Acceptance Criteria",
    "",
    "- [ ] The implementation plan for this codex section is documented.",
    "- [ ] Dependencies or sequencing constraints are identified.",
    "- [ ] Follow-up engineering tasks are clear enough to execute.",
    "",
  ].join("\n");

  return {
    index,
    sourceFile,
    title,
    issueTitle,
    summary,
    subheadings,
    body,
    fileName: `${String(index).padStart(2, "0")}-${slugify(title)}.md`,
  };
}

async function main() {
  const files = (await fs.readdir(CODEX_SECTIONS_DIR))
    .filter((file) => file.endsWith(".md"))
    .sort();

  if (!files.length) {
    throw new Error(`No codex section files found in ${CODEX_SECTIONS_DIR}`);
  }

  await fs.rm(ISSUES_DIR, { recursive: true, force: true });
  await fs.mkdir(ISSUES_DIR, { recursive: true });

  const drafts = [];

  for (const [index, file] of files.entries()) {
    const content = await fs.readFile(path.join(CODEX_SECTIONS_DIR, file), "utf8");
    const parsed = parseSectionMarkdown(content);
    const draft = buildIssueDraft({
      index: index + 1,
      sourceFile: `codex/sections/${file}`,
      ...parsed,
    });

    drafts.push(draft);
    await fs.writeFile(path.join(ISSUES_DIR, draft.fileName), `${draft.body}\n`, "utf8");
  }

  const indexMarkdown = [
    "# Issue Drafts",
    "",
    "These drafts were generated from the codex section files and are intended to become GitHub issues.",
    "",
    ...drafts.map(
      (draft) =>
        `- [${draft.issueTitle}](./issues/${draft.fileName}) from \`${draft.sourceFile}\``
    ),
    "",
  ].join("\n");

  await fs.writeFile(INDEX_PATH, indexMarkdown, "utf8");
  await fs.writeFile(JSON_PATH, `${JSON.stringify(drafts, null, 2)}\n`, "utf8");

  console.log(`Saved ${drafts.length} issue drafts to ${ISSUES_DIR}`);
}

main().catch((error) => {
  console.error(error.stack || String(error));
  process.exit(1);
});
