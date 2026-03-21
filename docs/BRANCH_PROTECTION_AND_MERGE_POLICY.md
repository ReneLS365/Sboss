# Branch Protection and Merge Policy

## Purpose
Protect `main` from red CI, incomplete reviews, and unsafe merges.

## Target branch
`main`

## Required GitHub settings
- Require a pull request before merging
- Require at least 1 approval
- Dismiss stale approvals when new commits are pushed
- Require conversation resolution before merging
- Require status checks to pass before merging
- Require branches to be up to date before merging, unless merge queue is enabled
- Require linear history
- Include administrators
- Do not allow force pushes
- Do not allow deletions

## Required status checks
Use one canonical backend check name only:
- `backend-build-test-linux`

Do not mark duplicate or experimental checks as required.

## Merge methods
Preferred:
- Squash merge

Allowed:
- Rebase merge only if history policy requires it and team agrees

Forbidden:
- Direct pushes to `main`
- Merge while required checks are red
- Merge with unresolved review conversations

## Merge queue
Enable only after the canonical backend workflow is stable and supports `merge_group`.

## Break-glass policy
Use only for production-impacting emergencies.
Required steps:
1. record reason publicly in PR or issue
2. merge the smallest rollback or hotfix possible
3. restore protections immediately
4. open follow-up repair PR the same day
