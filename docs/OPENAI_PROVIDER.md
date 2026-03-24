# OpenAI Provider Strategy

## Initial Provider

Dragon Idea Engine should be API-first for agent execution.

The initial production provider is:

- OpenAI Responses API

The initial implementation direction is:

- C# backend orchestration
- provider abstraction in backend services
- OpenAI as the first real agent model provider
- additional providers added later behind the same interface

## Why API-first

The API is a better fit than a CLI-first design for autonomous operation because it gives us:

- cleaner background execution
- better timeout and retry control
- structured request/response handling
- easier logging and tracing
- simpler scaling across multiple workers
- cleaner provider abstraction

## Current Interface

The backend now defines:

- `IAgentModelProvider`
- `AgentModelRequest`
- `AgentModelResponse`
- `AgentModelProviderDescriptor`
- `OpenAiResponsesProvider`

This keeps provider-specific logic in one place and lets role-based agents stay focused on behavior instead of transport.

## Recommended OpenAI Path

For unattended coding and agentic backend work, the current focus should use:

- OpenAI Responses API for agent execution
- role-specific prompt construction in the backend
- structured audit logging of prompts, outputs, and workflow transitions

Official references:

- OpenAI Responses API: https://platform.openai.com/docs/api-reference/responses
- OpenAI developer docs overview: https://developers.openai.com/

## Configuration Storage

Production agent/provider configuration should not live in `.env` files.

Required direction:

- store agent/provider configuration in the database
- encrypt provider credentials and sensitive agent settings at rest
- use environment variables only for bootstrap, local development, or one-time migration flows
- allow CLI flags to override database-backed values for the current process without mutating the stored configuration

Bootstrap environment variables:

- `OPENAI_API_KEY`
- `OPENAI_MODEL` with a backend default of `gpt-5`
- `OPENAI_RESPONSES_ENDPOINT` only when overriding the default endpoint

## Deferred

These are intentionally later:

- fallback local models
- Anthropic or other hosted providers
- Codex CLI as a local dev-only adapter
- provider routing by cost or capability
