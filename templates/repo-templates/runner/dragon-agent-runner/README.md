# dragon-agent-runner

Bootstrap template for the Dragon agent runner process.

## Responsibilities

- load agent plugins
- run CLI commands
- connect to RabbitMQ
- execute queued jobs
- return results

## CLI

```bash
dragon-agent-runner developer --repo crm --issue 42
```

## Service Mode

```bash
dragon-agent-runner --service
```

## Source Context

Parent epic: #3 [Epic] Dragon Idea Engine Infrastructure Architecture
Source section: `codex/sections/03-dragon-idea-engine-infrastructure-architecture.md`
## User Story
As a platform operator, I want the phase 2 raspberry pi cluster capability, so that phase 2 introduces **horizontal scaling** using multiple Pi nodes.
## Description
Phase 2 introduces **horizontal scaling** using multiple Pi nodes.
Cluster model:
## Acceptance Criteria
- [ ] The Phase 2 Raspberry Pi Cluster behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: RabbitMQ.
