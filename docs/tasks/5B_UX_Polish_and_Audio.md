# Task 5B – UX Polish & Audio

## Goal
Enhance user experience by adding polish to interactions and incorporating appropriate audio feedback.  This includes UI refinements, improved feedback during placement, and adding metallic “CLANK” sounds and particle effects to emphasize successful combos and errors.

## Phase Check
- **Current task:** 5B — UX Polish & Audio.
- **Next task:** 5C — Load & Stress Test.
- **Why allowed:** After anti‑cheat hardening, release prep continues with user experience improvements.

## Scope
### In scope
- Refine client‑side UI elements for clarity and responsiveness.
- Add audio cues for successful actions, failed validations, combos, and score multipliers.
- Add particle effects or visual polish for scoring events.
- Ensure audio/visual additions do not affect authoritative decisions.
### Out of scope
- Any changes to backend logic; all scoring and validation remain server‑owned.
- Stress testing or deployment tasks (5C–5E).

## Allowed Files
- `src/client/unity/**` – for UI and audio implementations.
- `assets/audio/**` or similar directories for audio files (ensuring licensing is handled properly).
- `docs/tasks/5B_UX_Polish_and_Audio.md`.

## Forbidden Files
- Backend domain code; no logic changes.
- CI/workflow files.

## Acceptance Criteria
1. The client provides clear, responsive feedback on placement, validation outcomes, and combos.
2. Audio cues play at appropriate times without latency and do not impact performance.
3. Visual polish improves clarity without introducing client‑side authority or logic.
4. Tests or manual QA verify that new feedback does not impact existing functions.

## Validation
No backend build impact expected. Perform manual UI/UX QA using Unity. Run existing backend tests to ensure no regression.

## Notes
- Consult user accessibility guidelines (e.g. volume controls, visual contrast) during polish work.