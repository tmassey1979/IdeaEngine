#!/usr/bin/env node

const fs = require('fs/promises');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const CODEX_SECTIONS_DIR = path.join(ROOT, 'codex', 'sections');
const PLANNING_DIR = path.join(ROOT, 'planning');
const EPICS_DIR = path.join(PLANNING_DIR, 'epics');
const STORIES_DIR = path.join(PLANNING_DIR, 'stories');
const BACKLOG_INDEX = path.join(PLANNING_DIR, 'BACKLOG.md');
const BACKLOG_JSON = path.join(PLANNING_DIR, 'backlog.json');

const META_HEADING_PATTERNS = [
  /^purpose$/i,
  /^long[- ]term vision$/i,
  /^future expansion$/i,
  /^future risk extensions$/i,
  /^future\b/i,
  /^future section placeholder$/i,
  /^temporary rule$/i,
  /^recommended next codex section/i,
  /^next codex section/i,
  /^one more codex section/i,
  /^one more codex section you will likely want next/i,
  /^next recommended section/i,
  /^next section that/i,
  /^long term goal$/i,
];

const ACTOR_RULES = [
  [/master codex|addendum/i, 'platform architect'],
  [/infrastructure|cluster|raspberry pi|docker|deployment|orchestration|registry|sdk|schema/i, 'platform operator'],
  [/memory|knowledge|learning|self-improvement|discovery/i, 'platform architect'],
  [/idea scoring|idea discovery|risk|ethics|human|policy|distribution|collaboration|compliance/i, 'product owner'],
  [/project generation|unity|asset|media/i, 'delivery team'],
];

function stripQuotedHeader(content) {
  return content.replace(/^(>.*\n)+\n?/, '').trim();
}

function slugify(value) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .replace(/-{2,}/g, '-');
}

