# Task 5C – Load & Stress Test

## Goal
Assess the system’s performance under peak load conditions.  Simulate high concurrency scenarios around daily challenge peaks, large leaderboards, and mass ghost submissions.  Identify bottlenecks in the API, database, and game logic, and recommend optimizations.

## Phase Check
- **Current task:** 5C — Load & Stress Test.
- **Next task:** 5D — MVP Scope Lock & QA.
- **Why allowed:** After user‑facing polish, the release prep phase requires ensuring that the system can scale.

## Scope
### In scope
- Design and implement load testing scripts or tools (e.g. k6, JMeter, custom harness) to simulate concurrent API calls across validation, scoring, leaderboard, and challenge endpoints.
- Set up staging environments that mirror production infrastructure.
- Capture metrics (latency, throughput, error rates) and profile database queries under load.
- Identify critical paths and propose optimizations (indexes, caching, code improvements) without implementing them here.
### Out of scope
- Implementing the optimizations themselves; these may be part of 5D or 5E.
- Client‑side performance profiling (focus is server side).

## Allowed Files
- Testing harness or scripts (may live under `scripts/` or `tests/`).
- Infrastructure configurations for staging if needed.
- `docs/tasks/5C_Load_and_Stress_Test.md`.

## Forbidden Files
- Core gameplay logic files; this task is about testing, not feature changes.

## Acceptance Criteria
1. A repeatable load test suite exists that can be run in CI or manually.
2. Test reports identify performance under high concurrency across major services.
3. Bottlenecks are documented and prioritized for remediation.
4. Server does not crash under expected peak loads; error rates remain within acceptable thresholds.

## Validation
Run load tests in a staging environment. Provide metrics and analysis. Confirm that baseline performance is documented and regression tests can compare future improvements.

## Notes
- Coordinate with infrastructure teams to simulate production‑like conditions.