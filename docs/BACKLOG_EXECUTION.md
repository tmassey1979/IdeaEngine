## Issue #105: [Story] Dragon Idea Engine Master Codex: Agent Runner

Project:
Responsibilities:

## Issue #106: [Story] Dragon Idea Engine Master Codex: Plugin System

Agents are dynamically loaded.
Directory:

## Issue #107: [Story] Dragon Idea Engine Master Codex: Message Queue Format

Standard job message:
Runner logic:

## Issue #108: [Story] Dragon Idea Engine Master Codex: Agent Types

Implement the Agent Types portion of Dragon Idea Engine â€” Master Codex.

## Issue #112: [Story] Dragon Idea Engine Master Codex: Issue Generator Agent

Transforms architecture into issues.
Example:

## Issue #120: [Story] Dragon Idea Engine Master Codex: Repository Workspace Model

Developer agents must use **isolated workspaces**.
Example:

## Issue #121: [Story] Dragon Idea Engine Master Codex: Credentials System

Dragon Idea Engine supports **hierarchical credentials**.

## Issue #122: [Story] Dragon Idea Engine Master Codex: Supported Git Providers

System must support multiple providers.

## Issue #123: [Story] Dragon Idea Engine Master Codex: UI Dashboard

React UI runs on the Raspberry Pi server.
Features:

## Issue #124: [Story] Dragon Idea Engine Master Codex: Ideas

Implement the Ideas portion of Dragon Idea Engine â€” Master Codex.

## Issue #125: [Story] Dragon Idea Engine Master Codex: Projects

Implement the Projects portion of Dragon Idea Engine â€” Master Codex.

## Issue #126: [Story] Dragon Idea Engine Master Codex: Jobs

Implement the Jobs portion of Dragon Idea Engine â€” Master Codex.

## Issue #127: [Story] Dragon Idea Engine Master Codex: Agents

Implement the Agents portion of Dragon Idea Engine â€” Master Codex.

## Issue #128: [Story] Dragon Idea Engine Master Codex: Credentials

Implement the Credentials portion of Dragon Idea Engine â€” Master Codex.

## Issue #129: [Story] Dragon Idea Engine Master Codex: Docker Deployment

Recommended containers:
Scaling workers:

## Issue #130: [Story] Dragon Idea Engine Master Codex: System Safety

Agents must implement safeguards.

## Issue #131: [Story] Dragon Idea Engine Master Codex: Self Improvement

Dragon Idea Engine should improve itself.
The system monitors:

## Issue #201: [Story] Dragon Idea Engine Master Codex Addendum: AGENT JOB SCHEMA

Implement the AGENT_JOB_SCHEMA portion of Dragon Idea Engine â€” Master Codex Addendum.

## Issue #202: [Story] Dragon Idea Engine Master Codex Addendum: Job Message Schema

All jobs published to RabbitMQ must follow this structure.

## Issue #203: [Story] Dragon Idea Engine Master Codex Addendum: Job Fields

Implement the Job Fields portion of Dragon Idea Engine â€” Master Codex Addendum.

## Issue #204: [Story] Dragon Idea Engine Master Codex Addendum: Job Result Schema

Agents must return results in this format.

## Issue #205: [Story] Dragon Idea Engine Master Codex Addendum: Job Status Values

Implement the Job Status Values portion of Dragon Idea Engine â€” Master Codex Addendum.

## Issue #206: [Story] Dragon Idea Engine Master Codex Addendum: Retry Policy

Default retry configuration:
Example:

## Issue #207: [Story] Dragon Idea Engine Master Codex Addendum: Dead Letter Queue

Failed jobs are routed to:
These can be inspected and replayed.

## Issue #208: [Story] Dragon Idea Engine Master Codex Addendum: Observability

Every job execution must emit:
This enables debugging and performance monitoring.

## Issue #215: [Story] Dragon Idea Engine Master Codex Addendum: Workspace Utilities

SDK automatically provides isolated workspaces.
Example:

## Issue #216: [Story] Dragon Idea Engine Master Codex Addendum: Git Utilities

The SDK provides helpers:
Example:

## Issue #217: [Story] Dragon Idea Engine Master Codex Addendum: Credential Manager

Agents access credentials through SDK.
Example:

## Issue #218: [Story] Dragon Idea Engine Master Codex Addendum: Job Publishing

Agents can publish new jobs.
Example:

## Issue #219: [Story] Dragon Idea Engine Master Codex Addendum: Logging

SDK includes structured logging.
Example:

## Issue #221: [Story] Dragon Idea Engine Master Codex Addendum: Codex Governance

All changes to the system must follow:
The **Master Codex is the authoritative specification** for Dragon Idea Engine.

## Issue #401: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Sources

Ideas may originate from:
All ideas are normalized into a single structure.

## Issue #402: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Data Structure

Example idea record:
Stored in the `ideas` table.

## Issue #404: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Classification

The **Idea Classification Agent** extracts metadata.
Fields produced:

## Issue #405: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scoring Criteria

Each idea receives a weighted score.
Base scoring model:

## Issue #406: [Story] IDEA SCORING AND SELECTION SYSTEM: Scoring Dimensions

Implement the Scoring Dimensions portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #407: [Story] IDEA SCORING AND SELECTION SYSTEM: Market Demand

Measures potential user demand.
Range:

## Issue #408: [Story] IDEA SCORING AND SELECTION SYSTEM: Technical Feasibility

Measures difficulty relative to system capability.
Range:

## Issue #409: [Story] IDEA SCORING AND SELECTION SYSTEM: Development Cost

Estimates resource cost.
Range:

## Issue #410: [Story] IDEA SCORING AND SELECTION SYSTEM: Differentiation

Measures uniqueness compared to existing tools.
Range:

## Issue #411: [Story] IDEA SCORING AND SELECTION SYSTEM: Monetization Potential

Optional but useful for commercial use.
Range:

## Issue #412: [Story] IDEA SCORING AND SELECTION SYSTEM: Strategic Value

Measures alignment with Dragon Idea Engine goals.
Range:

## Issue #413: [Story] IDEA SCORING AND SELECTION SYSTEM: Example Scoring

Idea:
Example scoring:

## Issue #414: [Story] IDEA SCORING AND SELECTION SYSTEM: Decision Thresholds

The Decision Engine determines the outcome.

## Issue #415: [Story] IDEA SCORING AND SELECTION SYSTEM: Build Outcome

If the idea is approved:
The system automatically triggers:

## Issue #416: [Story] IDEA SCORING AND SELECTION SYSTEM: Deferred Ideas

Deferred ideas remain in the queue.
They may be reevaluated when:

## Issue #417: [Story] IDEA SCORING AND SELECTION SYSTEM: Archived Ideas

Archived ideas are stored but inactive.
They can be reactivated manually.

## Issue #418: [Story] IDEA SCORING AND SELECTION SYSTEM: Rejected Ideas

Rejected ideas are ignored by the system but retained for audit history.

## Issue #420: [Story] IDEA SCORING AND SELECTION SYSTEM: Anti-Explosion Limits

To prevent runaway project creation the system enforces limits.
Example policies:

## Issue #421: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Dashboard (UI)

The Dragon UI should display:

## Issue #422: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Queue

Implement the Idea Queue portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #423: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scores

Display:

## Issue #424: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Voting

Users may vote:
Votes influence scoring.

## Issue #425: [Story] IDEA SCORING AND SELECTION SYSTEM: AI Research Agent (Future)

A future **Research Agent** may enrich idea scoring by analyzing:
This improves idea evaluation accuracy.

## Issue #426: [Story] IDEA SCORING AND SELECTION SYSTEM: Long-Term Goal

The Idea Scoring System transforms Dragon Idea Engine into a **structured innovation pipeline** rather than a random project generator.
The system should ultimately behave like:

## Issue #601: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Model

Ethical evaluation produces an **Ethics Risk Score**.
This penalty is applied alongside the legal risk score.

## Issue #602: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Dimensions

Implement the Ethical Risk Dimensions portion of ETHICS_ANALYSIS_AGENT.

## Issue #603: [Story] ETHICS ANALYSIS AGENT: Harm Potential

Could the system cause harm if misused?
Examples:

## Issue #604: [Story] ETHICS ANALYSIS AGENT: Manipulation Risk

Evaluates psychological manipulation.
Examples:

## Issue #605: [Story] ETHICS ANALYSIS AGENT: Privacy Intrusion

Ethical privacy concerns beyond legal compliance.
Examples:

## Issue #606: [Story] ETHICS ANALYSIS AGENT: Social Impact

Evaluates societal harm potential.
Examples:

## Issue #607: [Story] ETHICS ANALYSIS AGENT: Misuse Potential

Measures how easily a tool could be weaponized.
Examples:

## Issue #608: [Story] ETHICS ANALYSIS AGENT: Ethics Decision Threshold

Automatic rejection occurs if:
Or if the project clearly enables:

## Issue #612: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Agents are grouped into functional domains.

## Issue #614: [Story] ETHICS ANALYSIS AGENT: Software Development Agents

Each agent specializes in specific languages or frameworks.
Example agents:

## Issue #615: [Story] ETHICS ANALYSIS AGENT: Hardware Development Agents

Dragon Idea Engine will support electronics projects.
Hardware agents may generate:

## Issue #616: [Story] ETHICS ANALYSIS AGENT: Media Production Agents

Some projects require media assets.
Media agents can generate:

## Issue #617: [Story] ETHICS ANALYSIS AGENT: Research Agents

Research agents analyze external data.
Examples:

## Issue #619: [Story] ETHICS ANALYSIS AGENT: Operations Agents

Operations agents manage system lifecycle.
Examples:

## Issue #620: [Story] ETHICS ANALYSIS AGENT: MULTI MODEL AGENT FRAMEWORK

Implement the MULTI_MODEL_AGENT_FRAMEWORK portion of ETHICS_ANALYSIS_AGENT.

## Issue #621: [Story] ETHICS ANALYSIS AGENT: Model Routing

