# Task 3D – Loadout & Fog of War

## Goal
Build a timed, spatial mini‑game for packing the service van (Loadout) and introduce a Fog‑of‑War mechanic that hides parts of the level until the player reaches them.  Players must organize components to fit within the van’s space constraints; incorrect packing or omissions will impact their ability to complete the job.

## Phase Check
- **Current task:** 3D — Loadout & Fog of War.
- **Next task:** 3E — XP & Progression.
- **Why allowed:** `docs/MASTER_STATUS.md` shows 3D as the task following Wear & Tear; this mini‑game deepens the meta‑loop without venturing into Phase 4 features.

## Scope
### In scope
- Implement a server‑side representation of the van’s cargo space and rules for how components can be arranged.
- Provide an API to submit a loadout arrangement; server validates packing legality and completeness.
- Introduce a Fog‑of‑War system that determines which parts of a level are initially hidden and reveals them based on player progress or triggers; the server must track and serve fog state.
- Persist loadout configurations and fog state.
- Ensure loadout impacts subsequent tasks (e.g. missing components results in early failure).
### Out of scope
- XP and progression (3E).
- Leaderboards or asynchronous competition (Phase 4).
- Client‑side solving of the packing puzzle; the server authoritatively validates the arrangement.

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations for loadout/fog tables if needed.
- `src/client/unity/**` only for visual representation; no authoritative logic on the client.
- `docs/tasks/3D_Loadout_and_Fog_of_War.md`.

## Forbidden Files
- Workflow config and CI files.
- Phase 4 or 5 feature directories.

## Acceptance Criteria
1. The server maintains a cargo space model and verifies that a submitted loadout fits within space constraints.
2. Fog‑of‑War state is persisted server‑side and updated deterministically as players progress.
3. A loadout submission missing required components triggers deterministic failure when the player attempts to place those components.
4. API endpoints return clear statuses for valid vs. invalid loadouts and fog reveals.
5. Unit/integration tests cover packing success/failure and fog reveal logic.

## Validation
Use restore/build/test commands plus additional scenarios: submit different loadouts and ensure server responses match expectations; simulate level progress to verify fog reveals.

## Notes
- The packing puzzle should be solved client‑side, but only validated on the server.