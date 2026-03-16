# [Story] ETHICS ANALYSIS AGENT: Ethical Risk Model

Parent epic: [Epic] ETHICS ANALYSIS AGENT
Source section: `codex/sections/06-ethics-analysis-agent.md`

## User Story

As a product owner, I want the ethical risk model capability, so that ethical evaluation produces an **Ethics Risk Score**.

## Description

Ethical evaluation produces an **Ethics Risk Score**.

This penalty is applied alongside the legal risk score.

## Acceptance Criteria

- [ ] The Ethical Risk Model behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: Ethics Penalty = 0 – 25, Final Score = Idea Score − Risk Penalty − Ethics Penalty.
- [ ] Dependencies and integration points with the rest of ETHICS ANALYSIS AGENT are documented.

## Dev Notes

- Parent epic: [Epic] ETHICS ANALYSIS AGENT
- Source section: `codex/sections/06-ethics-analysis-agent.md`
- Known technical details:
  - Ethics Penalty = 0 – 25
  - Final Score = Idea Score − Risk Penalty − Ethics Penalty

### Source Excerpt

Ethical evaluation produces an **Ethics Risk Score**.

```
Ethics Penalty = 0 – 25
```

This penalty is applied alongside the legal risk score.

```
Final Score = Idea Score − Risk Penalty − Ethics Penalty
```

---

