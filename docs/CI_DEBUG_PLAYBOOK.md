# CI Debug Playbook

## Purpose
This playbook defines the fastest deterministic path from a red GitHub Actions run to a verified root cause and a safe fix.

## Scope
- .NET 8 backend
- PostgreSQL service container
- Npgsql integration tests
- GitHub Actions
- PR and main branch failures

## Golden rule
Do not guess from old screenshots or stale logs.
Always inspect the latest failing run tied to the current PR head SHA.

## Triage order
1. Confirm PR number, head branch, and head SHA.
2. Inspect the latest failing workflow run.
3. Classify the failure before changing code.
4. Reproduce locally only after log-based classification.
5. Apply the smallest safe fix.
6. Re-run failed jobs.
7. Merge only after green checks.

## Core commands
```bash
# PR state
gh pr view <PR_NUMBER> -R ReneLS365/Sboss --json title,url,headRefName,headRefOid,baseRefName,statusCheckRollup

# Watch checks live
gh pr checks <PR_NUMBER> -R ReneLS365/Sboss --watch

# Recent runs for branch
gh run list -R ReneLS365/Sboss -b <BRANCH_NAME> --limit 10

# Failed logs only
gh run view <RUN_ID> -R ReneLS365/Sboss --log-failed

# Re-run only failed jobs with debug
gh run rerun <RUN_ID> -R ReneLS365/Sboss --failed --debug

# Download artifacts
gh run download <RUN_ID> -R ReneLS365/Sboss
```

## Failure classes
### 1. Build failure
Symptoms:
- `dotnet build` fails before tests start
- compile error, missing symbol, inaccessible API, wrong namespace

Action:
- inspect changed files only
- verify package version in csproj
- remove unsupported API usage
- rebuild locally

### 2. Test database provisioning failure
Symptoms:
- database does not exist
- connection refused
- cannot connect to Postgres
- fail happens before migrations or schema reset

Action:
- verify `.github/workflows/backend-ci.yml`
- verify `POSTGRES_DB`
- verify healthcheck database name
- verify `SBOSS_TEST_DATABASE`
- ensure fixture and workflow point to the same isolated database

### 3. Schema reset / pooling failure
Symptoms:
- `57P01`
- terminated connection
- missing relation after reset
- intermittent Npgsql failures after schema reset

Action:
- inspect `PostgresDatabaseFixture`
- inspect pooling and data source lifecycle
- prefer truncate/reset identity over destructive schema drop if active session conflicts continue
- keep tests non-pooled for reset connection

### 4. Migration ordering failure
Symptoms:
- missing table
- seed fails because baseline migration did not run first
- migration file order drift

Action:
- verify migration list order
- verify seed runs only after full migration chain
- add static guardrail tests if ordering is not already asserted

### 5. Governance failure
Symptoms:
- roadmap validation fails
- docs/status guardrail fails
- branch policy or PR metadata checks fail

Action:
- inspect `PLANS.md`, `MASTER_STATUS.md`, roadmap guardrails, and workflow validation scripts
- do not bypass policy checks silently

## Local reproduction
```bash
# checkout PR
gh pr checkout <PR_NUMBER> -R ReneLS365/Sboss

# start local Postgres matching CI
docker run --rm --name sboss-pg \
  -e POSTGRES_USER=sboss \
  -e POSTGRES_PASSWORD=sboss_dev_password \
  -e POSTGRES_DB=sboss_tests \
  -p 5432:5432 \
  postgres:16

# set test DB env
export SBOSS_TEST_DATABASE="Host=localhost;Port=5432;Database=sboss_tests;Username=sboss;Password=sboss_dev_password"

# run same flow as CI
python3 scripts/validate-roadmap-status.py
dotnet restore Sboss.sln
dotnet build Sboss.sln --configuration Release -warnaserror --no-restore
dotnet test Sboss.sln --configuration Release -warnaserror --no-build
```

## Required artifacts on failure
- test TRX files
- workflow summary
- failed step logs
- exact PR head SHA

## Merge rule
A failure is not fixed until the latest PR head run is green.
