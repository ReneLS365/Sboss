# Task 3A – Yard Capacity & Inventory

## Goal
Design and implement the logistics core for players’ **Yard** (depot).  The Yard must track a player’s maximum storage capacity and maintain an inventory of purchased scaffold components.  The server must enforce hard‐caps (e.g. a starting capacity of 1500 units) and reject purchases or contract acceptance if they exceed the available capacity or inventory.

## Phase Check
- **Current task:** 3A — Yard Capacity & Inventory (as listed in `docs/MASTER_STATUS.md`).
- **Next task:** 3B — Akkord & Crew Split.  We must not begin crew management or wage splitting here.
- **Why allowed:** `docs/MASTER_STATUS.md` shows 3A as the active task; prior tasks up to 2F have been completed.

## Scope
### In scope
- Define domain entities for `Yard`, `YardCapacity` and `InventoryItem` in the backend (`Sboss.Domain`).
- Represent each component type’s unit size/weight (space usage) and maintain totals per player.
- Implement PostgreSQL tables and an EF Core repository/service to persist the Yard and Inventory state.
- Add API endpoints in `Sboss.Api` to purchase components and query current inventory/capacity.
- Validate purchases against the player’s remaining capacity and against the economic balance (reuse the Phase 1 transaction service).
- Implement gating logic that rejects a contract start if the job’s material requirements exceed the player’s inventory or capacity.
- Provide additive SQL migration script(s) to create new tables/columns.
### Out of scope
- Client‑side UI/UX for showing the Yard or shopping (Unity remains a dumb client).
- Any “Wear & Tear” or material degradation logic (belongs in 3C).
- Crew wage splitting or performance (belongs in 3B).
- Changes to game economy aside from purchasing scaffold components.

## Allowed Files
- `src/backend/Sboss.Domain/**` – domain models and business logic for Yard and Inventory.
- `src/backend/Sboss.Application/**` – application services orchestrating Yard operations.
- `src/backend/Sboss.Contracts/**` – DTOs for Yard requests/responses.
- `src/backend/Sboss.Infrastructure/**` – EF Core persistence, repositories.
- `src/backend/Sboss.Api/**` – API endpoints for Yard operations.
- `src/backend/tests/**` – unit/integration tests for Yard logic.
- `src/backend/db/scripts/**` – additive SQL migration for Yard tables.
- `docs/tasks/3A_Yard_Capacity_and_Inventory.md` (this file).

## Forbidden Files
- `src/client/unity/**` – no client logic; Unity remains dumb.
- `.github/workflows/**` – no CI or workflow changes in this task.
- `src/backend/Sboss.Scoring/**` or scoring logic; scoring is covered in earlier tasks.
- Changes to `docs/MASTER_STATUS.md` – roadmap updates happen outside this task.

## Acceptance Criteria
1. A player’s Yard capacity and inventory are persisted and modifiable via server APIs.
2. Component purchases are accepted only if the cost can be paid and total space used does not exceed the Yard’s capacity.
3. Contract/gate validation rejects tasks if the required materials exceed current inventory or available capacity.
4. Inventory operations are atomic and server-authoritative – no client assumptions about success.
5. The SQL migration applies cleanly and is additive; database resets for tests succeed.
6. Unit tests cover successful purchases, over-capacity rejections, and gating logic.

## Validation
Run:
```
python3 scripts/validate-roadmap-status.py
dotnet restore Sboss.sln
dotnet build Sboss.sln -warnaserror
dotnet test Sboss.sln -warnaserror
```
Ensure that `SBOSS_TEST_DATABASE` is configured and that the new Yard tables do not break existing tests. Use `rg` to verify no backend Yard logic leaks into `src/client/unity/`.

## Notes
- Use asynchronous C# calls and respect existing domain patterns (e.g. aggregate roots, repositories).
- Keep changes small and modular to avoid scope creep.
- Document any new events or domain invariants.