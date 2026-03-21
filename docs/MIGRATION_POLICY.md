# Migration Policy

## Purpose
Keep schema changes deterministic, ordered, reviewable, and safe.

## Rules
- Every schema change must be additive unless a removal is explicitly approved.
- Migration files must be ordered and immutable after merge.
- Seed logic must run only after the full required migration chain.
- Runtime code must not silently create or mutate schema outside migrations.

## Naming
Use ordered filenames.
Example:
- `0001_phase_1b_baseline.sql`
- `0002_phase_1d_economy_tables.sql`

## Required contents
Each migration must clearly express:
- target tables
- indexes
- constraints
- defaults
- version expectations

## Seed rules
- seed data must be deterministic
- no `NOW()` or unstable timestamps in baseline seed data
- seed files must be safe to run in the intended test/bootstrap context

## Review checklist
Before merge:
- migration order verified
- seed ordering verified
- schema guardrail tests updated if needed
- integration tests still pass from clean database state

## Forbidden
- hidden schema drift in app startup
- runtime-only table creation
- non-deterministic seed content
- destructive migration edits after merge
