> Extracted from the shared ChatGPT conversation:
> https://chatgpt.com/share/69b808f3-3e40-8001-b31f-26e66ab44bb5
> Assistant turn: 42
> Original timestamp: 2026-03-13T18:27:00.915Z

# Dragon Idea Engine — Master Codex Addendum

Add the following sections to:

```
/docs/MASTER_CODEX.md
```

---

# AGENT_JOB_SCHEMA

## Purpose

Define the **standard contract for agent jobs** across the entire system.

All agents must consume and produce jobs using this schema.

This ensures:

```
compatibility
traceability
retry support
debugging
orchestration
```

---

# Job Message Schema

All jobs published to RabbitMQ must follow this structure.

```json
{
  "jobId": "uuid",
  "agent": "developer",
  "action": "implement_issue",
  "repo": "dragon-crm",
  "project": "DragonCRM",
  "issue": 42,
  "priority": "normal",
  "createdAt": "timestamp",
  "payload": {},
  "metadata": {
    "requestedBy": "system",
    "source": "orchestrator"
  }
}
```

---

# Job Fields

### jobId

Unique identifier.

```
UUID
```

Example:

```
6f2c8f7a-1f43-4a3b-bd9f-6f92e7cbbfa3
```

---

### agent

Target agent plugin.

Examples:

```
idea
architect
developer
review
test
refactor
documentation
```

---

### action

Specific operation the agent should perform.

Examples:

```
create_architecture
generate_issues
implement_issue
review_pr
run_tests
refactor_code
generate_docs
```

---

### repo

Target repository.

Example:

```
dragon-crm
```

---

### project

Logical project grouping.

Example:

```
DragonCRM
```

---

### issue

Issue number if applicable.

Example:

```
42
```

---

### payload

Agent-specific parameters.

Example:

```json
{
  "branch": "feature/login",
  "baseBranch": "main"
}
```

---

### metadata

Tracking and audit information.

Example:

```json
{
  "requestedBy": "system",
  "source": "feedback_agent"
}
```

---

# Job Result Schema

Agents must return results in this format.

```json
{
  "jobId": "uuid",
  "status": "success",
  "agent": "developer",
  "duration": 12345,
  "result": {},
  "logs": [],
  "errors": []
}
```

---

# Job Status Values

```
queued
running
success
failed
retry
deadletter
```

---

# Retry Policy

Default retry configuration:

```
maxRetries: 3
retryDelay: exponential
```

Example:

```
10s
30s
90s
```

---

# Dead Letter Queue

Failed jobs are routed to:

```
dragon.deadletter
```

These can be inspected and replayed.

---

# Observability

Every job execution must emit:

```
logs
metrics
execution time
agent version
```

This enables debugging and performance monitoring.

---

# DRAGON AGENT SDK

## Purpose

Provide a **standard library for building agents quickly**.

Without an SDK every agent would implement:

```
rabbitmq connection
logging
job parsing
credential access
workspace management
```

The SDK centralizes this.

---

# SDK Package

```
dragon-agent-sdk
```

Language (initial):

```
Node / TypeScript
```

Future:

```
Python
Go
Rust
```

---

# SDK Responsibilities

The SDK provides:

```
job parsing
message publishing
workspace management
git utilities
credential resolution
logging
agent lifecycle hooks
```

---

# Agent Interface

Every agent must implement this interface.

```ts
export interface DragonAgent {

  name: string

  description: string

  version: string

  registerArgs(cli)

  execute(context: AgentContext): Promise<AgentResult>

}
```

---

# Agent Context

The runtime context passed to agents.

```ts
interface AgentContext {

  job
  payload
  workspace
  logger
  credentials
  repo
  config

}
```

---

# Agent Result

Standard result structure.

```ts
interface AgentResult {

  success: boolean

  message?: string

  artifacts?: any

  metrics?: {}

}
```

---

# Workspace Utilities

SDK automatically provides isolated workspaces.

Example:

```
/workspaces
  dragon-crm
    issue-42
```

Usage:

```ts
const repo = await workspace.cloneRepo(repoUrl)
```

---

# Git Utilities

The SDK provides helpers:

```
cloneRepo()
createBranch()
commit()
push()
createPullRequest()
```

Example:

```ts
await git.createBranch("feature/login")
```

---

# Credential Manager

Agents access credentials through SDK.

Example:

```ts
context.credentials.get("github")
```

Credential resolution order:

```
project credentials
global credentials
```

---

# Job Publishing

Agents can publish new jobs.

Example:

```ts
await jobs.publish({
  agent: "review",
  action: "review_pr",
  repo: "dragon-crm",
  payload: { pr: 22 }
})
```

---

# Logging

SDK includes structured logging.

Example:

```ts
logger.info("starting developer agent")
logger.error("test failure")
```

Logs automatically include:

```
jobId
agent
timestamp
```

---

# Example Agent Using SDK

```ts
export const developerAgent: DragonAgent = {

  name: "developer",

  description: "implements repository issues",

  version: "1.0",

  async execute(context) {

    const repo = await context.workspace.cloneRepo(context.repo)

    await context.git.createBranch("feature-" + context.job.issue)

    // AI coding step

    await context.git.commit("feat: implement issue")

    await context.git.push()

    return {
      success: true
    }

  }

}
```

---

# Future SDK Extensions

Planned features:

```
AI model abstraction
prompt templates
code analysis tools
architecture scanners
test generation
security scanning
```

---

# Codex Governance

All changes to the system must follow:

```
PR review
architecture validation
codex update
```

The **Master Codex is the authoritative specification** for Dragon Idea Engine.

---

# Next Recommended Section for the Codex

There is one more piece that will make this system **much easier to scale and maintain**:

```
AGENT_LIFECYCLE.md
```

This defines:

```
agent registration
versioning
agent upgrades
deprecation
plugin marketplace
```

It would also let Dragon Idea Engine eventually support **community-created agents**.

---

If you want, I can also design the **full GitHub repo layout for DragonIdeaEngine** so the system can eventually **self-generate new agents and services automatically.** That part is where this project becomes *really powerful*.
