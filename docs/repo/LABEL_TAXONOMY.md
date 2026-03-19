# Label Taxonomy

| Label | Purpose | When to Use |
|---|---|---|
| `phase:foundation` | Marks historical bootstrap/foundation work. | Use only when touching Phase 0 completion records or historical cleanup tied to the bootstrap baseline. |
| `phase:1` | Marks current authoritative core domain work. | Add to Phase 1 PRs/issues, including preflight repairs required to start 1A safely. |
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
- Add new phase labels only when the roadmap advances into that phase.
