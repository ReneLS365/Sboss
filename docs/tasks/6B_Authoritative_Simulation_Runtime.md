# Task 6B – Authoritative Simulation Runtime

## Goal
Implement the backend-owned live-session simulation core for active jobs.

## Phase Check
- **Current task:** 6B — Authoritative Simulation Runtime.
- **Next task:** 6C — Session Transport & Intent Streaming.
- **Why allowed:** 6A must first lock session authority, cadence, and persistence boundaries.

## Scope
### In scope
- Introduce backend services for creating, running, updating, and closing live sessions.
- Model authoritative in-session state for active jobs, participants, timers, and simulation events.
- Implement server-side simulation stepping/tick execution for session-owned state.
- Integrate session outcomes with existing validation rules where required.
- Persist session summaries and completion outcomes as defined by 6A.
### Out of scope
- Final transport/protocol wiring.
- Client prediction/reconciliation.
- Broad refactors of MVP meta-loop systems.

## Allowed Files
- Backend runtime paths needed for session orchestration and tests.
- `docs/tasks/6B_Authoritative_Simulation_Runtime.md`
- Additive migrations if persistence changes are required.

## Forbidden Files
- Broad client-authoritative logic.
- `.github/workflows/**`
- Unscoped changes to unrelated MVP systems.

## Acceptance Criteria
1. Backend can create and run authoritative live sessions.
2. Session state evolves server-side without relying on client-calculated outcomes.
3. Session completion produces deterministic, persistable outcomes.
4. Tests cover session creation, stepping, completion, and failure paths.

## Validation
- Restore/build/test commands.
- Session runtime integration tests.

## Notes
- Keep runtime additions modular and reversible.
