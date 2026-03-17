# Dragon UI

Static mockup for the operator dashboard.

Open `index.html` in a browser for the current concept.

The mock also reads `sample-status.json`, which mirrors the backend
`Dragon.Backend.Cli status` snapshot shape. That lets the dashboard render
real orchestrator fields like queued job counts, workflow notes, current stage,
latest execution summary, and execution notes while we stay in a static
prototype phase.

To avoid local `file://` CORS issues, the refresh flow also writes
`sample-status.js` and `sample-status.previous.js`. The page loads those script
wrappers first, so opening `index.html` directly from disk still works.

Each refresh also rotates the prior snapshot into
`sample-status.previous.json` before writing the new one. That preserves one
comparison point in the UI export path and gives future dashboard slices a
stable “previous snapshot” file to reference.

The mock UI now uses that previous snapshot as a client-side fallback for trend
comparison when the freshly exported snapshot does not yet carry a backend
comparison baseline.

To refresh that payload from the repo root, run:

```bash
npm run status:ui
```

To run the local loop and refresh the payload at the end of the run:

```bash
npm run run:ui
```
