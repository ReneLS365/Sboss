# Task 4E – Social Push & Sabotage

## Goal
Develop lightweight social features that allow players to send notifications when they beat another player’s score and perform mild sabotage actions asynchronously.  Notifications should encourage friendly competition without enabling griefing or real‑time harassment.

## Phase Check
- **Current task:** 4E — Social Push & Sabotage.
- **Next task:** 5A — Anti‑Cheat Hardening.
- **Why allowed:** After asynchronous challenges are in place (4D), social features enrich the competitive experience without requiring synchronous multiplayer.

## Scope
### In scope
- Implement a notification system to inform a player when their score has been surpassed (push), including minimal metadata (player ID, new score, seed/challenge).
- Create server‑side definitions of limited sabotage actions (e.g. temporary capacity reduction or forcing a small penalty) that can be applied asynchronously to another player’s next run.
- Ensure sabotage is balanced: it must not permanently harm progress or cause total job failure.
- Add APIs to send and receive social actions, with rate limiting and permission checks.
- Persist social actions and their effects.
### Out of scope
- Real‑time chat or high‑impact PvP mechanics.
- Anti‑cheat enforcement (belongs to 5A).

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations for social notifications and sabotage records.
- `docs/tasks/4E_Social_Push_and_Sabotage.md`.

## Forbidden Files
- Client logic that bypasses server authority or leaks personal data.
- `.github/workflows/**` changes.

## Acceptance Criteria
1. The server can send and store notifications about surpassed scores; recipients can retrieve them via API.
2. Sabotage actions are server‑validated, limited in scope, and applied only once to the target player’s next relevant run.
3. Rate limits and permissions prevent abuse; players cannot spam sabotage.
4. Tests confirm proper persistence, notifications, rate limits, and boundaries of sabotage effects.

## Validation
Run restore/build/test commands and simulate multiple push and sabotage scenarios. Ensure effects are limited and reversible.

## Notes
- Social actions should encourage competition but respect fairness and data privacy.