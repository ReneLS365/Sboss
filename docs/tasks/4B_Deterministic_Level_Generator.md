# Task 4B – Deterministic Level Generator

## Goal
Develop a level generator that produces mathematically identical levels from a given seed.  This ensures fairness for asynchronous competition by guaranteeing that two players with the same seed play the exact same level layout and challenge parameters.

## Phase Check
- **Current task:** 4B — Deterministic Level Generator.
- **Next task:** 4C — Ghost Data Pipeline.
- **Why allowed:** The roadmap calls for a deterministic generator after leaderboards to support reproducible competition.

## Scope
### In scope
- Design algorithms for generating level topology, component placement, and environmental variables based solely on a seed input.
- Ensure that the generator is pure and produces identical output for identical seeds across different machines.
- Store generated level data (or generation parameters) so that the same seed yields the same results later.
- Add API endpoints for requesting new seeds and retrieving existing seeds’ configuration.
- Add integration tests to verify reproducibility across multiple runs.
### Out of scope
- Leaderboard ranking logic (4A), ghost recording (4C), or daily challenges (4D).
- On‑the‑fly modifications of levels after generation.

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations if needed to record seeds and level parameters.
- `docs/tasks/4B_Deterministic_Level_Generator.md`.

## Forbidden Files
- Unity client code implementing generation; the server must generate and serve levels.
- Workflow scripts.

## Acceptance Criteria
1. Given a specific seed, the generator returns the exact same level configuration each time.
2. New seed requests generate unique seeds; repeated calls with the same seed return the same configuration.
3. Integration tests validate deterministic output across multiple runs and machine environments.

## Validation
Run restore/build/test commands and include seed reproducibility tests. Use `rg` to confirm no generation logic leaked to the client.

## Notes
- Consider using pure functional constructs in C# where possible; avoid static mutable state in generation.