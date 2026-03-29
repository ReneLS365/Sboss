# Task 3B – Akkord & Crew Split

## Goal
Introduce a backend system for managing workers (svende and lærlinge) and splitting earnings (akkord).  This involves tracking the crew composition, assigning roles, and calculating profit shares based on predetermined ratios (e.g. 60/40 between workers and company).  The server must enforce these splits and reflect them in payouts.

## Phase Check
- **Current task:** 3B — Akkord & Crew Split (per `docs/MASTER_STATUS.md`).
- **Next task:** 3C — Wear & Tear System.
- **Why allowed:** 3A is marked complete; 3B is the next defined step in Phase 3 (Company & Meta‑loop).

## Scope
### In scope
- Define domain models for `CrewMember`, `CrewRole`, and a `Crew` aggregate representing the group working on a job.
- Create logic to split earnings based on role (e.g. svende vs lærlinge) and configured ratios.
- Integrate with the existing transaction service to credit payouts to individual crew accounts.
- Add API endpoints for forming a crew, assigning members, and reviewing earnings splits.
- Provide migration to support storing crew memberships and roles.
### Out of scope
- Any prediction, placement, or capacity logic from previous phases.
- Wear & Tear (3C) and XP/Progression systems (3E).
- UI implementation in Unity.

## Allowed Files
- Backend domain/application/infrastructure/API/tests directories similar to 3A.
- SQL migration scripts under `src/backend/db/scripts/` as needed.
- `docs/tasks/3B_Akkord_and_Crew_Split.md` (this file).

## Forbidden Files
- `src/client/unity/**` – no client logic.
- `.github/workflows/**` – no CI modifications.
- `src/backend/Sboss.Scoring/**` – scoring is already done.

## Acceptance Criteria
1. The server can create and persist crews with assigned members and roles.
2. Earnings are split according to configured ratios and credited to crew members via the transaction service.
3. Endpoints validate that only authorized players can join or leave a crew.
4. Unit tests verify correct split calculations and payout flows.
5. Database migrations apply cleanly.

## Validation
Run usual restore/build/test commands, plus transaction service integration tests. Verify no crew logic leaks client-side.

## Notes
- Consider performance when updating payouts; splits should be calculated deterministically.