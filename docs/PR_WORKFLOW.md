# Pull Request Workflow

## Purpose
Define the standard path from task start to safe merge.

## Branching
Create short-lived branches from current `main`.
Use descriptive names tied to the scoped task.

Examples:
- `fix/pr14-test-db-alignment`
- `feat/economy-transaction-service`
- `docs/ci-debug-playbook`

## Required PR lifecycle
1. Confirm latest repo state.
2. Read `AGENTS.md`, `PLANS.md`, and active status docs.
3. Scope the task narrowly.
4. Implement only the required change.
5. Run local validation.
6. Open a draft PR first.
7. Iterate on review fixes on the same PR branch.
8. Mark ready for review only when checks are expected to pass.
9. Merge only after green CI and resolved conversations.

## PR body must contain
- problem statement
- technical rationale
- assumptions
- validation performed
- explicit out-of-scope items

## Review fix rule
If a PR is still open, follow-up fixes stay on the same branch and same PR unless the change is explicitly re-scoped.

## Done definition
A task is done only when:
- code is merged
- CI is green on the final PR head
- any required status docs are updated
- no unresolved red follow-up remains
