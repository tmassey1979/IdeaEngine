# [Story] REUSABLE COMPONENT LIBRARY: Shared Infrastructure Deployment

Parent epic: [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`

## User Story

As a platform operator, I want the shared infrastructure deployment capability, so that all shared services should be deployed using containers.

## Description

All shared services should be deployed using containers.

Recommended approach:

## Acceptance Criteria

- [ ] The Shared Infrastructure Deployment behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: postgres, redis, rabbitmq.
- [ ] Dependencies and integration points with the rest of REUSABLE COMPONENT LIBRARY are documented.

## Dev Notes

- Parent epic: [Epic] REUSABLE COMPONENT LIBRARY
- Source section: `codex/sections/07-reusable-component-library.md`
- Known technical details:
  - postgres
  - redis
  - rabbitmq
  - keycloak
  - prometheus

### Source Excerpt

All shared services should be deployed using containers.

Recommended approach:

-

Example services stack:

```
postgres
redis
rabbitmq
keycloak
prometheus
grafana
api gateway
```

These services run as the **platform layer** for Dragon Idea Engine.

---

