# PLANS

## Active scoped task
- Phase: **4 — Asynchronous Competition**
- Task: **4A — Leaderboard API**
- Status lock: `docs/MASTER_STATUS.md` is set to current task **4A**.

## Scope (4A)
Implement the server-authoritative leaderboard backend slice defined in `docs/tasks/4A_Leaderboard_API.md`: domain model, repository/persistence, application service, API controller, and tests for sorting, pagination, rank calculation, resets, and validation.

## Allowed files (4A)
- `docs/MASTER_STATUS.md`
- `PLANS.md`
- `docs/tasks/4A_Leaderboard_API.md`
- `server/Sboss.Domain/Leaderboards/*`
- `server/Sboss.Application/Leaderboards/*`
- `server/Sboss.Infrastructure/Repositories/Leaderboards/*`
- `server/Sboss.Api/Controllers/LeaderboardController.cs`
- `tests/Leaderboards/*`

## Non-goals (4A)
- Unity/client leaderboard rendering or UX work.
- Real-time streaming (WebSockets/SignalR); REST + polling only.
- Changes to prior-phase systems (yard capacity, inventory, economy, scouting, scoring algorithms).
- Phase 6 transport/runtime simulation work.
- Any work outside task 4A scope.
