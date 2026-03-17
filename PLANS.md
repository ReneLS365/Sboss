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

## Follow-up Tasks
- [x] PR #1 review fix: correct `SchemaSanityTests` schema path resolution to target `src/backend/db/schema.sql` from test output directory.


## Phase 0 Repair (PR #1 Green Checks)
- **task:** Targeted repair to make PR #1 pass all required checks.
- **scope_lock:** Fase 0 only; no new features or architecture changes.
- **suspected_root_causes:**
  - Missing package references causing backend compile failures in CI.
  - Schema sanity path fragility from test output directory.
  - CI workflow must keep restore/build/test order and avoid non-essential blocking workflows.
- **acceptance_for_repair:**
  - `dotnet restore Sboss.sln` succeeds.
  - `dotnet build Sboss.sln --configuration Release` succeeds.
  - `dotnet test Sboss.sln --configuration Release` succeeds.
  - Backend CI workflow remains valid for PR execution path.
- **root_cause_summary:**
  - `Sboss.Infrastructure` lacked explicit abstractions package references for `IServiceCollection`/`IConfiguration` extension signatures in CI compile context.
  - `Sboss.Api` lacked explicit Swagger package reference for `AddSwaggerGen`/`UseSwagger` extension methods.
  - `SchemaSanityTests` used a fragile relative path from test output layout.
- **files_touched_for_repair:**
  - `PLANS.md`
  - `src/backend/Sboss.Infrastructure/Sboss.Infrastructure.csproj`
  - `src/backend/Sboss.Api/Sboss.Api.csproj`
  - `src/backend/tests/Sboss.Api.Tests/SchemaSanityTests.cs`
- **status_after_fix:**
  - restore/build/test commands run successfully in Release configuration.
  - targeted repair stayed within Fase 0 scope with no feature expansion.
- **fase_0_exit_note:**
  - Fase 0 exit criteria are satisfied for PR #1 repair path: backend compiles, tests pass, workflow path is valid.
