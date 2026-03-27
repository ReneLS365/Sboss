# Codex Workflow

## Purpose
This document locks the repository execution model before Phase 1A begins.

## Control Rules
 - `docs/MASTER_STATUS.md` is the only roadmap and in-repo progress source.
 - Work must remain inside the active phase and task scope.
 - Draft pull requests only until human review approves the change set.
 - Server-authoritative architecture is mandatory; no client-owned truth may be introduced.
 - Do not create manual status mirrors; if a status surface is manual and redundant with `docs/MASTER_STATUS.md`, it should not exist.

## Required Delivery Sequence
1. Confirm the requested work is inside the current phase in `docs/MASTER_STATUS.md`.
2. Make the smallest possible change set that satisfies the scoped task.
3. Validate the diff against the task's declared allowlist, forbidden paths, and non-goals.
4. For documentation-only tasks, prove that no runtime code was modified; for implementation tasks, prove that only the scoped runtime paths changed.
5. Open a draft PR using the repository templates.

## Task Definition Rules
- Use `docs/TASK_TEMPLATE.md` for new scoped tasks.
- Include explicit allowed files and forbidden files.
- Define acceptance criteria and validation commands before implementation begins.
- Keep implementation notes separate from roadmap state; roadmap state belongs only in `docs/MASTER_STATUS.md`.

## Review Gates
- Scope gate: the diff must match the declared allowed files.
- Task-scope gate: validation rules must follow the task's declared allowlist and forbidden paths instead of assuming every task is docs-only.
- Phase gate: the task must not advance work from a future phase.
- Architecture gate: changes must preserve server-authoritative ownership.
- Documentation gate: README and templates must point contributors to the locked control documents when relevant.

## Forbidden Drift
 - Do not treat pull requests, issues, or task documents as roadmap replacements.
 - Avoid hidden sources of roadmap/progress status outside `docs/MASTER_STATUS.md`.
 - Do not implement future-phase runtime logic under the cover of documentation or scaffolding work.
