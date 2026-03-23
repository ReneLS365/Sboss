# Sboss — Master Status

## Current Position
- Current phase: **Phase 1 — Authoritative Core Domain**
- Completed phase: **Phase 0 — Foundation / Bootstrap**
- Current task: **1I — Hardening + invariants**
- Next task: **2A — Tick model + schema**

---

## Status overview
- [x] Phase 0 — Foundation / Bootstrap
- [ ] Phase 1 — Authoritative Core Domain
- [ ] Phase 2 — Deterministic Tick Engine
- [ ] Phase 3 — Identity / Auth Binding
- [ ] Phase 4 — Company Management Loop
- [ ] Phase 5 — Contract System
- [ ] Phase 6 — Inventory / Equipment
- [ ] Phase 7 — Economy Hardening
- [ ] Phase 8 — Shards / Multiplayer Authority
- [ ] Phase 9 — Unity Dumb Client Shell
- [ ] Phase 10 — First-Person Worker Prototype
- [ ] Phase 11 — Scaffold Build Rules
- [ ] Phase 12 — Progression / Unlocks
- [ ] Phase 13 — Market / Trade
- [ ] Phase 14 — Social / Competition
- [ ] Phase 15 — Observability / Admin Tooling
- [ ] Phase 16 — Content Pipeline / Balancing
- [ ] Phase 17 — Hardening / Load / Security
- [ ] Phase 18 — MVP Release

---

## Phase 1 — Authoritative Core Domain
- [x] 1A Domain entities + contracts
- [x] 1B Database schema + migration baseline
- [x] 1C Core repositories
- [x] 1D Economy transaction service
- [x] 1E Contract job state machine
- [x] 1F Company/job application services
- [x] 1G First vertical slice HTTP endpoints
- [x] 1H Integration tests for exploit resistance
- [ ] 1I Hardening + invariants

Audit note:
- The current repo already contains exploit-resistance integration coverage for the scoped Phase 1 HTTP mutation slice, including economy transaction replay handling, contract job transition conflict coverage, and contract job application duplicate/concurrency coverage. Phase 1H is complete, and Phase 1I is now the active task before Phase 2 begins.

## Phase 2 — Deterministic Tick Engine
- [ ] 2A Tick model + schema
- [ ] 2B Tick processor skeleton
- [ ] 2C Move job progression into ticks
- [ ] 2D Tick idempotency + lock safety
- [ ] 2E Tick tests + replay tests
- [ ] 2F Tick observability

## Phase 3 — Identity / Auth Binding
- [ ] 3A Auth strategy decision
- [ ] 3B Player identity binding
- [ ] 3C Request user context
- [ ] 3D Ownership enforcement
- [ ] 3E Authorization tests

## Phase 4 — Company Management Loop
- [ ] 4A Company stats model
- [ ] 4B Crew/workforce model
- [ ] 4C Company progression rules
- [ ] 4D Company management endpoints
- [ ] 4E Company progression tests

## Phase 5 — Contract System
- [ ] 5A Contract templates
- [ ] 5B Contract generation service
- [ ] 5C Reward formula layer
- [ ] 5D Failure/timeout/cancel states
- [ ] 5E Contract history and results
- [ ] 5F Contract abuse tests

## Phase 6 — Inventory / Equipment
- [ ] 6A Inventory item model
- [ ] 6B Company inventory storage
- [ ] 6C Inventory mutation service
- [ ] 6D Bind jobs to inventory requirements
- [ ] 6E Inventory endpoints
- [ ] 6F Inventory exploit tests

## Phase 7 — Economy Hardening
- [ ] 7A Unify all currency entry points
- [ ] 7B Versioning and concurrency review
- [ ] 7C Idempotency coverage audit
- [ ] 7D Economy audit tooling
- [ ] 7E Fuzz/retry abuse tests

## Phase 8 — Shards / Multiplayer Authority
- [ ] 8A Shard model
- [ ] 8B Shard-aware persistence
- [ ] 8C Tick ownership per shard
- [ ] 8D Session/presence registry
- [ ] 8E Concurrency/load simulation

## Phase 9 — Unity Dumb Client Shell
- [ ] 9A Unity project bootstrap
- [ ] 9B API client layer
- [ ] 9C Login/bootstrap state fetch
- [ ] 9D Company dashboard UI
- [ ] 9E Job management UI
- [ ] 9F Inventory/economy UI

## Phase 10 — First-Person Worker Prototype
- [ ] 10A Interaction model
- [ ] 10B First-person shell
- [ ] 10C Server action requests
- [ ] 10D Worker-task binding
- [ ] 10E Anti-spam/anti-speed validation

## Phase 11 — Scaffold Build Rules
- [ ] 11A Scaffold part data model
- [ ] 11B Placement validation service
- [ ] 11C Assembly rules
- [ ] 11D Dismantle rules
- [ ] 11E Scaffold test suite

## Phase 12 — Progression / Unlocks
- [ ] 12A XP/level model
- [ ] 12B Reward-to-progression binding
- [ ] 12C Unlock trees
- [ ] 12D Progression anti-duplication tests

## Phase 13 — Market / Trade
- [ ] 13A Listing/order model
- [ ] 13B Trade execution service
- [ ] 13C Escrow/reservation logic
- [ ] 13D Market endpoints
- [ ] 13E Trade exploit tests

## Phase 14 — Social / Competition
- [ ] 14A Team/company roles
- [ ] 14B Invitations and membership flows
- [ ] 14C Leaderboards and ranking
- [ ] 14D Competitive event hooks

## Phase 15 — Observability / Admin Tooling
- [ ] 15A Structured logging
- [ ] 15B Metrics/health instrumentation
- [ ] 15C Internal admin endpoints
- [ ] 15D Incident/debug workflows

## Phase 16 — Content Pipeline / Balancing
- [ ] 16A Externalized content definitions
- [ ] 16B Content import/versioning
- [ ] 16C Balancing workflow

## Phase 17 — Hardening / Load / Security
- [ ] 17A Load test harness
- [ ] 17B Concurrency stress tests
- [ ] 17C Abuse/fuzz suite
- [ ] 17D Backup/restore/migration safety
- [ ] 17E Performance trimming

## Phase 18 — MVP Release
- [ ] 18A MVP scope lock
- [ ] 18B Release QA pass
- [ ] 18C Deployment and rollout
- [ ] 18D Post-release issue funnel
