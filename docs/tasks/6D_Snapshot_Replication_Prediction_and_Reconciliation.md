# Task 6D – Snapshot Replication, Prediction & Reconciliation

## Goal
Replicate authoritative live-session state to Unity and add prediction/reconciliation without breaking backend authority.

## Phase Check
- **Current task:** 6D — Snapshot Replication, Prediction & Reconciliation.
- **Next task:** 6E — Persistence & Meta-loop Bridge.
- **Why allowed:** Prediction and reconciliation depend on a working session runtime and transport.

## Scope
### In scope
- Define and stream authoritative session snapshots or deltas.
- Implement Unity-side prediction for immediate responsiveness.
- Implement rollback and reconciliation against authoritative server state.
- Add baseline interest management to limit unnecessary replication.
- Measure prediction error, rollback frequency, and bandwidth.
### Out of scope
- Changing economy, progression, or scoring authority.
- Broad UI/UX polish unrelated to replication.
- Deployment scaling work beyond what is needed to validate replication.

## Allowed Files
- Backend session replication paths.
- Unity client prediction/reconciliation paths.
- Shared transport contracts for session state messages.
- `docs/tasks/6D_Snapshot_Replication_Prediction_and_Reconciliation.md`

## Forbidden Files
- Client-authoritative scoring/economy/progression.
- `.github/workflows/**`

## Acceptance Criteria
1. Clients receive authoritative state replication for live sessions.
2. Unity prediction improves responsiveness without owning final outcomes.
3. Rollback/reconciliation corrects divergence deterministically.
4. Tests cover out-of-order updates, corrections, and prediction mismatch cases.

## Validation
- Build/test for backend and client session paths.
- Deterministic replay/reconciliation test scenarios.

## Notes
- Reconciliation policy must remain explicit and measurable.