function humanizeTitle(title) {
  return title
    .replace(/[—–]/g, ' ')
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function chooseActor(title) {
  const normalized = humanizeTitle(title);
  for (const [pattern, actor] of ACTOR_RULES) {
    if (pattern.test(normalized)) return actor;
  }
  return 'platform operator';
}

function summarizeText(text, maxParagraphs = 2) {
  const cleaned = text
    .replace(/```[\s\S]*?```/g, '')
    .replace(/(^|\n)---(?=\n|$)/g, '$1')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
  const paragraphs = cleaned.split(/\n\n+/).map((p) => p.trim()).filter(Boolean);
  return paragraphs.slice(0, maxParagraphs).join('\n\n').trim();
}

function extractHeadings(markdown) {
  const regex = /^(#{1,6})\s+([^\n]+)$/gm;
  const headings = [];
  let match;
  while ((match = regex.exec(markdown))) {
    headings.push({
      level: match[1].length,
      text: match[2].trim(),
      start: match.index,
      end: regex.lastIndex,
    });
  }
  return headings;
}

function parseSection(content, fileName) {
  const markdown = stripQuotedHeader(content);
  const headings = extractHeadings(markdown);
  if (!headings.length) {
    throw new Error(`No headings found in ${fileName}`);
  }

  const titleHeading = headings[0];
  const title = titleHeading.text;
  const titleSlug = slugify(title);

  const sections = [];
  for (let i = 1; i < headings.length; i += 1) {
    const heading = headings[i];
    const next = headings[i + 1];
    const body = markdown.slice(heading.end, next ? next.start : markdown.length).trim();
    sections.push({
      level: heading.level,
      heading: heading.text,
      slug: slugify(heading.text),
      body,
      summary: summarizeText(body, 2),
    });
  }

  const intro = markdown.slice(titleHeading.end, sections.length ? headings[1].start : markdown.length).trim();
  const purposeSection = sections.find((section) => /^purpose$/i.test(section.heading));
  const summary = summarizeText(intro || purposeSection?.body || markdown, 2);

  const storySections = sections.filter((section) => {
    if (section.level > 2) return false;
    return !META_HEADING_PATTERNS.some((pattern) => pattern.test(section.heading));
  });

  return {
    fileName,
    sourceFile: `codex/sections/${fileName}`,
    title,
    slug: titleSlug,
    summary,
    actor: chooseActor(title),
    sections,
    storySections,
  };
}

function firstSentence(text) {
  const cleaned = text.replace(/\n+/g, ' ').trim();
  const match = cleaned.match(/^(.*?[.!?])(?:\s|$)/);
  return (match ? match[1] : cleaned).trim();
}

function epicGoal(title) {
  const human = humanizeTitle(title);
  if (/master codex addendum/i.test(human)) return 'a formal codex addendum that defines the agent contract and SDK surface';
  if (/master codex/i.test(human)) return 'a master codex that defines the platform, agents, and governance model';
  if (/infrastructure architecture/i.test(human)) return 'a deployment architecture for Dragon Idea Engine';
  if (/idea scoring/i.test(human)) return 'an idea scoring and selection system';
  if (/risk analysis/i.test(human)) return 'risk and compliance scoring before ideas are approved';
  if (/ethics analysis/i.test(human)) return 'an ethics analysis agent';
  if (/reusable component/i.test(human)) return 'a reusable component library';
  if (/memory and knowledge/i.test(human)) return 'an agent memory and knowledge system';
  if (/open source policy/i.test(human)) return 'a mandatory open source policy';
  if (/distribution and collaboration/i.test(human)) return 'a platform distribution and collaboration model';
  if (/orchestration engine/i.test(human)) return 'an agent orchestration engine';
  if (/capability registry/i.test(human)) return 'an agent capability registry and discovery system';
  if (/project generation pipeline/i.test(human)) return 'a project generation pipeline';
  if (/unity development/i.test(human)) return 'a Unity development agent suite';
  if (/asset and media/i.test(human)) return 'an asset and media generation pipeline';
  if (/knowledge and learning/i.test(human)) return 'a knowledge and learning system';
  if (/distributed agent cluster/i.test(human)) return 'a distributed agent cluster architecture';
  if (/security and compliance/i.test(human)) return 'a security and compliance validation system';
  if (/autonomous idea discovery/i.test(human)) return 'an autonomous idea discovery system';
  if (/self-improvement and evolution/i.test(human)) return 'an agent self-improvement and evolution system';
  if (/human collaboration and override/i.test(human)) return 'a human collaboration and override system';
  return `the ${human}`;
}

function epicBenefit(entry) {
  const human = humanizeTitle(entry.title);
  if (/master codex addendum/i.test(human)) return 'agent jobs, SDK behavior, and governance rules stay consistent across the platform';
  if (/master codex/i.test(human)) return 'the platform has a shared implementation and governance blueprint';
  if (/infrastructure architecture/i.test(human)) return 'the platform can run locally on Raspberry Pi and scale cleanly later';
  if (/idea scoring/i.test(human)) return 'the system only invests in ideas worth building';
  if (/risk analysis/i.test(human)) return 'unsafe or non-compliant ideas are filtered before build work starts';
  if (/ethics analysis/i.test(human)) return 'harmful ideas are caught before they enter the build pipeline';
  if (/reusable component/i.test(human)) return 'new projects can be assembled faster with less duplicated code';
  if (/memory and knowledge/i.test(human)) return 'agents can learn from previous outcomes instead of starting from zero';
  if (/open source policy/i.test(human)) return 'generated projects follow the intended distribution rules';
  if (/distribution and collaboration/i.test(human)) return 'the platform can grow a healthy operating and contribution model';
  if (/orchestration engine/i.test(human)) return 'agent work is routed, sequenced, and recovered consistently';
  if (/capability registry/i.test(human)) return 'the system knows which agents exist and what they can do';
  if (/project generation pipeline/i.test(human)) return 'approved ideas can move through a repeatable delivery flow';
  if (/unity development/i.test(human)) return 'Unity-based products can be built with the same platform workflow';
  if (/asset and media/i.test(human)) return 'media work can be generated and validated alongside code';
  if (/knowledge and learning/i.test(human)) return 'the platform continuously improves its recommendations and decisions';
  if (/distributed agent cluster/i.test(human)) return 'multiple nodes can execute work reliably as one system';
  if (/security and compliance/i.test(human)) return 'security and regulatory checks are built into delivery';
  if (/autonomous idea discovery/i.test(human)) return 'the platform can surface promising ideas without waiting for manual input';
  if (/self-improvement and evolution/i.test(human)) return 'agents can improve their own prompts, strategies, and model choices over time';
  if (/human collaboration and override/i.test(human)) return 'humans can safely guide, approve, and interrupt the system when needed';
  const sentence = firstSentence(entry.summary);
  return sentence.replace(/\.$/, '');
}

function storyGoal(heading) {
  const value = humanizeTitle(heading)
    .replace(/^the\s+/i, '')
    .replace(/^an\s+/i, '')
    .replace(/^a\s+/i, '')
    .trim();
  return `the ${value.toLowerCase()} capability`;
}

function storyBenefit(section, epicTitle) {
  const sentence = firstSentence(section.summary || '');
  if (!sentence) return `the ${humanizeTitle(epicTitle).toLowerCase()} epic can be completed predictably`;
  const cleaned = sentence.replace(/\.$/, '').trim();
  return cleaned.charAt(0).toLowerCase() + cleaned.slice(1);
}

function extractTechnicalDetails(text, limit = 5) {
  const details = [];
  const codeBlockRegex = /```(?:[^\n]*)\n([\s\S]*?)```/g;
  let match;
  while ((match = codeBlockRegex.exec(text))) {
    const lines = match[1]
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line && line.length <= 80)
      .filter((line) => !/[┌┐└┘│▼▲]/.test(line));
    for (const line of lines) {
      if (!details.includes(line)) details.push(line);
      if (details.length >= limit) return details;
    }
  }

  const bulletLines = text
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => /^[-*]\s+/.test(line))
    .map((line) => line.replace(/^[-*]\s+/, '').trim())
    .filter((line) => line.length <= 90);

  for (const line of bulletLines) {
    if (!details.includes(line)) details.push(line);
    if (details.length >= limit) return details;
  }

  return details;
}

