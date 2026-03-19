# API Contracts

## GET /health
- Response: `{ status, utcTime }`

## GET /api/v1/seasons/current
- Response: current season metadata from the authoritative active season baseline.

## GET /api/v1/level-seeds/{seedId}
- Response: level seed payload for requested UUID.
- Server returns `404` when seed is missing.

## POST /api/v1/match-results
- Request: account/season/level-seed refs + score metrics.
- Response: persisted ID + validation status.
- Validation pipeline is server-side and mandatory.

## Baseline note
- The bootstrap HTTP surface remains intentionally small while Phase 1 establishes authoritative core-domain internals behind these existing contracts.
