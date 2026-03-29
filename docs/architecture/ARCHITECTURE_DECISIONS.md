# Architecture Decisions

## ADR-0001 Server-Authoritative Core
- Status: Accepted
- Decision: Keep all authority in backend + PostgreSQL.
- Consequence: Unity remains dumb client.

## ADR-0002 Solution and Namespace Convention
- Status: Accepted
- Decision: Use `Sboss.sln` and `Sboss.*` project namespaces.
- Consequence: Stable package naming for scale without introducing extra runtimes.

## ADR-0003 Data-First Bootstrap
- Status: Accepted
- Decision: Define SQL schema + contracts/entities before gameplay logic.
- Consequence: Enables locked API contracts and CI validation in Phase 0.

## ADR-0004 Minimal Operational Labels
- Status: Accepted
- Decision: Use concise workflow labels only.
- Consequence: Avoid label sprawl and phase creep.


## ADR-0005 Future Real-Time Expansion Boundary
- Status: Accepted
- Decision: Treat real-time simulation as a post-MVP expansion phase, not as an implicit rewrite of the Lean MVP execution model.
- Consequence: Current roadmap phases keep their existing server-authoritative API-driven design, while any future live-session runtime must be introduced through explicit roadmap tasks and preserve backend authority.
