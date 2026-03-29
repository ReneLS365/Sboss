# Task 4D – Daily Challenge System

## Goal
Create a system that offers a new global challenge every 24 hours.  Each challenge uses a specific seed and ruleset; players compete asynchronously during the 24‑hour window, and scores are ranked on leaderboards.  The server must schedule, start, and close challenges automatically.

## Phase Check
- **Current task:** 4D — Daily Challenge System.
- **Next task:** 4E — Social Push & Sabotage.
- **Why allowed:** After ghost data (4C), daily challenges integrate deterministic levels and leaderboards into time‑bound events.

## Scope
### In scope
- Add backend scheduling of daily challenge events, using a job runner or cron‑like mechanism to rotate seeds.
- Use the deterministic level generator (4B) to select a seed and configure challenge rules.
- Reset leaderboard slices for the daily challenge on start; finalize results and archive ghost data at the end of the window.
- Provide API endpoints to fetch the current daily challenge, submit scores/ghosts, and view results.
- Persist challenge history.
### Out of scope
- Social notifications or sabotage actions (4E).
- Non‑deterministic or real‑time events.

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migration scripts for challenge scheduling and history.
- `docs/tasks/4D_Daily_Challenge_System.md`.

## Forbidden Files
- Unity client code that schedules or scores challenges.
- Workflow/pipeline modifications unrelated to challenge scheduling.

## Acceptance Criteria
1. A new daily challenge is scheduled and published automatically every 24 hours.
2. Scores and ghosts submitted during the challenge are ranked on a dedicated daily leaderboard.
3. At the end of the window, the leaderboard is finalized and archived; a new challenge begins.
4. APIs return current challenge details and allow score/ghost submission only during the challenge period.
5. Tests validate scheduling, expiration, and scoring logic.

## Validation
Run restore/build/test commands and simulate multiple challenge cycles by altering the scheduler to shorter intervals in tests.

## Notes
- Use server time in UTC to schedule windows consistently.