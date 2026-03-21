# Release and Hotfix Runbook

## Purpose
Provide a repeatable release and emergency repair path.

## Normal release flow
1. Confirm `main` is green.
2. Confirm required checks are passing.
3. Confirm no pending migration ambiguity.
4. Merge approved PRs.
5. Deploy using the standard pipeline.
6. Verify application health, logs, and critical API paths.
7. Record release note or tag if relevant.

## Hotfix flow
1. Identify exact production-impacting failure.
2. Create smallest possible hotfix branch.
3. Avoid unrelated cleanup.
4. Run local validation.
5. Open PR.
6. Fast-track review.
7. Merge only with explicit acknowledgement of risk.
8. Verify after deploy.
9. Open follow-up cleanup task if debt was introduced.

## Rollback rule
Rollback is preferred over improvising speculative fixes on a broken release.

## Break-glass checklist
- reason documented
- owner identified
- rollback path known
- protections restored after emergency action
