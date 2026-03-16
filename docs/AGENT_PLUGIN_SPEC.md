# Agent Plugin Spec

Each agent plugin exports:

```js
module.exports = createAgent({
  name: "architect",
  description: "Designs system architecture",
  version: "0.1.0",
  execute: async (context) => ({ ... })
});
```

## Required fields

- `name`: unique command and registry identifier
- `description`: short human-readable summary
- `version`: agent build/version string
- `execute(context)`: async handler returning a serializable agent result

## Runner contract

The runner provides:

- `mode`: `cli` or `service`
- `args`: parsed positional arguments
- `flags`: parsed CLI flags
- `job`: validated shared job payload in service mode
- `payload`: normalized job payload
- `workspace`: isolated workspace utilities and a workspace path
- `git`: repo helpers for clone/branch/commit/push/pr payloads
- `credentials`: project-first credential resolution helper
- `jobs`: queue publishing helper
- `logger`: structured logging helper

The result returned by `execute` should follow the standard agent result shape:

```js
{
  success: true,
  message: "optional summary",
  artifacts: {},
  metrics: {}
}
```
