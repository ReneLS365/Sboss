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

### `docs/MASTER_STATUS.md`
Source of truth for:
- current roadmap phase
- current roadmap task
- next roadmap task
- canonical in-repo phase/progress status

### `PLANS.md`
Source of truth for:
- scoped in-repo task execution records
- current task scope
- allowed files
- forbidden files
- non-goals
- acceptance criteria
- implementation notes tied to the active roadmap task

### Google Docs / Drive
Source of truth for:
- large working notes
- vision documents
- design narratives
- archival drafts
- human review documents that are not enforced by CI

## Non-negotiable rule
The same operational truth must not live in two active places at once.

## Examples
- PR state belongs in GitHub, not in a Google Doc.
- Required checks belong in GitHub settings and workflow YAML, not in external notes.
- Active architecture decisions belong in the repo, not in a stale PDF.
- Current roadmap phase/task status belongs in `docs/MASTER_STATUS.md`, not in any external planning system.
- Scoped execution details belong in `PLANS.md`, but they must not contradict `docs/MASTER_STATUS.md`.
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
