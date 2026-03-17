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

## Dummy API Config
Use `SbossClient/Client/App/api-config.json` for local endpoint binding.
