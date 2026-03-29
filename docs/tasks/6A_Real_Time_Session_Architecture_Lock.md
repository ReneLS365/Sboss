# Task 6A – Real-Time Session Architecture Lock

## Goal
Define the authoritative architecture for post-MVP live sessions before any real-time runtime code is introduced.

## Phase Check
- **Current task:** 6A — Real-Time Session Architecture Lock (once `docs/MASTER_STATUS.md` advances here).
- **Next task:** 6B — Authoritative Simulation Runtime.
- **Why allowed:** Real-time simulation is explicitly deferred until Phase 6 so MVP phases can finish without architecture drift.

## Scope
### In scope
- Define the live-session domain boundary between persistent meta-loop state and ephemeral session state.
- Specify session lifecycle states (created, active, paused, completed, aborted).
- Define authoritative tick/update policy, simulation cadence, and reconciliation boundaries.
- Compare candidate low-latency transport models and select one with rationale.
- Define what remains authoritative in PostgreSQL versus what may live in session memory during active simulation.
- Define security, anti-cheat, and persistence handoff constraints for real-time sessions.
### Out of scope
- Implementing the session runtime.
- Implementing transport sockets, streaming, or replication code.
- Modifying current MVP runtime behavior.

## Allowed Files
- `docs/MASTER_STATUS.md` if roadmap advancement is part of the scoped work.
- `docs/FINAL_ARCHITECTURE.md`
- `docs/architecture/ARCHITECTURE_DECISIONS.md`
- `docs/tasks/6A_Real_Time_Session_Architecture_Lock.md`
- Additional docs-only design files under `docs/architecture/` if explicitly listed in scope.

## Forbidden Files
- `src/backend/**`
- `src/client/**`
- `.github/workflows/**`

## Acceptance Criteria
1. A documented authoritative live-session architecture exists.
2. Session-state ownership and persistence boundaries are explicit.
3. Transport selection criteria and decision are documented.
4. The design preserves backend authority for economy, progression, scoring, and anti-cheat.

## Validation
- Review docs for consistency with `docs/MASTER_STATUS.md`.
- Confirm no runtime code changed.

## Notes
- This task is a design lock. It must reduce ambiguity before runtime work starts.
