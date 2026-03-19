# Final Architecture Lock

## Authority Model
- PostgreSQL is the persistent source of truth.
- `.NET 8` backend is authoritative runtime for all game-rule decisions.
- Unity client is non-authoritative (render/input/UX/network transport only).

## Runtime Layers
1. **Sboss.Api**: HTTP surface, validation pipeline entry, endpoint composition.
2. **Sboss.Domain**: entities/value semantics, invariants, service contracts.
3. **Sboss.Infrastructure**: PostgreSQL integration/repositories.
4. **Sboss.Contracts**: transport-level request/response contracts.

## Data Ownership
- Account/profile/progression/inventory/match results/seeds/seasons/cosmetics belong to backend + database.
- Client submits intents only; backend validates and persists.

## Security and Anti-Cheat Baseline
- Server-side validation path required for match submissions.
- Validation status persisted for each match result.
- No client-calculated authoritative outcomes accepted without backend checks.

## Phase Guardrails
- This document is an architecture lock, not a Phase 0 execution freeze.
- Work must follow `docs/MASTER_STATUS.md` for the active roadmap phase and next task.
- The server-authoritative trust boundary remains mandatory for all phases.
