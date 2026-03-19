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

## Current Roadmap Phase
- **Phase 1 (Authoritative Core Domain)**

## Project Status
Project status, explicit current phase, and next task are tracked in [docs/MASTER_STATUS.md](docs/MASTER_STATUS.md).

Current next task:
- **1A — Domain entities + contracts**

## Execution Model
- Roadmap and progress source of truth: [docs/MASTER_STATUS.md](docs/MASTER_STATUS.md)
- Repository execution and governance workflow: [docs/CODEX_WORKFLOW.md](docs/CODEX_WORKFLOW.md)
- Task definition format for scoped work: [docs/TASK_TEMPLATE.md](docs/TASK_TEMPLATE.md)
- Execution follows the roadmap phase declared in `docs/MASTER_STATUS.md`; `PLANS.md` is used only to scope the current task inside that active phase.

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
- Baseline schema and seed scripts auto-apply on first startup.
