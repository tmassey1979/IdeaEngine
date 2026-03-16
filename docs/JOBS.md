# Job Contracts

Dragon Idea Engine jobs use a shared schema across the runner and agent plugins.

## Job Message Schema

```json
{
  "jobId": "uuid",
  "agent": "developer",
  "action": "implement_issue",
  "repo": "dragon-crm",
  "project": "DragonCRM",
  "issue": 42,
  "priority": "normal",
  "createdAt": "timestamp",
  "payload": {},
  "metadata": {
    "requestedBy": "system",
    "source": "orchestrator"
  }
}
```

## Result Schema

```json
{
  "jobId": "uuid",
  "status": "success",
  "agent": "developer",
  "duration": 123,
  "result": {},
  "logs": [],
  "errors": []
}
```

## Status Values

- `queued`
- `running`
- `success`
- `failed`
- `retry`
- `deadletter`

## Retry Policy

- `maxRetries: 3`
- `retryDelay: exponential`
- schedule: `10s`, `30s`, `90s`

## Runner Behavior

- service mode validates every inbound job
- invalid jobs return a structured failure result
- valid jobs are routed to the matching agent plugin
