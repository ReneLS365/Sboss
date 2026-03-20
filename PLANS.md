# Sboss Phase Plan

## Current Phase
- **Current_phase:** 1 (Authoritative Core Domain)
- **Execution_mode:** Follow `docs/MASTER_STATUS.md` and complete roadmap tasks in sequence.
- **Architecture_lock:** Server-authoritative backend, Unity dumb client.
- **Workflow_lock:** Plan-first, scoped changes, update plan as work progresses.

---

## Task Record — P1A-PREFLIGHT-BASELINE-REPAIR
- **Task ID:** P1A-PREFLIGHT-BASELINE-REPAIR
- **Title:** Phase 1A preflight baseline repair and Phase 1 doc alignment
- **Phase:** Phase 1 — Authoritative Core Domain
- **Status:** DONE
- **Branch:** work
- **PR:** #7 (merged)
- **Scope:**
  - Align active contributor docs that still present Phase 0-only execution rules.
  - Align in-memory season and level-seed fixtures with `src/backend/db/seed.sql`.
  - Add/adjust tests so fixture drift is caught automatically before 1A implementation starts.
- **Allowed files:**
  - `PLANS.md`
  - `docs/FINAL_ARCHITECTURE.md`
  - `docs/repo/GITHUB_WORKFLOW.md`
  - `docs/repo/LABEL_TAXONOMY.md`
  - `docs/api/API_CONTRACTS.md`
  - `.github/repo-labels.json`
  - `src/backend/Sboss.Infrastructure/Repositories/InMemorySeasonRepository.cs`
  - `src/backend/Sboss.Infrastructure/Repositories/InMemoryLevelSeedRepository.cs`
  - `src/backend/db/seed.sql`
  - `src/backend/tests/Sboss.Api.Tests/LevelSeedsEndpointTests.cs`
  - `src/backend/tests/Sboss.Api.Tests/MatchResultsContractTests.cs`
  - `src/backend/tests/Sboss.Api.Tests/HealthEndpointTests.cs`
  - `src/backend/tests/Sboss.Api.Tests/SchemaSanityTests.cs`
  - `src/backend/tests/Sboss.Api.Tests/SeasonEndpointTests.cs`
- **Non-goals:**
  - No new domain invariants or state machines.
  - No database repository implementation.
  - No auth, economy, progression, multiplayer, or endpoint expansion.
  - No schema redesign or broad domain-model refactor.
- **Acceptance criteria:**
  - Active docs no longer present Phase 0 as the operational frame for current work.
  - In-memory season and level-seed fixtures match `src/backend/db/seed.sql`.
  - Positive API tests cover the current season endpoint and known level-seed lookup.
  - No new runtime surface area is introduced.
- **Blockers:** None.
- **Follow-up review actions (2026-03-19):**
  - Tighten `SchemaSanityTests` so seed validation targets the `seasons` and `level_seeds` insert rows directly rather than matching GUIDs anywhere in `src/backend/db/seed.sql`.
- **Last updated:** 2026-03-19

---

## Task Record — P1A-AUTHORITATIVE-DOMAIN-AND-CONTRACT-SEPARATION
- **Task ID:** P1A-AUTHORITATIVE-DOMAIN-AND-CONTRACT-SEPARATION
- **Title:** Phase 1A authoritative domain entities and contract separation
- **Phase:** Phase 1 — Authoritative Core Domain
- **Status:** DONE
- **Branch:** work
- **PR:** #9 (merged)
- **Scope:**
  - Replace anemic backend entities for Account, Season, LevelSeed, and MatchResult with controlled authoritative domain models.
  - Keep transport contracts in `Sboss.Contracts` and move domain rules/state ownership into `Sboss.Domain`.
  - Update repositories and the current HTTP slice so domain entities are persisted and mapped explicitly to contracts.
  - Add tests that prove invariant enforcement and current endpoint stability.
- **Allowed files:**
  - `PLANS.md`
  - `docs/MASTER_STATUS.md`
  - `src/backend/Sboss.Domain/**`
  - `src/backend/Sboss.Contracts/**`
  - `src/backend/Sboss.Infrastructure/Repositories/**`
  - `src/backend/Sboss.Api/Program.cs`
  - `src/backend/Sboss.Api/Validation/**`
  - `src/backend/tests/Sboss.Api.Tests/**`
- **Non-goals:**
  - No gameplay systems, matchmaking, economy transaction engine, or auth expansion.
  - No tick engine or roadmap work beyond Phase 1A.
  - No schema redesign unless a strict Phase 1A requirement forces it.
  - No unrelated refactors outside the current HTTP surface.