Example routing strategy:

## Issue #622: [Story] ETHICS ANALYSIS AGENT: Example Model Assignment

Example configuration:
Models may come from providers such as:

## Issue #624: [Story] ETHICS ANALYSIS AGENT: Task Router

A **Task Router** selects which agent handles a task.
Example:

## Issue #625: [Story] ETHICS ANALYSIS AGENT: Agent Collaboration

Agents can collaborate.
Example workflow:

## Issue #626: [Story] ETHICS ANALYSIS AGENT: Local Raspberry Pi Deployment Compatibility

Because Dragon Idea Engine will run on a **Raspberry Pi base image**, agents must support:
The system must gracefully degrade if cloud models are unavailable.

## Issue #701: [Story] REUSABLE COMPONENT LIBRARY: Component Categories

Reusable components are organized into standardized domains.
Each component is versioned and stored in a centralized registry.

## Issue #703: [Story] REUSABLE COMPONENT LIBRARY: PI EDITION CORE SERVICES

The Raspberry Pi edition will provide **shared infrastructure services** running locally.
These services act as **platform primitives** for all generated projects.

## Issue #704: [Story] REUSABLE COMPONENT LIBRARY: Database Layer

All generated applications should use centralized database services rather than spawning their own instances.
Recommended databases:

## Issue #705: [Story] REUSABLE COMPONENT LIBRARY: Messaging Layer

Microservices require asynchronous communication.
The Pi edition should run a shared message broker using:

## Issue #706: [Story] REUSABLE COMPONENT LIBRARY: Authentication and Identity

All generated applications should rely on a centralized authentication service.
Recommended solution:

## Issue #707: [Story] REUSABLE COMPONENT LIBRARY: API Gateway Component

A reusable API gateway component should standardize service access.
Responsibilities:

## Issue #708: [Story] REUSABLE COMPONENT LIBRARY: Logging and Observability

A shared logging stack helps monitor generated applications.
Recommended stack:

## Issue #709: [Story] REUSABLE COMPONENT LIBRARY: Standard Service Templates

Reusable service templates should exist for common architectures.
Examples:

## Issue #710: [Story] REUSABLE COMPONENT LIBRARY: Hardware Component Library

Because Dragon Idea Engine may generate electronics projects, reusable hardware modules should exist.
Examples:

## Issue #711: [Story] REUSABLE COMPONENT LIBRARY: Agent Usage Rules

When generating systems, agents must follow this priority order:
Agents should **never duplicate infrastructure logic** unnecessarily.

## Issue #712: [Story] REUSABLE COMPONENT LIBRARY: Component Versioning

All reusable components must follow semantic versioning.
Example:

## Issue #713: [Story] REUSABLE COMPONENT LIBRARY: Component Dependency Graph

The component registry maintains a dependency graph.
Example:

## Issue #714: [Story] REUSABLE COMPONENT LIBRARY: Pi Edition Resource Constraints

Because the system runs on **Raspberry Pi hardware**, components must be optimized for low-resource environments.
Constraints:

## Issue #29: [Story] Dragon Idea Engine Master Codex: Agent Types

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the agent types capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Agent Types portion of Dragon Idea Engine â€” Master Codex.

## Issue #29: [Story] Dragon Idea Engine Master Codex: Agent Types

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the agent types capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Agent Types portion of Dragon Idea Engine â€” Master Codex.

## Issue #29: [Story] Dragon Idea Engine Master Codex: Agent Types

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the agent types capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Agent Types portion of Dragon Idea Engine â€” Master Codex.

## Issue #33: [Story] Dragon Idea Engine Master Codex: Issue Generator Agent

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the issue generator agent capability, so that transforms architecture into issues.

## Description
Transforms architecture into issues.

## Issue #33: [Story] Dragon Idea Engine Master Codex: Issue Generator Agent

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the issue generator agent capability, so that transforms architecture into issues.

## Description
Transforms architecture into issues.

## Issue #33: [Story] Dragon Idea Engine Master Codex: Issue Generator Agent

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the issue generator agent capability, so that transforms architecture into issues.

## Description
Transforms architecture into issues.

## Issue #41: [Story] Dragon Idea Engine Master Codex: Repository Workspace Model

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the repository workspace model capability, so that developer agents must use **isolated workspaces**.

## Description
Developer agents must use **isolated workspaces**.

## Issue #41: [Story] Dragon Idea Engine Master Codex: Repository Workspace Model

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the repository workspace model capability, so that developer agents must use **isolated workspaces**.

## Description
Developer agents must use **isolated workspaces**.

## Issue #41: [Story] Dragon Idea Engine Master Codex: Repository Workspace Model

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the repository workspace model capability, so that developer agents must use **isolated workspaces**.

## Description
Developer agents must use **isolated workspaces**.

## Issue #42: [Story] Dragon Idea Engine Master Codex: Credentials System

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the credentials system capability, so that dragon Idea Engine supports **hierarchical credentials**.

## Description
Dragon Idea Engine supports **hierarchical credentials**.

## Issue #42: [Story] Dragon Idea Engine Master Codex: Credentials System

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the credentials system capability, so that dragon Idea Engine supports **hierarchical credentials**.

## Description
Dragon Idea Engine supports **hierarchical credentials**.

## Issue #42: [Story] Dragon Idea Engine Master Codex: Credentials System

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the credentials system capability, so that dragon Idea Engine supports **hierarchical credentials**.

## Description
Dragon Idea Engine supports **hierarchical credentials**.

## Issue #43: [Story] Dragon Idea Engine Master Codex: Supported Git Providers

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the supported git providers capability, so that system must support multiple providers.

## Description
System must support multiple providers.

## Issue #43: [Story] Dragon Idea Engine Master Codex: Supported Git Providers

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the supported git providers capability, so that system must support multiple providers.

## Description
System must support multiple providers.

## Issue #43: [Story] Dragon Idea Engine Master Codex: Supported Git Providers

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the supported git providers capability, so that system must support multiple providers.

## Description
System must support multiple providers.

## Issue #44: [Story] Dragon Idea Engine Master Codex: UI Dashboard

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the ui dashboard capability, so that react UI runs on the Raspberry Pi server.

## Description
React UI runs on the Raspberry Pi server.

## Issue #44: [Story] Dragon Idea Engine Master Codex: UI Dashboard

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the ui dashboard capability, so that react UI runs on the Raspberry Pi server.

## Description
React UI runs on the Raspberry Pi server.

## Issue #44: [Story] Dragon Idea Engine Master Codex: UI Dashboard

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the ui dashboard capability, so that react UI runs on the Raspberry Pi server.

## Description
React UI runs on the Raspberry Pi server.

## Issue #45: [Story] Dragon Idea Engine Master Codex: Ideas

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the ideas capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Ideas portion of Dragon Idea Engine â€” Master Codex.

## Issue #45: [Story] Dragon Idea Engine Master Codex: Ideas

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the ideas capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Ideas portion of Dragon Idea Engine â€” Master Codex.

## Issue #45: [Story] Dragon Idea Engine Master Codex: Ideas

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the ideas capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Ideas portion of Dragon Idea Engine â€” Master Codex.

## Issue #46: [Story] Dragon Idea Engine Master Codex: Projects

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the projects capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Projects portion of Dragon Idea Engine â€” Master Codex.

## Issue #46: [Story] Dragon Idea Engine Master Codex: Projects

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the projects capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Projects portion of Dragon Idea Engine â€” Master Codex.

## Issue #46: [Story] Dragon Idea Engine Master Codex: Projects

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the projects capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Projects portion of Dragon Idea Engine â€” Master Codex.

## Issue #47: [Story] Dragon Idea Engine Master Codex: Jobs

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the jobs capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Jobs portion of Dragon Idea Engine â€” Master Codex.

## Issue #47: [Story] Dragon Idea Engine Master Codex: Jobs

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the jobs capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Jobs portion of Dragon Idea Engine â€” Master Codex.

## Issue #47: [Story] Dragon Idea Engine Master Codex: Jobs

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the jobs capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Jobs portion of Dragon Idea Engine â€” Master Codex.

## Issue #48: [Story] Dragon Idea Engine Master Codex: Agents

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the agents capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Agents portion of Dragon Idea Engine â€” Master Codex.

## Issue #48: [Story] Dragon Idea Engine Master Codex: Agents

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the agents capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Agents portion of Dragon Idea Engine â€” Master Codex.

## Issue #48: [Story] Dragon Idea Engine Master Codex: Agents

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the agents capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Agents portion of Dragon Idea Engine â€” Master Codex.

## Issue #49: [Story] Dragon Idea Engine Master Codex: Credentials

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the credentials capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Credentials portion of Dragon Idea Engine â€” Master Codex.

## Issue #49: [Story] Dragon Idea Engine Master Codex: Credentials

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the credentials capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Credentials portion of Dragon Idea Engine â€” Master Codex.

## Issue #49: [Story] Dragon Idea Engine Master Codex: Credentials

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the credentials capability, so that the dragon idea engine master codex epic can be completed predictably.

## Description
Implement the Credentials portion of Dragon Idea Engine â€” Master Codex.

## Issue #50: [Story] Dragon Idea Engine Master Codex: Docker Deployment

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the docker deployment capability, so that recommended containers: Scaling workers:.

## Description
Recommended containers:

## Issue #50: [Story] Dragon Idea Engine Master Codex: Docker Deployment

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the docker deployment capability, so that recommended containers: Scaling workers:.

## Description
Recommended containers:

## Issue #50: [Story] Dragon Idea Engine Master Codex: Docker Deployment

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the docker deployment capability, so that recommended containers: Scaling workers:.

## Description
Recommended containers:

## Issue #51: [Story] Dragon Idea Engine Master Codex: System Safety

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the system safety capability, so that agents must implement safeguards.

## Description
Agents must implement safeguards.

## Issue #51: [Story] Dragon Idea Engine Master Codex: System Safety

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the system safety capability, so that agents must implement safeguards.

