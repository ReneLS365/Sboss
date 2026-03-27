# Sboss Unity Shell

## Authority Boundary
**No gameplay authority in client.**

Client responsibilities:
- Rendering
- Input collection
- UX presentation
- Calling backend APIs

Server responsibilities:
- Score validation
- Seed validation
- Economy/progression
- Anti-cheat and authoritative outcomes

## Shell Structure
- `SbossClient/Client/App`
- `SbossClient/Client/Networking`
- `SbossClient/Client/Presentation`
- `SbossClient/Client/Input`

## Phase 2B Shell Components
- `SbossClient/Client/Presentation/IsometricCameraController.cs`
  - Stable isometric framing with pan/zoom for shell navigation only.
- `SbossClient/Client/Input/PlacementDragShell.cs`
  - Captures drag input and ghost preview position.
  - Emits placement request payloads without local legality checks.
- `SbossClient/Client/Presentation/MobileBottomActionBarController.cs`
  - Mobile-thumb bottom action bar for shell actions.
- `SbossClient/Client/Networking/PlacementRequestDispatcher.cs`
  - Transport-only request dispatch placeholder (authoritative result is server-owned).
- `SbossClient/Client/App/Phase2BShellBootstrap.cs`
  - Wires camera, drag shell, bottom bar, and dispatcher together.

## Explicit Non-Goals (Phase 2B)
- No client-side prediction/rollback.
- No score, capacity, stability, or legality decisions on client.
- No replacement of backend validation queue or validators.

## Dummy API Config
Use `SbossClient/Client/App/api-config.json` for local endpoint binding.
