# Label Taxonomy (Phase 0)

| Label | Purpose | When to Use |
|---|---|---|
| `phase:0` | Marks work constrained to bootstrap phase. | Add to all Phase 0 PRs/issues. |
| `backend` | Backend/API/domain/infrastructure scope. | Backend code, API contracts, validation pipeline stubs. |
| `client` | Unity client shell scope. | Client folder structure, Unity integration placeholders. |
| `db` | Database schema/data scope. | SQL schema, migrations, seed updates. |
| `infra` | Environment and orchestration scope. | Docker compose, env config, runtime setup. |
| `docs` | Documentation scope. | Architecture, roadmap, API docs, workflow docs. |
| `security` | Security/validation/anti-cheat scope. | Server-side validation gates, trust boundaries. |
| `tests` | Automated test scope. | Unit/integration/smoke/schema checks. |
| `ci` | CI pipeline scope. | GitHub Actions, build/test automation. |
| `blocked` | Work cannot proceed due to dependency. | Missing prerequisite, external blocker. |
| `needs-review` | Awaiting reviewer action. | PR is ready for technical review. |

## Stability Rules
- Keep names short, stable, and operational.
- No vanity labels.
- No labels for Phase 1+ features.
