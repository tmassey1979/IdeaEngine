# [Epic] RISK ANALYSIS AND COMPLIANCE SCORING

Source section: `codex/sections/05-risk-analysis-and-compliance-scoring.md`

## User Story

As a product owner, I want risk and compliance scoring before ideas are approved, so that unsafe or non-compliant ideas are filtered before build work starts.

## Summary

Before an idea can be approved for development, Dragon Idea Engine must evaluate **legal, regulatory, and ethical risks** associated with the project.

This ensures the system does not automatically generate projects that expose the operator to legal liability.

## Acceptance Criteria

- [ ] All major implementation slices for this codex section are represented by child stories.
- [ ] The epic outcome is documented clearly enough to guide implementation and review.
- [ ] Known dependencies, sequencing constraints, and governance concerns are captured.

## Child Stories

- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Risk Score Model
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Risk Categories
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Privacy Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Intellectual Property Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Platform Policy Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Financial Regulation Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Medical / Health Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Security Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Compliance Risk
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Risk Evaluation Pipeline
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Risk Example
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: High-Risk Idea Rejection
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Risk Dashboard
- [ ] [Story] RISK ANALYSIS AND COMPLIANCE SCORING: Compliance Awareness for Agents

## Dev Notes

- Actor: product owner
- Source section: `codex/sections/05-risk-analysis-and-compliance-scoring.md`
- Planned child stories: 14
- Known technical details:
  - privacy violations
  - regulated industries
  - copyright infringement
  - data misuse
  - platform abuse
  - Risk Penalty = 0 – 40
  - Final Score = Idea Score − Risk Penalty
  - Idea Score: 75