## Description
Agents must implement safeguards.

## Issue #51: [Story] Dragon Idea Engine Master Codex: System Safety

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the system safety capability, so that agents must implement safeguards.

## Description
Agents must implement safeguards.

## Issue #52: [Story] Dragon Idea Engine Master Codex: Self Improvement

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the self improvement capability, so that dragon Idea Engine should improve itself.

## Description
Dragon Idea Engine should improve itself.

## Issue #52: [Story] Dragon Idea Engine Master Codex: Self Improvement

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the self improvement capability, so that dragon Idea Engine should improve itself.

## Description
Dragon Idea Engine should improve itself.

## Issue #52: [Story] Dragon Idea Engine Master Codex: Self Improvement

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`

## User Story
As a platform architect, I want the self improvement capability, so that dragon Idea Engine should improve itself.

## Description
Dragon Idea Engine should improve itself.

## Issue #55: [Story] Dragon Idea Engine Master Codex Addendum: Job Fields

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story
As a platform architect, I want the job fields capability, so that the dragon idea engine master codex addendum epic can be completed predictably.

## Description
Implement the Job Fields portion of Dragon Idea Engine â€” Master Codex Addendum.

## Issue #55: [Story] Dragon Idea Engine Master Codex Addendum: Job Fields

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story
As a platform architect, I want the job fields capability, so that the dragon idea engine master codex addendum epic can be completed predictably.

## Description
Implement the Job Fields portion of Dragon Idea Engine â€” Master Codex Addendum.

## Issue #55: [Story] Dragon Idea Engine Master Codex Addendum: Job Fields

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story
As a platform architect, I want the job fields capability, so that the dragon idea engine master codex addendum epic can be completed predictably.

## Description
Implement the Job Fields portion of Dragon Idea Engine â€” Master Codex Addendum.

## Issue #73: [Story] Dragon Idea Engine Master Codex Addendum: Codex Governance

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story
As a platform architect, I want the codex governance capability, so that all changes to the system must follow: The **Master Codex is the authoritative specification** for Dragon Idea Engine.

## Description
All changes to the system must follow:

## Issue #73: [Story] Dragon Idea Engine Master Codex Addendum: Codex Governance

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story
As a platform architect, I want the codex governance capability, so that all changes to the system must follow: The **Master Codex is the authoritative specification** for Dragon Idea Engine.

## Description
All changes to the system must follow:

## Issue #73: [Story] Dragon Idea Engine Master Codex Addendum: Codex Governance

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`

## User Story
As a platform architect, I want the codex governance capability, so that all changes to the system must follow: The **Master Codex is the authoritative specification** for Dragon Idea Engine.

## Description
All changes to the system must follow:

## Issue #91: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Sources

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea sources capability, so that ideas may originate from: All ideas are normalized into a single structure.

## Description
Ideas may originate from:

## Issue #91: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Sources

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea sources capability, so that ideas may originate from: All ideas are normalized into a single structure.

## Description
Ideas may originate from:

## Issue #91: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Sources

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea sources capability, so that ideas may originate from: All ideas are normalized into a single structure.

## Description
Ideas may originate from:

## Issue #92: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Data Structure

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea data structure capability, so that example idea record: Stored in the `ideas` table.

## Description
Example idea record:

## Issue #92: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Data Structure

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea data structure capability, so that example idea record: Stored in the `ideas` table.

## Description
Example idea record:

## Issue #92: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Data Structure

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea data structure capability, so that example idea record: Stored in the `ideas` table.

## Description
Example idea record:

## Issue #94: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Classification

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea classification capability, so that the **Idea Classification Agent** extracts metadata.

## Description
The **Idea Classification Agent** extracts metadata.

## Issue #94: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Classification

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea classification capability, so that the **Idea Classification Agent** extracts metadata.

## Description
The **Idea Classification Agent** extracts metadata.

## Issue #94: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Classification

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea classification capability, so that the **Idea Classification Agent** extracts metadata.

## Description
The **Idea Classification Agent** extracts metadata.

## Issue #95: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scoring Criteria

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea scoring criteria capability, so that each idea receives a weighted score.

## Description
Each idea receives a weighted score.

## Issue #95: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scoring Criteria

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea scoring criteria capability, so that each idea receives a weighted score.

## Description
Each idea receives a weighted score.

## Issue #95: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scoring Criteria

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the idea scoring criteria capability, so that each idea receives a weighted score.

## Description
Each idea receives a weighted score.

## Issue #96: [Story] IDEA SCORING AND SELECTION SYSTEM: Scoring Dimensions

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the scoring dimensions capability, so that the idea scoring and selection system epic can be completed predictably.

## Description
Implement the Scoring Dimensions portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #96: [Story] IDEA SCORING AND SELECTION SYSTEM: Scoring Dimensions

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the scoring dimensions capability, so that the idea scoring and selection system epic can be completed predictably.

## Description
Implement the Scoring Dimensions portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #96: [Story] IDEA SCORING AND SELECTION SYSTEM: Scoring Dimensions

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the scoring dimensions capability, so that the idea scoring and selection system epic can be completed predictably.

## Description
Implement the Scoring Dimensions portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #97: [Story] IDEA SCORING AND SELECTION SYSTEM: Market Demand

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the market demand capability, so that measures potential user demand.

## Description
Measures potential user demand.

## Issue #97: [Story] IDEA SCORING AND SELECTION SYSTEM: Market Demand

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the market demand capability, so that measures potential user demand.

## Description
Measures potential user demand.

## Issue #97: [Story] IDEA SCORING AND SELECTION SYSTEM: Market Demand

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the market demand capability, so that measures potential user demand.

## Description
Measures potential user demand.

## Issue #98: [Story] IDEA SCORING AND SELECTION SYSTEM: Technical Feasibility

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the technical feasibility capability, so that measures difficulty relative to system capability.

## Description
Measures difficulty relative to system capability.

## Issue #98: [Story] IDEA SCORING AND SELECTION SYSTEM: Technical Feasibility

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the technical feasibility capability, so that measures difficulty relative to system capability.

## Description
Measures difficulty relative to system capability.

## Issue #98: [Story] IDEA SCORING AND SELECTION SYSTEM: Technical Feasibility

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the technical feasibility capability, so that measures difficulty relative to system capability.

## Description
Measures difficulty relative to system capability.

## Issue #99: [Story] IDEA SCORING AND SELECTION SYSTEM: Development Cost

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the development cost capability, so that estimates resource cost.

## Description
Estimates resource cost.

## Issue #99: [Story] IDEA SCORING AND SELECTION SYSTEM: Development Cost

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the development cost capability, so that estimates resource cost.

## Description
Estimates resource cost.

## Issue #99: [Story] IDEA SCORING AND SELECTION SYSTEM: Development Cost

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the development cost capability, so that estimates resource cost.

## Description
Estimates resource cost.

## Issue #100: [Story] IDEA SCORING AND SELECTION SYSTEM: Differentiation

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the differentiation capability, so that measures uniqueness compared to existing tools.

## Description
Measures uniqueness compared to existing tools.

## Issue #100: [Story] IDEA SCORING AND SELECTION SYSTEM: Differentiation

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the differentiation capability, so that measures uniqueness compared to existing tools.

## Description
Measures uniqueness compared to existing tools.

