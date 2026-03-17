# Dragon UI

Static mockup for the operator dashboard.

Open `index.html` in a browser for the current concept.

The mock also reads `sample-status.json`, which mirrors the backend
`Dragon.Backend.Cli status` snapshot shape. That lets the dashboard render
real orchestrator fields like queued job counts, workflow notes, current stage,
latest execution summary, and execution notes while we stay in a static
prototype phase.

To refresh that payload from the repo root, run:

```bash
npm run status:ui
```
