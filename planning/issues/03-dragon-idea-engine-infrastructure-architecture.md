# Codex: Dragon Idea Engine — Infrastructure Architecture

Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`

## Summary

This section defines the **deployment model and repository layout** for the Dragon Idea Engine platform.
The system is designed to operate in two phases:
```
Phase 1: Single-node Raspberry Pi Engine
Phase 2: Distributed Raspberry Pi Cluster
```
The **single-node system must always remain fully functional** without requiring cloud services.
# Deployment Philosophy
Dragon Idea Engine prioritizes:
```
self-hosted
portable
low-power infrastructure
edge deployment
distributed scalability
```
Primary target platform:
```
Raspberry Pi 5
```
Recommended configuration:
```
8GB RAM
NVMe SSD via PCIe
Ubuntu Server
Docker
```
# Phase 1 — Raspberry Pi Autonomous Engine
Phase 1 runs **all components on a single device**.
Architecture:
```
                ┌───────────────────────┐
                │ Dragon UI (React)     │
                └─────────────┬─────────┘
                              │
                              ▼
                      ┌───────────────┐
                      │ Dragon API    │
                      └───────┬───────┘
                              │
                              ▼
                      ┌───────────────┐
                      │ Orchestrator  │
                      └───────┬───────┘
                              │
                              ▼
                        RabbitMQ Queue
                              │
                              ▼
                     Agent Runner Workers
                              │
                              ▼
                        Agent Plugins
```
Everything runs locally.
# Containers on the Pi
The Pi runs a Docker stack containing:
```
dragon-ui
dragon-api
dragon-orchestrator
dragon-agent-runner
rabbitmq
postgres
```
Optional:
```
ollama (local AI models)
redis (cache)
```
# Local AI Support
Dragon Idea Engine should support both:
```
cloud AI providers
local models
```
Local inference options:
```
Ollama
Local Llama models
Mistral
Code Llama
```
This allows:
```
offline operation
lower cost
privacy
```
# Raspberry Pi Image
The project will eventually produce a **Dragon Idea Engine OS image**.
Image includes:
```
Ubuntu Server
Docker
RabbitMQ
Postgres
Dragon Engine services
Agent Runner
React UI
```
Boot experience:
```
power on
engine starts
UI accessible on LAN
```
Example:
```
http://dragon.local
```
# Storage Layout
Recommended filesystem structure:
```
/dragon-engine
    /config
    /repos
    /workspaces
    /logs
    /agents
    /models
```
Details:
```
repos       → cached repositories
workspaces  → temporary agent workspaces
logs        → job and system logs
agents      → plugin agents
models      → local AI models
```
# Workspace Model
Developer agents create temporary workspaces.
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
implement issue
push PR
destroy workspace
```
This ensures:
```
parallel agents
clean builds
safe execution
```
# Phase 2 — Raspberry Pi Cluster
Phase 2 introduces **horizontal scaling** using multiple Pi nodes.
Cluster model:
```
                ┌───────────────────┐
                │ Control Node Pi   │
                │ Dragon Engine     │
                └──────────┬────────┘
                           │
                      RabbitMQ
                           │
        ┌───────────────┬───────────────┬───────────────┐
        │ Worker Pi     │ Worker Pi     │ Worker Pi     │
        │ Agent Runner  │ Agent Runner  │ Agent Runner  │
        └───────────────┴───────────────┴───────────────┘
```
# Cluster Technology
Recommended orchestrator:
```
Docker Swarm
```
Reasons:
```
simple setup
lightweight
native Docker support
works well on ARM
```
Alternative options:
```
K3s (lightweight Kubernetes)
Nomad
```
# Cluster Responsibilities
### Control Node
Runs:
```
dragon-ui
dragon-api
orchestrator
rabbitmq
postgres
```
### Worker Nodes
Run:
```
dragon-agent-runner
agent plugins
```
Workers consume jobs from RabbitMQ.
# Scaling Model
Example cluster:
```
1 control node
5 worker nodes
```
Possible concurrent agents:
```
50+ jobs
```
Because:
```
each Pi runs multiple containers
```
# Job Scheduling
RabbitMQ distributes jobs across workers.
Agents become:
```
stateless workers
```
Advantages:
```
fault tolerance
easy scaling
high throughput
```
# Repository Storage Strategy
Repositories may be stored on:
```
local disk
shared NFS
distributed storage
```
Recommended:
```
control node repo cache
worker temporary clones
```
# Network Requirements
All nodes must share a local network.
Example:
```
1Gb Ethernet
```
Recommended topology:
```
Gigabit switch
wired network
```
# Cluster Expansion
Adding nodes should be simple.
Example:
```
flash Dragon Pi image
connect to network
join swarm
```
New worker automatically begins consuming jobs.
# Edge + Cloud Hybrid (Optional)
Future architecture may support hybrid operation.
Example:
```
local cluster
+
cloud GPU agents
```
Possible uses:
```
heavy AI workloads
large model inference
code analysis
```
But the system must **never require cloud infrastructure** to function.
# Repository Layout (Full Project)
Final recommended GitHub structure for the main repository:
```
DragonIdeaEngine
│
├─ docs
│   MASTER_CODEX.md
│   AGENT_JOB_SCHEMA.md
│   AGENT_PLUGIN_SPEC.md
│   AGENT_LIFECYCLE.md
│   ARCHITECTURE.md
│
├─ runner
│   dragon-agent-runner
│
├─ sdk
│   dragon-agent-sdk
│
├─ agents
│   architect-agent
│   developer-agent
│   review-agent
│   test-agent
│   refactor-agent
│   documentation-agent
│   feedback-agent
│
├─ services
│   dragon-api
│   dragon-orchestrator
│
├─ ui
│   dragon-ui
│
├─ infrastructure
│   docker
│   swarm
│   raspberrypi-image
│
├─ templates
│   project-templates
│
└─ examples
    sample-project
```
# Long Term Vision
Dragon Idea Engine becomes:
```
a personal autonomous development lab
a distributed Pi software factory
an extensible AI agent ecosystem
```
Running on a small cluster of Raspberry Pis, it can:
```
generate new applications
maintain repositories
process user feedback
evolve software continuously
```
All from a **self-hosted system sitting on a desk or rack**.

## Scope Checklist

- [ ] Next Codex Section I Recommend

## Acceptance Criteria

- [ ] The implementation plan for this codex section is documented.
- [ ] Dependencies or sequencing constraints are identified.
- [ ] Follow-up engineering tasks are clear enough to execute.