- **Acceptance criteria:**
  - Account, Season, LevelSeed, and MatchResult enforce validated construction with no arbitrary public mutation.
  - Domain validation status is constrained and not represented as a free-form string in the domain.
  - Repository interfaces and implementations use domain entities or domain-safe results rather than transport DTOs.
  - Existing endpoints remain stable for `GET /api/v1/seasons/current`, `GET /api/v1/level-seeds/{seedId}`, and `POST /api/v1/match-results`.
  - Domain and endpoint tests prove invariant failures and invalid match-result rejection.
  - `docs/MASTER_STATUS.md` is advanced only if restore/build/test and acceptance checks all succeed.
- **Blockers:** None.
- **Follow-up review actions (2026-03-19):**
  - Validate and normalize Account and Season mutation inputs into local variables before applying state changes so failed validation cannot partially mutate aggregates.
  - Add regression tests that prove invalid account and season updates leave existing entity state unchanged, including explicit null-string handling.
- **Last updated:** 2026-03-19

---

## Task Record — P1B-DATABASE-SCHEMA-MIGRATION-BASELINE
- **Task ID:** P1B-DATABASE-SCHEMA-MIGRATION-BASELINE
- **Title:** Phase 1B database schema and migration baseline
- **Phase:** Phase 1 — Authoritative Core Domain
- **Status:** DONE
- **Branch:** work
- **PR:** #10 (merged)
- **Scope:**
  - Establish a real migration baseline for the existing PostgreSQL schema used by the current Phase 1 HTTP slice.
  - Make schema creation and seed/bootstrap application deterministic from a clean database.
  - Add validation coverage and runnable scripts that prove baseline schema + seed apply cleanly without expanding into repositories or gameplay logic.
- **Allowed files:**
  - `PLANS.md`
  - `docs/MASTER_STATUS.md`
  - `README.md`
  - `docker-compose.yml`
  - `src/backend/db/schema.sql`
  - `src/backend/db/seed.sql`
  - `src/backend/db/migrations/**`
  - `src/backend/db/scripts/**`
  - `src/backend/tests/Sboss.Api.Tests/**`
- **Non-goals:**
  - No Phase 1C repository implementation or persistence abstraction expansion.
  - No economy services, contract/job state machine work, gameplay systems, auth work, or Unity/client changes.
  - No schema redesign beyond what baseline correctness strictly requires for the existing Phase 1A entities and HTTP slice.
- **Acceptance criteria:**
  - A migration baseline exists in-repo and can create the current authoritative schema from scratch deterministically.
  - Seed/bootstrap data applies cleanly against that baseline and remains compatible with the existing season, level-seed, and match-result HTTP slice.
  - Repo-provided validation proves schema + seed can be applied on a clean PostgreSQL database.
  - Current API/test surface is not broken by the baseline changes.
  - `docs/MASTER_STATUS.md` advances to the next roadmap task only if all Phase 1B validation succeeds.
- **Blockers:** None recorded.
- **Follow-up review actions (2026-03-19):**
  - Ensure `src/backend/db/scripts/docker-init.sh` uses the Postgres entrypoint's Unix-socket bootstrap path instead of a `localhost` TCP DSN so first-run migration + seed succeeds on an empty Docker volume.
  - Add regression coverage that the Docker init bootstrap path avoids `localhost` and still invokes the migration + seed scripts.
- **Last updated:** 2026-03-19

---

## Task Record — P1C-CORE-REPOSITORIES
- **Task ID:** P1C-CORE-REPOSITORIES
- **Title:** Phase 1C core repositories
- **Phase:** Phase 1 — Authoritative Core Domain
- **Status:** DONE
- **Branch:** work
- **PR:** #11 (merged)
- **Scope:**
  - Repair status tracking so merged Phase 1B work is reflected correctly before new implementation proceeds.
  - Implement explicit PostgreSQL-backed repositories for Account, Season, LevelSeed, and MatchResult aligned to the current authoritative backend HTTP slice.
  - Add roadmap-status validation that blocks status drift between `PLANS.md`, `docs/MASTER_STATUS.md`, and the active Phase 1 task.
- **Allowed files:**
  - `PLANS.md`
  - `docs/MASTER_STATUS.md`
  - `src/backend/Sboss.Infrastructure/Repositories/**`
  - `src/backend/Sboss.Infrastructure/**`
  - `src/backend/Sboss.Domain/**`
  - `src/backend/Sboss.Contracts/**`
  - `src/backend/Sboss.Api/**`
  - `src/backend/tests/**`
  - `.github/workflows/**`
  - `scripts/**`
  - `README.md` only if repository/bootstrap instructions strictly require it
- **Non-goals:**
  - No client/Unity changes.
  - No economy systems, matchmaking systems, tick engine, or auth expansion beyond repository needs.
  - No fake generic repository abstraction layer or unrelated refactors.
