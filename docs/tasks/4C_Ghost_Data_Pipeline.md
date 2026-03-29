# Task 4C – Ghost Data Pipeline

## Goal
Implement a pipeline to record, store, and replay players’ build sequences as lightweight JSON (“ghost data”).  This enables asynchronous competitions where players can watch or compete against a recorded run without network synchronization.

## Phase Check
- **Current task:** 4C — Ghost Data Pipeline.
- **Next task:** 4D — Daily Challenge System.
- **Why allowed:** After deterministic levels (4B), ghost replays are needed for asynchronous challenges and social features.

## Scope
### In scope
- Capture sequence of validated placement intents and timing data during gameplay and serialize into a compact ghost format.
- Design a schema to store ghost data keyed by player, seed, and timestamp.
- Provide an API to submit ghost recordings and fetch ghosts for a specific seed and player or for leaderboards.
- Implement replay functionality on the backend to serve ghost data to clients; the client will play back the sequence visually but must not treat it as authoritative.
- Add tests to verify correct capture and retrieval of ghost data.
### Out of scope
- Real‑time netcode or live replays.
- Daily challenge scheduling (belongs to 4D).

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations for ghost storage.
- `docs/tasks/4C_Ghost_Data_Pipeline.md`.

## Forbidden Files
- Client code that treats ghost data as authoritative game state.
- Workflow or CI files.

## Acceptance Criteria
1. Validated placement sequences and their timing are recorded into ghost files.
2. Ghost data can be requested and returned via API for given seed/player combinations.
3. The pipeline stores data efficiently to avoid bloat.
4. Tests confirm that ghosts play back exactly the recorded sequence and timing.

## Validation
Run restore/build/test commands. Verify that ghost data round‑trips: record, persist, fetch, and play back exactly the same sequence.

## Notes
- Keep ghost files minimal; avoid including any derived scores or server secrets.