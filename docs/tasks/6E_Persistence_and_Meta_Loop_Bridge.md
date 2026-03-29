# Task 6E – Persistence & Meta-loop Bridge

## Goal
Bridge live-session outcomes back into the existing persistent game systems so real-time simulation works with the rest of Sboss.

## Phase Check
- **Current task:** 6E — Persistence & Meta-loop Bridge.
- **Next task:** None (future roadmap extension may continue from here).
- **Why allowed:** Real-time sessions are only complete when their outcomes correctly update the persistent meta-loop.

## Scope
### In scope
- Commit live-session outcomes into contract jobs, yard inventory, wear/tear, scoring, ghost data, XP, and progression as applicable.
- Ensure idempotent persistence of session completion and reward distribution.
- Extend anti-cheat and audit trails to cover live-session outcomes.
- Define rollback or compensation behavior when session completion persistence fails.
- Add reporting/telemetry for session outcome integrity.
### Out of scope
- Major redesign of already-shipped MVP systems.
- New competitive features outside the session bridge.
- Store release and deployment work.

## Allowed Files
- Backend persistence, service, and test paths required for bridging outcomes.
- Additive migrations if necessary.
- `docs/tasks/6E_Persistence_and_Meta_Loop_Bridge.md`

## Forbidden Files
- Client-authoritative persistence logic.
- `.github/workflows/**`
- Unrelated roadmap expansions.

## Acceptance Criteria
1. Live-session completion updates persistent meta-loop systems correctly.
2. Outcome persistence is atomic or compensating where required.
3. Anti-cheat and audit logs cover real-time sessions.
4. Tests prove bridge correctness across success and failure cases.

## Validation
- Restore/build/test commands.
- End-to-end session completion persistence tests.

## Notes
- This task is the integration point that makes real-time simulation coexist with the existing roadmap systems.
