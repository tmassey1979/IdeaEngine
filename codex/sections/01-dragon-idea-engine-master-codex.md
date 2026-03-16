> Extracted from the shared ChatGPT conversation:
> https://chatgpt.com/share/69b808f3-3e40-8001-b31f-26e66ab44bb5
> Assistant turn: 40
> Original timestamp: 2026-03-13T18:19:44.771Z

# Dragon Idea Engine — Master Codex

## Purpose

Dragon Idea Engine is an **autonomous software factory** designed to turn ideas into fully developed software projects using modular AI agents.

The system:

```
Idea → Architecture → Repo → Issues → Code → Review → Deploy → Feedback
```

Agents operate through a **plugin-based runtime** and communicate through an asynchronous message queue.

Event-driven agent systems commonly use a queue broker so independent workers can process tasks asynchronously and scale horizontally.

---

# Core System Principles

### 1. Agents Are Plugins

Agents are not hardcoded.

They are **pluggable modules** loaded by the Agent Runner.

Inspired by patterns like:

```
dotnet CLI
git command plugins
kubectl plugins
```

Example:

```
dragon-agent-runner architect
dragon-agent-runner developer
dragon-agent-runner review
```

---

### 2. Runner Executes Agents

The **Agent Runner** is the runtime host.

Responsibilities:

```
load plugins
parse CLI arguments
execute agents
listen to message queues
route jobs
collect results
```

Modes:

```
CLI Mode
Service Mode
```

---

### 3. Event Driven Architecture

Agents communicate via queues.

Recommended broker:

```
RabbitMQ
```

This allows:

```
horizontal scaling
asynchronous tasks
fault tolerance
distributed workers
```

---

# System Architecture

```
                Dragon Idea Engine UI
                        │
                        ▼
                Dragon API Controller
                        │
                        ▼
                    RabbitMQ
                        │
       ┌────────────────┴─────────────────┐
       │                                  │
Dragon Agent Runner                Dragon Agent Runner
   (worker)                            (worker)
       │                                  │
       ▼                                  ▼
  Agent Plugins                       Agent Plugins
```

Multiple runners can exist.

Example:

```
10+ workers
100+ concurrent jobs
```

---

# Repository Structure

Dragon Idea Engine should use a **multi-repo workspace structure**.

## Root Repo

```
DragonIdeaEngine
```

Structure:

```
DragonIdeaEngine
│
├─ docs
│   MASTER_CODEX.md
│   AGENT_PLUGIN_SPEC.md
│   ARCHITECTURE.md
│
├─ runner
│   dragon-agent-runner
│
├─ agents
│   architect-agent
│   developer-agent
│   review-agent
│   test-agent
│   refactor-agent
│
├─ services
│   dragon-api
│   dragon-orchestrator
│
├─ ui
│   dragon-ui
│
├─ sdk
│   dragon-agent-sdk
│
└─ templates
    repo-templates
```

---

# Agent Runner

Project:

```
dragon-agent-runner
```

Responsibilities:

```
load agent plugins
run CLI commands
connect to RabbitMQ
execute queued jobs
return results
```

CLI example:

```
dragon-agent-runner developer --repo crm --issue 42
```

Service mode:

```
dragon-agent-runner --service
```

---

# Plugin System

Agents are dynamically loaded.

Directory:

```
/agents
```

Example:

```
agents/
  architect/
  developer/
  review/
```

Plugin interface:

```
name
description
registerArgs()
execute()
```

Example plugin skeleton:

```
export interface DragonAgent {

  name: string

  description: string

  registerArgs(cli)

  execute(context)
}
```

---

# Message Queue Format

Standard job message:

```
{
  "agent": "developer",
  "repo": "dragon-crm",
  "issue": 42,
  "priority": "normal",
  "payload": {}
}
```

Runner logic:

```
route message → matching agent
```

---

# Agent Types

## Idea Agent

Processes idea submissions.

Sources:

```
repo issues
UI submissions
feedback modules
```

Output:

```
project proposal
classification
score
```

---

## Architect Agent

Generates architecture documents.

Artifacts:

```
ARCHITECTURE.md
TECH_STACK.md
ROADMAP.md
```

---

## Repository Manager Agent

Creates repositories.

Tasks:

```
create repo
apply templates
configure CI
add feedback module
```

---

## Issue Generator Agent

Transforms architecture into issues.

Example:

```
Create auth API
Create login UI
Write tests
Add documentation
```

---

## Developer Agent

Implements issues.

Workflow:

```
clone repo
create branch
write code
run tests
push PR
```

---

## Review Agent

Reviews pull requests.

Checks:

```
security
architecture
style
tests
```

---

## Test Agent

Improves test coverage.

Tasks:

```
generate tests
detect missing cases
run suites
```

---

## Refactor Agent

Improves maintainability.

Tasks:

```
simplify code
reduce complexity
update dependencies
```

---

## Documentation Agent

Maintains documentation automatically.

Outputs:

```
README
API docs
architecture diagrams
```

---

## Feedback Agent

Processes user feedback from deployed apps.

Sources:

```
in-app feedback module
issues
analytics
```

Creates improvement issues.

---

# Autonomous Software Factory Loop

The system runs continuously.

Pipeline:

```
Idea
 ↓
Architect
 ↓
Repository
 ↓
Issue Generation
 ↓
Development
 ↓
Review
 ↓
Testing
 ↓
Deployment
 ↓
Feedback
 ↓
Improvement
```

This creates a **continuous evolution loop**.

---

# Repository Workspace Model

Developer agents must use **isolated workspaces**.

Example:

```
/workspaces
   dragon-crm
      issue-42
      issue-43
```

Workflow:

```
clone repo
create branch
implement issue
push PR
destroy workspace
```

Benefits:

```
no repo corruption
parallel agents
clean environments
```

---

# Credentials System

Dragon Idea Engine supports **hierarchical credentials**.

### Global Credentials

Configured in system settings.

Examples:

```
GitHub
GitLab
OpenAI
Anthropic
RabbitMQ
```

---

### Project Credentials

Projects may override credentials.

Example:

```
Project A → uses personal GitHub account
Project B → uses organization GitHub
```

Hierarchy:

```
project credentials
     ↓
global credentials
```

---

# Supported Git Providers

System must support multiple providers.

```
GitHub
GitLab
Gitea
Bitbucket (optional)
```

---

# UI Dashboard

React UI runs on the Raspberry Pi server.

Features:

## Ideas

```
view submitted ideas
approve or reject
view scores
```

---

## Projects

```
repositories
health status
activity
```

---

## Jobs

```
queued
running
failed
completed
```

---

## Agents

```
enabled agents
performance
logs
```

---

## Credentials

```
global credentials
project overrides
```

---

# Docker Deployment

Recommended containers:

```
dragon-ui
dragon-api
dragon-orchestrator
dragon-agent-runner
rabbitmq
postgres
```

Scaling workers:

```
docker compose up --scale agent-runner=10
```

---

# System Safety

Agents must implement safeguards.

### Job Timeouts

```
30 minute max
```

### Retry Limits

```
3 retries
```

### Resource Limits

```
CPU
memory
LLM cost limits
```

---

# Self Improvement

Dragon Idea Engine should improve itself.

The system monitors:

```
agent failures
PR rejection rates
test failures
runtime performance
```

It can create issues in:

```
DragonIdeaEngine
```

Example:

```
Developer agent generating unstable code
```

Agents then improve the platform itself.

---

# Long-Term Vision

Dragon Idea Engine becomes a **self-expanding ecosystem**.

Capabilities:

```
create new software products
maintain them
evolve them
generate revenue
improve the AI agents
```

The system evolves toward an **autonomous development organization**.

---

✅ Next thing I strongly recommend adding to the codex (this will make the system **10× more reliable**):

```
AGENT_JOB_SCHEMA.md
```

This will define:

- message formats
- job states
- retry logic
- error reporting
- observability

Without that spec, distributed agents will become very hard to debug.

If you want, I can also write the **Agent SDK design** next — which will make it incredibly easy for you (or the system itself) to create new agents in **minutes instead of hours**.
