# Agent Plugin Spec

Each agent plugin exports:

```js
module.exports = createAgent({
  id: "architect",
  description: "Designs system architecture",
  run: async (context) => ({ ... })
});
```

## Required fields

- `id`: unique command and registry identifier
- `description`: short human-readable summary
- `run(context)`: async handler returning a serializable result

## Runner contract

The runner provides:

- `mode`: `cli` or `service`
- `args`: parsed positional and flag arguments
- `job`: validated shared job payload in service mode
- `logger`: structured logging helper

The result returned by `run` should be JSON-serializable.