function buildEpic(entry, index) {
  const storyTitle = `As a ${entry.actor}, I want ${epicGoal(entry.title)}, so that ${epicBenefit(entry)}.`;
  const childStories = entry.storySections.map((section, storyIndex) => ({
    id: `${String(index).padStart(2, '0')}.${String(storyIndex + 1).padStart(2, '0')}`,
    epicIndex: index,
    epicTitle: entry.title,
    epicSlug: entry.slug,
    sourceFile: entry.sourceFile,
    heading: section.heading,
    title: `[Story] ${humanizeTitle(entry.title)}: ${humanizeTitle(section.heading)}`,
    userStory: `As a ${entry.actor}, I want ${storyGoal(section.heading)}, so that ${storyBenefit(section, entry.title)}.`,
    description: section.summary || `Implement the ${section.heading} portion of ${entry.title}.`,
    acceptanceCriteria: [
      `The ${humanizeTitle(section.heading)} behavior is implemented according to the codex definition.`,
      ...(() => {
        const details = extractTechnicalDetails(section.body, 3);
        if (!details.length) return [];
        return [`The implementation covers these codex details: ${details.join(', ')}.`];
      })(),
      `Dependencies and integration points with the rest of ${humanizeTitle(entry.title)} are documented.`,
    ],
    devNotes: {
      sourceSection: entry.sourceFile,
      parentEpic: `[Epic] ${humanizeTitle(entry.title)}`,
      technicalDetails: extractTechnicalDetails(section.body, 5),
      rawReference: section.body.trim(),
    },
    fileName: `${String(index).padStart(2, '0')}-${String(storyIndex + 1).padStart(2, '0')}-${slugify(`${entry.slug}-${section.heading}`)}.md`,
  }));

  const epic = {
    id: String(index).padStart(2, '0'),
    sourceFile: entry.sourceFile,
    title: `[Epic] ${humanizeTitle(entry.title)}`,
    rawTitle: entry.title,
    userStory: storyTitle,
    summary: entry.summary,
    acceptanceCriteria: [
      'All major implementation slices for this codex section are represented by child stories.',
      'The epic outcome is documented clearly enough to guide implementation and review.',
      'Known dependencies, sequencing constraints, and governance concerns are captured.',
    ],
    devNotes: {
      sourceSection: entry.sourceFile,
      actor: entry.actor,
      technicalDetails: extractTechnicalDetails(entry.summary + '\n' + entry.sections.slice(0, 5).map((s) => s.body).join('\n'), 8),
      sectionHeadings: entry.storySections.map((section) => section.heading),
    },
    childStories,
    fileName: `${String(index).padStart(2, '0')}-${entry.slug}.md`,
  };

  return epic;
}

