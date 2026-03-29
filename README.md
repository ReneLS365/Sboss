# Sboss

## Project Purpose
Sboss is bootstrapped as a server-authoritative online game foundation where .NET 8 + PostgreSQL own all authoritative state and validation.

## Locked Tech Stack
- Backend: ASP.NET Core Web API (.NET 8)
- Domain/Contracts: `Sboss.*` class libraries
- Persistence: PostgreSQL
- Client: Unity shell (`SbossClient`) as dumb client only
- Local orchestration: Docker Compose
- CI: GitHub Actions

## Project Status
Project status, explicit current phase, and next task are tracked in [docs/MASTER_STATUS.md](docs/MASTER_STATUS.md).

## Roadmap Horizon
- Lean MVP roadmap: Phases 1-5
- Planned post-MVP expansion: Phase 6 — Real-Time Simulation Expansion
- Active execution is still gated only by the current phase/task in `docs/MASTER_STATUS.md`

## Execution Model
- Roadmap and progress source of truth: [docs/MASTER_STATUS.md](docs/MASTER_STATUS.md)
- Repository execution and governance workflow: [docs/CODEX_WORKFLOW.md](docs/CODEX_WORKFLOW.md)
- Task definition format for scoped work: [docs/TASK_TEMPLATE.md](docs/TASK_TEMPLATE.md)
- Execution follows the roadmap phase declared in `docs/MASTER_STATUS.md`.

## Local Setup
1. Copy env file:
   - `cp .env.example .env`
2. Start PostgreSQL:
   - `docker compose up -d postgres`
3. Restore/build backend:
   - `dotnet restore Sboss.sln`
   - `dotnet build Sboss.sln -warnaserror`
4. Run API:
   - `dotnet run --project src/backend/Sboss.Api/Sboss.Api.csproj`
5. Run tests:
   - `dotnet test Sboss.sln -warnaserror`

## Folder Structure
- `docs/` locked architecture, roadmap, contracts, and workflow docs
- `src/backend/` .NET solution projects + db scripts
- `src/client/unity/` Unity shell and client boundaries
- `.github/workflows/` CI and optional repo automation

## Run Backend Locally
- Ensure PostgreSQL is running via Docker Compose.
- Set `CONNECTIONSTRINGS__DEFAULT` in `.env` (or environment).
- Start API project and verify:
  - `GET /health`
  - `GET /api/v1/seasons/current`
  - `GET /api/v1/level-seeds/{seedId}`
  - `POST /api/v1/match-results`

## Start DB Locally
- `docker compose up -d postgres`
- Migration baseline and seed scripts auto-apply on first startup.
- Manual deterministic bootstrap path:
  - `export DATABASE_URL=postgresql://sboss:sboss_dev_password@localhost:5432/sboss`
  - `src/backend/db/scripts/apply-migrations.sh`
  - `src/backend/db/scripts/apply-seed.sh`
- Clean-database validation path:
  - `export POSTGRES_ADMIN_URL=postgresql://sboss:sboss_dev_password@localhost:5432/postgres`
  - `src/backend/db/scripts/validate-bootstrap.sh`
