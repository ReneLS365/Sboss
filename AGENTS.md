# AGENTS

## Phase Lock
- **Fase 0 only**. No implementation beyond bootstrap foundation.

## Architecture Lock
- Server-authoritative is mandatory.
- Unity client is limited to rendering, input, UX, and network transport.
- No client-owned truth for score, progression, economy, anti-cheat, validation, seeds, or leaderboard.

## Workflow Rules
- Update `PLANS.md` before implementation.
- Plan-first execution is required for all non-trivial tasks.
- Draft PR only; never push directly to `main`.
- Prevent architectural drift from locked docs.

## Tooling Rules
- Use `rg` over grep for search.
- Use `apply_patch` for targeted edits when modifying existing files.

## Definition of Done (Phase 0)
- Locked architecture docs in place.
- Backend shell (`.NET 8`) runs with required endpoints.
- PostgreSQL schema baseline exists.
- Unity shell documented with dumb-client constraints.
- CI baseline exists for backend restore/build/test.
- Labels taxonomy and manifest are available for Phase 0 operations.
- No Fase 1+ scope creep.