function markdownList(items) {
  return items.map((item) => `- [ ] ${item}`).join('\n');
}

function buildEpicMarkdown(epic) {
  return `# ${epic.title}

Source section: \`${epic.sourceFile}\`

## User Story

${epic.userStory}

## Summary

${epic.summary}

## Acceptance Criteria

${markdownList(epic.acceptanceCriteria)}

## Child Stories

${epic.childStories.map((story) => `- [ ] ${story.title}`).join('\n')}

## Dev Notes

- Actor: ${epic.devNotes.actor}
- Source section: \`${epic.devNotes.sourceSection}\`
- Planned child stories: ${epic.childStories.length}
${epic.devNotes.technicalDetails.length ? `- Known technical details:\n${epic.devNotes.technicalDetails.map((detail) => `  - ${detail}`).join('\n')}` : '- Known technical details: none extracted yet'}
`;
}

function buildStoryMarkdown(story) {
  return `# ${story.title}

Parent epic: ${story.devNotes.parentEpic}
Source section: \`${story.sourceFile}\`

## User Story

${story.userStory}

## Description

${story.description}

## Acceptance Criteria

${markdownList(story.acceptanceCriteria)}

## Dev Notes

- Parent epic: ${story.devNotes.parentEpic}
- Source section: \`${story.devNotes.sourceSection}\`
${story.devNotes.technicalDetails.length ? `- Known technical details:\n${story.devNotes.technicalDetails.map((detail) => `  - ${detail}`).join('\n')}` : '- Known technical details: none extracted yet'}
${story.devNotes.rawReference ? `
### Source Excerpt

${story.devNotes.rawReference}
` : ''}`;
}

function buildBacklogIndex(epics) {
  const lines = [
    '# Backlog',
    '',
    'This backlog is derived from the codex and organized into epic issues plus feature-sliced child stories.',
    '',
    '## Epics',
    '',
  ];

  for (const epic of epics) {
    lines.push(`- [${epic.title}](./epics/${epic.fileName})`);
    for (const story of epic.childStories) {
      lines.push(`  - [${story.title}](./stories/${story.fileName})`);
    }
  }

  lines.push('');
  return lines.join('\n');
}

async function main() {
  const files = (await fs.readdir(CODEX_SECTIONS_DIR)).filter((file) => file.endsWith('.md')).sort();
  const parsedSections = [];
  for (const file of files) {
    const content = await fs.readFile(path.join(CODEX_SECTIONS_DIR, file), 'utf8');
    parsedSections.push(parseSection(content, file));
  }

  const epics = parsedSections.map((section, index) => buildEpic(section, index + 1));
  const stories = epics.flatMap((epic) => epic.childStories);

  await fs.rm(EPICS_DIR, { recursive: true, force: true });
  await fs.rm(STORIES_DIR, { recursive: true, force: true });
  await fs.mkdir(EPICS_DIR, { recursive: true });
  await fs.mkdir(STORIES_DIR, { recursive: true });

  for (const epic of epics) {
    await fs.writeFile(path.join(EPICS_DIR, epic.fileName), `${buildEpicMarkdown(epic)}\n`, 'utf8');
    for (const story of epic.childStories) {
      await fs.writeFile(path.join(STORIES_DIR, story.fileName), `${buildStoryMarkdown(story)}\n`, 'utf8');
    }
  }

  await fs.writeFile(BACKLOG_INDEX, `${buildBacklogIndex(epics)}\n`, 'utf8');
  await fs.writeFile(BACKLOG_JSON, `${JSON.stringify({ epics, stories }, null, 2)}\n`, 'utf8');

  console.log(`Saved ${epics.length} epics and ${stories.length} stories.`);
}

main().catch((error) => {
  console.error(error.stack || String(error));
  process.exit(1);
});
