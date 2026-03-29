# Task 3C – Wear & Tear System

## Goal
Implement a server‑owned degradation model for scaffold components (Wear & Tear).  When players make mistakes or overuse materials, the integrity of each component should decrease deterministically.  This system must penalize improper placement or repeated use, encouraging strategic decisions and maintenance.

## Phase Check
- **Current task:** 3C — Wear & Tear System.
- **Next task:** 3D — Loadout & Fog of War.
- **Why allowed:** After completing 3B, `docs/MASTER_STATUS.md` advances to 3C; wear/tear is part of the Phase 3 meta‑loop.

## Scope
### In scope
- Design a degradation metric and threshold for each component type.
- Update domain models to include an `Integrity` value that decreases based on misuse or time.
- Integrate wear calculations into the validation path: placing a component incorrectly or removing it after placement should reduce its integrity.
- Create endpoints or mutation paths to inspect component health and perform repairs or replacements (consuming resources).
- Implement persistence for component integrity and repair history.
### Out of scope
- Yard capacity/inventory (3A), crew splits (3B), loadout mini‑game (3D).
- Client prediction; all wear logic is server‑side.

## Allowed Files
- Backend domain/application/infrastructure/API/tests as in previous tasks.
- SQL migrations if new fields/tables are needed.
- `docs/tasks/3C_Wear_and_Tear_System.md`.

## Forbidden Files
- Unity client code.
- Workflow or pipeline files.

## Acceptance Criteria
1. Each component has an integrity metric stored and updated server‑side.
2. Incorrect use of components (as defined by validation rules) reduces integrity by a defined amount.
3. Components fail or become unusable when integrity reaches a minimum threshold.
4. Repair endpoints allow integrity to be restored by consuming resources.
5. Unit tests verify wear decreases appropriately and repairs restore integrity.

## Validation
Run restore/build/test commands. Include scenarios for correct vs. incorrect placement and verify wear effects.

## Notes
- Wear should be deterministic and not random; results must be reproducible for auditing.