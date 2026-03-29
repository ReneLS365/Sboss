# Task 3E – XP & Progression

## Goal
Introduce a progression system where players earn experience points (XP) from completing jobs and unlock more challenging level templates (e.g. Offshore Rotations).  The server must calculate XP rewards, track player levels, and manage unlock thresholds in an authoritative manner.

## Phase Check
- **Current task:** 3E — XP & Progression.
- **Next task:** 4A — Leaderboard API.
- **Why allowed:** After 3D, `docs/MASTER_STATUS.md` designates XP & Progression as the final task in Phase 3.

## Scope
### In scope
- Define XP values for job completion, difficulty modifiers, and performance bonuses.
- Implement domain models to track a player’s XP and progression level.
- Add API endpoints for retrieving XP and notifying level‑ups.
- Enforce unlock requirements so that more complex level templates are only selectable when the player has sufficient XP.
- Persist XP and level data.
### Out of scope
- Leaderboard ranking (Phase 4A), ghost replays (Phase 4C), daily challenges (Phase 4D).
- Crew splitting (3B) beyond how it might indirectly affect XP (no new logic here).
- Visual representation in Unity (client remains dumb).

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations for XP storage if required.
- `docs/tasks/3E_XP_and_Progression.md`.

## Forbidden Files
- Client code (Unity) for progression decisions.
- Workflow or pipeline files.

## Acceptance Criteria
1. XP is awarded server‑side based on job completion and difficulty; players cannot spoof XP.
2. Level thresholds and unlocks are enforced; selecting an unlocked template is allowed while locked templates are rejected.
3. XP and level data persist across sessions.
4. Tests validate correct XP accumulation, level‑up, and unlock logic.

## Validation
Run restore/build/test commands. Simulate job completions of varying difficulty and verify XP increases and unlocks.

## Notes
- Rewards should be deterministic and based on server‑verified performance metrics.