## Issue #100: [Story] IDEA SCORING AND SELECTION SYSTEM: Differentiation

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`

## User Story
As a product owner, I want the differentiation capability, so that measures uniqueness compared to existing tools.

## Description
Measures uniqueness compared to existing tools.

## Issue #132: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Dimensions

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the ethical risk dimensions capability, so that the ethics analysis agent epic can be completed predictably.

## Description
Implement the Ethical Risk Dimensions portion of ETHICS_ANALYSIS_AGENT.

## Issue #132: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Dimensions

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the ethical risk dimensions capability, so that the ethics analysis agent epic can be completed predictably.

## Description
Implement the Ethical Risk Dimensions portion of ETHICS_ANALYSIS_AGENT.

## Issue #132: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Dimensions

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the ethical risk dimensions capability, so that the ethics analysis agent epic can be completed predictably.

## Description
Implement the Ethical Risk Dimensions portion of ETHICS_ANALYSIS_AGENT.

## Issue #133: [Story] ETHICS ANALYSIS AGENT: Harm Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the harm potential capability, so that could the system cause harm if misused?.

## Description
Could the system cause harm if misused?

## Issue #133: [Story] ETHICS ANALYSIS AGENT: Harm Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the harm potential capability, so that could the system cause harm if misused?.

## Description
Could the system cause harm if misused?

## Issue #133: [Story] ETHICS ANALYSIS AGENT: Harm Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the harm potential capability, so that could the system cause harm if misused?.

## Description
Could the system cause harm if misused?

## Issue #134: [Story] ETHICS ANALYSIS AGENT: Manipulation Risk

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the manipulation risk capability, so that evaluates psychological manipulation.

## Description
Evaluates psychological manipulation.

## Issue #134: [Story] ETHICS ANALYSIS AGENT: Manipulation Risk

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the manipulation risk capability, so that evaluates psychological manipulation.

## Description
Evaluates psychological manipulation.

## Issue #134: [Story] ETHICS ANALYSIS AGENT: Manipulation Risk

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the manipulation risk capability, so that evaluates psychological manipulation.

## Description
Evaluates psychological manipulation.

## Issue #135: [Story] ETHICS ANALYSIS AGENT: Privacy Intrusion

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the privacy intrusion capability, so that ethical privacy concerns beyond legal compliance.

## Description
Ethical privacy concerns beyond legal compliance.

## Issue #135: [Story] ETHICS ANALYSIS AGENT: Privacy Intrusion

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the privacy intrusion capability, so that ethical privacy concerns beyond legal compliance.

## Description
Ethical privacy concerns beyond legal compliance.

## Issue #135: [Story] ETHICS ANALYSIS AGENT: Privacy Intrusion

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the privacy intrusion capability, so that ethical privacy concerns beyond legal compliance.

## Description
Ethical privacy concerns beyond legal compliance.

## Issue #136: [Story] ETHICS ANALYSIS AGENT: Social Impact

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the social impact capability, so that evaluates societal harm potential.

## Description
Evaluates societal harm potential.

## Issue #136: [Story] ETHICS ANALYSIS AGENT: Social Impact

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the social impact capability, so that evaluates societal harm potential.

## Description
Evaluates societal harm potential.

## Issue #136: [Story] ETHICS ANALYSIS AGENT: Social Impact

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the social impact capability, so that evaluates societal harm potential.

## Description
Evaluates societal harm potential.

## Issue #137: [Story] ETHICS ANALYSIS AGENT: Misuse Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the misuse potential capability, so that measures how easily a tool could be weaponized.

## Description
Measures how easily a tool could be weaponized.

## Issue #137: [Story] ETHICS ANALYSIS AGENT: Misuse Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the misuse potential capability, so that measures how easily a tool could be weaponized.

## Description
Measures how easily a tool could be weaponized.

## Issue #137: [Story] ETHICS ANALYSIS AGENT: Misuse Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the misuse potential capability, so that measures how easily a tool could be weaponized.

## Description
Measures how easily a tool could be weaponized.

## Issue #138: [Story] ETHICS ANALYSIS AGENT: Ethics Decision Threshold

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the ethics decision threshold capability, so that automatic rejection occurs if: Or if the project clearly enables:.

## Description
Automatic rejection occurs if:

## Issue #138: [Story] ETHICS ANALYSIS AGENT: Ethics Decision Threshold

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the ethics decision threshold capability, so that automatic rejection occurs if: Or if the project clearly enables:.

## Description
Automatic rejection occurs if:

## Issue #138: [Story] ETHICS ANALYSIS AGENT: Ethics Decision Threshold

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the ethics decision threshold capability, so that automatic rejection occurs if: Or if the project clearly enables:.

## Description
Automatic rejection occurs if:

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the agent categories capability, so that agents are grouped into functional domains.

## Description
Agents are grouped into functional domains.

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the agent categories capability, so that agents are grouped into functional domains.

## Description
Agents are grouped into functional domains.

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the agent categories capability, so that agents are grouped into functional domains.

## Description
Agents are grouped into functional domains.

## Issue #144: [Story] ETHICS ANALYSIS AGENT: Software Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the software development agents capability, so that each agent specializes in specific languages or frameworks.

## Description
Each agent specializes in specific languages or frameworks.

## Issue #144: [Story] ETHICS ANALYSIS AGENT: Software Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the software development agents capability, so that each agent specializes in specific languages or frameworks.

## Description
Each agent specializes in specific languages or frameworks.

## Issue #144: [Story] ETHICS ANALYSIS AGENT: Software Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the software development agents capability, so that each agent specializes in specific languages or frameworks.

## Description
Each agent specializes in specific languages or frameworks.

## Issue #145: [Story] ETHICS ANALYSIS AGENT: Hardware Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the hardware development agents capability, so that dragon Idea Engine will support electronics projects.

## Description
Dragon Idea Engine will support electronics projects.

## Issue #145: [Story] ETHICS ANALYSIS AGENT: Hardware Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the hardware development agents capability, so that dragon Idea Engine will support electronics projects.

## Description
Dragon Idea Engine will support electronics projects.

## Issue #145: [Story] ETHICS ANALYSIS AGENT: Hardware Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the hardware development agents capability, so that dragon Idea Engine will support electronics projects.

## Description
Dragon Idea Engine will support electronics projects.

## Issue #146: [Story] ETHICS ANALYSIS AGENT: Media Production Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the media production agents capability, so that some projects require media assets.

## Description
Some projects require media assets.

## Issue #146: [Story] ETHICS ANALYSIS AGENT: Media Production Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the media production agents capability, so that some projects require media assets.

## Description
Some projects require media assets.

## Issue #146: [Story] ETHICS ANALYSIS AGENT: Media Production Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the media production agents capability, so that some projects require media assets.

## Description
Some projects require media assets.

## Issue #147: [Story] ETHICS ANALYSIS AGENT: Research Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the research agents capability, so that research agents analyze external data.

## Description
Research agents analyze external data.

## Issue #147: [Story] ETHICS ANALYSIS AGENT: Research Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the research agents capability, so that research agents analyze external data.

## Description
Research agents analyze external data.

## Issue #147: [Story] ETHICS ANALYSIS AGENT: Research Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the research agents capability, so that research agents analyze external data.

## Description
Research agents analyze external data.

## Issue #149: [Story] ETHICS ANALYSIS AGENT: Operations Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the operations agents capability, so that operations agents manage system lifecycle.

## Description
Operations agents manage system lifecycle.

## Issue #149: [Story] ETHICS ANALYSIS AGENT: Operations Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the operations agents capability, so that operations agents manage system lifecycle.

## Description
Operations agents manage system lifecycle.

## Issue #149: [Story] ETHICS ANALYSIS AGENT: Operations Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the operations agents capability, so that operations agents manage system lifecycle.

## Description
Operations agents manage system lifecycle.

## Issue #150: [Story] ETHICS ANALYSIS AGENT: MULTI MODEL AGENT FRAMEWORK

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story
As a product owner, I want the multi model agent framework capability, so that the ethics analysis agent epic can be completed predictably.

## Description
Implement the MULTI_MODEL_AGENT_FRAMEWORK portion of ETHICS_ANALYSIS_AGENT.

## Issue #715: [Story] REUSABLE COMPONENT LIBRARY: Shared Infrastructure Deployment

All shared services should be deployed using containers.
Recommended approach:

## Issue #801: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Memory Categories

Knowledge is stored in structured domains.
Each category feeds different agents during decision-making.

## Issue #803: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Project Outcome Tracking

Each completed project receives a structured evaluation.
Example record:

## Issue #805: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Component Reliability Scoring

Reusable components are continuously evaluated.
Metrics tracked:

## Issue #806: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Bug and Failure Pattern Tracking

When bugs occur, the system records patterns.
Example entry:

## Issue #807: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Market Signal Tracking

The system monitors market interest signals.
Sources may include:

## Issue #808: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Agent Performance Tracking

Each agent's effectiveness is monitored.
Metrics tracked:

## Issue #809: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Knowledge Retrieval

Agents retrieve knowledge during planning or development tasks.
Example retrieval flow:

## Issue #811: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Knowledge Retention Policy

To prevent unbounded growth, the system periodically archives stale knowledge.
Rules may include:

## Issue #813: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Raspberry Pi Deployment Considerations

The knowledge system must operate within Pi resource limits.
Strategies include:

## Issue #901: [Story] MANDATORY OPEN SOURCE POLICY: License Selection

Default license for all generated projects:
- **MIT License** â€“ permissive, widely used
- Optional alternatives (configurable per project):

## Issue #902: [Story] MANDATORY OPEN SOURCE POLICY: Agent Enforcement

All development agents must include a **license header** in every generated file.
Example MIT header:

## Issue #903: [Story] MANDATORY OPEN SOURCE POLICY: Repository Creation Rules

When generating a new repository:
This ensures transparency from the first commit.

## Issue #904: [Story] MANDATORY OPEN SOURCE POLICY: Component Reuse

All reusable components must also comply with open source licensing:

## Issue #906: [Story] MANDATORY OPEN SOURCE POLICY: Ethical and Legal Alignment

Open source licensing also supports ethical and legal policies:
This complements the **Ethics Agent** and **Risk Analysis Agent**.

## Issue #907: [Story] MANDATORY OPEN SOURCE POLICY: Enforcement in Pi Edition

On Raspberry Pi deployments:
Agents running in offline mode must still include license headers and metadata to comply once connected.

## Issue #1001: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Status

This section intentionally avoids defining licensing or ownership policies until the long-term governance model is finalized.
The current focus is on **technical architecture and ecosystem collaboration**, not legal structure.

## Issue #1002: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Platform Distribution Vision

Dragon Idea Engine is designed to eventually operate as a **distributed innovation platform** where multiple users and systems can participate.
Possible participation models may include:

## Issue #1004: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Idea Contribution Concept

The system may allow external users to submit ideas to the platform.
Ideas could be:

## Issue #1005: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Hardware Distribution Possibilities

Dragon Idea Engine may eventually be distributed as **preconfigured hardware systems**.
Potential formats include:

## Issue #1006: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Ecosystem Goals

The long-term platform aims to encourage:
However, governance, licensing, and commercial structures will be addressed in a future codex revision.

## Issue #1102: [Story] AGENT ORCHESTRATION ENGINE: Task Router

The Task Router determines which agent should perform a given task.
Inputs to the router include:

## Issue #1104: [Story] AGENT ORCHESTRATION ENGINE: Task Queue System

Tasks are distributed using a message queue.
Recommended implementation:

## Issue #1106: [Story] AGENT ORCHESTRATION ENGINE: Execution Monitor

The Execution Monitor tracks task progress and system health.
Metrics tracked include:

## Issue #1107: [Story] AGENT ORCHESTRATION ENGINE: Task Lifecycle

Each task progresses through defined states.
The orchestration engine transitions tasks between these states automatically.

## Issue #1108: [Story] AGENT ORCHESTRATION ENGINE: Failure Handling

If an agent fails to complete a task, the system initiates recovery procedures.
Possible actions include:

## Issue #1109: [Story] AGENT ORCHESTRATION ENGINE: Parallel Task Execution

Some workflows support parallel execution.
Example:

## Issue #1110: [Story] AGENT ORCHESTRATION ENGINE: Agent Collaboration

Agents may exchange outputs directly through shared artifacts.
Examples:

## Issue #1111: [Story] AGENT ORCHESTRATION ENGINE: Resource-Aware Scheduling

Because the system may run on Raspberry Pi hardware, orchestration must consider resource constraints.
Scheduling factors include:

## Issue #1114: [Story] AGENT ORCHESTRATION ENGINE: Human Intervention

Certain tasks may require human oversight.
Triggers include:

## Issue #1114: [Story] AGENT ORCHESTRATION ENGINE: Human Intervention

Certain tasks may require human oversight.
Triggers include:

## Issue #1114: [Story] AGENT ORCHESTRATION ENGINE: Human Intervention

Certain tasks may require human oversight.
Triggers include:

## Issue #1115: [Story] AGENT ORCHESTRATION ENGINE: Raspberry Pi Deployment Considerations

When running on Pi hardware:
This ensures stable operation even on modest hardware.

## Issue #1115: [Story] AGENT ORCHESTRATION ENGINE: Raspberry Pi Deployment Considerations

When running on Pi hardware:
This ensures stable operation even on modest hardware.

## Issue #1115: [Story] AGENT ORCHESTRATION ENGINE: Raspberry Pi Deployment Considerations

When running on Pi hardware:
This ensures stable operation even on modest hardware.

## Issue #1401: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Project Types

Unity agents may generate multiple types of projects.
Examples include:

## Issue #1401: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Project Types

Unity agents may generate multiple types of projects.
Examples include:

## Issue #1401: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Project Types

Unity agents may generate multiple types of projects.
Examples include:

## Issue #1402: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Agent Categories

Unity development agents are divided into specialized domains.
Each agent type performs specific tasks.

## Issue #1402: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Agent Categories

Unity development agents are divided into specialized domains.
Each agent type performs specific tasks.

## Issue #1402: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Agent Categories

Unity development agents are divided into specialized domains.
Each agent type performs specific tasks.

## Issue #1404: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity UI Design Agents

These agents specialize in 2D and UI development within Unity.
Responsibilities:

## Issue #1404: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity UI Design Agents

These agents specialize in 2D and UI development within Unity.
Responsibilities:

## Issue #1404: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity UI Design Agents

These agents specialize in 2D and UI development within Unity.
Responsibilities:

## Issue #1405: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity 3D Environment Agents

These agents create 3D worlds and scene layouts.
Responsibilities:

## Issue #1405: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity 3D Environment Agents

These agents create 3D worlds and scene layouts.
Responsibilities:

## Issue #1405: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity 3D Environment Agents

These agents create 3D worlds and scene layouts.
Responsibilities:

## Issue #1406: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Programming Agents

Unity programming agents generate gameplay logic and application behavior.
Primary language:

## Issue #1406: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Programming Agents

Unity programming agents generate gameplay logic and application behavior.
Primary language:

## Issue #1406: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Programming Agents

Unity programming agents generate gameplay logic and application behavior.
Primary language:

## Issue #1407: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Asset Generation Agents

These agents generate or integrate media assets used by Unity.
Assets may include:

## Issue #1407: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Asset Generation Agents

These agents generate or integrate media assets used by Unity.
Assets may include:

## Issue #1407: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Asset Generation Agents

These agents generate or integrate media assets used by Unity.
Assets may include:

## Issue #1408: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Shader Agents

For advanced visual projects, shader agents generate graphics effects.
Responsibilities:

## Issue #1408: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Shader Agents

For advanced visual projects, shader agents generate graphics effects.
Responsibilities:

## Issue #1408: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Shader Agents

For advanced visual projects, shader agents generate graphics effects.
Responsibilities:

## Issue #1409: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Optimization Agents

These agents improve performance.
Optimization targets:

## Issue #1409: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Optimization Agents

These agents improve performance.
Optimization targets:

## Issue #1409: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Optimization Agents

These agents improve performance.
Optimization targets:

## Issue #1412: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Build Targets

Unity agents must support multiple deployment targets.
Examples include:

## Issue #1412: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Build Targets

Unity agents must support multiple deployment targets.
Examples include:

## Issue #1412: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Build Targets

Unity agents must support multiple deployment targets.
Examples include:

## Issue #1413: [Story] UNITY DEVELOPMENT AGENT SUITE: Raspberry Pi Considerations

Although Unity itself typically runs on stronger hardware, Pi clusters can still support Unity pipelines.
Pi nodes may perform:

## Issue #1413: [Story] UNITY DEVELOPMENT AGENT SUITE: Raspberry Pi Considerations

Although Unity itself typically runs on stronger hardware, Pi clusters can still support Unity pipelines.
Pi nodes may perform:

## Issue #1413: [Story] UNITY DEVELOPMENT AGENT SUITE: Raspberry Pi Considerations

Although Unity itself typically runs on stronger hardware, Pi clusters can still support Unity pipelines.
Pi nodes may perform:

## Issue #1601: [Story] KNOWLEDGE AND LEARNING SYSTEM: Core Knowledge Domains

The knowledge system organizes information into structured domains.

## Issue #1601: [Story] KNOWLEDGE AND LEARNING SYSTEM: Core Knowledge Domains

The knowledge system organizes information into structured domains.

## Issue #1601: [Story] KNOWLEDGE AND LEARNING SYSTEM: Core Knowledge Domains

The knowledge system organizes information into structured domains.

## Issue #1603: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Indexing

To allow fast learning queries, knowledge entries are indexed.
Example indexes:

## Issue #1603: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Indexing

To allow fast learning queries, knowledge entries are indexed.
Example indexes:

## Issue #1603: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Indexing

To allow fast learning queries, knowledge entries are indexed.
Example indexes:

## Issue #1604: [Story] KNOWLEDGE AND LEARNING SYSTEM: Learning Agents

Dedicated agents analyze stored knowledge.
Agent categories include:

## Issue #1604: [Story] KNOWLEDGE AND LEARNING SYSTEM: Learning Agents

Dedicated agents analyze stored knowledge.
Agent categories include:

## Issue #1604: [Story] KNOWLEDGE AND LEARNING SYSTEM: Learning Agents

Dedicated agents analyze stored knowledge.
Agent categories include:

## Issue #1605: [Story] KNOWLEDGE AND LEARNING SYSTEM: Pattern Detection

Pattern agents search for trends in ideas and projects.
Examples:

## Issue #1605: [Story] KNOWLEDGE AND LEARNING SYSTEM: Pattern Detection

Pattern agents search for trends in ideas and projects.
Examples:

## Issue #1605: [Story] KNOWLEDGE AND LEARNING SYSTEM: Pattern Detection

Pattern agents search for trends in ideas and projects.
Examples:

## Issue #1607: [Story] KNOWLEDGE AND LEARNING SYSTEM: Technology Ranking

The system maintains rankings for technologies.
Example metrics:

## Issue #1607: [Story] KNOWLEDGE AND LEARNING SYSTEM: Technology Ranking

The system maintains rankings for technologies.
Example metrics:

## Issue #1607: [Story] KNOWLEDGE AND LEARNING SYSTEM: Technology Ranking

The system maintains rankings for technologies.
Example metrics:

## Issue #1609: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Feedback Into Agents

Agents receive knowledge feedback.
Example improvements:

## Issue #1609: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Feedback Into Agents

Agents receive knowledge feedback.
Example improvements:

## Issue #1609: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Feedback Into Agents

Agents receive knowledge feedback.
Example improvements:

## Issue #1610: [Story] KNOWLEDGE AND LEARNING SYSTEM: Raspberry Pi Deployment Considerations

Because the system may run on Raspberry Pi clusters, the knowledge system must remain lightweight.
Strategies include:

## Issue #1610: [Story] KNOWLEDGE AND LEARNING SYSTEM: Raspberry Pi Deployment Considerations

Because the system may run on Raspberry Pi clusters, the knowledge system must remain lightweight.
Strategies include:

## Issue #1610: [Story] KNOWLEDGE AND LEARNING SYSTEM: Raspberry Pi Deployment Considerations

Because the system may run on Raspberry Pi clusters, the knowledge system must remain lightweight.
Strategies include:

## Issue #345: [Recovery] Issue #147: [Story] ETHICS ANALYSIS AGENT: Research Agents

Recovery story for quarantined issue #147.
Context:
- source issue: #147
- blocked stage: test
- quarantine reason: Quarantined after 3 repeated failed test executions. Latest failure: test / test-147-1774274285058.
- changed paths: docs/BACKLOG_EXECUTION.md

## Issue #347: [Recovery] Issue #149: [Story] ETHICS ANALYSIS AGENT: Operations Agents

Recovery story for quarantined issue #149.
Context:
- source issue: #149
- blocked stage: test
- quarantine reason: Quarantined after 3 repeated failed test executions. Latest failure: test / test-149-1774274511130.
- changed paths: docs/BACKLOG_EXECUTION.md

## Issue #29: [Story] Dragon Idea Engine Master Codex: Agent Types

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the agent types capability, so that the dragon idea engine master codex epic can be completed predictably.
## Description
Implement the Agent Types portion of Dragon Idea Engine — Master Codex.

## Issue #33: [Story] Dragon Idea Engine Master Codex: Issue Generator Agent

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the issue generator agent capability, so that transforms architecture into issues.
## Description
Transforms architecture into issues.

## Issue #37: [Story] Dragon Idea Engine Master Codex: Refactor Agent

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the refactor agent capability, so that improves maintainability.
## Description
Improves maintainability.

## Issue #38: [Story] Dragon Idea Engine Master Codex: Documentation Agent

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the documentation agent capability, so that maintains documentation automatically.
## Description
Maintains documentation automatically.

## Issue #44: [Story] Dragon Idea Engine Master Codex: UI Dashboard

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the ui dashboard capability, so that react UI runs on the Raspberry Pi server.
## Description
React UI runs on the Raspberry Pi server.

## Issue #45: [Story] Dragon Idea Engine Master Codex: Ideas

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the ideas capability, so that the dragon idea engine master codex epic can be completed predictably.
## Description
Implement the Ideas portion of Dragon Idea Engine — Master Codex.

## Issue #46: [Story] Dragon Idea Engine Master Codex: Projects

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the projects capability, so that the dragon idea engine master codex epic can be completed predictably.
## Description
Implement the Projects portion of Dragon Idea Engine — Master Codex.

## Issue #47: [Story] Dragon Idea Engine Master Codex: Jobs

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the jobs capability, so that the dragon idea engine master codex epic can be completed predictably.
## Description
Implement the Jobs portion of Dragon Idea Engine — Master Codex.

## Issue #48: [Story] Dragon Idea Engine Master Codex: Agents

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the agents capability, so that the dragon idea engine master codex epic can be completed predictably.
## Description
Implement the Agents portion of Dragon Idea Engine — Master Codex.

## Issue #51: [Story] Dragon Idea Engine Master Codex: System Safety

Parent epic: #1 [Epic] Dragon Idea Engine Master Codex
Source section: `codex/sections/01-dragon-idea-engine-master-codex.md`
## User Story
As a platform architect, I want the system safety capability, so that agents must implement safeguards.
## Description
Agents must implement safeguards.

## Issue #55: [Story] Dragon Idea Engine Master Codex Addendum: Job Fields

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
## User Story
As a platform architect, I want the job fields capability, so that the dragon idea engine master codex addendum epic can be completed predictably.
## Description
Implement the Job Fields portion of Dragon Idea Engine — Master Codex Addendum.

## Issue #73: [Story] Dragon Idea Engine Master Codex Addendum: Codex Governance

Parent epic: #2 [Epic] Dragon Idea Engine Master Codex Addendum
Source section: `codex/sections/02-dragon-idea-engine-master-codex-addendum.md`
## User Story
As a platform architect, I want the codex governance capability, so that all changes to the system must follow: The **Master Codex is the authoritative specification** for Dragon Idea Engine.
## Description
All changes to the system must follow:

## Issue #91: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Sources

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea sources capability, so that ideas may originate from: All ideas are normalized into a single structure.
## Description
Ideas may originate from:

## Issue #92: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Data Structure

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea data structure capability, so that example idea record: Stored in the `ideas` table.
## Description
Example idea record:

## Issue #94: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Classification

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea classification capability, so that the **Idea Classification Agent** extracts metadata.
## Description
The **Idea Classification Agent** extracts metadata.

## Issue #95: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scoring Criteria

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea scoring criteria capability, so that each idea receives a weighted score.
## Description
Each idea receives a weighted score.

## Issue #96: [Story] IDEA SCORING AND SELECTION SYSTEM: Scoring Dimensions

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the scoring dimensions capability, so that the idea scoring and selection system epic can be completed predictably.
## Description
Implement the Scoring Dimensions portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #97: [Story] IDEA SCORING AND SELECTION SYSTEM: Market Demand

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the market demand capability, so that measures potential user demand.
## Description
Measures potential user demand.

## Issue #98: [Story] IDEA SCORING AND SELECTION SYSTEM: Technical Feasibility

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the technical feasibility capability, so that measures difficulty relative to system capability.
## Description
Measures difficulty relative to system capability.

## Issue #99: [Story] IDEA SCORING AND SELECTION SYSTEM: Development Cost

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the development cost capability, so that estimates resource cost.
## Description
Estimates resource cost.

## Issue #100: [Story] IDEA SCORING AND SELECTION SYSTEM: Differentiation

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the differentiation capability, so that measures uniqueness compared to existing tools.
## Description
Measures uniqueness compared to existing tools.

## Issue #101: [Story] IDEA SCORING AND SELECTION SYSTEM: Monetization Potential

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the monetization potential capability, so that optional but useful for commercial use.
## Description
Optional but useful for commercial use.

## Issue #102: [Story] IDEA SCORING AND SELECTION SYSTEM: Strategic Value

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the strategic value capability, so that measures alignment with Dragon Idea Engine goals.
## Description
Measures alignment with Dragon Idea Engine goals.

## Issue #103: [Story] IDEA SCORING AND SELECTION SYSTEM: Example Scoring

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the example scoring capability, so that idea: Example scoring:.
## Description
Idea:

## Issue #104: [Story] IDEA SCORING AND SELECTION SYSTEM: Decision Thresholds

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the decision thresholds capability, so that the Decision Engine determines the outcome.
## Description
The Decision Engine determines the outcome.

## Issue #106: [Story] IDEA SCORING AND SELECTION SYSTEM: Deferred Ideas

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the deferred ideas capability, so that deferred ideas remain in the queue.
## Description
Deferred ideas remain in the queue.

## Issue #107: [Story] IDEA SCORING AND SELECTION SYSTEM: Archived Ideas

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the archived ideas capability, so that archived ideas are stored but inactive.
## Description
Archived ideas are stored but inactive.

## Issue #108: [Story] IDEA SCORING AND SELECTION SYSTEM: Rejected Ideas

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the rejected ideas capability, so that rejected ideas are ignored by the system but retained for audit history.
## Description
Rejected ideas are ignored by the system but retained for audit history.

## Issue #110: [Story] IDEA SCORING AND SELECTION SYSTEM: Anti-Explosion Limits

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the anti-explosion limits capability, so that to prevent runaway project creation the system enforces limits.
## Description
To prevent runaway project creation the system enforces limits.

## Issue #111: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Dashboard (UI)

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea dashboard (ui) capability, so that the Dragon UI should display:.
## Description
The Dragon UI should display:

## Issue #112: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Queue

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea queue capability, so that the idea scoring and selection system epic can be completed predictably.
## Description
Implement the Idea Queue portion of IDEA_SCORING_AND_SELECTION_SYSTEM.

## Issue #113: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Scores

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea scores capability, so that display:.
## Description
Display:

## Issue #114: [Story] IDEA SCORING AND SELECTION SYSTEM: Idea Voting

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the idea voting capability, so that users may vote: Votes influence scoring.
## Description
Users may vote:

## Issue #116: [Story] IDEA SCORING AND SELECTION SYSTEM: Long-Term Goal

Parent epic: #4 [Epic] IDEA SCORING AND SELECTION SYSTEM
Source section: `codex/sections/04-idea-scoring-and-selection-system.md`
## User Story
As a product owner, I want the long-term goal capability, so that the Idea Scoring System transforms Dragon Idea Engine into a **structured innovation pipeline** rather than a random project generator.
## Description
The Idea Scoring System transforms Dragon Idea Engine into a **structured innovation pipeline** rather than a random project generator.

## Issue #131: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Model

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the ethical risk model capability, so that ethical evaluation produces an **Ethics Risk Score**.
## Description
Ethical evaluation produces an **Ethics Risk Score**.

## Issue #132: [Story] ETHICS ANALYSIS AGENT: Ethical Risk Dimensions

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the ethical risk dimensions capability, so that the ethics analysis agent epic can be completed predictably.
## Description
Implement the Ethical Risk Dimensions portion of ETHICS_ANALYSIS_AGENT.

## Issue #133: [Story] ETHICS ANALYSIS AGENT: Harm Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the harm potential capability, so that could the system cause harm if misused?.
## Description
Could the system cause harm if misused?

## Issue #134: [Story] ETHICS ANALYSIS AGENT: Manipulation Risk

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the manipulation risk capability, so that evaluates psychological manipulation.
## Description
Evaluates psychological manipulation.

## Issue #135: [Story] ETHICS ANALYSIS AGENT: Privacy Intrusion

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the privacy intrusion capability, so that ethical privacy concerns beyond legal compliance.
## Description
Ethical privacy concerns beyond legal compliance.

## Issue #136: [Story] ETHICS ANALYSIS AGENT: Social Impact

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the social impact capability, so that evaluates societal harm potential.
## Description
Evaluates societal harm potential.

## Issue #137: [Story] ETHICS ANALYSIS AGENT: Misuse Potential

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the misuse potential capability, so that measures how easily a tool could be weaponized.
## Description
Measures how easily a tool could be weaponized.

## Issue #138: [Story] ETHICS ANALYSIS AGENT: Ethics Decision Threshold

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the ethics decision threshold capability, so that automatic rejection occurs if: Or if the project clearly enables:.
## Description
Automatic rejection occurs if:

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the agent categories capability, so that agents are grouped into functional domains.
## Description
Agents are grouped into functional domains.

## Issue #144: [Story] ETHICS ANALYSIS AGENT: Software Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the software development agents capability, so that each agent specializes in specific languages or frameworks.
## Description
Each agent specializes in specific languages or frameworks.

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the agent categories capability, so that agents are grouped into functional domains.
## Description
Agents are grouped into functional domains.

## Issue #145: [Story] ETHICS ANALYSIS AGENT: Hardware Development Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the hardware development agents capability, so that dragon Idea Engine will support electronics projects.
## Description
Dragon Idea Engine will support electronics projects.

## Issue #146: [Story] ETHICS ANALYSIS AGENT: Media Production Agents

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the media production agents capability, so that some projects require media assets.
## Description
Some projects require media assets.

## Issue #151: [Story] ETHICS ANALYSIS AGENT: Model Routing

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the model routing capability, so that example routing strategy:.
## Description
Example routing strategy:

## Issue #155: [Story] ETHICS ANALYSIS AGENT: Agent Collaboration

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the agent collaboration capability, so that agents can collaborate.
## Description
Agents can collaborate.

## Issue #156: [Story] ETHICS ANALYSIS AGENT: Local Raspberry Pi Deployment Compatibility

Parent epic: #6 [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`
## User Story
As a product owner, I want the local raspberry pi deployment compatibility capability, so that because Dragon Idea Engine will run on a **Raspberry Pi base image**, agents must support: The system must gracefully degrade if cloud models are unavailable.
## Description
Because Dragon Idea Engine will run on a **Raspberry Pi base image**, agents must support:

