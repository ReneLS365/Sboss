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
- **PR:** DRAFT / pending
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
- **Status:** NEXT
- **Branch:** TBD
- **PR:** Not started
- **Scope:**
  - Implement the existing roadmap item `1B Database schema + migration baseline` without widening scope beyond the defined Phase 1 sequence.
- **Allowed files:**
  - To be defined when the Phase 1B task is formally started.
- **Non-goals:**
  - No Phase 1C+ repository or service expansion before the 1B plan is scoped.
- **Acceptance criteria:**
  - To be defined in the scoped Phase 1B task record before implementation starts.
- **Blockers:** None recorded.
- **Last updated:** 2026-03-19

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
