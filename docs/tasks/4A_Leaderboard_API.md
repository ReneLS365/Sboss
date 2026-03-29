# Task 4A – Leaderboard API

## Goal
Create a backend service that exposes leaderboard standings across global, regional, and crew categories.  The API will allow clients to fetch rankings based on various filters (e.g. top 100 worldwide, crew‑specific leaderboard).  Data must be based on authoritative scores recorded in Phase 2 and Phase 3 activities.

## Phase Check
- **Current task:** 4A — Leaderboard API.
- **Next task:** 4B — Deterministic Level Generator.
- **Why allowed:** After completing Phase 3, the roadmap moves into Phase 4 (Asynchronous Competition) starting with leaderboards.

## Scope
### In scope
- Design a schema for storing leaderboard entries keyed by player, crew, and region.
- Implement a query API that returns sorted scores, supports pagination, and filters (global, regional, crew).
- Provide endpoint(s) for updating leaderboard entries when new scores are posted or existing scores improve.
- Ensure secure and performant retrieval of leaderboard slices without exposing complete data sets.
- Consider caching strategies for frequently accessed leaderboard data.
### Out of scope
- Deterministic level generation (4B), ghost data pipeline (4C), daily challenge logic (4D), or social push features (4E).
- Client UI for leaderboards.
- Real‑time netcode; competition remains asynchronous.

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations for leaderboard tables.
- `docs/tasks/4A_Leaderboard_API.md`.

## Forbidden Files
- Unity client code for leaderboard display.
- Workflow or pipeline configuration.

## Acceptance Criteria
1. Leaderboard entries persist scores and metadata (player ID, crew ID, region).
2. The API returns ordered leaderboard slices with configurable page size and filters.
3. Higher scores replace lower scores for the same player in a category.
4. Security measures prevent unauthorized modification or scraping of entire leaderboards.
5. Tests verify correct ordering, pagination, and update logic.

## Validation
Run restore/build/test commands and simulate posting scores, then fetching leaderboards with different filters. Ensure pagination and ordering behave correctly.

## Notes
- Use indexes in PostgreSQL for performance on sorted queries.