## Issue #157: [Story] REUSABLE COMPONENT LIBRARY: Component Categories

Parent epic: #7 [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`
## User Story
As a platform operator, I want the component categories capability, so that reusable components are organized into standardized domains.
## Description
Reusable components are organized into standardized domains.

## Issue #166: [Story] REUSABLE COMPONENT LIBRARY: Hardware Component Library

Parent epic: #7 [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`
## User Story
As a platform operator, I want the hardware component library capability, so that because Dragon Idea Engine may generate electronics projects, reusable hardware modules should exist.
## Description
Because Dragon Idea Engine may generate electronics projects, reusable hardware modules should exist.

## Issue #167: [Story] REUSABLE COMPONENT LIBRARY: Agent Usage Rules

Parent epic: #7 [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`
## User Story
As a platform operator, I want the agent usage rules capability, so that when generating systems, agents must follow this priority order: Agents should **never duplicate infrastructure logic** unnecessarily.
## Description
When generating systems, agents must follow this priority order:

## Issue #168: [Story] REUSABLE COMPONENT LIBRARY: Component Versioning

Parent epic: #7 [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`
## User Story
As a platform operator, I want the component versioning capability, so that all reusable components must follow semantic versioning.
## Description
All reusable components must follow semantic versioning.

## Issue #169: [Story] REUSABLE COMPONENT LIBRARY: Component Dependency Graph

