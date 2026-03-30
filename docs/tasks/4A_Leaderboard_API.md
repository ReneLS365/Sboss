# 4A Leaderboard API

This file defines the Codex task for **Phase 4A** of the Sboss Lean MVP roadmap. Phase 4A introduces a leaderboard API that exposes player standings, for example based on XP or other scoring metrics, via the backend. The goal is to implement the authoritative logic on the server, with proper persistence and tests, while leaving client-side UI work for later phases.

## Roadmap context

**Phase 3E (XP & Progression)** must be completed and merged before starting this task.

According to `docs/MASTER_STATUS.md`, this task becomes active when `CURRENT_TASK=4A` and should be implemented only then.

Update `PLANS.md` to reflect the activation of this task and define its scope and non-goals when beginning work.

## Objective

Implement an authoritative leaderboard API in the server. The API must provide sorted standings of players or firms based on server-computed scores. The implementation should be asynchronous, event-driven, and fully validated on the backend. Unity must remain a dumb client that only calls the API and renders returned results.

## Scope

The following deliverables are in scope for this task:

- **Domain model**: Introduce a `LeaderboardEntry` aggregate in the domain layer (`Sboss.Domain`) with fields such as `PlayerId`, `PlayerName`, `Score`, `Rank`, `Scope`, and `LastUpdatedUtc`.
- **Repository and storage**: Create a leaderboard repository in `Sboss.Infrastructure` to read and persist leaderboard entries in PostgreSQL. Migrations for the necessary tables must be added.
- **Application service**: Implement a service in `Sboss.Application` that:
  - consumes domain events, for example XP changes or payouts, to update leaderboard scores
  - calculates ranks and returns paginated leaderboards sorted by score descending
  - supports different scopes, such as global, friends, or clan, and periodic resets, such as daily, weekly, or seasonal
- **API controller**: Add a new controller under `Sboss.Api/Controllers/LeaderboardController.cs` exposing endpoints such as:
  - `GET /leaderboards?scope=global&take=50&afterRank=0`
  - `GET /leaderboards/{playerId}`
  - `POST /leaderboards/reset`
- **Tests**: Add unit and integration tests verifying repository, service, and controller behavior using xUnit. Tests should cover sorting, pagination, rank calculation, resets, and validation.

## Allowed files

The following new or modified files are allowed as part of this task:

- `docs/MASTER_STATUS.md` — update `CURRENT_TASK` when activating this task
- `PLANS.md` — define scope, allowed files, and non-goals for 4A
- `docs/tasks/4A_Leaderboard_API.md` — this file
- `server/Sboss.Domain/Leaderboards/*` — domain model and events
- `server/Sboss.Application/Leaderboards/*` — application services
- `server/Sboss.Infrastructure/Repositories/Leaderboards/*` — repository implementation and migrations
- `server/Sboss.Api/Controllers/LeaderboardController.cs` — web API controller
- `tests/Leaderboards/*` — unit and integration tests

## Non-goals

The following are out of scope for this task and must not be implemented here:

- Any Unity or client UI work. Rendering the leaderboard in Unity will be handled in a later phase.
- Real-time streaming via WebSockets or SignalR. A simple REST API with polling is sufficient for the MVP.
- Changes to yard capacity, inventory, economy, scouting, or score algorithms defined in previous phases.
- Multiplayer session transport and networking, which is covered in Phase 6.

## Description

### Authoritative design

Consistent with the server-authoritative architecture, all leaderboard scoring logic must live in the backend. Unity may request rankings but must never compute or cache scores. Score updates should be triggered by domain events such as settlement payouts or XP gains.

The service must:

- order players by score descending
- assign dense ranks, with no gaps
- return paginated results

Filtering parameters such as `scope`, `take`, and `afterRank` must be validated. Invalid parameters should return appropriate errors.

### Reset mechanisms

Leaderboards should support resets at configurable intervals, for example daily, weekly, or seasonal. Resets clear or archive existing entries and start a new period.

The `POST /leaderboards/reset` endpoint should:

- be restricted to admin roles
- trigger an event that the service listens for to perform the reset

Do not implement scheduled jobs in this task. Assume that an external scheduler or admin call will initiate resets.

## Sample API contract

### `GET /leaderboards`

Returns a JSON array of leaderboard entries with fields such as `playerId`, `playerName`, `score`, `rank`, and `scope`.

Query parameters:

- `scope`: required. The leaderboard scope, for example `global`, `friends`, or `clan`. Reject unknown values.
- `take`: optional. Maximum number of entries to return. Default `50`, maximum `100`.
- `afterRank`: optional. Rank offset for pagination. When omitted, returns from rank `1`.

```http
GET /leaderboards?scope=global&take=50&afterRank=0
```

### `GET /leaderboards/{playerId}`

Returns a player's rank and neighboring entries, for example ±5 ranks, to provide context.

```http
GET /leaderboards/{playerId}
```

### `POST /leaderboards/reset`

Accepts a JSON body specifying `scope` and `period`, and resets the leaderboard.

Requirements:

- must require admin authorization
- must respond asynchronously with `202 Accepted`
- must publish a domain event to perform the reset

```http
POST /leaderboards/reset
Content-Type: application/json

{
  "scope": "global",
  "period": "weekly"
}
```

## Testing strategy

Use unit tests to validate ranking logic. Given a list of player scores, ensure the service assigns correct dense ranks and returns expected pagination slices.

Use integration tests to validate API endpoints through an in-memory server and database.

Include tests for:

- invalid parameter handling
- unauthorized access to reset
- sorting behavior
- pagination slices
- rank calculation
- reset flow

## Acceptance criteria

- The API returns sorted leaderboards with correct ranks and supports pagination.
- Score updates are triggered by domain events and persisted to the database.
- Requests with invalid parameters, such as unknown scope or negative `take` or `afterRank`, return appropriate error responses.
- The reset endpoint requires admin authorization and performs a leaderboard reset by publishing an event.
- All new code is covered by tests and passes the existing test suite.
- There are no breaking changes to previous phases and no leakage of authoritative logic to the client.

## Completion

Once the 4A leaderboard API is fully implemented and validated, update `MASTER_STATUS.md` to advance the roadmap to the next task and submit a draft pull request per `docs/CODEX_WORKFLOW.md`.
