# React Dashboard

Pi MVC milestone React UI for Dragon Idea Engine.

## Local development

From the repo root:

```bash
npm run react-ui:dev
```

Or from this folder:

```bash
npm run dev
```

## Tests

Unit tests:

```bash
npm run test
```

Playwright:

```bash
npm run test:e2e
```

## Docker

The root `docker-compose.yml` now serves this app as `dragon-ui` on:

```text
http://127.0.0.1:5080
```

The nginx container proxies:

- `/api/*` to `dragon-api`
- `/status` to `dragon-backend` for diagnostics only

The React app itself now reads only from `/api/*`.