- **Acceptance criteria:**
  - Phase 1B status is repaired first in both `PLANS.md` and `docs/MASTER_STATUS.md`.
  - Core repositories exist for Account, Season, LevelSeed, and MatchResult using explicit row-to-domain mapping.
  - Repository tests prove the current domain entities round-trip against the Phase 1B migration baseline and deterministic seed path.
  - Automated validation fails on roadmap/status drift and runs in CI.
  - Current API/build/test validation succeeds before task completion is recorded.
- **Blockers:** None recorded.
- **Follow-up review actions (2026-03-20):**
  - Fix the roadmap-status guardrail so it derives the active phase section from `docs/MASTER_STATUS.md` and allows `Next task` to point at the step after an `IN_PROGRESS` task.
  - Refuse to run repository integration test database resets unless `SBOSS_TEST_DATABASE` is explicitly set to a non-development database.
- **Last updated:** 2026-03-20

---

## Task Record — P1D-ECONOMY-TRANSACTION-SERVICE
- **Task ID:** P1D-ECONOMY-TRANSACTION-SERVICE
- **Title:** Phase 1D economy transaction service
- **Phase:** Phase 1 — Authoritative Core Domain
- **Status:** IN_PROGRESS
- **Branch:** work
- **PR:** Draft PR prepared via make_pr
- **Scope:**
  - Update roadmap tracking so Phase 1D is the active task after merged Phase 1C repository work.
  - Add authoritative account balance state and an append-only economy transaction ledger in PostgreSQL.
  - Implement strict idempotent backend-only economy mutation handling for credits and debits through a single service entry point.
  - Expose one minimal HTTP write endpoint that calls the transaction service and add integration coverage for duplicate/replay/race safety.
- **Allowed files:**
  - `PLANS.md`
  - `docs/MASTER_STATUS.md`
  - `src/backend/Sboss.Api/**`
  - `src/backend/Sboss.Contracts/**`
  - `src/backend/Sboss.Domain/**`
  - `src/backend/Sboss.Infrastructure/**`
  - `src/backend/db/schema.sql`
  - `src/backend/db/migrations/**`
  - `src/backend/db/seed.sql`
  - `src/backend/tests/Sboss.Api.Tests/**`
- **Non-goals:**
  - No client or Unity authority for any balance or ledger mutation.
  - No generic service-wrapper abstraction layer, no economy UI, and no Phase 1E job-state-machine work.
  - No direct balance writes outside the economy transaction service.
  - No inventory, market, contract payout, or progression feature expansion beyond the minimum authoritative write path.
- **Acceptance criteria:**
  - `account_balances` and `economy_transactions` exist in the canonical schema/migration path with append-only ledger behavior and unique idempotency protection.
  - `IEconomyTransactionService` / `EconomyTransactionService` is the only backend write entry point for economy mutations and performs atomic balance + ledger updates.
  - Duplicate retries return the original authoritative result without double-applying currency.
  - Concurrent duplicate attempts do not create ledger dupes or balance inflation.
  - Unknown accounts and insufficient funds are rejected without partial writes.
  - Integration tests prove successful credit/debit, duplicate retry idempotency, concurrent duplicate safety, insufficient funds rejection, unknown account rejection, and ledger/balance consistency.
  - Build and test validation pass before the task can be promoted beyond `IN_PROGRESS`.
- **Blockers:** None recorded.
- **Follow-up review actions (2026-03-20):**
  - Restore `src/backend/db/migrations/0001_phase_1b_baseline.sql` to the original Phase 1B baseline and ship economy tables/constraints via a new forward-only migration so checksum-based upgrades remain valid.
  - Persist and replay the original authoritative balance snapshot for idempotent economy retries so duplicate responses cannot drift after later balance changes.
  - Fix `src/backend/tests/Sboss.Api.Tests/PostgresDatabaseFixture.cs` so the shared PostgreSQL test reset applies the full 1D migration chain (`0001`, then `0002`) before `seed.sql`, keeping test bootstrap aligned with CI and preventing missing-relation failures.
  - Replace database-drop reset logic in `PostgresDatabaseFixture` with in-database schema reset so CI does not kill the Postgres container and surface `57P01` during economy/repository integration tests.
- **Last updated:** 2026-03-20

---

## Task Breakdown
1. Lock architecture and roadmap documents for server-authoritative baseline.
2. Establish repository standards and agent-control files.
3. Scaffold Sboss.sln with `Sboss.*` .NET 8 projects.
4. Implement API shell endpoints (no gameplay logic).
5. Provide PostgreSQL baseline schema + seed SQL.
6. Provide Unity shell structure and explicit DB-client rule.
7. Add Docker Compose for PostgreSQL.
8. Add CI baseline for backend restore/build/test.
9. Define GitHub label taxonomy and declarative labels file.