Parent epic: #7 [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`
## User Story
As a platform operator, I want the component dependency graph capability, so that the component registry maintains a dependency graph.
## Description
The component registry maintains a dependency graph.

## Issue #171: [Story] REUSABLE COMPONENT LIBRARY: Shared Infrastructure Deployment

Parent epic: #7 [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`
## User Story
As a platform operator, I want the shared infrastructure deployment capability, so that all shared services should be deployed using containers.
## Description
All shared services should be deployed using containers.

## Issue #172: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Memory Categories

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the memory categories capability, so that knowledge is stored in structured domains.
## Description
Knowledge is stored in structured domains.

## Issue #174: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Project Outcome Tracking

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the project outcome tracking capability, so that each completed project receives a structured evaluation.
## Description
Each completed project receives a structured evaluation.

## Issue #176: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Component Reliability Scoring

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the component reliability scoring capability, so that reusable components are continuously evaluated.
## Description
Reusable components are continuously evaluated.

## Issue #177: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Bug and Failure Pattern Tracking

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the bug and failure pattern tracking capability, so that when bugs occur, the system records patterns.
## Description
When bugs occur, the system records patterns.

## Issue #178: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Market Signal Tracking

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the market signal tracking capability, so that the system monitors market interest signals.
## Description
The system monitors market interest signals.

