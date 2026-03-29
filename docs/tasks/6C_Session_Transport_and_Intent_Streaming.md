# Task 6C – Session Transport & Intent Streaming

## Goal
Add low-latency bidirectional transport between Unity clients and backend live sessions.

## Phase Check
- **Current task:** 6C — Session Transport & Intent Streaming.
- **Next task:** 6D — Snapshot Replication, Prediction & Reconciliation.
- **Why allowed:** Transport work depends on the session architecture and runtime being defined first.

## Scope
### In scope
- Implement the selected real-time transport from 6A for session intents and server updates.
- Authenticate and authorize session connections.
- Stream client intents to backend sessions and stream server acknowledgements/events back.
- Handle reconnect, disconnect, timeout, and session-resume policies.
- Instrument latency, dropped updates, and transport health.
### Out of scope
- Client-side prediction and reconciliation.
- Reworking non-session REST APIs.
- Social, leaderboard, or post-session meta-loop expansion.

## Allowed Files
- Backend transport/session integration paths.
- Unity transport adapter paths if needed for non-authoritative networking glue only.
- `docs/tasks/6C_Session_Transport_and_Intent_Streaming.md`

## Forbidden Files
- Moving authority to the client.
- `.github/workflows/**`
- Unrelated meta-loop features.

## Acceptance Criteria
1. Authenticated clients can join a live session over the selected transport.
2. Player intents reach the authoritative session runtime with acknowledgements.
3. Disconnect and reconnect behavior is defined and tested.
4. Transport metrics are observable.

## Validation
- Runtime and integration tests for join/send/reconnect flows.
- Manual transport smoke test if applicable.

## Notes
- Transport remains a carrier only; the backend remains final arbiter.
