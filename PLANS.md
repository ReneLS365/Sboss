# Sboss Phase Plan

## Current Phase
- **Current_phase:** 0 (Bootstrap Foundation)
- **Phase_lock:** Phase 0 only. No gameplay, management systems, leaderboard logic, auth provider integrations, or real-time networking.

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
- PostgreSQL integration is infrastructure-only in Phase 0.

---

## Assumptions
- .NET 8 SDK available
- Docker available
- Phase 0 allows schema-first + contract-first APIs
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

## Phase 0 Repair (PR #1 Green Checks)
**task:** Fix CI + build issues  

**scope_lock:** Phase 0 only  

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

**problem_statement:**
- Templates blocked valid work due to global forbidden paths

**requirements:**
- Remove hardcoded globs
- Replace with task-scoped instructions

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