---

## Risks
- Local environment may not include Unity editor.
- CI strictness (warnings-as-errors) can block scaffolding.
- PostgreSQL integration in this completed bootstrap stage was infrastructure-only.

---

## Assumptions
- .NET 8 SDK available
- Docker available
- The completed bootstrap stage allowed schema-first + contract-first APIs
- Label automation via file is acceptable

---

## Acceptance Criteria
- Architecture docs locked and consistent
- Backend builds and API starts
- DB schema + seed exists
- Unity client explicitly non-authoritative
- CI pipeline works
- Governance docs enforce phase lock

---

## Completion Checklist
- [x] Architecture defined
- [x] Repo structure created
- [x] Backend scaffolded
- [x] API shell implemented
- [x] DB schema created
- [x] Unity shell documented
- [x] CI working
- [x] Labels defined

---

## Follow-up Tasks
- [x] Fix schema path in tests
- [x] Ensure 404 behavior for unknown IDs

---

## Documentation Task — Master Status Tracking
**task:** Add repository-level roadmap tracking  
**scope_lock:** Docs only  

**allowed_files:**
- docs/MASTER_STATUS.md
- README.md

**requirements:**
- Full phase checklist
- Mark Phase 0 complete
- Add “Current Position”
- Link from README

**non_goals:**
- No runtime or code changes

---

## Bootstrap Repair (PR #1 Green Checks)
**task:** Fix CI + build issues  

**scope_lock:** Bootstrap repair only  

**root_cause_summary:**
- Missing package references
- Fragile schema path

**acceptance:**
- restore/build/test all pass

**status_after_fix:**
- CI passes
- No feature expansion

---

## Documentation Repair — Task Template Forbidden Paths
**task:** Remove hard-coded forbidden globs  

**scope_lock:** Docs only  

**allowed_files:**
- docs/TASK_TEMPLATE.md
- .github/ISSUE_TEMPLATE/01-codex-task.md
- .github/repo-labels.json

**problem_statement:**
- Templates blocked valid work due to global forbidden paths

**requirements:**
- Remove hardcoded globs
- Replace with task-scoped instructions
- Ensure any auto-applied issue labels exist in the declarative label manifest

**validation:**
- `rg -nE "src/\\*\\*|tests/\\*\\*|\\.github/workflows/\\*\\*" docs/TASK_TEMPLATE.md .github/ISSUE_TEMPLATE/01-codex-task.md`
- `python - <<'PY'\nfrom pathlib import Path\ntext = Path('.github/repo-labels.json').read_text(encoding='utf-8')\nprint('\"name\": \"codex\"' in text)\nPY`
- `git diff --name-only`

**non_goals:**
- No runtime/code changes

---

## Governance Layer Task — Codex Control Scaffolding
**task:** Add governance layer before Phase 1A  

**scope_lock:** Docs only  

**allowed_files:**
- docs/CODEX_WORKFLOW.md
- docs/TASK_TEMPLATE.md
- .github/PULL_REQUEST_TEMPLATE.md
- .github/ISSUE_TEMPLATE/01-codex-task.md
- CODEOWNERS
- README.md

**requirements:**
- Plan-first workflow
- Codex-controlled tasks
- PR + Issue templates
- Governance routing

**non_goals:**
- No Phase 1A implementation

---

## Governance Repair — Remove Stale Phase 0 Lock
**task:** Remove stale Phase 0 lock  

**scope:** Docs only  

**outcome:**
- Repo governance aligned to roadmap-driven execution

---

## Follow-up Governance Repair — Phase 1 Tracker Clarification
**task:** Clarify current-phase tracking before Phase 1 execution

**scope_lock:** Docs only

**allowed_files:**
- docs/MASTER_STATUS.md
- AGENTS.md
- docs/roadmap/ROADMAP_LOCK.md
- README.md

**problem_statement:**
- Governance docs now point to roadmap-driven execution, but `docs/MASTER_STATUS.md` does not explicitly declare the active phase and `docs/roadmap/ROADMAP_LOCK.md` overstates `PLANS.md` as the active execution tracker before a scoped Phase 1 task is recorded.

**requirements:**
- Add an explicit current-phase field to `docs/MASTER_STATUS.md`
- Keep `docs/MASTER_STATUS.md` as the roadmap phase + next task source of truth
- Reframe `PLANS.md` as task-scoping support rather than the current-phase tracker
- Align contributor-facing docs with the clarified governance model

**validation:**
- `rg -n "Current phase:|Next task:|Execution Tracker|task-scoping" docs/MASTER_STATUS.md AGENTS.md docs/roadmap/ROADMAP_LOCK.md README.md`
- `git diff --name-only`

**non_goals:**
- No runtime/code changes
- No Phase 1 implementation work
