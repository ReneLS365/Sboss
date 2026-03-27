# AGENTS

## Phase Governance
- `docs/MASTER_STATUS.md` is the source of truth for the explicit current roadmap phase and next task.
- Work must continue from the latest confirmed repo/project state.
- Do not work outside the current roadmap phase.
- Do not invent a stricter lock than the roadmap itself.

## Architecture Lock
- Server-authoritative is mandatory.
- Unity client is limited to rendering, input, UX, and network transport.
- No client-owned truth for score, progression, economy, anti-cheat, validation, seeds, or leaderboard.

## Workflow Rules
- Draft PR only; never push directly to `main`.
- Prevent architectural drift from locked docs.

## Tooling Rules
- Use `rg` over grep for search.
- Use `apply_patch` for targeted edits when modifying existing files.

<!-- Phase 0 completion record removed -->
