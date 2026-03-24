# Dragon API

Read-only API layer for the Pi React dashboard.

Current scope:

- consumes internal read endpoints from `dragon-backend`
- exposes frontend-facing dashboard and idea read models
- keeps the React UI off raw `/status` payloads
- does not own persistence or submit/write workflows

Useful commands:

```bash
dotnet run --project services/dragon-api
dotnet test services/dragon-api/tests/Dragon.Api.Tests.csproj
```

Default local endpoint:

```text
http://127.0.0.1:5079
```

Endpoints:

- `GET /health`
- `GET /api/dashboard`
- `GET /api/ideas`
- `GET /api/ideas/{id}`
