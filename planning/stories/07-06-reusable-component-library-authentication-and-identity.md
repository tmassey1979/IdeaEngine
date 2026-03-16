# [Story] REUSABLE COMPONENT LIBRARY: Authentication and Identity

Parent epic: [Epic] REUSABLE COMPONENT LIBRARY
Source section: `codex/sections/07-reusable-component-library.md`

## User Story

As a platform operator, I want the authentication and identity capability, so that all generated applications should rely on a centralized authentication service.

## Description

All generated applications should rely on a centralized authentication service.

Recommended solution:

## Acceptance Criteria

- [ ] The Authentication and Identity behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: OAuth2, OpenID Connect, Single Sign-On.
- [ ] Dependencies and integration points with the rest of REUSABLE COMPONENT LIBRARY are documented.

## Dev Notes

- Parent epic: [Epic] REUSABLE COMPONENT LIBRARY
- Source section: `codex/sections/07-reusable-component-library.md`
- Known technical details:
  - OAuth2
  - OpenID Connect
  - Single Sign-On
  - Role-based access control
  - identity federation

### Source Excerpt

All generated applications should rely on a centralized authentication service.

Recommended solution:

-

Keycloak provides:

```
OAuth2
OpenID Connect
Single Sign-On
Role-based access control
identity federation
```

Generated projects should integrate via standard OIDC flows.

Example roles:

```
admin
developer
viewer
agent
```

---

