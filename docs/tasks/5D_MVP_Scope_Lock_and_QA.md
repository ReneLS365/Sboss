# Task 5D – MVP Scope Lock & QA

## Goal
Lock the MVP feature set, freeze any further additions, and perform a thorough quality assurance pass across all systems.  The objective is to stabilize, fix bugs, and ensure all acceptance criteria for prior phases are met before release.

## Phase Check
- **Current task:** 5D — MVP Scope Lock & QA.
- **Next task:** 5E — Deployment Pipelines.
- **Why allowed:** After stress testing (5C), it’s time to finalize scope, fix defects, and prepare for deployment.

## Scope
### In scope
- Declare the feature freeze: no new scope except critical bug fixes.
- Conduct regression tests across all API endpoints, game flows, and integrations.
- Address open bugs and finalize documentation.
- Update versioning and release notes.
- Prepare final compliance and security reviews.
### Out of scope
- New features or enhancements.
- Performance optimizations unless critical for stability.

## Allowed Files
- `src/backend/**`, `src/client/unity/**`, `docs/`, and test directories for bug fixes only.
- `docs/tasks/5D_MVP_Scope_Lock_and_QA.md`.

## Forbidden Files
- Introduction of new feature modules.
- Non‑critical refactoring; keep changes minimal and targeted.

## Acceptance Criteria
1. All critical bugs identified through QA are resolved.
2. Regression tests pass across all major flows.
3. Documentation and release notes reflect the final product scope and behaviour.
4. Compliance and security checks are passed.

## Validation
Run full test suite and manual QA. Confirm no new features are added. Validate that the product meets the MVP criteria defined at project start.

## Notes
- Strict change control applies; changes require justification and review.