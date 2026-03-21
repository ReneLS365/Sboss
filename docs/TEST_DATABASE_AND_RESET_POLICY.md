# Test Database and Reset Policy

## Purpose
This document defines how SBoss integration tests provision, connect to, reset, and validate PostgreSQL databases.

## Rules
- Integration tests must never point at the default development database.
- Test resets must run against an isolated database only.
- The workflow database name and the fixture database name must always match.
- Reset logic must be deterministic and safe under retries.

## Required environment variable
`SBOSS_TEST_DATABASE`

Example:
```text
Host=localhost;Port=5432;Database=sboss_tests;Username=sboss;Password=sboss_dev_password
```

## Allowed test database names
- `sboss_tests`
- `sboss_tests_<suffix>` for parallel or isolated runs

## Forbidden test database names
- `sboss`
- any shared development or production database name

## CI policy
The GitHub Actions Postgres service must provision the same database referenced by `SBOSS_TEST_DATABASE`.

Required alignment:
- `POSTGRES_DB=sboss_tests`
- healthcheck targets `sboss_tests`
- `SBOSS_TEST_DATABASE` uses `Database=sboss_tests`

## Reset strategy
### Preferred
- dedicated non-pooled reset connection
- truncate application tables
- restart identities where relevant
- preserve database existence
- re-apply seed deterministically

### Allowed only when proven safe
- drop/create schema during test startup

### Forbidden
- resetting shared dev databases
- relying on implicit database creation
- destructive resets on pooled active sessions without teardown

## Pooling policy
Before reset, dispose or clear any tracked Npgsql data sources that can keep active sessions alive.
Use a non-pooled connection string for the reset path.

## Required checks
- fixture rejects forbidden database names
- schema/migration guardrail tests assert migration ordering
- workflow config must stay aligned with fixture expectations

## Failure handling
When a reset-related failure occurs:
1. confirm actual database name
2. confirm database exists
3. confirm reset connection is non-pooled
4. inspect active pooling lifecycle
5. if `DROP SCHEMA` keeps killing sessions, replace it with truncate-based reset
