# [Story] SECURITY AND COMPLIANCE VALIDATION SYSTEM: Risk Scoring System

Parent epic: [Epic] SECURITY AND COMPLIANCE VALIDATION SYSTEM
Source section: `codex/sections/18-security-and-compliance-validation-system.md`

## User Story

As a product owner, I want the risk scoring system capability, so that each project receives a combined risk score.

## Description

Each project receives a combined risk score.

Score categories include:

## Acceptance Criteria

- [ ] The Risk Scoring System behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: security risk, legal risk, privacy risk.
- [ ] Dependencies and integration points with the rest of SECURITY AND COMPLIANCE VALIDATION SYSTEM are documented.

## Dev Notes

- Parent epic: [Epic] SECURITY AND COMPLIANCE VALIDATION SYSTEM
- Source section: `codex/sections/18-security-and-compliance-validation-system.md`
- Known technical details:
  - security risk
  - legal risk
  - privacy risk
  - ethical risk
  - operational risk

### Source Excerpt

Each project receives a combined risk score.

Score categories include:

```text
security risk
legal risk
privacy risk
ethical risk
operational risk
```

Example scoring model:

```text
0–25  Low risk
26–50 Moderate risk
51–75 High risk
76–100 Critical risk
```

Projects above a configured threshold cannot be deployed automatically.

---

