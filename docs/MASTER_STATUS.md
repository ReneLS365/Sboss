# Sboss — Master Status (Lean MVP)

## Current Position
- Current phase: **Phase 3 — Company & Meta-loop**
- Completed phase: **Phase 2 — Core Gameplay Validation**
- Current task: **3E — XP & Progression**
- Next task: **4A — Leaderboard API**
- Future expansion phase: **Phase 6 — Real-Time Simulation Expansion**

---

## Status overview
- [x] Phase 1 — Authoritative Core Domain (HTTP, DB, Economy Ledger)
- [x] Phase 2 — Core Gameplay Validation (Command Queue, Unity RTS-kamera, Score-motor)
- [ ] Phase 3 — Company & Meta-loop (Yard Inventory, Akkord-system, XP)
- [ ] Phase 4 — Asynchronous Competition (Leaderboards, Daily Challenges, Ghost Data)
- [ ] Phase 5 — Release Prep (Anti-cheat, QA, Deployment)
- [ ] Phase 6 — Real-Time Simulation Expansion (Live Session Runtime, Transport, Replication)

---

## Phase 1 — Authoritative Core Domain (FÆRDIG)
Fokus: Fundament, sikkerhed og stateless HTTP-arkitektur.
- [x] 1A Domain entities + contracts
- [x] 1B Database schema + migration baseline
- [x] 1C Core repositories
- [x] 1D Economy transaction service
- [x] 1E Contract job state machine
- [x] 1F Company/job application services
- [x] 1G First vertical slice HTTP endpoints
- [x] 1H Integration tests for exploit resistance
- [x] 1I Hardening + invariants

## Phase 2 — Core Gameplay Validation
Fokus: Det faktiske spil. Implementering af event-baseret Command Queue og Client-Side Prediction for et flydende RTS-byggeflow.
- [x] 2A Command Validation Queue: Server-side modtagelse og lynhurtig validering af diskrete bygge-actions (placér, fjern).
- [x] 2B Unity Isometrisk "Sjakbajs" Shell: Implementering af isometrisk 3D-kamera, drag-and-drop interaktion og mobiloptimeret UI-bundbar.
- [x] 2C Client-Side Prediction: Tillad Unity at placere dele øjeblikkeligt lokalt, mens server godkender asynkront. Rollback ved server-afvisning.
- [x] 2D Scoring Engine: Server-autoritativ beregning af stabilitet, combo-multiplier og tid.
- [x] 2E Scaffold Assembly Rules: Definer geometrisk logik for forbindelser (Blå ramme -> Gult dæk -> Rød diagonal).
- [x] 2F Vertical Slice Test: Spil én komplet runde fra opgaveaflæsning til server-valideret score.

## Phase 3 — Company & Meta-loop
Fokus: Progression og økonomi (RTS-logistik).
- [x] 3A Yard Capacity & Inventory: Begrænset lagerplads, køb af stilladsdele. Hard-cap blokerer store events.
- [x] 3B Akkord & Crew Split: Enhedsstyring. Balancering af dyre svende (hastighed) mod billige lærlinge (XP). 60/40 profitdeling.
- [x] 3C Wear & Tear System: Simuleret slitage ved fejlplaceringer (Materialeintegritet).
- [x] 3D Loadout & Fog of War: Tidsbegrænset spatialt minigame for at pakke varevognen korrekt før opgaven låses.
- [ ] 3E XP & Progression: Oplåsning af sværere bane-templates (f.eks. Offshore Rotationer).

## Phase 4 — Asynchronous Competition
Fokus: Territory Control og rivalisering uden synkron realtids-netkode.
- [ ] 4A Leaderboard API: Globale, regionale og Crew-baserede ranglister.
- [ ] 4B Deterministisk Level Generator: Sikr at seeds genererer 100% matematisk identiske baner.
- [ ] 4C Ghost Data Pipeline: Optag bygge-sekvenser og gem som letvægts JSON til asynkrone replays.
- [ ] 4D Daily Challenge System: 24-timers roterende global udfordring. "Territory Control" vha. highscores.
- [ ] 4E Social Push & Sabotage: Notifikationer ved slåede rekorder. Mulighed for mild asynkron sabotage mod rivaler.

## Phase 5 — Release Prep
Fokus: Stabilitet, on-boarding og lancering.
- [ ] 5A Anti-Cheat Hardening: Valider byggetider mod teoretisk minimums-tid. Afvis klientmanipulation.
- [ ] 5B UX Polish & Audio: Tilføj metalliske CLANK-lyde og visuel partikel-feedback på combos.
- [ ] 5C Load & Stress Test: Test database-concurrency under Daily Challenge peaks.
- [ ] 5D MVP Scope Lock & QA: Frys features, udfør QA-pas.
- [ ] 5E Deployment Pipelines: App Store og Play Store distribution.


## Phase 6 — Real-Time Simulation Expansion
Fokus: Udvid MVP'en med live sessioner og lav-latens simulation uden at bryde server-authoritative grænser.
- [ ] 6A Real-Time Session Architecture Lock: Definér session-model, tick-politik, transportvalg og persistence-boundary før runtime-kode.
- [ ] 6B Authoritative Simulation Runtime: Implementér backend-ejet live session runtime for aktive jobs og spiller-intents.
- [ ] 6C Session Transport & Intent Streaming: Tilføj lav-latens, tovejstransport mellem Unity og backend-sessioner.
- [ ] 6D Snapshot Replication, Prediction & Reconciliation: Replikér session-state til klienter med prediction, rollback og reconciliation.
- [ ] 6E Persistence & Meta-loop Bridge: Commit live session-resultater tilbage til kontrakter, inventory, wear/tear, score, ghost data og progression.
