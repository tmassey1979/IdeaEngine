# Autonomy Roadmap

## Target

The near-term goal is not "zero oversight at any cost."

The real target is:

- exception-based supervision
- autonomous issue selection
- autonomous implementation
- autonomous validation
- autonomous redeploy after approved major changes
- human involvement only for approvals, failures, or policy exceptions

## Minimum Viable Unattended Loop

To keep building and improving itself with minimal monitoring, the system needs:

1. API-driven agent execution
2. persistent background worker hosting
3. branch/PR-based git delivery
4. approval policy for risky changes
5. notifications for quarantines, stalls, and deployment gates
6. redeploy orchestration after validated major changes

## Execution Stages

### 1. Intake

- receive idea
- classify it
- choose project
- detect required technologies
- detect whether physical tools are required
- convert approved ideas into epic/story structure

### 2. Build

- select next runnable story
- build a role-specific prompt
- send it through the OpenAI API provider
- apply bounded repository changes
- commit to an isolated branch

### 3. Validate

- run review stage
- run test stage
- quarantine repeated failures
- create or reuse recovery stories
- sync progress back to GitHub

### 4. Deliver

- open or update PR
- merge only when policy allows
- redeploy automatically after validated major changes
- verify deployment health

### 5. Learn

- record execution traces
- summarize what worked and failed
- create self-improvement backlog items
- prioritize fixes that increase future autonomy

## Redeploy Rules

Automatic redeploy should only happen when:

- the change is classified as deployment-safe
- validation has passed
- policy does not require manual approval
- health checks exist for the affected service

Examples of major changes that should trigger controlled redeploy logic:

- backend orchestration changes
- provider integration changes
- queue or workflow state changes
- UI changes in the deployed operator interface

## Safety Gates

Before full unattended operation, the platform should enforce:

- protected branches
- PR checks
- deployment health checks
- secrets isolation
- policy-based approval for risky areas
- operator notifications for stalled or quarantined work

## Current Build Priority

The highest-value path now is:

1. finish API-first provider integration
2. route agent roles through that provider
3. run the orchestrator as a persistent service
4. add notifications and deployment policy
5. add auto-redeploy for validated major changes
