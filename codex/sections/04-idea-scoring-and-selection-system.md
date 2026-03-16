> Extracted from the shared ChatGPT conversation:
> https://chatgpt.com/share/69b808f3-3e40-8001-b31f-26e66ab44bb5
> Assistant turn: 46
> Original timestamp: 2026-03-13T20:22:01.982Z

# IDEA_SCORING_AND_SELECTION_SYSTEM

## Purpose

Dragon Idea Engine receives ideas from multiple sources. Not all ideas should become projects.

The Idea Scoring System evaluates each idea and determines whether the system should:

```
build
defer
reject
archive
```

This prevents uncontrolled repository growth and focuses development resources on high-value opportunities.

---

# Idea Sources

Ideas may originate from:

```
DragonIdeaEngine repository issues
UI submissions
feedback modules embedded in projects
automated research agents
market data imports
```

All ideas are normalized into a single structure.

---

# Idea Data Structure

Example idea record:

```json
{
  "ideaId": "uuid",
  "title": "Local contractor CRM",
  "description": "Simple CRM for contractors",
  "tags": ["crm","small-business"],
  "submittedBy": "user",
  "source": "ui",
  "createdAt": "timestamp"
}
```

Stored in the `ideas` table.

---

# Scoring Pipeline

Ideas move through a multi-stage evaluation pipeline.

```
Idea Submission
       ↓
Idea Classification Agent
       ↓
Idea Scoring Agent
       ↓
Idea Decision Engine
       ↓
Project Creation (optional)
```

---

# Idea Classification

The **Idea Classification Agent** extracts metadata.

Fields produced:

```
category
target_users
market_segment
technical_domain
estimated_complexity
estimated_development_time
```

Example:

```
category: business tools
users: contractors
complexity: medium
stack: web
```

---

# Idea Scoring Criteria

Each idea receives a weighted score.

Base scoring model:

```
Total Score = Weighted Sum
```

Default maximum:

```
100 points
```

---

# Scoring Dimensions

## Market Demand

Measures potential user demand.

Range:

```
0 – 25
```

Factors:

```
problem relevance
market size
existing search interest
community interest
```

---

## Technical Feasibility

Measures difficulty relative to system capability.

Range:

```
0 – 20
```

Factors:

```
agent capability
technology maturity
integration complexity
```

---

## Development Cost

Estimates resource cost.

Range:

```
0 – 15
```

Higher score means **lower cost**.

Factors:

```
estimated development time
number of components
external dependencies
```

---

## Differentiation

Measures uniqueness compared to existing tools.

Range:

```
0 – 15
```

Factors:

```
existing competitors
unique value
automation potential
```

---

## Monetization Potential

Optional but useful for commercial use.

Range:

```
0 – 15
```

Factors:

```
subscription potential
licensing potential
market willingness to pay
```

---

## Strategic Value

Measures alignment with Dragon Idea Engine goals.

Range:

```
0 – 10
```

Examples:

```
improves agent ecosystem
demonstrates platform capability
extends reusable modules
```

---

# Example Scoring

Idea:

```
Contractor CRM
```

Example scoring:

```
Market Demand         20
Technical Feasibility 17
Development Cost      10
Differentiation        8
Monetization          11
Strategic Value        7
-------------------------
Total Score           73
```

---

# Decision Thresholds

The Decision Engine determines the outcome.

```
Score ≥ 70 → BUILD
Score 50-69 → DEFER
Score 30-49 → ARCHIVE
Score < 30 → REJECT
```

---

# Build Outcome

If the idea is approved:

The system automatically triggers:

```
Architect Agent
Repository Manager Agent
Issue Generator Agent
```

Which begins the project lifecycle.

---

# Deferred Ideas

Deferred ideas remain in the queue.

They may be reevaluated when:

```
agent capabilities improve
market signals increase
related projects emerge
```

---

# Archived Ideas

Archived ideas are stored but inactive.

They can be reactivated manually.

---

# Rejected Ideas

Rejected ideas are ignored by the system but retained for audit history.

---

# Idea Feedback Loop

Ideas may gain score over time based on signals.

Signals include:

```
user upvotes
similar feedback reports
usage analytics
market trends
```

The **Idea Scoring Agent** periodically reevaluates stored ideas.

---

# Anti-Explosion Limits

To prevent runaway project creation the system enforces limits.

Example policies:

```
maxActiveProjects = 20
maxNewProjectsPerWeek = 3
maxConcurrentIdeaEvaluations = 10
```

---

# Idea Dashboard (UI)

The Dragon UI should display:

## Idea Queue

```
new ideas
pending scoring
deferred ideas
```

---

## Idea Scores

Display:

```
total score
dimension breakdown
decision outcome
```

---

## Idea Voting

Users may vote:

```
upvote
downvote
```

Votes influence scoring.

---

# AI Research Agent (Future)

A future **Research Agent** may enrich idea scoring by analyzing:

```
market reports
github trends
startup databases
product hunt
reddit discussions
```

This improves idea evaluation accuracy.

---

# Long-Term Goal

The Idea Scoring System transforms Dragon Idea Engine into a **structured innovation pipeline** rather than a random project generator.

The system should ultimately behave like:

```
an automated product incubator
```

Where ideas are evaluated, built, improved, and evolved over time.

---

## Recommended Next Codex Section

The next extremely valuable section to add would be:

```
PROJECT_HEALTH_AND_RETIREMENT_SYSTEM
```

This would allow the engine to automatically determine:

```
which projects to improve
which projects to pause
which projects to retire
```

Without this, long-running systems accumulate abandoned projects over time.
