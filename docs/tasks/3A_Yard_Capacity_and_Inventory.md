# Task 3A – Yard Capacity & Inventory

## Goal
Design and implement the logistics core for players’ **Yard** (depot).  The Yard must track a player’s maximum storage capacity and maintain an inventory of purchased scaffold components.  The server must enforce hard‐caps (e.g. a starting capacity of 1500 units) and reject purchases or contract acceptance if they exceed the available capacity or inventory.

## Phase Check
- **Current task:** 3A — Yard Capacity & Inventory (as listed in `docs/MASTER_STATUS.md`).
- **Next task:** 3B — Akkord & Crew Split.  We must not begin crew management or wage splitting here.
- **Why allowed:** `docs/MASTER_STATUS.md` shows 3A as the active task; prior tasks up to 2F have been completed.

## Scope
### In scope
- Persist authoritative yard capacity per account with a default hard-cap baseline.
- Persist authoritative scaffold inventory per account and expose authoritative yard snapshots.
- Add exactly two backend endpoints for this slice:
  - Read yard state for an account.
  - Purchase supported scaffold components for an account.
- Enforce server-side purchase rejection for:
  - quantity <= 0
  - unknown itemCode
  - missing account
  - insufficient funds
  - resulting capacity overflow
- Replace the existing placeholder placement gating path so match-result placement validation rejects sequences that require more owned components than the account inventory contains.
- Add narrow migration + tests for the above behavior only.
### Out of scope
- Any Unity/client change.
- Any 3B+ behavior (crew split, wages, wear/tear, loadout/fog, XP, leaderboards, ghost data).
- Economy redesigns, scoring redesigns, command queue redesigns, broad refactors, or shared abstraction prep.

## Allowed Files (Frozen)
- `docs/tasks/3A_Yard_Capacity_and_Inventory.md`
- `src/backend/Sboss.Api/Program.cs`
- `src/backend/Sboss.Contracts/Yard/GetYardStateResponse.cs`
- `src/backend/Sboss.Contracts/Yard/PostYardPurchaseRequest.cs`
- `src/backend/Sboss.Contracts/Yard/PostYardPurchaseResponse.cs`
- `src/backend/Sboss.Infrastructure/ServiceCollectionExtensions.cs`
- `src/backend/Sboss.Infrastructure/Repositories/IYardRepository.cs`
- `src/backend/Sboss.Infrastructure/Repositories/PostgresYardRepository.cs`
- `src/backend/Sboss.Infrastructure/Services/AuthoritativeComponentCatalog.cs`
- `src/backend/Sboss.Infrastructure/Services/IAuthoritativeComponentCatalog.cs`
- `src/backend/db/migrations/0005_phase_3a_yard_capacity_inventory.sql`
- `src/backend/db/seed.sql`
- `src/backend/tests/Sboss.Api.Tests/PostgresDatabaseFixture.cs`
- `src/backend/tests/Sboss.Api.Tests/MatchResultsContractTests.cs`
- `src/backend/tests/Sboss.Api.Tests/YardEndpointsTests.cs`

## Forbidden Files
- `docs/MASTER_STATUS.md`
- `src/client/unity/**`
- `.github/workflows/**`
- Any file not listed in **Allowed Files (Frozen)**.

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

## Non-goals
- No new architectural layer beyond a narrow repository/service extension used by these two endpoints and placement gating.
- No additional yard endpoints, no UI DTO expansion beyond current slice needs, no broad inventory model rewrite.
- No prep work for 3B/3C/3D/3E.