## Issue #179: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Agent Performance Tracking

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the agent performance tracking capability, so that each agent's effectiveness is monitored.
## Description
Each agent's effectiveness is monitored.

## Issue #180: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Knowledge Retrieval

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the knowledge retrieval capability, so that agents retrieve knowledge during planning or development tasks.
## Description
Agents retrieve knowledge during planning or development tasks.

## Issue #182: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Knowledge Retention Policy

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the knowledge retention policy capability, so that to prevent unbounded growth, the system periodically archives stale knowledge.
## Description
To prevent unbounded growth, the system periodically archives stale knowledge.

## Issue #184: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Raspberry Pi Deployment Considerations

Parent epic: #8 [Epic] AGENT MEMORY AND KNOWLEDGE SYSTEM
Source section: `codex/sections/08-agent-memory-and-knowledge-system.md`
## User Story
As a platform architect, I want the raspberry pi deployment considerations capability, so that the knowledge system must operate within Pi resource limits.
## Description
The knowledge system must operate within Pi resource limits.

## Issue #185: [Story] MANDATORY OPEN SOURCE POLICY: License Selection

Parent epic: #9 [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`
## User Story
As a product owner, I want the license selection capability, so that default license for all generated projects: - **MIT License** – permissive, widely used - Optional alternatives (configurable per project):.
## Description
Default license for all generated projects:

## Issue #123: [Story] Dragon Idea Engine Master Codex: UI Dashboard

React UI runs on the Raspberry Pi server.
Features:

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Quarantined after 3 repeated failed test executions. Latest failure: test / test-142-1774377950279.

## Issue #166: [Story] REUSABLE COMPONENT LIBRARY: Hardware Component Library

Quarantined after 3 repeated failed test executions. Latest failure: test / test-166-1774379218066.

## Issue #168: [Story] REUSABLE COMPONENT LIBRARY: Component Versioning

Quarantined after 3 repeated failed test executions. Latest failure: test / test-168-1774379382700.

## Issue #178: [Story] AGENT MEMORY AND KNOWLEDGE SYSTEM: Market Signal Tracking

Quarantined after 3 repeated failed test executions. Latest failure: test / test-178-1774380274889.

## Issue #186: [Story] MANDATORY OPEN SOURCE POLICY: Agent Enforcement

Parent epic: #9 [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`
## User Story
As a product owner, I want the agent enforcement capability, so that all development agents must include a **license header** in every generated file.
## Description
All development agents must include a **license header** in every generated file.

## Issue #188: [Story] MANDATORY OPEN SOURCE POLICY: Component Reuse

Parent epic: #9 [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`
## User Story
As a product owner, I want the component reuse capability, so that all reusable components must also comply with open source licensing:.
## Description
All reusable components must also comply with open source licensing:

## Issue #190: [Story] MANDATORY OPEN SOURCE POLICY: Ethical and Legal Alignment

Parent epic: #9 [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`
## User Story
As a product owner, I want the ethical and legal alignment capability, so that open source licensing also supports ethical and legal policies: This complements the **Ethics Agent** and **Risk Analysis Agent**.
## Description
Open source licensing also supports ethical and legal policies:

## Issue #191: [Story] MANDATORY OPEN SOURCE POLICY: Enforcement in Pi Edition

Parent epic: #9 [Epic] MANDATORY OPEN SOURCE POLICY
Source section: `codex/sections/09-mandatory-open-source-policy.md`
## User Story
As a product owner, I want the enforcement in pi edition capability, so that on Raspberry Pi deployments: Agents running in offline mode must still include license headers and metadata to comply once connected.
## Description
On Raspberry Pi deployments:

## Issue #192: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Status

Parent epic: #10 [Epic] PLATFORM DISTRIBUTION AND COLLABORATION MODEL
Source section: `codex/sections/10-platform-distribution-and-collaboration-model.md`
## User Story
As a product owner, I want the status capability, so that this section intentionally avoids defining licensing or ownership policies until the long-term governance model is finalized.
## Description
This section intentionally avoids defining licensing or ownership policies until the long-term governance model is finalized.

## Issue #193: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Platform Distribution Vision

Parent epic: #10 [Epic] PLATFORM DISTRIBUTION AND COLLABORATION MODEL
Source section: `codex/sections/10-platform-distribution-and-collaboration-model.md`
## User Story
As a product owner, I want the platform distribution vision capability, so that dragon Idea Engine is designed to eventually operate as a **distributed innovation platform** where multiple users and systems can participate.
## Description
Dragon Idea Engine is designed to eventually operate as a **distributed innovation platform** where multiple users and systems can participate.

## Issue #195: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Idea Contribution Concept

Parent epic: #10 [Epic] PLATFORM DISTRIBUTION AND COLLABORATION MODEL
Source section: `codex/sections/10-platform-distribution-and-collaboration-model.md`
## User Story
As a product owner, I want the idea contribution concept capability, so that the system may allow external users to submit ideas to the platform.
## Description
The system may allow external users to submit ideas to the platform.

## Issue #196: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Hardware Distribution Possibilities

Parent epic: #10 [Epic] PLATFORM DISTRIBUTION AND COLLABORATION MODEL
Source section: `codex/sections/10-platform-distribution-and-collaboration-model.md`
## User Story
As a product owner, I want the hardware distribution possibilities capability, so that dragon Idea Engine may eventually be distributed as **preconfigured hardware systems**.
## Description
Dragon Idea Engine may eventually be distributed as **preconfigured hardware systems**.

## Issue #197: [Story] PLATFORM DISTRIBUTION AND COLLABORATION MODEL: Ecosystem Goals

Parent epic: #10 [Epic] PLATFORM DISTRIBUTION AND COLLABORATION MODEL
Source section: `codex/sections/10-platform-distribution-and-collaboration-model.md`
## User Story
As a product owner, I want the ecosystem goals capability, so that the long-term platform aims to encourage: However, governance, licensing, and commercial structures will be addressed in a future codex revision.
## Description
The long-term platform aims to encourage:

## Issue #201: [Story] AGENT ORCHESTRATION ENGINE: Task Queue System

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the task queue system capability, so that tasks are distributed using a message queue.
## Description
Tasks are distributed using a message queue.

## Issue #204: [Story] AGENT ORCHESTRATION ENGINE: Task Lifecycle

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the task lifecycle capability, so that each task progresses through defined states.
## Description
Each task progresses through defined states.

## Issue #205: [Story] AGENT ORCHESTRATION ENGINE: Failure Handling

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the failure handling capability, so that if an agent fails to complete a task, the system initiates recovery procedures.
## Description
If an agent fails to complete a task, the system initiates recovery procedures.

## Issue #206: [Story] AGENT ORCHESTRATION ENGINE: Parallel Task Execution

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the parallel task execution capability, so that some workflows support parallel execution.
## Description
Some workflows support parallel execution.

## Issue #207: [Story] AGENT ORCHESTRATION ENGINE: Agent Collaboration

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the agent collaboration capability, so that agents may exchange outputs directly through shared artifacts.
## Description
Agents may exchange outputs directly through shared artifacts.

## Issue #208: [Story] AGENT ORCHESTRATION ENGINE: Resource-Aware Scheduling

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the resource-aware scheduling capability, so that because the system may run on Raspberry Pi hardware, orchestration must consider resource constraints.
## Description
Because the system may run on Raspberry Pi hardware, orchestration must consider resource constraints.

## Issue #211: [Story] AGENT ORCHESTRATION ENGINE: Human Intervention

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the human intervention capability, so that certain tasks may require human oversight.
## Description
Certain tasks may require human oversight.

## Issue #212: [Story] AGENT ORCHESTRATION ENGINE: Raspberry Pi Deployment Considerations

