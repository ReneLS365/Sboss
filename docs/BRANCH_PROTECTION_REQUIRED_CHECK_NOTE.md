# Branch Protection Required Check Note

This note clarifies the live branch protection configuration until `docs/BRANCH_PROTECTION_AND_MERGE_POLICY.md` is updated in place.

## Canonical required status check
The active GitHub ruleset for `main` requires this exact status check context:
- `backend-ci / build-test`

Do not use the ruleset name as the required status check value.
The ruleset name may differ from the emitted GitHub Actions status context.

## Review rule currently enabled in the live ruleset
The active ruleset also requires:
- approval of the most recent reviewable push

That means the latest reviewable push must be approved before merge.
This is stricter than a basic 1-approval rule and should be reflected in the main policy document.

## Operational rule
When configuring required checks in branch protection, always copy the exact live status context shown on the PR checks tab.
Do not guess from workflow filenames, ruleset names, or old notes.
