# Architecture

## Flow

```
Idea -> Architecture -> Repo -> Issues -> Code -> Review -> Deploy -> Feedback
```

## Current Workspace

```
docs/
runner/
agents/
services/
ui/
sdk/
templates/
```

## Current Runtime

- `dragon-agent-runner` loads agent plugins from the workspace.
- agents expose a manifest and a `run(context)` handler.
- CLI mode runs one agent directly.
- service mode reads newline-delimited JSON jobs, validates them against the shared schema, and emits structured job results.

## Next Expected Layers

- queue-backed orchestration
- API and UI surfaces
- issue generation and execution pipelines
- persistent job storage and credentials management
