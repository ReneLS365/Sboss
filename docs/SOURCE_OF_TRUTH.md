# Source of Truth

## Purpose
This document prevents project drift by defining exactly where truth lives for each class of SBoss information.

## Canonical systems
### GitHub
Source of truth for:
- code
- pull requests
- commits
- branches
- workflow files
- CI status
- release history
- repo policy files

### Repository docs
Source of truth for:
- engineering policy
- architecture decisions
- CI/runbooks
- migration policy
- branch and PR workflow
- status files tracked in git

### Google Docs / Drive
Source of truth for:
- large working notes
- vision documents
- design narratives
- archival drafts
- human review documents that are not enforced by CI

### ClickUp or equivalent task board
Source of truth for:
- task status
- backlog order
- owner assignment
- deadlines

## Non-negotiable rule
The same operational truth must not live in two active places at once.

## Examples
- PR state belongs in GitHub, not in a Google Doc.
- Required checks belong in GitHub settings and workflow YAML, not in ClickUp.
- Active architecture decisions belong in the repo, not in a stale PDF.
- Legacy brainstorm material can stay in Drive, but it does not override repo state.

## Naming rule
- Current name: SBoss
- Legacy alias: StilBoss
- Do not create new active docs under StilBoss naming

## Document hygiene rule
Every new document must answer:
1. what system owns this truth?
2. is this active or archive material?
3. does this duplicate something already enforced in repo?
