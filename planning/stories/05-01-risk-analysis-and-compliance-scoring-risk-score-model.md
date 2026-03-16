# [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Risk Score Model

Parent epic: [Epic] RISK ANALYSIS AND COMPLIANCE SCORING
Source section: `codex/sections/05-risk-analysis-and-compliance-scoring.md`

## User Story

As a product owner, I want the risk score model capability, so that risk scoring works inversely.

## Description

Risk scoring works inversely.

Higher risk reduces the final score.

## Acceptance Criteria

- [ ] The Risk Score Model behavior is implemented according to the codex definition.
- [ ] The implementation covers these codex details: Risk Penalty = 0 – 40, Final Score = Idea Score − Risk Penalty, Idea Score: 75.
- [ ] Dependencies and integration points with the rest of RISK ANALYSIS AND COMPLIANCE SCORING are documented.

## Dev Notes

- Parent epic: [Epic] RISK ANALYSIS AND COMPLIANCE SCORING
- Source section: `codex/sections/05-risk-analysis-and-compliance-scoring.md`
- Known technical details:
  - Risk Penalty = 0 – 40
  - Final Score = Idea Score − Risk Penalty
  - Idea Score: 75
  - Risk Penalty: 18
  - Final Score: 57

### Source Excerpt

Risk scoring works inversely.

Higher risk reduces the final score.

```
Risk Penalty = 0 – 40
```

Final idea score becomes:

```
Final Score = Idea Score − Risk Penalty
```

Example:

```
Idea Score: 75
Risk Penalty: 18
Final Score: 57
Decision: DEFER
```

---

