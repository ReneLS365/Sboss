# Task 5A – Anti‑Cheat Hardening

## Goal
Strengthen cheat detection and prevention for the release.  Validate player performance against theoretical minimums and ensure no client manipulation can produce illicit advantages.  Build comprehensive server‑side checks and instrumentation to detect anomalies.

## Phase Check
- **Current task:** 5A — Anti‑Cheat Hardening.
- **Next task:** 5B — UX Polish & Audio.
- **Why allowed:** Phase 5 is release prep; cheat hardening is the first priority for ensuring a fair launch.

## Scope
### In scope
- Implement detection for impossible scores or completion times based on known level parameters and required physical moves.
- Harden existing validation paths against tampering; ensure all inputs are validated and not trusted from client.
- Record suspicious behaviour and flag accounts for review.
- Add metrics collection to monitor potential exploit patterns.
- Include integration tests verifying detection thresholds and enforcement actions.
### Out of scope
- UX polish, audio, stress tests, or deployment (5B–5E).
- Social sabotage adjustments (already implemented in 4E).

## Allowed Files
- Backend domain/application/infrastructure/API/tests.
- SQL migrations if needed for storing anti‑cheat metrics.
- `docs/tasks/5A_Anti_Cheat_Hardening.md`.

## Forbidden Files
- Client code; anti‑cheat is fully server‑side.
- Workflow/pipeline changes.

## Acceptance Criteria
1. Server rejects or flags runs where completion time or actions fall outside theoretical minimums.
2. All user input is validated; no trust of client‑calculated values.
3. Suspicious runs are logged and marked for review; legitimate runs pass unimpeded.
4. Tests verify detection logic using both legitimate and fraudulent sequences.

## Validation
Run restore/build/test commands and simulate runs with improbable times or sequence speeds. Verify that cheat detection triggers and normal runs remain unaffected.

## Notes
- Keep detection logic configurable to adjust thresholds as needed post‑launch.