Parent epic: #11 [Epic] AGENT ORCHESTRATION ENGINE
Source section: `codex/sections/11-agent-orchestration-engine.md`
## User Story
As a platform operator, I want the raspberry pi deployment considerations capability, so that when running on Pi hardware: This ensures stable operation even on modest hardware.
## Description
When running on Pi hardware:

## Issue #240: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Project Types

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity project types capability, so that unity agents may generate multiple types of projects.
## Description
Unity agents may generate multiple types of projects.

## Issue #241: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Agent Categories

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity agent categories capability, so that unity development agents are divided into specialized domains.
## Description
Unity development agents are divided into specialized domains.

## Issue #243: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity UI Design Agents

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity ui design agents capability, so that these agents specialize in 2D and UI development within Unity.
## Description
These agents specialize in 2D and UI development within Unity.

## Issue #244: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity 3D Environment Agents

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity 3d environment agents capability, so that these agents create 3D worlds and scene layouts.
## Description
These agents create 3D worlds and scene layouts.

## Issue #245: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Programming Agents

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity programming agents capability, so that unity programming agents generate gameplay logic and application behavior.
## Description
Unity programming agents generate gameplay logic and application behavior.

## Issue #246: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Asset Generation Agents

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity asset generation agents capability, so that these agents generate or integrate media assets used by Unity.
## Description
These agents generate or integrate media assets used by Unity.

## Issue #247: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Shader Agents

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity shader agents capability, so that for advanced visual projects, shader agents generate graphics effects.
## Description
For advanced visual projects, shader agents generate graphics effects.

## Issue #248: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Optimization Agents

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity optimization agents capability, so that these agents improve performance.
## Description
These agents improve performance.

## Issue #251: [Story] UNITY DEVELOPMENT AGENT SUITE: Unity Build Targets

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the unity build targets capability, so that unity agents must support multiple deployment targets.
## Description
Unity agents must support multiple deployment targets.

## Issue #252: [Story] UNITY DEVELOPMENT AGENT SUITE: Raspberry Pi Considerations

Parent epic: #14 [Epic] UNITY DEVELOPMENT AGENT SUITE
Source section: `codex/sections/14-unity-development-agent-suite.md`
## User Story
As a delivery team, I want the raspberry pi considerations capability, so that although Unity itself typically runs on stronger hardware, Pi clusters can still support Unity pipelines.
## Description
Although Unity itself typically runs on stronger hardware, Pi clusters can still support Unity pipelines.

## Issue #267: [Story] KNOWLEDGE AND LEARNING SYSTEM: Core Knowledge Domains

Parent epic: #16 [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`
## User Story
As a platform architect, I want the core knowledge domains capability, so that the knowledge system organizes information into structured domains.
## Description
The knowledge system organizes information into structured domains.

## Issue #269: [Story] KNOWLEDGE AND LEARNING SYSTEM: Knowledge Indexing

Parent epic: #16 [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`
## User Story
As a platform architect, I want the knowledge indexing capability, so that to allow fast learning queries, knowledge entries are indexed.
## Description
To allow fast learning queries, knowledge entries are indexed.

## Issue #270: [Story] KNOWLEDGE AND LEARNING SYSTEM: Learning Agents

Parent epic: #16 [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`
## User Story
As a platform architect, I want the learning agents capability, so that dedicated agents analyze stored knowledge.
## Description
Dedicated agents analyze stored knowledge.

## Issue #271: [Story] KNOWLEDGE AND LEARNING SYSTEM: Pattern Detection

Parent epic: #16 [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`
## User Story
As a platform architect, I want the pattern detection capability, so that pattern agents search for trends in ideas and projects.
## Description
Pattern agents search for trends in ideas and projects.

## Issue #273: [Story] KNOWLEDGE AND LEARNING SYSTEM: Technology Ranking

Parent epic: #16 [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`
## User Story
As a platform architect, I want the technology ranking capability, so that the system maintains rankings for technologies.
## Description
The system maintains rankings for technologies.

## Issue #276: [Story] KNOWLEDGE AND LEARNING SYSTEM: Raspberry Pi Deployment Considerations

Parent epic: #16 [Epic] KNOWLEDGE AND LEARNING SYSTEM
Source section: `codex/sections/16-knowledge-and-learning-system.md`
## User Story
As a platform architect, I want the raspberry pi deployment considerations capability, so that because the system may run on Raspberry Pi clusters, the knowledge system must remain lightweight.
## Description
Because the system may run on Raspberry Pi clusters, the knowledge system must remain lightweight.

## Issue #315: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Core Principles

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the core principles capability, so that agent evolution is guided by the following principles: All improvements must maintain system stability and compliance with the Security and Compliance Validation System.
## Description
Agent evolution is guided by the following principles:

## Issue #317: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Agent Performance Profiles

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the agent performance profiles capability, so that each agent has a performance profile stored in the knowledge system.
## Description
Each agent has a performance profile stored in the knowledge system.

## Issue #318: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Improvement Triggers

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the improvement triggers capability, so that agent improvement processes may be triggered by several conditions.
## Description
Agent improvement processes may be triggered by several conditions.

## Issue #319: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Improvement Strategies

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the improvement strategies capability, so that the system may apply several strategies to improve agents.
## Description
The system may apply several strategies to improve agents.

## Issue #320: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Experimental Agent Variants

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the experimental agent variants capability, so that when improving an agent, the system may create experimental variants.
## Description
When improving an agent, the system may create experimental variants.

## Issue #322: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Safe Deployment Process

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the safe deployment process capability, so that agent upgrades follow a controlled rollout process.
## Description
Agent upgrades follow a controlled rollout process.

## Issue #323: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Agent Retirement

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the agent retirement capability, so that agents that consistently perform poorly may be retired.
## Description
Agents that consistently perform poorly may be retired.

## Issue #324: [Story] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM: Cross-Agent Learning

Parent epic: #20 [Epic] AGENT SELF IMPROVEMENT AND EVOLUTION SYSTEM
Source section: `codex/sections/20-agent-self-improvement-and-evolution-system.md`
## User Story
As a platform operator, I want the cross-agent learning capability, so that agents may learn from the successes of other agents.
## Description
Agents may learn from the successes of other agents.

## Issue #327: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Human Roles

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the human roles capability, so that the system supports several types of human participants.
## Description
The system supports several types of human participants.

## Issue #328: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Human Interaction Interfaces

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the human interaction interfaces capability, so that humans interact with the system through several interfaces.
## Description
Humans interact with the system through several interfaces.

## Issue #330: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Feedback Collection

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the feedback collection capability, so that human users may provide feedback to improve the system.
## Description
Human users may provide feedback to improve the system.

## Issue #331: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Collaborative Development

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the collaborative development capability, so that humans and agents may collaborate on projects.
## Description
Humans and agents may collaborate on projects.

## Issue #332: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Safety Controls

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the safety controls capability, so that humans must be able to quickly stop unsafe behavior.
## Description
Humans must be able to quickly stop unsafe behavior.

## Issue #334: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Training and Guidance System

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the training and guidance system capability, so that the platform may assist human users with recommendations.
## Description
The platform may assist human users with recommendations.

## Issue #335: [Story] HUMAN COLLABORATION AND OVERRIDE SYSTEM: Raspberry Pi Deployment Considerations

Parent epic: #21 [Epic] HUMAN COLLABORATION AND OVERRIDE SYSTEM
Source section: `codex/sections/21-human-collaboration-and-override-system.md`
## User Story
As a product owner, I want the raspberry pi deployment considerations capability, so that for installations running on Raspberry Pi clusters, the collaboration interface may run on a central node accessible through a web interface.
## Description
For installations running on Raspberry Pi clusters, the collaboration interface may run on a central node accessible through a web interface.

## Issue #349: Redesign UI into dashboard-first product shell

## Goal
Replace the overloaded war-room homepage with a cleaner dashboard-first shell for Dragon Idea Engine.
## Scope
- Make Dashboard the primary landing page
- Reduce dense all-at-once operational data on the homepage
- Reorganize navigation around Dashboard, Submit Idea, Idea Queue, and Projects

## Issue #350: Add idea detail view with backlog and agile board

## Goal
Make each idea expandable into a deeper execution workspace with backlog and agile-style delivery visibility.
## Scope
- Add idea detail view for approved or active ideas
- Show summary, status, queue position, and MVP ETA
- Show backlog items/stories for the idea

## Issue #352: Add Idea Queue view with approval state, queue position, and MVP ETA

## Goal
Add a first-class Idea Queue view so submitted ideas can be tracked through review, approval, and execution order.
## Scope
- Show submitted ideas and approval status
- Show whether ideas are approved, rejected, under review, or queued
- Show queue position for approved ideas

## Issue #354: Expose host and service telemetry for dashboard system health

## Goal
Support the redesigned dashboard with real operational telemetry for the Pi-hosted engine.
## Scope
- Surface CPU, memory, and disk telemetry from the running host or container environment
- Surface health for backend and supporting services
- Make telemetry available to the UI in a compact dashboard-friendly shape

## Issue #353: Add guided idea submission wizard with stack and technology intake

## Goal
Create a dedicated idea submission wizard that captures product intent and technical preferences before an idea enters review.
## Scope
- Add a separate guided intake flow for new ideas
- Capture stack, languages, frameworks, deployment target, and constraints
- Separate must-have MVP scope from nice-to-have scope
- Deferred to the last Pi-MVP UI task because it needs another design pass before final implementation

## Issue #142: [Story] ETHICS ANALYSIS AGENT: Agent Categories

Quarantined after 4 repeated failed test executions. Latest failure: test / test-142-1774380884902.

## Issue #166: [Story] REUSABLE COMPONENT LIBRARY: Hardware Component Library

Quarantined after 4 repeated failed test executions. Latest failure: test / test-166-1774380913650.

## Issue #168: [Story] REUSABLE COMPONENT LIBRARY: Component Versioning

Quarantined after 4 repeated failed test executions. Latest failure: test / test-168-1774380934638.
