# Title
3C — Wear & Tear System

# Goal
Introduce authoritative material wear tracking tied to placement/removal mistakes or invalid handling outcomes, without moving integrity logic to the client.

# Phase Check
- Current phase from `docs/MASTER_STATUS.md`: **Phase 3 — Company & Meta-loop**.
- Next planned task from `docs/MASTER_STATUS.md`: **3D — Loadout & Fog of War**.
- Why this task is allowed in the current phase: `docs/MASTER_STATUS.md` now marks 3B complete and makes 3C the active task, so scoped wear/integrity implementation is phase-valid.

# Scope
- In scope:
  - Server-owned wear/integrity state for relevant scaffold/material entities.
  - Deterministic wear application rules triggered by backend-owned failure, misplacement, or invalid handling outcomes.
  - Persistence changes required to store and update wear/integrity state.
  - Minimal backend service/repository/API surface needed to read and apply wear state.
  - Tests that prove wear accumulation behavior and invariants.
- Out of scope:
  - Unity/client visuals or UX for damage representation.
  - Real-time runtime/session architecture work.
  - Yard inventory redesign.
  - Loadout/Fog-of-War logic (3D scope).
  - XP/progression unlocks (3E scope).
  - Cosmetic-only damage systems with no authoritative gameplay effect.
  - Any client-owned truth for material integrity or wear progression.

# Allowed Files
- `src/backend/Sboss.Domain/**` (wear/integrity entities, value objects, invariants).
- `src/backend/Sboss.Application/**` (use-cases/commands/queries for wear handling).
- `src/backend/Sboss.Infrastructure/**` (repositories/services + persistence wiring).
- `src/backend/Sboss.Api/**` (minimal endpoints/contracts required for wear read/apply flows).
- `src/backend/db/migrations/**` and `src/backend/db/schema.sql` (wear persistence schema updates).
- `src/backend/tests/**` (deterministic wear and invariant coverage).
- `docs/tasks/3C_Wear_and_Tear_System.md`.
- `PLANS.md` only if later introduced and explicitly required by workflow.

# Forbidden Files
- `src/client/**`
- `src/unity/**`
- `src/backend/Sboss.Infrastructure/Scoring/**` and unrelated score systems.
- `docs/tasks/3D_*`
- `docs/tasks/3E_*`
- `.github/**`
- Any future-phase runtime/session transport paths unrelated to 3C wear state.

# Acceptance Criteria
- Wear/integrity state is authoritative and server-owned.
- Wear/integrity cannot be mutated by client-only state or trust boundaries.
- Deterministic automated tests prove wear accumulation and invariants.
- A smallest viable 3C implementation path is clearly defined across domain, persistence, and API seams.
- No implementation scope creep into 3D Loadout/Fog-of-War or 3E XP/Progression.

# Validation
- Commands to run:
  - `rg -n "Current task|Next task|3B|3C|3D" docs/MASTER_STATUS.md docs/tasks`
  - `git diff -- docs/MASTER_STATUS.md docs/tasks/3C_Wear_and_Tear_System.md PLANS.md`
  - `python3 scripts/validate-roadmap-status.py` (if script exists).
- Manual checks:
  - Confirm only docs roadmap/task files changed for this status-advance PR.
  - Confirm `docs/MASTER_STATUS.md` shows 3C as current task and 3D as next task.
  - Confirm 3B is marked complete while Phase 3 remains incomplete.
  - Confirm this task document stays implementation-scoped and does not replace roadmap state.

# Notes
- `docs/MASTER_STATUS.md` remains the single roadmap/progress source of truth; this task file scopes execution only.
- Runtime/backend wear implementation is intentionally deferred to a separate 3C implementation PR.

# Author Checklist
- Confirm forbidden paths are task-specific (not copied defaults).
- Confirm task aligns with current phase in `docs/MASTER_STATUS.md`.
- Confirm scope does not exceed phase boundaries.
