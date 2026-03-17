# Sboss Phase Plan

## Current Phase
- **current_phase:** 0 (Bootstrap Foundation)
- **phase_lock:** Fase 0 only. No gameplay, management systems, leaderboard logic, auth provider integrations, realtime networking, or economy balancing.

## Task Breakdown
1. Lock architecture and roadmap documents for server-authoritative baseline.
2. Establish repository standards and agent-control files.
3. Scaffold `Sboss.sln` with `Sboss.*` .NET 8 projects.
4. Implement API shell endpoints and contracts only.
5. Provide PostgreSQL baseline schema + seed SQL.
6. Provide Unity shell structure and explicit dumb-client rule.
7. Add Docker Compose local orchestration for PostgreSQL.
8. Add CI baseline for backend restore/build/test.
9. Define operational GitHub label taxonomy and declarative labels file.

## Risks
- Local environment may not include Unity editor for full project generation.
- CI strictness (`TreatWarningsAsErrors`) can block scaffolding if templates evolve.
- PostgreSQL integration is infrastructure-only in Phase 0; behavior remains stubbed where domain logic is intentionally deferred.

## Assumptions
- .NET 8 SDK is available for solution scaffolding and tests.
- Docker is available for local PostgreSQL startup.
- Phase 0 accepts schema-first and contract-first API shells without gameplay logic.
- Label automation via declarative file is acceptable if GitHub API access is not configured.

## Acceptance Criteria
- Locked architecture docs are present and consistent with server-authoritative constraints.
- `Sboss.sln` builds and backend API starts with required endpoints.
- PostgreSQL baseline schema and seed files exist with required tables/fields.
- Unity shell directories and README explicitly prohibit gameplay authority in client.
- CI workflow exists for backend restore/build/test.
- `AGENTS.md` and `PLANS.md` enforce phase lock and process constraints.
- Label taxonomy is documented and machine-readable label manifest is included.

## Completion Checklist
- [x] PLANS updated before implementation
- [x] Architecture docs completed
- [x] Repo config files completed
- [x] Backend projects scaffolded
- [x] API shell endpoints implemented
- [x] DB schema + seed baseline created
- [x] Unity shell documented
- [x] Docker Compose + env example created
- [x] Backend tests implemented
- [x] CI baseline added
- [x] Label taxonomy + labels manifest added
- [x] Local validation commands executed
- [x] Commit created
- [x] Draft PR prepared
